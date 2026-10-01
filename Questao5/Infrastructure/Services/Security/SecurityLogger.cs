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

        public void AccessDenied(string correlationId, string subjectFingerprint, string accountFingerprint)
        {
            LogAccessDenied(logger, correlationId, subjectFingerprint, accountFingerprint, "Denied");
        }

        [LoggerMessage(
            EventId = 5301,
            Level = LogLevel.Warning,
            Message = "Account access denied. CorrelationId: {CorrelationId}; SubjectFingerprint: {SubjectFingerprint}; AccountFingerprint: {AccountFingerprint}; Outcome: {Outcome}")]
        private static partial void LogAccessDenied(
            ILogger logger,
            string correlationId,
            string subjectFingerprint,
            string accountFingerprint,
            string outcome);

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
