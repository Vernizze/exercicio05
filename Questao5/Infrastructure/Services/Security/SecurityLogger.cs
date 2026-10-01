namespace Questao5.Infrastructure.Services.Security
{
    public sealed partial class SecurityLogger
    {
        private readonly ILogger<SecurityLogger> logger;

        public SecurityLogger(ILogger<SecurityLogger> logger)
        {
            this.logger = logger;
        }

        public void AuthenticationFailed(string correlationId, string reason)
        {
            LogAuthenticationFailed(logger, correlationId, reason, "Rejected");
        }

        [LoggerMessage(
            EventId = 5300,
            Level = LogLevel.Warning,
            Message = "Authentication failed. CorrelationId: {CorrelationId}; Reason: {Reason}; Outcome: {Outcome}")]
        private static partial void LogAuthenticationFailed(
            ILogger logger,
            string correlationId,
            string reason,
            string outcome);
    }
}
