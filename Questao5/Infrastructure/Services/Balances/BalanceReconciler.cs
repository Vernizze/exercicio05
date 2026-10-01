using Questao5.Application.Balances;
using Questao5.Domain.Repositories;
using Questao5.Infrastructure.Services.Security;

namespace Questao5.Infrastructure.Services.Balances
{
    public sealed class BalanceReconciler : IBalanceReconciler
    {
        private readonly IUnitOfWorkFactory unitOfWorkFactory;

        public BalanceReconciler(IUnitOfWorkFactory unitOfWorkFactory)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
        }

        public BalanceReconciliationResult Reconcile()
        {
            // Transação de leitura: saldo consolidado e movimentos são comparados no mesmo snapshot.
            using var unitOfWork = unitOfWorkFactory.BeginRead();

            var fingerprints = BalanceProjection.FindDivergentAccounts(unitOfWork)
                .Select(SecurityFingerprint.ForAccount)
                .ToArray();

            return new BalanceReconciliationResult(fingerprints);
        }
    }
}
