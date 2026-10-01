namespace Questao5.Infrastructure.Services.Balances
{
    public sealed class BalanceOperationalOptions
    {
        public const string SectionName = "Balance";

        public int TimeoutSeconds { get; set; } = 5;

        public int EndpointPermitLimit { get; set; } = 30;

        public int WindowSeconds { get; set; } = 60;

        public int ConcurrencyPermitLimit { get; set; } = 8;
    }
}
