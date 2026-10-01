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

        /// <summary>
        /// Consulta o saldo atual de uma conta corrente.
        /// </summary>
        /// <remarks>
        /// O saldo é a soma dos créditos menos a soma dos débitos persistidos até o momento. Uma conta sem
        /// movimentos tem saldo `0.00`. A conta deve pertencer ao correntista identificado pelo token e estar ativa.
        ///
        /// A resposta de sucesso é enviada com `Cache-Control: no-store`.
        /// </remarks>
        /// <param name="idContaCorrente" example="FA99D033-7067-ED11-96C6-7C5DFA4A16C9">
        /// Identificação da conta corrente, com 1 a 37 caracteres. Espaços dentro do identificador devem ser
        /// escritos como `%20`.
        /// </param>
        /// <param name="cancellationToken">Cancelamento da requisição.</param>
        /// <response code="200">Saldo atual da conta.</response>
        /// <response code="400">
        /// Requisição rejeitada. Regras de negócio trazem `code`: `INVALID_ACCOUNT` (conta não cadastrada ou
        /// de outro correntista) ou `INACTIVE_ACCOUNT`. Identificador vazio ou acima de 37 caracteres traz
        /// `errors` por campo, sem `code`.
        /// </response>
        /// <response code="401">Token ausente ou inválido.</response>
        /// <response code="429">Limite de frequência ou de concorrência excedido.</response>
        /// <response code="500">Erro interno. Nenhum detalhe interno é devolvido.</response>
        /// <response code="504">A consulta não foi concluída dentro do tempo limite.</response>
        [HttpGet]
        [ProducesResponseType<GetBalanceHttpResponse>(StatusCodes.Status200OK, "application/json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout, "application/problem+json")]
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
