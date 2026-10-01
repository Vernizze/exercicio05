namespace Questao5.Application.Balances
{
    public interface IBalanceQueryStore
    {
        /// <summary>
        /// Lê conta, titularidade e saldo consolidado em um único snapshot e valida, nesta ordem,
        /// existência da conta, titularidade e situação ativa.
        /// </summary>
        AccountBalance GetBalance(
            string accountHolderId,
            string accountId,
            CancellationToken cancellationToken);
    }

    public sealed record AccountBalance(long AccountNumber, string HolderName, decimal Balance);
}
