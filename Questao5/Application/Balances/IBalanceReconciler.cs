namespace Questao5.Application.Balances
{
    public interface IBalanceReconciler
    {
        /// <summary>
        /// Compara o saldo consolidado de cada conta com o recalculado a partir dos movimentos.
        /// </summary>
        BalanceReconciliationResult Reconcile();
    }

    public sealed record BalanceReconciliationResult(IReadOnlyList<string> DivergentAccountFingerprints)
    {
        public bool IsConsistent => DivergentAccountFingerprints.Count == 0;
    }
}
