using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Sqlite;

public sealed class DatabaseBootstrapForeignKeyTests
{
    [Fact]
    public void Setup_CreatesSchemaWithForeignKeysEnabledAndRejectsOrphanMovement()
    {
        using var database = new TemporarySqliteDatabase();
        var config = new DatabaseConfig(database.ConnectionString);
        var connectionFactory = new SqliteConnectionFactory(config);
        var bootstrap = new DatabaseBootstrap(connectionFactory);

        bootstrap.Setup();

        using var connection = connectionFactory.OpenConnection();
        Assert.Equal(1L, connection.ExecuteScalar<long>("PRAGMA foreign_keys;"));

        var exception = Assert.Throws<SqliteException>(() => connection.Execute(
            """
            INSERT INTO movimento(
                idmovimento,
                idcontacorrente,
                datamovimento,
                tipomovimento,
                valor)
            VALUES (
                @MovementId,
                @AccountId,
                @MovementDate,
                @MovementType,
                @Value);
            """,
            new
            {
                MovementId = Guid.NewGuid().ToString(),
                AccountId = Guid.NewGuid().ToString(),
                MovementDate = DateTimeOffset.UtcNow.ToString("O"),
                MovementType = "C",
                Value = 1.0
            }));

        Assert.Equal(19, exception.SqliteErrorCode);
    }
}