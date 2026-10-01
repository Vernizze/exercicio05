using Microsoft.Data.Sqlite;

namespace Questao5.Infrastructure.Sqlite
{
    public sealed class DatabaseConfig
    {
        public DatabaseConfig(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A configuração DatabaseName é obrigatória.", nameof(name));
            }

            var connectionStringBuilder = new SqliteConnectionStringBuilder(name)
            {
                ForeignKeys = true
            };

            Name = connectionStringBuilder.ToString();
        }

        public string Name { get; }
    }
}
