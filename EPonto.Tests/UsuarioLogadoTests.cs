using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EPonto.Autorizacao;

namespace EPonto.Tests;

public class UsuarioLogadoTests
{
    private static ClaimsPrincipal Usuario(string claimId, string id, string role) =>
        new(new ClaimsIdentity(new[] { new Claim(claimId, id), new Claim(ClaimTypes.Role, role) }, "jwt"));

    [Fact]
    public void Le_o_id_pela_claim_NameIdentifier_ou_sub()
    {
        Assert.Equal(7, Usuario(ClaimTypes.NameIdentifier, "7", "User").IdUsuario());
        Assert.Equal(7, Usuario(JwtRegisteredClaimNames.Sub, "7", "User").IdUsuario());
    }

    [Fact]
    public void Funcionario_so_acessa_os_proprios_dados()
    {
        var funcionario = Usuario(ClaimTypes.NameIdentifier, "7", "User");
        Assert.False(funcionario.EhAdmin());
        Assert.True(funcionario.PodeAcessarDadosDe(7));
        Assert.False(funcionario.PodeAcessarDadosDe(8));
    }

    [Fact]
    public void Admin_acessa_os_dados_de_qualquer_um()
    {
        var admin = Usuario(ClaimTypes.NameIdentifier, "1", "Admin");
        Assert.True(admin.EhAdmin());
        Assert.True(admin.PodeAcessarDadosDe(8));
    }

    [Fact]
    public void Sem_id_no_token_nao_acessa_nada()
    {
        var semId = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "User") }, "jwt"));
        Assert.Null(semId.IdUsuario());
        Assert.False(semId.PodeAcessarDadosDe(1));
    }
}
