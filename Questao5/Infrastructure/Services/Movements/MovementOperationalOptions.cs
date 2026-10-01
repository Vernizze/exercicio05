namespace Questao5.Infrastructure.Services.Movements
{
    public sealed class MovementOperationalOptions
    {
        public const string SectionName = "Movement";

        public int TimeoutSeconds { get; set; } = 5;

        public int EndpointPermitLimit { get; set; } = 30;

        public int GlobalPermitLimit { get; set; } = 120;

        public int WindowSeconds { get; set; } = 60;

        public int ConcurrencyPermitLimit { get; set; } = 8;
    }
}