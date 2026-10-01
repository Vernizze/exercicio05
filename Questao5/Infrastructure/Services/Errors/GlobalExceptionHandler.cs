using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Questao5.Application.Exceptions;

namespace Questao5.Infrastructure.Services.Errors
{
    public sealed partial class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> logger;
        private readonly IProblemDetailsService problemDetailsService;

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger,
            IProblemDetailsService problemDetailsService)
        {
            this.logger = logger;
            this.problemDetailsService = problemDetailsService;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is IdempotencyConflictException idempotencyConflictException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

                return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    Exception = exception,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status409Conflict,
                        Title = "A chave de idempotência está em conflito.",
                        Detail = idempotencyConflictException.Message,
                        Extensions =
                        {
                            ["code"] = idempotencyConflictException.Code
                        }
                    }
                }).ConfigureAwait(false);
            }

            if (exception is BusinessRuleException businessRuleException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

                return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    Exception = exception,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "A solicitação não pôde ser processada.",
                        Detail = businessRuleException.Message,
                        Extensions =
                        {
                            ["code"] = businessRuleException.Code
                        }
                    }
                }).ConfigureAwait(false);
            }

            LogUnhandledException(
                logger,
                httpContext.TraceIdentifier,
                exception.GetType().FullName ?? exception.GetType().Name);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Ocorreu um erro interno.",
                    Detail = "Não foi possível concluir a solicitação."
                }
            }).ConfigureAwait(false);
        }

        [LoggerMessage(
            EventId = 9000,
            Level = LogLevel.Error,
            Message = "Unhandled request exception. CorrelationId: {CorrelationId}; ExceptionType: {ExceptionType}")]
        private static partial void LogUnhandledException(
            ILogger logger,
            string correlationId,
            string exceptionType);
    }
}