namespace Questao5.Infrastructure.Services.Balances
{
    public sealed partial class BalanceLogger
    {
        private readonly ILogger<BalanceLogger> logger;

        public BalanceLogger(ILogger<BalanceLogger> logger)
        {
            this.logger = logger;
        }

        public void Consulted(string correlationId, string accountFingerprint)
        {
            LogConsulted(logger, correlationId, accountFingerprint, "Consulted");
        }

        public void BusinessRejection(string correlationId, string accountFingerprint, string ruleCode)
        {
            LogBusinessRejection(logger, correlationId, accountFingerprint, ruleCode, "Rejected");
        }

        public void UnexpectedFailure(string correlationId, string accountFingerprint, string exceptionType)
        {
            LogUnexpectedFailure(logger, correlationId, accountFingerprint, exceptionType, "Failed");
        }

        public void LimitExceeded(string correlationId, string limitName)
        {
            LogLimitExceeded(logger, correlationId, limitName, "Rejected");
        }

        [LoggerMessage(
            EventId = 5200,
            Level = LogLevel.Information,
            Message = "Balance consulted. CorrelationId: {CorrelationId}; AccountFingerprint: {AccountFingerprint}; Outcome: {Outcome}")]
        private static partial void LogConsulted(
            ILogger logger,
            string correlationId,
            string accountFingerprint,
            string outcome);

        [LoggerMessage(
            EventId = 5201,
            Level = LogLevel.Warning,
            Message = "Balance business rule rejected. CorrelationId: {CorrelationId}; AccountFingerprint: {AccountFingerprint}; RuleCode: {RuleCode}; Outcome: {Outcome}")]
        private static partial void LogBusinessRejection(
            ILogger logger,
            string correlationId,
            string accountFingerprint,
            string ruleCode,
            string outcome);

        [LoggerMessage(
            EventId = 5202,
            Level = LogLevel.Error,
            Message = "Balance query failed. CorrelationId: {CorrelationId}; AccountFingerprint: {AccountFingerprint}; ExceptionType: {ExceptionType}; Outcome: {Outcome}")]
        private static partial void LogUnexpectedFailure(
            ILogger logger,
            string correlationId,
            string accountFingerprint,
            string exceptionType,
            string outcome);

        [LoggerMessage(
            EventId = 5203,
            Level = LogLevel.Warning,
            Message = "Balance operational limit exceeded. CorrelationId: {CorrelationId}; LimitName: {LimitName}; Outcome: {Outcome}")]
        private static partial void LogLimitExceeded(
            ILogger logger,
            string correlationId,
            string limitName,
            string outcome);
    }
}
