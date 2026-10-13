using Application.Services;
using Domain.Entities.Login;
using Microsoft.Extensions.Configuration;

namespace EPonto.Tests;

public class RecuperacaoSenhaTests
{
    private readonly FakeLoginRepository _repo = new() { CodigoSalvo = 123456 };
    private readonly LoginService _service;

    public RecuperacaoSenhaTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        _service = new LoginService(_repo, config, new TentativasRecuperacaoSenha());
    }

    [Fact]
    public async Task Nao_troca_a_senha_sem_o_codigo()
    {
        Assert.False(await _service.AlteraSenhaLogin("nova-senha", FakeLoginRepository.Email, 0));
        Assert.Null(_repo.SenhaGravada);
    }

    [Fact]
    public async Task Nao_troca_a_senha_com_codigo_errado()
    {
        Assert.False(await _service.AlteraSenhaLogin("nova-senha", FakeLoginRepository.Email, 999999));
        Assert.Null(_repo.SenhaGravada);
    }

    [Fact]
    public async Task Troca_a_senha_com_o_codigo_certo_e_anula_o_codigo()
    {
        Assert.True(await _service.AlteraSenhaLogin("nova-senha", FakeLoginRepository.Email, 123456));

        Assert.NotNull(_repo.SenhaGravada);
        Assert.True(BCrypt.Net.BCrypt.Verify("nova-senha", _repo.SenhaGravada));
        Assert.Equal(0, _repo.CodigoSalvo);

        // O mesmo código não serve uma segunda vez
        _repo.SenhaGravada = null;
        Assert.False(await _service.AlteraSenhaLogin("outra-senha", FakeLoginRepository.Email, 123456));
        Assert.Null(_repo.SenhaGravada);
    }

    [Fact]
    public async Task Nao_aceita_codigo_expirado()
    {
        _repo.CodigoExpirado = true;
        Assert.False(await _service.AlteraSenhaLogin("nova-senha", FakeLoginRepository.Email, 123456));
    }

    [Fact]
    public async Task Cinco_erros_anulam_o_codigo()
    {
        for (int i = 0; i < TentativasRecuperacaoSenha.MaximoTentativas - 1; i++)
            Assert.False(await _service.ValidaCodigoRecuperacao(111111, FakeLoginRepository.Email));
        Assert.Equal(0, _repo.Invalidacoes);

        Assert.False(await _service.ValidaCodigoRecuperacao(111111, FakeLoginRepository.Email));
        Assert.Equal(1, _repo.Invalidacoes);

        // Depois disso nem o código certo vale; é preciso pedir outro
        Assert.False(await _service.AlteraSenhaLogin("nova-senha", FakeLoginRepository.Email, 123456));
    }

    [Fact]
    public async Task Recusa_senha_curta()
    {
        Assert.False(await _service.AlteraSenhaLogin("123", FakeLoginRepository.Email, 123456));
        Assert.Null(_repo.SenhaGravada);
    }

    [Fact]
    public async Task Recuperar_senha_de_email_inexistente_nao_revela_nada()
    {
        _repo.CodigoSalvo = 0;
        Assert.True(await _service.RecuperarSenha("nao-existe@exemplo.com"));
        Assert.Equal(0, _repo.CodigoSalvo);
    }

    [Fact]
    public async Task Login_responde_igual_para_email_inexistente_e_senha_errada()
    {
        var semEmail = await _service.RealizarLogin(new LoginModel { Email = "nao-existe@exemplo.com", Senha = "x" });
        var senhaErrada = await _service.RealizarLogin(new LoginModel { Email = FakeLoginRepository.Email, Senha = "errada" });

        Assert.False(semEmail.Sucesso);
        Assert.False(senhaErrada.Sucesso);
        Assert.Equal(semEmail.Mensagem, senhaErrada.Mensagem);
    }
}
