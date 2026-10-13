using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace EPonto.Autorizacao
{
    /// <summary>
    /// Leitura do usuário logado a partir do JWT. O id vem da claim "sub"
    /// (o JwtBearer a expõe como NameIdentifier) e o perfil da role (Admin ou User).
    /// </summary>
    public static class UsuarioLogado
    {
        public const string Admin = "Admin";

        public static int? IdUsuario(this ClaimsPrincipal user)
        {
            var valor = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return int.TryParse(valor, out var id) ? id : null;
        }

        public static bool EhAdmin(this ClaimsPrincipal user) => user.IsInRole(Admin);

        /// <summary>Admin acessa os dados de qualquer um; os demais, só os próprios.</summary>
        public static bool PodeAcessarDadosDe(this ClaimsPrincipal user, long idUsuario) =>
            user.EhAdmin() || user.IdUsuario() == idUsuario;

        /// <summary>403 no mesmo formato das demais respostas da API.</summary>
        public static ObjectResult AcessoNegado(this ControllerBase controller) =>
            controller.StatusCode(StatusCodes.Status403Forbidden, new
            {
                Sucesso = false,
                Mensagem = "Acesso negado. Você só pode acessar os seus próprios dados."
            });
    }
}
