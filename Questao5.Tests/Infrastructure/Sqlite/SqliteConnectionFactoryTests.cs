using Microsoft.Data.Sqlite;
using Questao5.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Sqlite;

public sealed class SqliteConnectionFactoryTests
{
    [Fact]
    public void OpenConnection_CreatesDirectoryAndEnablesForeignKeys()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "Questao5.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directoryPath, "nested", "database.sqlite");

        try
        {
            var config = new DatabaseConfig(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = false
            }.ToString());
            var factory = new SqliteConnectionFactory(config);

            using var connection = factory.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys;";

            Assert.Equal(1L, command.ExecuteScalar());
            Assert.True(File.Exists(databasePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}