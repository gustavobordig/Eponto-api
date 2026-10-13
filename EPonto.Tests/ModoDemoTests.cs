using System.Security.Claims;
using System.Threading.RateLimiting;
using EPonto.Demo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EPonto.Tests;

public class ModoDemoTests
{
    private static readonly ModoDemoOptions Ligado = new()
    {
        Ativo = true,
        Emails = new[] { "rh.teste@eponto.local", "ana.teste@eponto.local" },
        GravacoesPorUsuarioPorHora = 3,
        GravacoesTotaisPorHora = 5,
        TentativasDeLoginPorMinuto = 2,
    };

    private static PartitionedRateLimiter<HttpContext> Limitador(ModoDemoOptions opcoes, out IServiceProvider servicos)
    {
        var colecao = new ServiceCollection();
        colecao.AddSingleton(Options.Create(opcoes));
        colecao.AddLimitesDemo();
        servicos = colecao.BuildServiceProvider();
        return servicos.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
    }

    private static HttpContext Requisicao(IServiceProvider servicos, string metodo, string caminho, int? idUsuario = null, string ip = "10.0.0.1")
    {
        var context = new DefaultHttpContext { RequestServices = servicos };
        context.Request.Method = metodo;
        context.Request.Path = caminho;
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        if (idUsuario is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()!) }, "jwt"));
        return context;
    }

    private static bool Passa(PartitionedRateLimiter<HttpContext> limitador, HttpContext context) =>
        limitador.AttemptAcquire(context).IsAcquired;

    [Fact]
    public void Reconhece_as_contas_demo_pelo_email_sem_diferenciar_maiusculas()
    {
        Assert.True(Ligado.EhContaDemo("RH.Teste@eponto.local"));
        Assert.False(Ligado.EhContaDemo("outra@empresa.com"));
        Assert.False(Ligado.EhContaDemo(null));
        Assert.False(new ModoDemoOptions { Emails = Ligado.Emails }.EhContaDemo("rh.teste@eponto.local"));
    }

    [Fact]
    public void Bloqueia_gravacao_nos_perfis_e_libera_a_leitura()
    {
        Assert.NotNull(Ligado.Bloqueio("DELETE", "/api/Perfil/RemoverPerfil/1"));
        Assert.NotNull(Ligado.Bloqueio("POST", "/api/perfil/CadastrarVinculoPerfilUsuario"));
        Assert.Null(Ligado.Bloqueio("GET", "/api/Perfil/ListarPerfis"));
        Assert.Null(Ligado.Bloqueio("POST", "/api/RegistroPonto/Inserir"));
        Assert.Null(new ModoDemoOptions().Bloqueio("DELETE", "/api/Perfil/RemoverPerfil/1"));
    }

    [Fact]
    public void Cada_usuario_grava_ate_o_limite_por_hora_e_leitura_nao_conta()
    {
        var limitador = Limitador(Ligado, out var servicos);
        for (var i = 0; i < 10; i++)
            Assert.True(Passa(limitador, Requisicao(servicos, "GET", "/api/Usuario/1", 1)));

        for (var i = 0; i < 3; i++)
            Assert.True(Passa(limitador, Requisicao(servicos, "POST", "/api/RegistroPonto/Inserir", 1)));
        Assert.False(Passa(limitador, Requisicao(servicos, "PUT", "/api/Usuario/Atualizar/1", 1)));

        // Outro usuário tem a própria cota
        Assert.True(Passa(limitador, Requisicao(servicos, "POST", "/api/RegistroPonto/Inserir", 2)));
    }

    [Fact]
    public void Existe_um_teto_total_mesmo_criando_varios_usuarios()
    {
        var limitador = Limitador(Ligado, out var servicos);
        var aceitas = Enumerable.Range(1, 10).Count(id => Passa(limitador, Requisicao(servicos, "POST", "/api/Cargo/Inserir", id)));
        Assert.Equal(5, aceitas);
    }

    [Fact]
    public void Login_tem_limite_por_ip()
    {
        var limitador = Limitador(Ligado, out var servicos);
        Assert.True(Passa(limitador, Requisicao(servicos, "POST", "/api/login/RealizarLogin")));
        Assert.True(Passa(limitador, Requisicao(servicos, "POST", "/api/login/RealizarLogin")));
        Assert.False(Passa(limitador, Requisicao(servicos, "POST", "/api/login/RealizarLogin")));
        Assert.True(Passa(limitador, Requisicao(servicos, "POST", "/api/login/RealizarLogin", ip: "10.0.0.2")));
    }

    [Fact]
    public void Sem_modo_demo_nada_e_limitado()
    {
        var limitador = Limitador(new ModoDemoOptions(), out var servicos);
        for (var i = 0; i < 50; i++)
            Assert.True(Passa(limitador, Requisicao(servicos, "POST", "/api/RegistroPonto/Inserir", 1)));
    }

    [Fact]
    public void Seed_embutido_vale_para_qualquer_nome_de_banco()
    {
        var seed = RestaurarDemoJob.Seed();
        Assert.Contains("INSERT INTO USUARIO", seed);
        Assert.DoesNotContain("USE eponto", seed, StringComparison.OrdinalIgnoreCase);
    }
}
