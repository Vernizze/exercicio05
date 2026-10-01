using MediatR;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Questao5.Application.Exceptions;
using Questao5.Application.Queries.Requests;
using Questao5.Infrastructure.Services.Balances;
using Questao5.Infrastructure.Services.Controllers.Models;
using Questao5.Infrastructure.Services.Security;

namespace Questao5.Infrastructure.Services.Controllers
{
    [ApiController]
    [Route("api/v1/contas/{idContaCorrente}/saldo")]
    public sealed class BalanceController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly BalanceLogger balanceLogger;
        private readonly SecurityLogger securityLogger;

        public BalanceController(
            IMediator mediator,
            BalanceLogger balanceLogger,
            SecurityLogger securityLogger)
        {
            this.mediator = mediator;
            this.balanceLogger = balanceLogger;
            this.securityLogger = securityLogger;
        }

        [HttpGet]
        [ProducesResponseType<GetBalanceHttpResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [RequestTimeout(BalancePolicyNames.RequestTimeout)]
        public async Task<ActionResult<GetBalanceHttpResponse>> Get(
            [FromRoute, AccountIdentifier] string idContaCorrente,
            CancellationToken cancellationToken)
        {
            var accountHolderId = User.GetAccountHolderId();
            var accountFingerprint = SecurityFingerprint.ForAccount(idContaCorrente);

            try
            {
                var response = await mediator
                    .Send(new GetBalanceQuery(accountHolderId, idContaCorrente), cancellationToken)
                    .ConfigureAwait(false);

                balanceLogger.Consulted(HttpContext.TraceIdentifier, accountFingerprint);
                Response.Headers.CacheControl = "no-store";

                return Ok(GetBalanceHttpResponse.From(response));
            }
            catch (AccountOwnershipDeniedException)
            {
                securityLogger.AccessDenied(
                    HttpContext.TraceIdentifier,
                    SecurityFingerprint.Create(accountHolderId),
                    accountFingerprint);
                throw;
            }
            catch (BusinessRuleException exception)
            {
                balanceLogger.BusinessRejection(HttpContext.TraceIdentifier, accountFingerprint, exception.Code);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                balanceLogger.UnexpectedFailure(
                    HttpContext.TraceIdentifier,
                    accountFingerprint,
                    exception.GetType().FullName ?? exception.GetType().Name);
                throw;
            }
        }
    }
}
