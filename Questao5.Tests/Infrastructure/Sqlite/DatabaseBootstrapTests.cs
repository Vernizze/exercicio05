using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Sqlite;

public sealed class DatabaseBootstrapTests
{
    private const int ExpectedSeedCount = 6;

    private static readonly string[] ExpectedTables =
    [
        "contacorrente",
        "idempotencia",
        "movimento",
        "saldo_conta",
        "titularidade_conta"
    ];

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
        Assert.Equal(ExpectedTables, GetApplicationTables(connection));
        Assert.Equal(ExpectedSeedCount, CountAccounts(connection));
        Assert.Equal(2, connection.ExecuteScalar<int>("PRAGMA user_version;"));
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
        Assert.Equal(ExpectedTables, GetApplicationTables(verificationConnection));
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
        Assert.Equal(ExpectedTables, GetApplicationTables(verificationConnection));
        Assert.Equal(ExpectedSeedCount, CountAccounts(verificationConnection));
    }

    [Fact]
    public void Setup_FutureSchemaVersion_FailsWithoutChangingDatabase()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute("PRAGMA user_version = 3;");
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains("superior", exception.Message, StringComparison.Ordinal);
        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(3, verificationConnection.ExecuteScalar<int>("PRAGMA user_version;"));
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
        Assert.Equal(ExpectedTables, GetApplicationTables(connection));
        Assert.Equal(ExpectedSeedCount, CountAccounts(connection));
        Assert.Empty(connection.Query("PRAGMA foreign_key_check;"));
        Assert.Equal(2, connection.ExecuteScalar<int>("PRAGMA user_version;"));
    }

    [Fact]
    public void Setup_EmptyDatabase_SeedsAccountHoldersAndZeroBalances()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        bootstrap.Setup();

        using var connection = connectionFactory.OpenConnection();
        Assert.Equal(ExpectedSeedCount, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM titularidade_conta;"));
        Assert.Equal(
            ExpectedSeedCount,
            connection.ExecuteScalar<int>("SELECT COUNT(DISTINCT idcorrentista) FROM titularidade_conta;"));
        Assert.Equal(
            "04b276dc-0f45-4efc-bffc-911110198733",
            connection.QuerySingle<string>(
                """
                SELECT t.idcorrentista
                FROM titularidade_conta t
                JOIN contacorrente c ON c.idcontacorrente = t.idcontacorrente
                WHERE c.numero = 456;
                """));
        Assert.Equal(ExpectedSeedCount, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM saldo_conta;"));
        Assert.Equal(
            0,
            connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM saldo_conta WHERE saldo_centavos <> 0 OR versao <> 0;"));
    }

    [Fact]
    public void Setup_VersionOneDatabaseWithMovements_BackfillsBalanceProjection()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);
        CreateVersionOneDatabase(connectionFactory);

        using (var connection = connectionFactory.OpenConnection())
        {
            InsertMovement(connection, "C", 100.10);
            InsertMovement(connection, "C", 0.20);
            InsertMovement(connection, "D", 50.05);
        }

        bootstrap.Setup();

        using var verificationConnection = connectionFactory.OpenConnection();
        var projection = verificationConnection.QuerySingle<(long BalanceCents, long Version)>(
            "SELECT saldo_centavos, versao FROM saldo_conta WHERE idcontacorrente = 'legacy-account';");
        Assert.Equal(5025L, projection.BalanceCents);
        Assert.Equal(3L, projection.Version);
        Assert.Equal(2, verificationConnection.ExecuteScalar<int>("PRAGMA user_version;"));
        Assert.Equal(3, verificationConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM movimento;"));
        Assert.Equal(
            0,
            verificationConnection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM titularidade_conta WHERE idcontacorrente = 'legacy-account';"));
    }

    [Fact]
    public void Setup_ReferenceFixtureCopy_MigratesToVersionTwoWithoutLosingAccounts()
    {
        using var database = new TemporarySqliteDatabase();
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-database.sqlite"),
            database.DatabasePath);
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        bootstrap.Setup();

        using var connection = connectionFactory.OpenConnection();
        Assert.Equal(ExpectedTables, GetApplicationTables(connection));
        Assert.Equal(ExpectedSeedCount, CountAccounts(connection));
        Assert.Equal(ExpectedSeedCount, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM titularidade_conta;"));
        Assert.Equal(ExpectedSeedCount, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM saldo_conta;"));
        Assert.Equal(2, connection.ExecuteScalar<int>("PRAGMA user_version;"));
    }

    [Fact]
    public void Setup_RepeatedOnCurrentVersion_PreservesProjectionAndAccountHolders()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);
        bootstrap.Setup();

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(
                """
                UPDATE saldo_conta SET saldo_centavos = 12345, versao = 7
                WHERE idcontacorrente = 'FA99D033-7067-ED11-96C6-7C5DFA4A16C9';

                UPDATE titularidade_conta SET idcorrentista = '11111111-1111-1111-1111-111111111111'
                WHERE idcontacorrente = 'FA99D033-7067-ED11-96C6-7C5DFA4A16C9';
                """);
        }

        bootstrap.Setup();

        using var verificationConnection = connectionFactory.OpenConnection();
        var projection = verificationConnection.QuerySingle<(long BalanceCents, long Version)>(
            """
            SELECT saldo_centavos, versao FROM saldo_conta
            WHERE idcontacorrente = 'FA99D033-7067-ED11-96C6-7C5DFA4A16C9';
            """);
        Assert.Equal((12345L, 7L), projection);
        Assert.Equal(
            "11111111-1111-1111-1111-111111111111",
            verificationConnection.QuerySingle<string>(
                """
                SELECT idcorrentista FROM titularidade_conta
                WHERE idcontacorrente = 'FA99D033-7067-ED11-96C6-7C5DFA4A16C9';
                """));
        Assert.Equal(ExpectedSeedCount, verificationConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM saldo_conta;"));
    }

    [Theory]
    [InlineData(
        "CREATE TABLE titularidade_conta (idcontacorrente TEXT(37) PRIMARY KEY, idcorrentista TEXT(36));",
        "titularidade_conta")]
    [InlineData(
        """
        CREATE TABLE titularidade_conta (
            idcontacorrente TEXT(37) PRIMARY KEY,
            idcorrentista TEXT(36) NOT NULL
        );
        """,
        "chave estrangeira")]
    [InlineData(
        """
        CREATE TABLE saldo_conta (
            idcontacorrente TEXT(37) PRIMARY KEY,
            saldo_centavos REAL NOT NULL,
            versao INTEGER NOT NULL,
            FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
        );
        """,
        "saldo_centavos")]
    public void Setup_IncompatibleVersionTwoTable_FailsSafely(string incompatibleTableSql, string expectedDiagnostic)
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(incompatibleTableSql);
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains(expectedDiagnostic, exception.Message, StringComparison.OrdinalIgnoreCase);
        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(0, verificationConnection.ExecuteScalar<int>("PRAGMA user_version;"));
    }

    [Theory]
    [InlineData("'not-a-number'")]
    [InlineData("1.005")]
    [InlineData("-10.0")]
    [InlineData("0.0")]
    [InlineData("10000000000.0")]
    public void Setup_VersionOneDatabaseWithInvalidPersistedAmount_FailsWithoutMigrating(string persistedAmountSql)
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);
        CreateVersionOneDatabase(connectionFactory);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(
                $"""
                INSERT INTO movimento(idmovimento, idcontacorrente, datamovimento, tipomovimento, valor)
                VALUES ('invalid-movement', 'legacy-account', '01/10/2026', 'C', {persistedAmountSql});
                """);
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains("valor monetário", exception.Message, StringComparison.Ordinal);
        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(1, verificationConnection.ExecuteScalar<int>("PRAGMA user_version;"));
        Assert.Equal(
            ["contacorrente", "idempotencia", "movimento"],
            GetApplicationTables(verificationConnection));
    }

    [Fact]
    public void Setup_VersionOneDatabaseWithDivergentProjection_FailsWithoutMigrating()
    {
        using var database = new TemporarySqliteDatabase();
        var (connectionFactory, bootstrap) = CreateBootstrap(database);
        CreateVersionOneDatabase(connectionFactory);

        using (var connection = connectionFactory.OpenConnection())
        {
            connection.Execute(
                """
                CREATE TABLE saldo_conta (
                    idcontacorrente TEXT(37) PRIMARY KEY,
                    saldo_centavos INTEGER NOT NULL,
                    versao INTEGER NOT NULL,
                    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
                );

                INSERT INTO saldo_conta(idcontacorrente, saldo_centavos, versao)
                VALUES ('legacy-account', 99900, 1);
                """);
        }

        var exception = Assert.Throws<InvalidDatabaseSchemaException>(bootstrap.Setup);

        Assert.Contains("diverge", exception.Message, StringComparison.Ordinal);
        using var verificationConnection = connectionFactory.OpenConnection();
        Assert.Equal(1, verificationConnection.ExecuteScalar<int>("PRAGMA user_version;"));
    }

    private static void CreateVersionOneDatabase(SqliteConnectionFactory connectionFactory)
    {
        using var connection = connectionFactory.OpenConnection();
        connection.Execute(CreateAccountTableSql);
        connection.Execute(CreateMovementTableSql);
        connection.Execute(
            """
            CREATE TABLE idempotencia (
                chave_idempotencia TEXT(37) PRIMARY KEY,
                requisicao TEXT(1000),
                resultado TEXT(1000)
            );

            INSERT INTO contacorrente(idcontacorrente, numero, nome, ativo)
            VALUES ('legacy-account', 999, 'Conta legada', 1);

            PRAGMA user_version = 1;
            """);
    }

    private static void InsertMovement(SqliteConnection connection, string movementType, double amount)
    {
        connection.Execute(
            """
            INSERT INTO movimento(idmovimento, idcontacorrente, datamovimento, tipomovimento, valor)
            VALUES (@MovementId, 'legacy-account', '01/10/2026', @MovementType, @Amount);
            """,
            new { MovementId = Guid.NewGuid().ToString("D"), MovementType = movementType, Amount = amount });
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