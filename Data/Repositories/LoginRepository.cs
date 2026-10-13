using Dapper;
using Data.Connections;
using Data.Interfaces;
using Data.Util;
using Domain.Entities;
using Domain.Entities.Login;
using Domain.Entities.Perfil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Data.Repositories
{
    public class LoginRepository : ILoginRepository
    {
        #region conexão
        private readonly DbSession _dbSession;

        public LoginRepository(DbSession dbSession)
        {
            _dbSession = dbSession;
        }
        #endregion
        public async Task<LoginAuxiliarModel> BuscaUsuarioNoSistema(string email)
        {
            string sql = @"
                    SELECT U.ID_USUARIO,
                           U.SENHA
                      FROM USUARIO U
                     WHERE U.EMAIL = @EMAIL
                       AND U.IND_ATIVO = 1
                ";

            object auxParametros = new
            {
                EMAIL = email
            };

            using (var reader = _dbSession.Connection.ExecuteReader(sql, auxParametros))
            {
                if (reader.Read())
                {
                    return new LoginAuxiliarModel 
                    { 
                        Senha = reader["SENHA"].ToString(),
                        IdUsuario = reader["ID_USUARIO"].ToLong() 
                    };
                }
            }

            return new LoginAuxiliarModel
            {
                Senha = "",
                IdUsuario = 0
            };
        }
        public async Task<bool> VerificaPerfilAdministrador(int IdPerfil, long IdUsuario)
        {
            string sql = @"
                    SELECT P.IND_ACESSO_ADMIN AS IND_ADMIN
                      FROM PERFIL P
                      JOIN PERFIL_USUARIO PU ON PU.ID_PERFIL = P.ID_PERFIL
                     WHERE P.ID_PERFIL = @ID_PERFIL
                       AND PU.ID_USUARIO = @ID_USUARIO    
                ";

            object auxParametros = new
            {
                ID_PERFIL = IdPerfil,
                ID_USUARIO = IdUsuario
            };

            using (var reader = _dbSession.Connection.ExecuteReader(sql, auxParametros))
                if (reader.Read())
                    return reader["IND_ADMIN"].ToInt() == 1;

            return false;
        }
        public async Task<bool> InsereRegistroLogin(long IdUsuario, string token)
        {
            string sql = @"
                INSERT INTO LOGIN (
                    ID_USUARIO,
                    TOKEN,
                    DAT_LOGIN
                )
                VALUES (
                    @ID_USUARIO,
                    @TOKEN,
                    NOW()
                )
                ";

            object auxParametros = new
            {
                ID_USUARIO = IdUsuario,
                TOKEN = token
            };

            return _dbSession.Connection.ExecuteScalar(sql, auxParametros).ToBool();
        }
        public async Task<bool> SalvaCodigoRecuperacao(int codigo, string email)
        {
            string sql = @"
                INSERT INTO LOGIN (
                    ID_USUARIO,
                    COD_RECUPERACAO,
                    DAT_LOGIN
                )
                VALUES (
                    (SELECT U.ID_USUARIO FROM USUARIO U WHERE U.EMAIL = @EMAIL AND U.IND_ATIVO = 1),
                    @CODIGO,
                    NOW()
                )
                ";

            object auxParametros = new
            {
                CODIGO = codigo,
                EMAIL = email
            };

            return _dbSession.Connection.ExecuteScalar(sql, auxParametros).ToBool();
        }
        /// <summary>
        /// Último código de recuperação do e-mail, se ainda estiver dentro da validade.
        /// Devolve 0 quando não há código, ele expirou ou já foi usado.
        /// </summary>
        public async Task<int> BuscaCodigoRecuperacaoValido(string email, int validadeMinutos)
        {
            string sql = @"
                    SELECT L.COD_RECUPERACAO
                      FROM LOGIN L
                      JOIN USUARIO U ON U.ID_USUARIO = L.ID_USUARIO
                     WHERE U.EMAIL = @EMAIL
                       AND U.IND_ATIVO = 1
                       AND L.COD_RECUPERACAO IS NOT NULL
                       AND L.DAT_LOGIN >= NOW() - INTERVAL @VALIDADE MINUTE
                     ORDER BY L.DAT_LOGIN DESC
                     LIMIT 1
                ";

            var codigo = await _dbSession.Connection.ExecuteScalarAsync<int?>(sql, new { EMAIL = email, VALIDADE = validadeMinutos });
            return codigo ?? 0;
        }

        /// <summary>Anula os códigos de recuperação do e-mail (depois do uso ou de tentativas demais).</summary>
        public async Task InvalidaCodigosRecuperacao(string email)
        {
            string sql = @"
                    UPDATE LOGIN L
                      JOIN USUARIO U ON U.ID_USUARIO = L.ID_USUARIO
                       SET L.COD_RECUPERACAO = NULL
                     WHERE U.EMAIL = @EMAIL
                       AND L.COD_RECUPERACAO IS NOT NULL
                ";

            await _dbSession.Connection.ExecuteAsync(sql, new { EMAIL = email });
        }

        public async Task<bool> SalvaAlteracaoSenha(string senha, string email)
        {
            string sql = @"
                    UPDATE USUARIO U
                       SET U.SENHA = @SENHA
                     WHERE U.EMAIL = @EMAIL
                       AND U.IND_ATIVO = 1
                ";

            int linhas = await _dbSession.Connection.ExecuteAsync(sql, new { EMAIL = email, SENHA = senha });
            return linhas > 0;
        }
        public async Task<List<IdDescricaoPerfilModel>> RetornaPerfilUsuario(long idUsuario)
        {
            string sql = @"SELECT P.ID_PERFIL,
	                              P.DSC_PERFIL
                             FROM PERFIL_USUARIO PU
                             JOIN PERFIL P ON P.ID_PERFIL = PU.ID_PERFIL
                            WHERE PU.ID_USUARIO = @ID_USUARIO";

            var parametros = new
            {
                ID_USUARIO = idUsuario
            };

            var perfis = await _dbSession.Connection.QueryAsync<IdDescricaoPerfilModel>(sql, parametros);
            return perfis.AsList();
        }
    }
}
