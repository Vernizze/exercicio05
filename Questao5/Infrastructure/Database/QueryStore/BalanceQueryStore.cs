using Dapper;
using Questao5.Application.Balances;
using Questao5.Application.Exceptions;
using Questao5.Infrastructure.Sqlite;

namespace Questao5.Infrastructure.Database.QueryStore
{
    public sealed class BalanceQueryStore : IBalanceQueryStore
    {
        private readonly ISqliteConnectionFactory connectionFactory;

        public BalanceQueryStore(ISqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public AccountBalance GetBalance(
            string accountHolderId,
            string accountId,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accountHolderId);
            ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = connectionFactory.OpenConnection();

            // Transação de leitura (não imediata): conta, titularidade e saldo vêm do mesmo snapshot
            // e a consulta não disputa a reserva de escritor com a movimentação.
            using var transaction = connection.BeginTransaction(deferred: true);

            var account = connection.QuerySingleOrDefault<AccountRow>(
                """
                SELECT c.numero AS Number,
                       c.nome AS HolderName,
                       c.ativo AS Active,
                       t.idcorrentista AS HolderId,
                       s.saldo_centavos AS BalanceCents
                FROM contacorrente c
                LEFT JOIN titularidade_conta t ON t.idcontacorrente = c.idcontacorrente
                LEFT JOIN saldo_conta s ON s.idcontacorrente = c.idcontacorrente
                WHERE c.idcontacorrente = @AccountId COLLATE NOCASE;
                """,
                new { AccountId = accountId },
                transaction);

            cancellationToken.ThrowIfCancellationRequested();

            if (account is null)
            {
                throw AccountRuleViolations.InvalidAccount();
            }

            // Conta sem titularidade não pertence a ninguém (falha fechada).
            if (!string.Equals(account.HolderId, accountHolderId, StringComparison.OrdinalIgnoreCase))
            {
                throw AccountRuleViolations.OwnershipDenied();
            }

            if (account.Active != 1)
            {
                throw AccountRuleViolations.InactiveAccount();
            }

            if (account.BalanceCents is not long balanceCents)
            {
                throw new InvalidOperationException("O saldo consolidado da conta está ausente ou não é inteiro.");
            }

            return new AccountBalance(
                account.Number,
                account.HolderName,
                BalanceProjection.FromCents(balanceCents));
        }

        private sealed class AccountRow
        {
            public long Number { get; init; }

            public string HolderName { get; init; } = string.Empty;

            public long Active { get; init; }

            public string? HolderId { get; init; }

            public object? BalanceCents { get; init; }
        }
    }
}
