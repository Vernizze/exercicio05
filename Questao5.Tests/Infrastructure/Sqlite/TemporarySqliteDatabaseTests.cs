using Microsoft.Data.Sqlite;

namespace Questao5.Tests.Infrastructure.Sqlite;

public sealed class TemporarySqliteDatabaseTests
{
    [Fact]
    public void Constructor_CreatesUniqueDatabaseOutsideProjectWorkspace()
    {
        using var firstDatabase = new TemporarySqliteDatabase();
        using var secondDatabase = new TemporarySqliteDatabase();

        Assert.NotEqual(firstDatabase.DatabasePath, secondDatabase.DatabasePath);
        Assert.StartsWith(Path.GetTempPath(), firstDatabase.DatabasePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(firstDatabase.DatabasePath));
    }

    [Fact]
    public void OpenConnection_EnablesForeignKeysAndRejectsOrphanRecord()
    {
        using var database = new TemporarySqliteDatabase();
        using var connection = database.OpenConnection();

        using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = """
                CREATE TABLE parent (
                    id TEXT PRIMARY KEY
                );

                CREATE TABLE child (
                    id TEXT PRIMARY KEY,
                    parent_id TEXT NOT NULL,
                    FOREIGN KEY(parent_id) REFERENCES parent(id)
                );
                """;
            schemaCommand.ExecuteNonQuery();
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO child(id, parent_id) VALUES ($id, $parentId);";
        insertCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        insertCommand.Parameters.AddWithValue("$parentId", Guid.NewGuid().ToString());

        var exception = Assert.Throws<SqliteException>(() => insertCommand.ExecuteNonQuery());

        Assert.Equal(19, exception.SqliteErrorCode);
    }
}