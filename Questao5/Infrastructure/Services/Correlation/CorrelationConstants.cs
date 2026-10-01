namespace Questao5.Infrastructure.Services.Correlation
{
    public static class CorrelationConstants
    {
        public const string HeaderName = "X-Correlation-ID";
        public const string ProblemDetailsExtensionName = "correlationId";
        public const int MaximumLength = 64;
    }
}