using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace EPonto.Demo
{
    /// <summary>
    /// Configuração do modo demonstração (seção "Demo"). Desligado por padrão;
    /// no deploy público: Demo__Ativo=true, Demo__Emails__0=rh.teste@eponto.local…
    /// </summary>
    public class ModoDemoOptions
    {
        public bool Ativo { get; set; }

        /// <summary>Contas divulgadas para teste: não podem ser editadas, desativadas nem ter a senha trocada.</summary>
        public string[] Emails { get; set; } = Array.Empty<string>();

        /// <summary>Gravações (POST, PUT, PATCH, DELETE) por usuário a cada hora.</summary>
        public int GravacoesPorUsuarioPorHora { get; set; } = 30;

        /// <summary>Teto de gravações somando todos os usuários, a cada hora.</summary>
        public int GravacoesTotaisPorHora { get; set; } = 500;

        /// <summary>Tentativas nas rotas de login e senha por IP, a cada minuto.</summary>
        public int TentativasDeLoginPorMinuto { get; set; } = 10;

        /// <summary>Quando o banco volta aos dados de exemplo (cron, no fuso de São Paulo).</summary>
        public string CronRestauracao { get; set; } = "0 4 * * *";
    }

    /// <summary>Regras do modo demonstração, sem dependência de HTTP para poderem ser testadas.</summary>
    public static class ModoDemo
    {
        public const string MensagemLimite =
            "Limite da demonstração atingido: cada conta pode gravar algumas vezes por hora. Tente de novo mais tarde.";
        public const string MensagemContaProtegida =
            "Esta é uma conta de demonstração: ela não pode ser editada, desativada nem ter a senha trocada.";
        public const string MensagemPerfisProtegidos =
            "Na demonstração, os perfis de acesso não podem ser alterados.";

        public static bool EhGravacao(string metodo) =>
            HttpMethods.IsPost(metodo) || HttpMethods.IsPut(metodo) || HttpMethods.IsPatch(metodo) || HttpMethods.IsDelete(metodo);

        public static bool EhContaDemo(this ModoDemoOptions opcoes, string? email) =>
            opcoes.Ativo && !string.IsNullOrWhiteSpace(email) &&
            opcoes.Emails.Any(e => string.Equals(e.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));

        public static string? Email(this ClaimsPrincipal user) =>
            user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

        /// <summary>Rotas de login e recuperação de senha, que não exigem token.</summary>
        public static bool EhRotaDeLogin(string caminho) =>
            caminho.StartsWith("/api/login", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gravação bloqueada para qualquer usuário enquanto o modo está ligado.
        /// Hoje, só os perfis de acesso: removê-los tiraria o acesso de todo mundo.
        /// </summary>
        public static string? Bloqueio(this ModoDemoOptions opcoes, string metodo, string caminho)
        {
            if (!opcoes.Ativo || !EhGravacao(metodo)) return null;
            if (caminho.StartsWith("/api/Perfil", StringComparison.OrdinalIgnoreCase)) return MensagemPerfisProtegidos;
            return null;
        }
    }
}
