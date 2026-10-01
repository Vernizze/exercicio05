using Dapper;
using Microsoft.Data.Sqlite;
using System.Text;

namespace Questao5.Infrastructure.Sqlite
{
    internal static class SqliteSchemaValidator
    {
        private static readonly ColumnDefinition[] AccountColumns =
        [
            new("idcontacorrente", "TEXT(37)", false, 1, null),
            new("numero", "INTEGER(10)", true, 0, null),
            new("nome", "TEXT(100)", true, 0, null),
            new("ativo", "INTEGER(1)", true, 0, "0")
        ];

        private static readonly ColumnDefinition[] MovementColumns =
        [
            new("idmovimento", "TEXT(37)", false, 1, null),
            new("idcontacorrente", "INTEGER(10)", true, 0, null),
            new("datamovimento", "TEXT(25)", true, 0, null),
            new("tipomovimento", "TEXT(1)", true, 0, null),
            new("valor", "REAL", true, 0, null)
        ];

        private static readonly ColumnDefinition[] IdempotencyColumns =
        [
            new("chave_idempotencia", "TEXT(37)", false, 1, null),
            new("requisicao", "TEXT(1000)", false, 0, null),
            new("resultado", "TEXT(1000)", false, 0, null)
        ];

        private static readonly ColumnDefinition[] AccountHolderColumns =
        [
            new("idcontacorrente", "TEXT(37)", false, 1, null),
            new("idcorrentista", "TEXT(36)", true, 0, null)
        ];

        private static readonly ColumnDefinition[] BalanceColumns =
        [
            new("idcontacorrente", "TEXT(37)", false, 1, null),
            new("saldo_centavos", "INTEGER", true, 0, null),
            new("versao", "INTEGER", true, 0, null)
        ];

        public static void ValidateAccountHolderTable(SqliteConnection connection, SqliteTransaction transaction)
        {
            ValidateColumns(connection, transaction, "titularidade_conta", AccountHolderColumns);
            ValidateForeignKey(connection, transaction, "titularidade_conta");
        }

        public static void ValidateBalanceTable(SqliteConnection connection, SqliteTransaction transaction)
        {
            ValidateColumns(connection, transaction, "saldo_conta", BalanceColumns);
            ValidateForeignKey(connection, transaction, "saldo_conta");
        }

        public static void ValidateAccountTable(SqliteConnection connection, SqliteTransaction transaction)
        {
            ValidateColumns(connection, transaction, "contacorrente", AccountColumns);
            ValidateUniqueIndex(connection, transaction, "contacorrente", "numero");
            ValidateCheckConstraint(connection, transaction, "contacorrente", "CHECK(ATIVOIN(0,1))");
        }

        public static void ValidateMovementTable(SqliteConnection connection, SqliteTransaction transaction)
        {
            ValidateColumns(connection, transaction, "movimento", MovementColumns);
            ValidateCheckConstraint(connection, transaction, "movimento", "CHECK(TIPOMOVIMENTOIN('C','D'))");
            ValidateForeignKey(connection, transaction, "movimento");
        }

        public static void ValidateIdempotencyTable(SqliteConnection connection, SqliteTransaction transaction)
        {
            ValidateColumns(connection, transaction, "idempotencia", IdempotencyColumns);
        }

        private static void ValidateColumns(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string tableName,
            IReadOnlyCollection<ColumnDefinition> expectedColumns)
        {
            var actualColumns = connection.Query<TableColumn>(
                $"""
                SELECT
                    name AS Name,
                    type AS Type,
                    "notnull" AS "NotNull",
                    dflt_value AS DefaultValue,
                    pk AS PrimaryKey
                FROM pragma_table_info('{tableName}')
                ORDER BY cid;
                """,
                transaction: transaction).AsList();

            if (actualColumns.Count != expectedColumns.Count)
            {
                throw Incompatible(tableName, "quantidade de colunas divergente");
            }

            foreach (var expected in expectedColumns)
            {
                var actual = actualColumns.SingleOrDefault(column =>
                    string.Equals(column.Name, expected.Name, StringComparison.OrdinalIgnoreCase));

                if (actual is null)
                {
                    throw Incompatible(tableName, $"coluna '{expected.Name}' ausente");
                }

                if (!string.Equals(actual.Type, expected.Type, StringComparison.OrdinalIgnoreCase) ||
                    actual.NotNull != (expected.NotNull ? 1 : 0) ||
                    actual.PrimaryKey != expected.PrimaryKey ||
                    !DefaultsMatch(actual.DefaultValue, expected.DefaultValue))
                {
                    throw Incompatible(tableName, $"definição incompatível da coluna '{expected.Name}'");
                }
            }
        }

        private static void ValidateUniqueIndex(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string tableName,
            string columnName)
        {
            var indexes = connection.Query<TableIndex>(
                $"""
                SELECT name AS Name, "unique" AS "IsUnique"
                FROM pragma_index_list('{tableName}');
                """,
                transaction: transaction);

            foreach (var index in indexes.Where(index => index.IsUnique == 1))
            {
                var columns = connection.Query<string>(
                    $"SELECT name FROM pragma_index_info('{EscapeSqlLiteral(index.Name)}') ORDER BY seqno;",
                    transaction: transaction).AsList();

                if (columns.Count == 1 &&
                    string.Equals(columns[0], columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw Incompatible(tableName, $"índice único da coluna '{columnName}' ausente");
        }

        private static void ValidateForeignKey(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string tableName)
        {
            var foreignKeys = connection.Query<TableForeignKey>(
                $"""
                SELECT
                    "table" AS ReferencedTable,
                    "from" AS SourceColumn,
                    "to" AS ReferencedColumn
                FROM pragma_foreign_key_list('{tableName}');
                """,
                transaction: transaction).AsList();

            var isValid = foreignKeys.Count == 1 &&
                string.Equals(foreignKeys[0].ReferencedTable, "contacorrente", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(foreignKeys[0].SourceColumn, "idcontacorrente", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(foreignKeys[0].ReferencedColumn, "idcontacorrente", StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                throw Incompatible(tableName, "chave estrangeira de conta ausente ou incompatível");
            }
        }

        private static void ValidateCheckConstraint(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string tableName,
            string expectedConstraint)
        {
            var createSql = connection.QuerySingleOrDefault<string>(
                "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @TableName;",
                new { TableName = tableName },
                transaction);

            var normalizedSql = string.IsNullOrWhiteSpace(createSql) ? string.Empty : NormalizeSql(createSql);

            if (!normalizedSql.Contains(expectedConstraint, StringComparison.Ordinal) ||
                CountOccurrences(normalizedSql, "CHECK(") != 1)
            {
                throw Incompatible(tableName, "constraint CHECK ausente ou incompatível");
            }
        }

        private static int CountOccurrences(string value, string pattern)
        {
            var count = 0;
            var startIndex = 0;

            while ((startIndex = value.IndexOf(pattern, startIndex, StringComparison.Ordinal)) >= 0)
            {
                count++;
                startIndex += pattern.Length;
            }

            return count;
        }

        private static bool DefaultsMatch(string? actual, string? expected)
        {
            return string.Equals(
                NormalizeDefault(actual),
                NormalizeDefault(expected),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeDefault(string? value)
        {
            return value?.Trim().Trim('(', ')', '\'', '"');
        }

        private static string NormalizeSql(string sql)
        {
            var builder = new StringBuilder(sql.Length);

            foreach (var character in sql)
            {
                if (!char.IsWhiteSpace(character) && character != '"' && character != '`' && character != '[' && character != ']')
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            return builder.ToString();
        }

        private static string EscapeSqlLiteral(string value)
        {
            return value.Replace("'", "''", StringComparison.Ordinal);
        }

        private static InvalidDatabaseSchemaException Incompatible(string tableName, string reason)
        {
            return new InvalidDatabaseSchemaException($"A tabela '{tableName}' é incompatível: {reason}.");
        }

        private sealed record ColumnDefinition(
            string Name,
            string Type,
            bool NotNull,
            int PrimaryKey,
            string? DefaultValue);

        private sealed class TableColumn
        {
            public string Name { get; init; } = string.Empty;

            public string Type { get; init; } = string.Empty;

            public int NotNull { get; init; }

            public string? DefaultValue { get; init; }

            public int PrimaryKey { get; init; }
        }

        private sealed class TableIndex
        {
            public string Name { get; init; } = string.Empty;

            public int IsUnique { get; init; }
        }

        private sealed class TableForeignKey
        {
            public string ReferencedTable { get; init; } = string.Empty;

            public string SourceColumn { get; init; } = string.Empty;

            public string ReferencedColumn { get; init; } = string.Empty;
        }
    }
}