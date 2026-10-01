using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Questao5.Tests.Infrastructure.Sqlite;

internal sealed class TemporarySqliteDatabase : IDisposable
{
    private readonly string directoryPath;
    private bool disposed;

    public TemporarySqliteDatabase()
    {
        directoryPath = Path.Combine(
            Path.GetTempPath(),
            "Questao5.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directoryPath);
        DatabasePath = Path.Combine(directoryPath, "database.sqlite");

        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    public string ConnectionString { get; }

    public string DatabasePath { get; }

    public SqliteConnection OpenConnection()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";

        if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
        {
            connection.Dispose();
            throw new InvalidOperationException("As foreign keys do SQLite não foram habilitadas.");
        }

        return connection;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}