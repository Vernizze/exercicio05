namespace Questao5.Application.Queries.Responses
{
    public sealed record GetBalanceResponse(
        long AccountNumber,
        string HolderName,
        DateTimeOffset QueriedAt,
        decimal Balance);
}
