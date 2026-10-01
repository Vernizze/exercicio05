using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Sqlite;

public sealed class DatabaseBootstrapTests
{
    private const int ExpectedSeedCount = 6;

    private const string CreateAccountTableSql = """
        CREATE TABLE contacorrente (
            idcontacorrente TEXT(37) PRIMARY KEY,
            numero INTEGER(10) NOT NULL UNIQUE,
            nome TEXT(100) NOT NULL,
            ativo INTEGER(1) NOT NULL DEFAULT 0,
            CHECK(ativo IN (0, 1))
        );
        """;

    private const string CreateMovementTableSql = """
        CREATE TABLE movimento (
            idmovimento TEXT(37) PRIMARY KEY,
            idcontacorrente INTEGER(10) NOT NULL,
            datamovimento TEXT(25) NOT NULL,
            tipomovimento TEXT(1) NOT NULL,
            valor REAL NOT NULL,
            CHECK(tipomovimento IN ('C', 'D')),
            FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
        );
        """;

    [Fact]
    public void Setup_EmptyDatabase_CreatesCompleteVersionedSchemaAndSeed()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        bootstrap.Setup();

        using var connection = connectionFactory.OpenConnection();
        Assert.Equal(["contacorrente", "idempotencia", "movimento"], GetApplicationTables(connection));
        Assert.Equal(ExpectedSeedCount, CountAccounts(connection));
        Assert.Equal(1, connection.ExecuteScalar<int>("PRAGMA user_version;"));
        Assert.Empty(connection.Query("PRAGMA foreign_key_check;"));
        Assert.Equal("ok", connection.QuerySingle<string>("PRAGMA integrity_check;"));
    }

    [Fact]
    public void Setup_PartialCompatibleDatabase_CompletesSchemaAndPreservesExistingData()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(CreateAccountTableSql);
            connection.Execute(
                """
                INSERT INTO contacorrente(idcontacorrente, numero, nome, ativo)
                VALUES ('custom-account', 999, 'Conta preservada', 1);
                """);
        }

        bootstrap.Setup();

        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(["contacorrente", "idempotencia", "movimento"], GetApplicationTables(verificationConnection));
        Assert.Equal(ExpectedSeedCount + 1, CountAccounts(verificationConnection));
        Assert.Equal(
            "Conta preservada",
            verificationConnection.QuerySingle<string>(
                "SELECT nome FROM contacorrente WHERE idcontacorrente = 'custom-account';"));
    }

    [Fact]
    public void Setup_CompleteDatabase_DoesNotDuplicateOrOverwriteData()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);
        bootstrap.Setup();

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(
                """
                UPDATE contacorrente
                SET nome = 'Nome preservado'
                WHERE numero = 123;

                INSERT INTO contacorrente(idcontacorrente, numero, nome, ativo)
                VALUES ('custom-account', 999, 'Conta adicional', 1);
                """);
        }

        bootstrap.Setup();

        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(ExpectedSeedCount + 1, CountAccounts(verificationConnection));
        Assert.Equal(
            "Nome preservado",
            verificationConnection.QuerySingle<string>("SELECT nome FROM contacorrente WHERE numero = 123;"));
    }

    [Theory]
    [InlineData("CREATE TABLE contacorrente (idcontacorrente TEXT(37) PRIMARY KEY);", "contacorrente")]
    [InlineData("CREATE TABLE idempotencia (chave_idempotencia INTEGER PRIMARY KEY);", "idempotencia")]
    public void Setup_IncompatibleSchema_FailsSafely(string incompatibleTableSql, string expectedTableName)
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(incompatibleTableSql);
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains(expectedTableName, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        """
        CREATE TABLE contacorrente (
            idcontacorrente TEXT(37) PRIMARY KEY,
            numero INTEGER(10) NOT NULL,
            nome TEXT(100) NOT NULL,
            ativo INTEGER(1) NOT NULL DEFAULT 0,
            CHECK(ativo IN (0, 1))
        );
        """,
        "índice único")]
    [InlineData(
        """
        CREATE TABLE contacorrente (
            idcontacorrente TEXT(37) PRIMARY KEY,
            numero INTEGER(10) NOT NULL UNIQUE,
            nome TEXT(100) NOT NULL,
            ativo INTEGER(1) NOT NULL DEFAULT 1,
            CHECK(ativo IN (0, 1))
        );
        """,
        "ativo")]
    [InlineData(
        """
        CREATE TABLE contacorrente (
            idcontacorrente TEXT(37) PRIMARY KEY,
            numero INTEGER(10) NOT NULL UNIQUE,
            nome TEXT(100) NOT NULL,
            ativo INTEGER(1) NOT NULL DEFAULT 0
        );
        """,
        "CHECK")]
    public void Setup_IncompatibleAccountDefinition_RejectsSpecificSchemaDefect(
        string accountTableSql,
        string expectedDiagnostic)
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(accountTableSql);
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains(expectedDiagnostic, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(
        """
        CREATE TABLE movimento (
            idmovimento TEXT(37) PRIMARY KEY,
            idcontacorrente INTEGER(10) NOT NULL,
            datamovimento TEXT(25) NOT NULL,
            tipomovimento TEXT(1) NOT NULL,
            valor REAL NOT NULL,
            FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
        );
        """,
        "CHECK")]
    [InlineData(
        """
        CREATE TABLE movimento (
            idmovimento TEXT(37) PRIMARY KEY,
            idcontacorrente INTEGER(10) NOT NULL,
            datamovimento TEXT(25) NOT NULL,
            tipomovimento TEXT(1) NOT NULL,
            valor REAL NOT NULL,
            CHECK(tipomovimento IN ('C', 'D'))
        );
        """,
        "chave estrangeira")]
    [InlineData(
        """
        CREATE TABLE movimento (
            idmovimento TEXT(37) PRIMARY KEY,
            idcontacorrente INTEGER(10) NOT NULL,
            datamovimento TEXT(25) NOT NULL,
            tipomovimento TEXT(1) NOT NULL,
            valor REAL NOT NULL,
            CHECK(tipomovimento IN ('C', 'D')),
            CHECK(valor >= 0),
            FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
        );
        """,
        "CHECK")]
    public void Setup_IncompatibleMovementConstraints_RejectsSpecificSchemaDefect(
        string movementTableSql,
        string expectedDiagnostic)
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(CreateAccountTableSql);
            connection.Execute(movementTableSql);
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains(expectedDiagnostic, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Setup_CompatibleTablesWithMissingIdempotencyTable_CreatesOnlyMissingTable()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(CreateAccountTableSql);
            connection.Execute(CreateMovementTableSql);
        }

        bootstrap.Setup();

        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(["contacorrente", "idempotencia", "movimento"], GetApplicationTables(verificationConnection));
        Assert.Equal(ExpectedSeedCount, CountAccounts(verificationConnection));
    }

    [Fact]
    public void Setup_FutureSchemaVersion_FailsWithoutChangingDatabase()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute("PRAGMA user_version = 2;");
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains("superior", exception.Message, StringComparison.Ordinal);
        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(2, verificationConnection.ExecuteScalar<int>("PRAGMA user_version;"));
        Assert.Empty(GetApplicationTables(verificationConnection));
    }

    [Fact]
    public void Setup_FailureAfterCreatingTables_RollsBackAllChanges()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute("CREATE TABLE idempotencia (chave_idempotencia INTEGER PRIMARY KEY);");
        }

        Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(["idempotencia"], GetApplicationTables(verificationConnection));
        Assert.Equal(0, verificationConnection.ExecuteScalar<int>("PRAGMA user_version;"));
    }

    [Fact]
    public async Task Setup_ConcurrentInitializations_CreateSchemaAndSeedOnlyOnce()
    {
        using var database = new TemporarySqliteDatabase();
        var bootstraps = Enumerable.Range(0, 6)
            .Select(_ => CreateBootstrap(database).Bootstrap)
            .ToArray();

        await Task.WhenAll(bootstraps.Select(bootstrap => Task.Run(
            bootstrap.Setup,
            TestContext.Current.CancellationToken)));

        var connectionFactory = CreateBootstrap(database).ConnectionFactory;
        using var connection = connectionFactory.OpenConnection();
        Assert.Equal(["contacorrente", "idempotencia", "movimento"], GetApplicationTables(connection));
        Assert.Equal(ExpectedSeedCount, CountAccounts(connection));
        Assert.Empty(connection.Query("PRAGMA foreign_key_check;"));
        Assert.Equal(1, connection.ExecuteScalar<int>("PRAGMA user_version;"));
    }

    private static (SqliteConnectionFactory ConnectionFactory, DatabaseBootstrap Bootstrap) CreateBootstrap(
        TemporarySqliteDatabase database)
    {
        var config = new DatabaseConfig(database.ConnectionString);
        var connectionFactory = new SqliteConnectionFactory(config);
        return (connectionFactory, new DatabaseBootstrap(connectionFactory));
    }

    private static string[] GetApplicationTables(SqliteConnection connection)
    {
        return connection.Query<string>(
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """).ToArray();
    }

    private static int CountAccounts(SqliteConnection connection)
    {
        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM contacorrente;");
    }
}