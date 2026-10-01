using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Questao5.Infrastructure.Sqlite
{
    public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
    {
        private readonly DatabaseConfig databaseConfig;

        public SqliteConnectionFactory(DatabaseConfig databaseConfig)
        {
            this.databaseConfig = databaseConfig;
        }

        public SqliteConnection OpenConnection()
        {
            EnsureDatabaseDirectoryExists();

            var connection = new SqliteConnection(databaseConfig.Name);

            try
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA foreign_keys;";

                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                {
                    throw new InvalidOperationException("As foreign keys do SQLite não foram habilitadas.");
                }

                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private void EnsureDatabaseDirectoryExists()
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder(databaseConfig.Name);

            if (connectionStringBuilder.Mode == SqliteOpenMode.Memory ||
                string.Equals(connectionStringBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fullDatabasePath = Path.GetFullPath(connectionStringBuilder.DataSource);
            var directoryPath = Path.GetDirectoryName(fullDatabasePath);

            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}