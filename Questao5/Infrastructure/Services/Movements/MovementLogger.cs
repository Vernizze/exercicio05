namespace Questao5.Infrastructure.Services.Movements
{
    public sealed partial class MovementLogger
    {
        private readonly ILogger<MovementLogger> logger;

        public MovementLogger(ILogger<MovementLogger> logger)
        {
            this.logger = logger;
        }

        public void Created(
            string correlationId,
            string movementId,
            string idempotencyFingerprint)
        {
            LogCreated(logger, correlationId, movementId, idempotencyFingerprint, "Created");
        }

        public void Replayed(
            string correlationId,
            string movementId,
            string idempotencyFingerprint)
        {
            LogReplayed(logger, correlationId, movementId, idempotencyFingerprint, "Replayed");
        }

        public void Conflict(string correlationId, string idempotencyFingerprint)
        {
            LogConflict(logger, correlationId, idempotencyFingerprint, "Conflict");
        }

        public void BusinessRejection(string correlationId, string ruleCode)
        {
            LogBusinessRejection(logger, correlationId, ruleCode, "Rejected");
        }

        public void UnexpectedRollback(string correlationId, string exceptionType)
        {
            LogUnexpectedRollback(logger, correlationId, exceptionType, "RolledBack");
        }

        public void LimitExceeded(string correlationId, string limitName)
        {
            LogLimitExceeded(logger, correlationId, limitName, "Rejected");
        }

        [LoggerMessage(
            EventId = 5100,
            Level = LogLevel.Information,
            Message = "Movement confirmed. CorrelationId: {CorrelationId}; MovementId: {MovementId}; IdempotencyFingerprint: {IdempotencyFingerprint}; Outcome: {Outcome}")]
        private static partial void LogCreated(
            ILogger logger,
            string correlationId,
            string movementId,
            string idempotencyFingerprint,
            string outcome);

        [LoggerMessage(
            EventId = 5101,
            Level = LogLevel.Information,
            Message = "Movement replayed. CorrelationId: {CorrelationId}; MovementId: {MovementId}; IdempotencyFingerprint: {IdempotencyFingerprint}; Outcome: {Outcome}")]
        private static partial void LogReplayed(
            ILogger logger,
            string correlationId,
            string movementId,
            string idempotencyFingerprint,
            string outcome);

        [LoggerMessage(
            EventId = 5102,
            Level = LogLevel.Warning,
            Message = "Movement idempotency conflict. CorrelationId: {CorrelationId}; IdempotencyFingerprint: {IdempotencyFingerprint}; Outcome: {Outcome}")]
        private static partial void LogConflict(
            ILogger logger,
            string correlationId,
            string idempotencyFingerprint,
            string outcome);

        [LoggerMessage(
            EventId = 5103,
            Level = LogLevel.Warning,
            Message = "Movement business rule rejected. CorrelationId: {CorrelationId}; RuleCode: {RuleCode}; Outcome: {Outcome}")]
        private static partial void LogBusinessRejection(
            ILogger logger,
            string correlationId,
            string ruleCode,
            string outcome);

        [LoggerMessage(
            EventId = 5104,
            Level = LogLevel.Error,
            Message = "Movement transaction rolled back. CorrelationId: {CorrelationId}; ExceptionType: {ExceptionType}; Outcome: {Outcome}")]
        private static partial void LogUnexpectedRollback(
            ILogger logger,
            string correlationId,
            string exceptionType,
            string outcome);

        [LoggerMessage(
            EventId = 5105,
            Level = LogLevel.Warning,
            Message = "Movement operational limit exceeded. CorrelationId: {CorrelationId}; LimitName: {LimitName}; Outcome: {Outcome}")]
        private static partial void LogLimitExceeded(
            ILogger logger,
            string correlationId,
            string limitName,
            string outcome);
    }
}