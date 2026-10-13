using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace EPonto.Demo
{
    /// <summary>
    /// Volta o banco para os dados de exemplo (db/seed.sql, embutido na API):
    /// esvazia todas as tabelas do banco da conexão e roda o seed de novo.
    /// Só roda com o modo demonstração ligado; o banco do Hangfire fica em outra conexão.
    /// </summary>
    public class RestaurarDemoJob
    {
        private readonly IConfiguration _configuration;
        private readonly ModoDemoOptions _opcoes;
        private readonly ILogger<RestaurarDemoJob> _logger;

        public RestaurarDemoJob(IConfiguration configuration, IOptions<ModoDemoOptions> opcoes, ILogger<RestaurarDemoJob> logger)
        {
            _configuration = configuration;
            _opcoes = opcoes.Value;
            _logger = logger;
        }

        /// <summary>O seed sem o "USE eponto", para valer no banco da conexão (no Railway o nome pode ser outro).</summary>
        public static string Seed()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("seed.sql")
                ?? throw new InvalidOperationException("seed.sql não está embutido na API.");
            using var leitor = new StreamReader(stream);
            return Regex.Replace(leitor.ReadToEnd(), @"^\s*USE\s+\w+\s*;\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        public async Task Restaurar()
        {
            if (!_opcoes.Ativo) return;

            await using var conexao = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conexao.OpenAsync();

            var tabelas = new List<string>();
            await using (var listar = new MySqlCommand(
                "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'", conexao))
            await using (var leitor = await listar.ExecuteReaderAsync())
                while (await leitor.ReadAsync()) tabelas.Add(leitor.GetString(0));

            var sql = "SET FOREIGN_KEY_CHECKS = 0;\n" +
                      string.Concat(tabelas.Select(t => $"TRUNCATE TABLE `{t}`;\n")) +
                      "SET FOREIGN_KEY_CHECKS = 1;\n" +
                      Seed();

            await using var restaurar = new MySqlCommand(sql, conexao);
            await restaurar.ExecuteNonQueryAsync();
            _logger.LogInformation("Demonstração restaurada: {Tabelas} tabelas voltaram aos dados de exemplo.", tabelas.Count);
        }

        /// <summary>Na primeira subida, com o banco só com o esquema, carrega os dados de exemplo.</summary>
        public async Task RestaurarSeVazio()
        {
            if (!_opcoes.Ativo) return;

            await using var conexao = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conexao.OpenAsync();
            await using var contar = new MySqlCommand("SELECT COUNT(*) FROM USUARIO", conexao);
            if (Convert.ToInt64(await contar.ExecuteScalarAsync()) == 0)
                await Restaurar();
        }
    }
}
