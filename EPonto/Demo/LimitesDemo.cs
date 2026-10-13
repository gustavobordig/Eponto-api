using System.Threading.RateLimiting;
using EPonto.Autorizacao;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EPonto.Demo
{
    /// <summary>
    /// Limites do modo demonstração: gravações por usuário e no total, a cada hora,
    /// e tentativas de login por IP. Leituras não têm limite.
    /// </summary>
    public static class LimitesDemo
    {
        public static IServiceCollection AddLimitesDemo(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, token) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                        context.HttpContext.Response.Headers.RetryAfter = ((int)espera.TotalSeconds).ToString();
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(new { Sucesso = false, Mensagem = ModoDemo.MensagemLimite }, token);
                };

                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    PartitionedRateLimiter.Create<HttpContext, string>(context => PorUsuarioOuIp(context)),
                    PartitionedRateLimiter.Create<HttpContext, string>(context => Total(context)));
            });
            return services;
        }

        private static ModoDemoOptions Opcoes(HttpContext context) =>
            context.RequestServices.GetRequiredService<IOptions<ModoDemoOptions>>().Value;

        private static RateLimitPartition<string> PorUsuarioOuIp(HttpContext context)
        {
            var opcoes = Opcoes(context);
            var request = context.Request;
            if (!opcoes.Ativo || !ModoDemo.EhGravacao(request.Method))
                return RateLimitPartition.GetNoLimiter("livre");

            if (ModoDemo.EhRotaDeLogin(request.Path))
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
                return RateLimitPartition.GetFixedWindowLimiter($"login:{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opcoes.TentativasDeLoginPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                });
            }

            // Sem token, a rota responde 401 de qualquer jeito
            var id = context.User.IdUsuario();
            if (id is null) return RateLimitPartition.GetNoLimiter("anonimo");

            return RateLimitPartition.GetFixedWindowLimiter($"usuario:{id}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = opcoes.GravacoesPorUsuarioPorHora,
                Window = TimeSpan.FromHours(1),
            });
        }

        private static RateLimitPartition<string> Total(HttpContext context)
        {
            var opcoes = Opcoes(context);
            if (!opcoes.Ativo || !ModoDemo.EhGravacao(context.Request.Method) || ModoDemo.EhRotaDeLogin(context.Request.Path))
                return RateLimitPartition.GetNoLimiter("livre");

            return RateLimitPartition.GetFixedWindowLimiter("total", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = opcoes.GravacoesTotaisPorHora,
                Window = TimeSpan.FromHours(1),
            });
        }

        /// <summary>403 para as gravações bloqueadas na demonstração (ver ModoDemo.Bloqueio).</summary>
        public static IApplicationBuilder UseProtecoesDemo(this IApplicationBuilder app) =>
            app.Use(async (context, next) =>
            {
                var mensagem = Opcoes(context).Bloqueio(context.Request.Method, context.Request.Path);
                if (mensagem is null)
                {
                    await next();
                    return;
                }
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { Sucesso = false, Mensagem = mensagem });
            });
    }
}
