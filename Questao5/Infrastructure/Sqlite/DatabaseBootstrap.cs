using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Application.Balances;
using Questao5.Domain.Entities;
using Questao5.Infrastructure.Database;

namespace Questao5.Infrastructure.Sqlite
{
    public sealed class DatabaseBootstrap : IDatabaseBootstrap
    {
        private const int CurrentSchemaVersion = 2;

        private static readonly AccountSeed[] AccountSeeds =
        [
            new("B6BAFC09 -6967-ED11-A567-055DFA4A16C9", 123, "Katherine Sanchez", 1, "7d85c0f1-c90c-49e6-a2c7-0fb988c3d943"),
            new("FA99D033-7067-ED11-96C6-7C5DFA4A16C9", 456, "Eva Woodward", 1, "04b276dc-0f45-4efc-bffc-911110198733"),
            new("382D323D-7067-ED11-8866-7D5DFA4A16C9", 789, "Tevin Mcconnell", 1, "06dc3a47-fb77-4589-9e18-076f3860d1d2"),
            new("F475F943-7067-ED11-A06B-7E5DFA4A16C9", 741, "Ameena Lynn", 0, "cf18e8e5-35f2-498d-a77d-4dd6828316d4"),
            new("BCDACA4A-7067-ED11-AF81-825DFA4A16C9", 852, "Jarrad Mckee", 0, "dbb66add-5f1c-412e-9917-ac6048ea22da"),
            new("D2E02051-7067-ED11-94C0-835DFA4A16C9", 963, "Elisha Simons", 0, "3d03eefd-9941-44c8-b6bb-5644eff438e8")
        ];

        private readonly ISqliteConnectionFactory connectionFactory;

        public DatabaseBootstrap(ISqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public void Setup()
        {
            using var connection = connectionFactory.OpenConnection();
            using var transaction = connection.BeginTransaction(deferred: false);

            var schemaVersion = connection.ExecuteScalar<int>(
                "PRAGMA user_version;",
                transaction: transaction);

            if (schemaVersion > CurrentSchemaVersion)
            {
                throw new InvalidDatabaseSchemaException(
                    $"A versão do banco ({schemaVersion}) é superior à versão suportada ({CurrentSchemaVersion}).");
            }

            EnsureSchema(connection, transaction);

            // Seed e saldo inicial usam os repositórios, dentro da mesma transação da migração.
            using var unitOfWork = new UnitOfWork(connection, transaction, ownsTransaction: false);
            SeedAccounts(unitOfWork);
            BalanceProjection.Backfill(unitOfWork);

            if (schemaVersion < CurrentSchemaVersion)
            {
                ValidateBalanceReconciliation(unitOfWork);
            }

            ValidateDatabaseIntegrity(connection, transaction);

            connection.Execute(
                $"PRAGMA user_version = {CurrentSchemaVersion};",
                transaction: transaction);

            transaction.Commit();
        }

        private static void EnsureSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            EnsureTable(
                connection,
                transaction,
                "contacorrente",
                """
                CREATE TABLE IF NOT EXISTS contacorrente (
                    idcontacorrente TEXT(37) PRIMARY KEY,
                    numero INTEGER(10) NOT NULL UNIQUE,
                    nome TEXT(100) NOT NULL,
                    ativo INTEGER(1) NOT NULL DEFAULT 0,
                    CHECK(ativo IN (0, 1))
                );
                """);
            SqliteSchemaValidator.ValidateAccountTable(connection, transaction);

            EnsureTable(
                connection,
                transaction,
                "movimento",
                """
                CREATE TABLE IF NOT EXISTS movimento (
                    idmovimento TEXT(37) PRIMARY KEY,
                    idcontacorrente INTEGER(10) NOT NULL,
                    datamovimento TEXT(25) NOT NULL,
                    tipomovimento TEXT(1) NOT NULL,
                    valor REAL NOT NULL,
                    CHECK(tipomovimento IN ('C', 'D')),
                    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
                );
                """);
            SqliteSchemaValidator.ValidateMovementTable(connection, transaction);

            EnsureTable(
                connection,
                transaction,
                "idempotencia",
                """
                CREATE TABLE IF NOT EXISTS idempotencia (
                    chave_idempotencia TEXT(37) PRIMARY KEY,
                    requisicao TEXT(1000),
                    resultado TEXT(1000)
                );
                """);
            SqliteSchemaValidator.ValidateIdempotencyTable(connection, transaction);

            EnsureTable(
                connection,
                transaction,
                "titularidade_conta",
                """
                CREATE TABLE IF NOT EXISTS titularidade_conta (
                    idcontacorrente TEXT(37) PRIMARY KEY,
                    idcorrentista TEXT(36) NOT NULL,
                    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
                );
                """);
            SqliteSchemaValidator.ValidateAccountHolderTable(connection, transaction);

            EnsureTable(
                connection,
                transaction,
                "saldo_conta",
                """
                CREATE TABLE IF NOT EXISTS saldo_conta (
                    idcontacorrente TEXT(37) PRIMARY KEY,
                    saldo_centavos INTEGER NOT NULL,
                    versao INTEGER NOT NULL,
                    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
                );
                """);
            SqliteSchemaValidator.ValidateBalanceTable(connection, transaction);
        }

        private static void EnsureTable(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string tableName,
            string createStatement)
        {
            connection.Execute(createStatement, transaction: transaction);

            var exists = connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @TableName;",
                new { TableName = tableName },
                transaction) == 1;

            if (!exists)
            {
                throw new InvalidDatabaseSchemaException($"A tabela obrigatória '{tableName}' não foi criada.");
            }
        }

        private static void SeedAccounts(UnitOfWork unitOfWork)
        {
            foreach (var seed in AccountSeeds)
            {
                unitOfWork.ContaCorrenteCommand.InserirSeAusente(
                    new ContaCorrente(seed.Id, seed.Number, seed.Name, seed.Active == 1));

                // O titular só é registrado quando a conta ainda não possui um; titularidades existentes são preservadas.
                unitOfWork.TitularidadeContaCommand.InserirSeAusente(
                    new TitularidadeConta(seed.Id, seed.HolderId));
            }
        }

        private static void ValidateBalanceReconciliation(UnitOfWork unitOfWork)
        {
            if (BalanceProjection.FindDivergentAccounts(unitOfWork).Count > 0)
            {
                throw new InvalidDatabaseSchemaException(
                    "A projeção de saldo diverge dos movimentos persistidos.");
            }
        }

        private static void ValidateDatabaseIntegrity(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var foreignKeyViolations = connection.Query(
                "PRAGMA foreign_key_check;",
                transaction: transaction).AsList();

            if (foreignKeyViolations.Count > 0)
            {
                throw new InvalidDatabaseSchemaException("O banco contém violações de integridade referencial.");
            }

            var integrityResults = connection.Query<string>(
                "PRAGMA integrity_check;",
                transaction: transaction).AsList();

            if (integrityResults.Count != 1 ||
                !string.Equals(integrityResults[0], "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDatabaseSchemaException("A verificação de integridade do banco falhou.");
            }
        }

        private sealed record AccountSeed(string Id, int Number, string Name, int Active, string HolderId);
    }
}
