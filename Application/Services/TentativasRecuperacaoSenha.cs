using System.Collections.Concurrent;

namespace Application.Services
{
    /// <summary>
    /// Conta as tentativas erradas de código de recuperação por e-mail, para
    /// impedir que o código de 6 dígitos seja descoberto por força bruta.
    /// Fica em memória (registrado como singleton): vale para uma instância da API.
    /// </summary>
    public class TentativasRecuperacaoSenha
    {
        public const int MaximoTentativas = 5;

        private readonly ConcurrentDictionary<string, int> _falhas = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Registra uma falha e devolve se o limite foi atingido.</summary>
        public bool RegistrarFalha(string email) =>
            _falhas.AddOrUpdate(email, 1, (_, atual) => atual + 1) >= MaximoTentativas;

        public void Limpar(string email) => _falhas.TryRemove(email, out _);
    }
}
