using MediatR;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using Questao5.Infrastructure.Services.Controllers.Filters;
using Questao5.Infrastructure.Services.Controllers.Models;
using Questao5.Infrastructure.Services.Movements;

namespace Questao5.Infrastructure.Services.Controllers
{
    [ApiController]
    [Route("api/v1/movimentos")]
    public sealed class MovementController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly MovementLogger movementLogger;

        public MovementController(IMediator mediator, MovementLogger movementLogger)
        {
            this.mediator = mediator;
            this.movementLogger = movementLogger;
        }

        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType<CreateMovementHttpResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
        [RequestSizeLimit(MovementRequestBodySizeLimitAttribute.MaximumBodySize)]
        [MovementRequestBodySizeLimit]
        [RequestTimeout(MovementPolicyNames.RequestTimeout)]
        public async Task<ActionResult<CreateMovementHttpResponse>> Create(
            [FromBody] CreateMovementRequest request,
            CancellationToken cancellationToken)
        {
            var requestId = Guid.ParseExact(request.IdRequisicao!, "D").ToString("D");
            var fingerprint = IdempotencyFingerprint.Create(requestId);
            var command = new CreateMovementCommand(
                requestId,
                request.IdContaCorrente!,
                request.Valor!.Value,
                request.TipoMovimento!);

            try
            {
                var response = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

                if (response.IsReplay)
                {
                    movementLogger.Replayed(HttpContext.TraceIdentifier, response.MovementId, fingerprint);
                }
                else
                {
                    movementLogger.Created(HttpContext.TraceIdentifier, response.MovementId, fingerprint);
                }

                return Ok(new CreateMovementHttpResponse(response.MovementId));
            }
            catch (IdempotencyConflictException)
            {
                movementLogger.Conflict(HttpContext.TraceIdentifier, fingerprint);
                throw;
            }
            catch (BusinessRuleException exception)
            {
                movementLogger.BusinessRejection(HttpContext.TraceIdentifier, exception.Code);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                movementLogger.UnexpectedRollback(
                    HttpContext.TraceIdentifier,
                    exception.GetType().FullName ?? exception.GetType().Name);
                throw;
            }
        }
    }
}