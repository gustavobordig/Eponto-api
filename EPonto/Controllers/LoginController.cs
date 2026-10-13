using Application.DTOs;
using Application.Interfaces;
using Domain.Entities.Login;
using EPonto.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EPonto.Controllers
{
    [ApiController]
    [Route("api/login")]
    public class LoginController : ControllerBase
    {
        private readonly ILoginService _loginService;
        private readonly ModoDemoOptions _demo;
        public LoginController(ILoginService loginService, IOptions<ModoDemoOptions> demo)
        {
            _loginService = loginService;
            _demo = demo.Value;
        }

        /// <summary>As contas de demonstração não trocam de senha: senão, quem chegasse depois não entraria.</summary>
        private ObjectResult ContaDemoProtegida() =>
            StatusCode(StatusCodes.Status403Forbidden, new { Sucesso = false, Mensagem = ModoDemo.MensagemContaProtegida });

        [HttpPost]
        [Route("RealizarLogin")]
        public async Task<ActionResult<LoginDTO>> RealizarLogin(LoginModel paramLogin)
        {
            try
            {
                LoginDTO auxResult = await _loginService.RealizarLogin(paramLogin);
                if (auxResult.Sucesso)
                    return Ok(auxResult);

                return Unauthorized(new LoginDTO { Sucesso = false, Mensagem = auxResult.Mensagem});
            }
            catch (Exception ex)
            {
                return Unauthorized(new LoginDTO { Sucesso = false, Mensagem = ex.Message });
            }
        }
        [HttpPost]
        [Route("AutenticarPerfil")]
        public async Task<ActionResult<AutenticarDTO>> AutenticarPerfil(LoginPerfilModel paramLogin)
        {
            try
            {
                AutenticarDTO auxResult = await _loginService.AutenticarPerfil(paramLogin);
                if (auxResult.Sucesso)
                    return Ok(auxResult);

                return Unauthorized(new AutenticarDTO { Sucesso = false, Mensagem = auxResult.Mensagem });
            }
            catch (Exception ex)
            {
                return Unauthorized(new AutenticarDTO { Sucesso = false, Mensagem = ex.Message });
            }
        }
        [HttpPost ]
        [Route("RecuperarSenha")]
        public async Task<ActionResult<bool>> RecuperarSenha(RecuperacaoSenhaModel param)
        {
            if (_demo.EhContaDemo(param.email)) return ContaDemoProtegida();
            try
            {
                bool auxBool = await _loginService.RecuperarSenha(param.email);
                if (auxBool)
                    return Ok(true);

                return BadRequest(false);
            }
            catch (Exception ex)
            {
                return BadRequest(false);
            }
        }
        [HttpPost]
        [Route("ValidaCodigoRecuperacao")]
        public async Task<ActionResult<bool>> ValidaCodigoRecuperacao(ValidaCodigoRecuperacaoModel param)
        {
            if (_demo.EhContaDemo(param.email)) return ContaDemoProtegida();
            try
            {
                bool auxResult = await _loginService.ValidaCodigoRecuperacao(param.codigo, param.email);
                if (auxResult)
                    return Ok(true);

                return BadRequest(false);
            }
            catch (Exception ex)
            {
                return BadRequest(false);
            }
        }
        [HttpPost]
        [Route("AlteraSenhaLogin")]
        public async Task<ActionResult<bool>> AlteraSenhaLogin(AlteraSenhaLoginModel param)
        {
            if (_demo.EhContaDemo(param.email)) return ContaDemoProtegida();
            try
            {
                bool auxResult = await _loginService.AlteraSenhaLogin(param.senha, param.email, param.codigo);
                if (auxResult)
                    return Ok(true);

                return BadRequest(false);
            }
            catch (Exception ex)
            {
                return BadRequest(false);
            }
        }
    }
}
