using MediatR;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using Questao5.Controllers.Filters;
using Questao5.Controllers.Models;
using Questao5.Infrastructure.Services.Movements;
using Questao5.Infrastructure.Services.Security;

namespace Questao5.Controllers
{
    [ApiController]
    [Route("api/v1/movimentos")]
    public sealed class MovementController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly MovementLogger movementLogger;
        private readonly SecurityLogger securityLogger;

        public MovementController(
            IMediator mediator,
            MovementLogger movementLogger,
            SecurityLogger securityLogger)
        {
            this.mediator = mediator;
            this.movementLogger = movementLogger;
            this.securityLogger = securityLogger;
        }

        /// <summary>
        /// Movimenta uma conta corrente com um crédito ou um débito.
        /// </summary>
        /// <remarks>
        /// A conta deve pertencer ao correntista identificado pelo token e estar ativa.
        ///
        /// A operação é idempotente: `idRequisicao` identifica a requisição. Se o cliente perder a resposta,
        /// pode repetir exatamente a mesma requisição e receberá o mesmo `idMovimento`, sem que outro movimento
        /// seja criado. A mesma chave com dados diferentes é rejeitada com HTTP 409.
        ///
        /// Valor e tipo são conferidos antes da conta: uma requisição com valor ou tipo inválido responde
        /// `INVALID_VALUE` ou `INVALID_TYPE` mesmo que a conta não exista.
        /// </remarks>
        /// <param name="request">Dados da movimentação.</param>
        /// <param name="cancellationToken">Cancelamento da requisição.</param>
        /// <response code="200">Movimento confirmado, ou repetição idêntica de um movimento já confirmado.</response>
        /// <response code="400">
        /// Requisição rejeitada. Regras de negócio trazem `code`: `INVALID_VALUE`, `INVALID_TYPE`,
        /// `INVALID_ACCOUNT` (conta não cadastrada ou de outro correntista) ou `INACTIVE_ACCOUNT`.
        /// Erros estruturais, como campo ausente ou em formato inválido, trazem `errors` por campo, sem `code`.
        /// </response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="409">A chave de idempotência já foi usada com uma requisição diferente.</response>
        /// <response code="413">O corpo da requisição excede 4 KiB.</response>
        /// <response code="415">O conteúdo enviado não é `application/json`.</response>
        /// <response code="429">Limite de frequência ou de concorrência excedido.</response>
        /// <response code="500">Erro interno. Nenhum detalhe interno é devolvido.</response>
        /// <response code="504">A movimentação não foi concluída dentro do tempo limite.</response>
        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType<CreateMovementHttpResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout, "application/problem+json")]
        [RequestSizeLimit(MovementRequestBodySizeLimitAttribute.MaximumBodySize)]
        [MovementRequestBodySizeLimit]
        [RequestTimeout(MovementPolicyNames.RequestTimeout)]
        public async Task<ActionResult<CreateMovementHttpResponse>> Create(
            [FromBody] CreateMovementRequest request,
            CancellationToken cancellationToken)
        {
            var requestId = Guid.ParseExact(request.IdRequisicao!, "D").ToString("D");
            var fingerprint = IdempotencyFingerprint.Create(requestId);
            var accountHolderId = User.GetAccountHolderId();
            var command = new CreateMovementCommand(
                requestId,
                accountHolderId,
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
            catch (AccountOwnershipDeniedException)
            {
                securityLogger.AccessDenied(
                    HttpContext.TraceIdentifier,
                    SecurityFingerprint.Create(accountHolderId),
                    SecurityFingerprint.ForAccount(request.IdContaCorrente!));
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