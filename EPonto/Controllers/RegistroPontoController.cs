using EPonto.Autorizacao;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Entities.Ponto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EPonto.Controllers
{
    [Route("api/RegistroPonto")]
    [ApiController]
    public class RegistroPontoController : ControllerBase
    {
        private readonly IRegistroPontoService _registroPontoService;

        public RegistroPontoController(IRegistroPontoService registroPontoService)
        {
            _registroPontoService = registroPontoService;
        }

        [HttpPost]
        [Authorize]
        [Route("Inserir")]
        public async Task<IActionResult> CriarRegistroPonto([FromBody] RegistroPontoModel ponto)
        {
            if (!User.PodeAcessarDadosDe(ponto.IdUsuario)) return this.AcessoNegado();
            var resultado = await _registroPontoService.CriarRegistroPontoAsync(ponto);
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("{id}")]
        public async Task<IActionResult> ObterRegistroPontoId(int id)
        {
            var resultado = await _registroPontoService.ObterRegistroPontoIdAsync(id);
            if (resultado.Sucesso && resultado.RegistroPonto != null)
                return Ok(resultado);

            return NotFound(resultado);
        }

        [HttpGet]
        [Authorize]
        [Route("ObterRegistrosUsuario")]
        public async Task<IActionResult> ObterRegistrosUsuario(int idUsuario)
        {
            if (!User.PodeAcessarDadosDe(idUsuario)) return this.AcessoNegado();
            var resultado = await _registroPontoService.ObterRegistrosUsuarioAsync(idUsuario);
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("Listar")]
        public async Task<IActionResult> ListarTodosRegistrosPonto()
        {
            var resultado = await _registroPontoService.ListarTodosRegistrosPontoAsync();
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExcluirRegistroPonto(int id)
        {
            var resultado = await _registroPontoService.ExcluirRegistroPontoAsync(id);
            if (resultado.Sucesso)
                return Ok(resultado);

            return NotFound(resultado);
        }

        //[HttpPost]
        //[Route("Validar")]
        //public async Task<IActionResult> ValidarMes(int idUsuario, int anoReferencia, int mesReferencia, int statusValidacao)
        //{
        //    var resultado = await _registroPontoService.ValidarMesAsync(idUsuario, anoReferencia, mesReferencia, statusValidacao);
        //    if (resultado.Sucesso)
        //        return Ok(resultado);
        //    return BadRequest(resultado);
        //}

        [HttpPost]
        [Authorize]
        [Route("CriarSolicitacaoAlteracao")]
        public async Task<IActionResult> CriarSolicitacaoAlteracao([FromBody] SolicitacaoAjustePontoModel solicitacao)
        {
            if (!User.PodeAcessarDadosDe(solicitacao.IdSolicitante)) return this.AcessoNegado();
            var resultado = await _registroPontoService.CriarSolicitacaoAsync(solicitacao);
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("ListarSolicitacoesAlteracao")]
        public async Task<IActionResult> ListarSolicitacoes([FromQuery] int? status = null)
        {
            var resultado = await _registroPontoService.ListarSolicitacoesAsync(status);
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        /// <summary>
        /// Lista as solicitações de ajuste de ponto feitas pelo usuário (para ele acompanhar o status).
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("ListarSolicitacoesAlteracaoUsuario/{idUsuario}")]
        public async Task<IActionResult> ListarSolicitacoesUsuario(int idUsuario)
        {
            if (!User.PodeAcessarDadosDe(idUsuario)) return this.AcessoNegado();

            var resultado = await _registroPontoService.ListarSolicitacoesAsync(idSolicitante: idUsuario);
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("ObterSolicitacaoAlteracao/{id}")]
        public async Task<IActionResult> ObterSolicitacaoAlteracao(int id)
        {
            var resultado = await _registroPontoService.ObterSolicitacaoAltercaoPorIdAsync(id);

            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("ValidarSolicitacao/{idSolicitacao}")]
        public async Task<IActionResult> ValidarSolicitacao(int idSolicitacao, [FromBody] ValidarSolicitacao request)
        {
            var resultado = await _registroPontoService.AprovarReprovarSolicitacaoAsync(idSolicitacao, request.Aprovado);

            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

    }
}
