using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Application.Movements;
using System.Globalization;

namespace Questao5.Infrastructure.Sqlite
{
    /// <summary>
    /// Projeção persistida de saldo (<c>saldo_conta</c>): conversão para centavos inteiros,
    /// preenchimento inicial a partir dos movimentos e reconciliação.
    /// </summary>
    internal static class BalanceProjection
    {
        public static long ToCents(decimal amount)
        {
            var cents = checked(amount * 100m);

            if (decimal.Truncate(cents) != cents)
            {
                throw new InvalidOperationException("O valor monetário possui mais de duas casas decimais.");
            }

            return decimal.ToInt64(cents);
        }

        public static decimal FromCents(long cents)
        {
            if (cents == long.MinValue)
            {
                throw new InvalidOperationException("O saldo persistido está fora do limite suportado.");
            }

            // Constrói o decimal diretamente com escala 2, preservando zeros finais (0.00, 10.50).
            var magnitude = (ulong)Math.Abs(cents);
            return new decimal(
                unchecked((int)(magnitude & 0xFFFFFFFF)),
                unchecked((int)(magnitude >> 32)),
                0,
                cents < 0,
                2);
        }

        /// <summary>
        /// Cria a linha de saldo das contas que ainda não a possuem, reconstruindo o valor
        /// a partir dos movimentos persistidos. Linhas existentes não são alteradas.
        /// </summary>
        public static void Backfill(SqliteConnection connection, SqliteTransaction transaction)
        {
            var missingAccountIds = connection.Query<string>(
                """
                SELECT c.idcontacorrente
                FROM contacorrente c
                LEFT JOIN saldo_conta s ON s.idcontacorrente = c.idcontacorrente
                WHERE s.idcontacorrente IS NULL;
                """,
                transaction: transaction).AsList();

            if (missingAccountIds.Count == 0)
            {
                return;
            }

            var totals = ComputeFromMovements(connection, transaction);

            foreach (var accountId in missingAccountIds)
            {
                totals.TryGetValue(accountId, out var total);

                connection.Execute(
                    """
                    INSERT INTO saldo_conta(idcontacorrente, saldo_centavos, versao)
                    VALUES (@AccountId, @BalanceCents, @Version);
                    """,
                    new
                    {
                        AccountId = accountId,
                        total.BalanceCents,
                        Version = total.MovementCount
                    },
                    transaction);
            }
        }

        /// <summary>
        /// Compara a projeção com o saldo recalculado dos movimentos e devolve as contas divergentes.
        /// </summary>
        public static IReadOnlyList<string> FindDivergentAccounts(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var totals = ComputeFromMovements(connection, transaction);
            var rows = connection.Query<ProjectionRow>(
                """
                SELECT c.idcontacorrente AS AccountId,
                       s.saldo_centavos AS BalanceCents,
                       s.versao AS Version
                FROM contacorrente c
                LEFT JOIN saldo_conta s ON s.idcontacorrente = c.idcontacorrente;
                """,
                transaction: transaction);
            var divergentAccounts = new List<string>();

            foreach (var row in rows)
            {
                totals.TryGetValue(row.AccountId, out var expected);

                if (row.BalanceCents != expected.BalanceCents || row.Version != expected.MovementCount)
                {
                    divergentAccounts.Add(row.AccountId);
                }
            }

            return divergentAccounts;
        }

        private static Dictionary<string, AccountTotal> ComputeFromMovements(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var balances = new Dictionary<string, (decimal Balance, long Count)>(StringComparer.Ordinal);

            // Sem SUM(valor): cada REAL legado é convertido individualmente e acumulado em decimal.
            var movements = connection.Query<MovementRow>(
                """
                SELECT idcontacorrente AS AccountId,
                       tipomovimento AS MovementType,
                       valor AS Amount
                FROM movimento;
                """,
                transaction: transaction,
                buffered: false);

            foreach (var movement in movements)
            {
                var accountId = Convert.ToString(movement.AccountId, CultureInfo.InvariantCulture) ?? string.Empty;
                var amount = ToDecimal(movement.Amount);
                balances.TryGetValue(accountId, out var current);

                balances[accountId] = movement.MovementType switch
                {
                    "C" => (current.Balance + amount, checked(current.Count + 1)),
                    "D" => (current.Balance - amount, checked(current.Count + 1)),
                    _ => throw new InvalidDatabaseSchemaException(
                        "A tabela 'movimento' contém tipo de movimento incompatível.")
                };
            }

            return balances.ToDictionary(
                pair => pair.Key,
                pair => new AccountTotal(ToCents(pair.Value.Balance), pair.Value.Count),
                StringComparer.Ordinal);
        }

        private static decimal ToDecimal(object? persistedAmount)
        {
            if (persistedAmount is not double value || !double.IsFinite(value))
            {
                throw new InvalidDatabaseSchemaException(
                    "A tabela 'movimento' contém valor monetário não numérico ou não finito.");
            }

            decimal amount;

            try
            {
                amount = Convert.ToDecimal(value);
            }
            catch (OverflowException)
            {
                throw new InvalidDatabaseSchemaException(
                    "A tabela 'movimento' contém valor monetário fora do limite suportado.");
            }

            if (amount <= 0 ||
                amount > MovementRequestNormalizer.MaximumAmount ||
                decimal.Round(amount, 2) != amount)
            {
                throw new InvalidDatabaseSchemaException(
                    "A tabela 'movimento' contém valor monetário fora do contrato.");
            }

            return amount;
        }

        private readonly record struct AccountTotal(long BalanceCents, long MovementCount);

        private sealed class MovementRow
        {
            public object? AccountId { get; init; }

            public string? MovementType { get; init; }

            public object? Amount { get; init; }
        }

        private sealed class ProjectionRow
        {
            public string AccountId { get; init; } = string.Empty;

            public long? BalanceCents { get; init; }

            public long? Version { get; init; }
        }
    }
}
