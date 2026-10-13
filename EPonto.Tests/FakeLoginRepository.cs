using Data.Interfaces;
using Domain.Entities.Login;

namespace EPonto.Tests;

/// <summary>Repositório de login em memória, com um usuário e o último código gerado.</summary>
public class FakeLoginRepository : ILoginRepository
{
    public const string Email = "ana@exemplo.com";
    public string SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha-atual");
    public int CodigoSalvo;
    public bool CodigoExpirado;
    public int Invalidacoes;
    public string? SenhaGravada;

    public Task<LoginAuxiliarModel> BuscaUsuarioNoSistema(string email) =>
        Task.FromResult(email == Email
            ? new LoginAuxiliarModel { IdUsuario = 1, Senha = SenhaHash }
            : new LoginAuxiliarModel { IdUsuario = 0, Senha = "" });

    public Task<bool> SalvaCodigoRecuperacao(int codigo, string email)
    {
        CodigoSalvo = codigo;
        return Task.FromResult(true);
    }

    public Task<int> BuscaCodigoRecuperacaoValido(string email, int validadeMinutos) =>
        Task.FromResult(email == Email && !CodigoExpirado ? CodigoSalvo : 0);

    public Task InvalidaCodigosRecuperacao(string email)
    {
        Invalidacoes++;
        CodigoSalvo = 0;
        return Task.CompletedTask;
    }

    public Task<bool> SalvaAlteracaoSenha(string senha, string email)
    {
        SenhaGravada = senha;
        return Task.FromResult(true);
    }

    public Task<bool> InsereRegistroLogin(long IdUsuario, string token) => Task.FromResult(true);
    public Task<bool> VerificaPerfilAdministrador(int IdPerfil, long IdUsuario) => Task.FromResult(false);
    public Task<List<IdDescricaoPerfilModel>> RetornaPerfilUsuario(long idUsuario) =>
        Task.FromResult(new List<IdDescricaoPerfilModel>());
}
