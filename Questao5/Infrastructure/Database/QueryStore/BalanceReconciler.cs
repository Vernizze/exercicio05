using Questao5.Application.Balances;
using Questao5.Infrastructure.Services.Security;
using Questao5.Infrastructure.Sqlite;

namespace Questao5.Infrastructure.Database.QueryStore
{
    public sealed class BalanceReconciler : IBalanceReconciler
    {
        private readonly ISqliteConnectionFactory connectionFactory;

        public BalanceReconciler(ISqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public BalanceReconciliationResult Reconcile()
        {
            using var connection = connectionFactory.OpenConnection();

            // Transação de leitura: projeção e movimentos são comparados no mesmo snapshot.
            using var transaction = connection.BeginTransaction(deferred: true);

            var fingerprints = BalanceProjection.FindDivergentAccounts(connection, transaction)
                .Select(SecurityFingerprint.ForAccount)
                .ToArray();

            return new BalanceReconciliationResult(fingerprints);
        }
    }
}
