using Application.DTOs;
using Application.Interfaces;
using Data.Interfaces;
using Domain.Entities.Login;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Net;
using System.Net.Mail;
using Data.Repositories;
using Data.Util;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    public class LoginService : ILoginService
    {
        // Validade do código de recuperação de senha
        private const int ValidadeCodigoMinutos = 15;
        // Mesma mensagem para e-mail inexistente e senha errada, para não revelar quais e-mails existem
        private const string CredenciaisInvalidas = "E-mail ou senha incorretos.";

        private readonly ILoginRepository _loginRepository;
        private readonly IConfiguration _configuration;
        private readonly TentativasRecuperacaoSenha _tentativas;

        public LoginService(ILoginRepository loginRepository, IConfiguration configuration, TentativasRecuperacaoSenha tentativas)
        {
            _loginRepository = loginRepository;
            _configuration = configuration;
            _tentativas = tentativas;
        }

        public async Task<LoginDTO> RealizarLogin(LoginModel dadosInformados)
        {
            string Error = string.Empty;

            if(string.IsNullOrEmpty(dadosInformados.Senha) || string.IsNullOrEmpty(dadosInformados.Email))
                return new LoginDTO { Sucesso = false, Mensagem = "Para efetuar o login, deve ser informado um email e senha." };

            try
            {
                LoginAuxiliarModel usuario = await _loginRepository.BuscaUsuarioNoSistema(dadosInformados.Email);

                if (usuario.IdUsuario > 0)
                {
                    if (VerifyPassword(dadosInformados.Senha, usuario.Senha))
                    {
                        //string token = GenerateToken(usuario.IdUsuario.ToString(), dadosInformados.Email, usuario.Senha);
                        //await _loginRepository.InsereRegistroLogin(usuario.IdUsuario, token);

                        List<IdDescricaoPerfilModel> Perfis = await RetornaPerfilUsuario(usuario.IdUsuario);

                        return new LoginDTO
                        {
                            Sucesso = true,
                            Mensagem = "Login realizado com sucess.",
                            //IdUsuario = usuario.IdUsuario,
                            //Token = token,
                            PerfisUsuario = Perfis
                        };
                    }
                    else
                        Error = CredenciaisInvalidas;
                }
                else
                    Error = CredenciaisInvalidas;

                if (!string.IsNullOrEmpty(Error))
                    return new LoginDTO { Sucesso = false, Mensagem = Error };
                else
                    return new LoginDTO();
            }
            catch (Exception ex)
            {
                return new LoginDTO
                {
                    Sucesso = false,
                    Mensagem = ex.Message
                };
            }
        }

        public async Task<AutenticarDTO> AutenticarPerfil(LoginPerfilModel dadosInformados)
        {
            string Error = string.Empty;

            if (string.IsNullOrEmpty(dadosInformados.Senha) || string.IsNullOrEmpty(dadosInformados.Email))
                return new AutenticarDTO { Sucesso = false, Mensagem = "Para efetuar o login, deve ser informado um email e senha." };

            try
            {
                LoginAuxiliarModel usuario = await _loginRepository.BuscaUsuarioNoSistema(dadosInformados.Email);

                if (usuario.IdUsuario > 0)
                {
                    if (VerifyPassword(dadosInformados.Senha, usuario.Senha))
                    {
                        bool IsAdmin = await _loginRepository.VerificaPerfilAdministrador(dadosInformados.IdPerfil, usuario.IdUsuario);
                        string token = GenerateToken(usuario.IdUsuario.ToString(), dadosInformados.Email, usuario.Senha, IsAdmin);

                        await _loginRepository.InsereRegistroLogin(usuario.IdUsuario, token);

                        return new AutenticarDTO
                        {
                            Sucesso = true,
                            Mensagem = "Login realizado com sucesso.",
                            IdUsuario = usuario.IdUsuario,
                            Token = token
                        };
                    }
                    else
                        Error = CredenciaisInvalidas;
                }
                else
                    Error = CredenciaisInvalidas;

                if (!string.IsNullOrEmpty(Error))
                    return new AutenticarDTO { Sucesso = false, Mensagem = Error };
                else
                    return new AutenticarDTO();
            }
            catch (Exception ex)
            {
                return new AutenticarDTO
                {
                    Sucesso = false,
                    Mensagem = ex.Message
                };
            }
        }

        /// <summary>
        /// Gera e envia o código de recuperação. Devolve true mesmo quando o e-mail
        /// não existe, para não revelar quais e-mails estão cadastrados.
        /// </summary>
        public async Task<bool> RecuperarSenha(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                LoginAuxiliarModel usuario = await _loginRepository.BuscaUsuarioNoSistema(email);
                if (usuario.IdUsuario <= 0)
                    return true;

                int numero = RandomNumberGenerator.GetInt32(100000, 1000000);

                await _loginRepository.SalvaCodigoRecuperacao(numero, email);
                _tentativas.Limpar(email);

                await EnviarEmailAsync(email, "Recuperação de Senha EPonto",
                    $"O código de recuperação é {numero}. Ele vale por {ValidadeCodigoMinutos} minutos.");
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ValidaCodigoRecuperacao(int codigo, string email) =>
            await ConfereCodigo(codigo, email);

        /// <summary>
        /// Troca a senha. Antes, bastava o e-mail: qualquer pessoa trocava a senha de
        /// qualquer conta. Agora exige o código válido, que é anulado depois do uso.
        /// </summary>
        public async Task<bool> AlteraSenhaLogin(string senha, string email, int codigo)
        {
            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6 || string.IsNullOrWhiteSpace(email))
                return false;

            if (!await ConfereCodigo(codigo, email))
                return false;

            bool alterou = await _loginRepository.SalvaAlteracaoSenha(HashPassword(senha), email);
            await _loginRepository.InvalidaCodigosRecuperacao(email);
            _tentativas.Limpar(email);

            return alterou;
        }

        /// <summary>
        /// Confere o código dentro da validade. Cada erro conta uma tentativa; no
        /// limite, o código é anulado e é preciso pedir outro.
        /// </summary>
        private async Task<bool> ConfereCodigo(int codigo, string email)
        {
            if (codigo <= 0 || string.IsNullOrWhiteSpace(email))
                return false;

            int codigoValido = await _loginRepository.BuscaCodigoRecuperacaoValido(email, ValidadeCodigoMinutos);
            if (codigoValido > 0 && codigoValido == codigo)
                return true;

            if (_tentativas.RegistrarFalha(email))
            {
                await _loginRepository.InvalidaCodigosRecuperacao(email);
                _tentativas.Limpar(email);
            }

            return false;
        }

        public string HashPassword(string senha)
        {
            return BCrypt.Net.BCrypt.HashPassword(senha);
        }

        public bool VerifyPassword(string senha, string senhaHash)
        {
            return BCrypt.Net.BCrypt.Verify(senha, senhaHash);
        }
        public string GenerateToken(string idUsuario, string email, string senhaHash, bool isAdmin, int expireMinutes = 60)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key não configurado");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            string profile = isAdmin ? "Admin" : "User";

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, idUsuario),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Role, profile),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        public async Task EnviarEmailAsync(string destinatario, string assunto, string mensagem)
        {
            // Antes ficavam no código (repositório público). Agora vêm de Email__Remetente e Email__Senha.
            var remetente = _configuration["Email:Remetente"] ?? throw new InvalidOperationException("Email:Remetente não configurado");
            var senha = _configuration["Email:Senha"] ?? throw new InvalidOperationException("Email:Senha não configurado");

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587, // Pode ser 465 ou 587 (google)
                Credentials = new NetworkCredential(remetente, senha),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(remetente),
                Subject = assunto,
                Body = mensagem,
                IsBodyHtml = true, 
            };

            mailMessage.To.Add(destinatario);

            await smtpClient.SendMailAsync(mailMessage);
        }
        public async Task<List<IdDescricaoPerfilModel>> RetornaPerfilUsuario(long idUsuario)
        {
            try
            {
                var lista = await _loginRepository.RetornaPerfilUsuario(idUsuario);
                return lista;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao listar perfis do usuário: {ex.Message}");
            }
        }
    }

}
