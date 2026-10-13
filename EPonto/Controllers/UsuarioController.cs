using EPonto.Autorizacao;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Domain.Entities;
using Application.Interfaces;
using Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using EPonto.Demo;
using Microsoft.Extensions.Options;

namespace EPonto.Controllers
{
    [Route("api/Usuario")]
    [ApiController]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;
        private readonly ModoDemoOptions _demo;

        public UsuarioController(IUsuarioService usuarioService, IOptions<ModoDemoOptions> demo)
        {
            _usuarioService = usuarioService;
            _demo = demo.Value;
        }

        /// <summary>Contas divulgadas para teste não são editadas nem desativadas por quem as usa.</summary>
        private async Task<bool> EhContaDemo(int id) =>
            _demo.Ativo && _demo.EhContaDemo((await _usuarioService.ObterUsuarioPorIdAsync(id)).Usuario?.Email);

        private ObjectResult ContaDemoProtegida() =>
            StatusCode(StatusCodes.Status403Forbidden, new { Sucesso = false, Mensagem = ModoDemo.MensagemContaProtegida });

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("Inserir")]
        public async Task<IActionResult> CriarUsuario([FromBody] UsuarioModel usuario)
        {
            var resultado = await _usuarioService.CriarUsuarioAsync(usuario);
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> ObterUsuarioPorId(int id)
        {
            if (!User.PodeAcessarDadosDe(id)) return this.AcessoNegado();
            var resultado = await _usuarioService.ObterUsuarioPorIdAsync(id);
            if (resultado.Sucesso && resultado.Usuario != null)
                return Ok(resultado);

            return NotFound(resultado);
        }

        [Authorize]
        [HttpGet("Contrato/{id}")]
        public async Task<IActionResult> ObterContratoUsuario(int id)
        {
            if (!User.PodeAcessarDadosDe(id)) return this.AcessoNegado();
            var resultado = await _usuarioService.ObterContratoUsuarioAsync(id);
            if (resultado.Sucesso && resultado.Contrato != null)
                return Ok(resultado);

            return NotFound(resultado);
        }

        /// <summary>
        /// Lista todos os usuários.
        /// </summary>
        /// <remarks>
        /// Retorna todos os usuários cadastrados no sistema. Requer autenticação.
        /// </remarks>
        /// <response code="200">Usuários retornados com sucesso</response>
        /// <response code="400">Erro ao listar usuários</response>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("Listar")]
        public async Task<IActionResult> ListarTodosUsuarios()
        {
            var resultado = await _usuarioService.ListarTodosUsuariosAsync();
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [Authorize]
        [HttpPut("Atualizar/{id}")]
        public async Task<IActionResult> AtualizarUsuario(int id, [FromBody] UsuarioModel usuario)
        {
            if (!User.PodeAcessarDadosDe(id)) return this.AcessoNegado();
            if (usuario == null)
            {
                return BadRequest("Dados do usuário são obrigatórios.");
            }

            usuario.IdUsuario = id;
            if (await EhContaDemo(id)) return ContaDemoProtegida();

            // Quem não é admin edita os próprios dados, mas não o cargo nem a jornada
            if (!User.EhAdmin())
            {
                var atual = await _usuarioService.ObterUsuarioPorIdAsync(id);
                if (atual.Usuario == null)
                    return NotFound(atual);
                usuario.IdCargo = atual.Usuario.IdCargo;
                usuario.IdJornada = atual.Usuario.IdJornada;
            }

            var resultado = await _usuarioService.AtualizarUsuarioAsync(usuario);
            if (resultado.Sucesso)
                return Ok(resultado);

            return BadRequest(resultado);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("Deletar/{id}")]
        public async Task<IActionResult> ExcluirUsuario(int id)
        {
            if (await EhContaDemo(id)) return ContaDemoProtegida();
            var resultado = await _usuarioService.ExcluirUsuarioAsync(id);
            if (resultado.Sucesso)
                return Ok(resultado);

            return NotFound(resultado);
        }

        [Authorize]
        [HttpGet("Hierarquia")]
        [ProducesResponseType(typeof(IEnumerable<UsuarioModel>), 200)]
        public async Task<IActionResult> ObterHierarquia()
        {
            var arvore = await _usuarioService.ObterHierarquia();
            return Ok(arvore);
        }
    }
}
