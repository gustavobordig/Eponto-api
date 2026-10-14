namespace Application.Services
{
    /// <summary>
    /// Comentário opcional de quem aprova ou reprova um pedido (ajuste, ausência, férias).
    /// </summary>
    public static class ComentarioResposta
    {
        public const int Maximo = 500;

        public const string MensagemLonga = "O comentário pode ter no máximo 500 caracteres.";

        /// <summary>Sem espaços nas pontas; vazio vira null.</summary>
        public static string? Normalizar(string? comentario) =>
            string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();

        public static bool Longo(string? comentario) => (comentario?.Trim().Length ?? 0) > Maximo;
    }
}
