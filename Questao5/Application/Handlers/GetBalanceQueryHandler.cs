using MediatR;
using Questao5.Application.Balances;
using Questao5.Application.Exceptions;
using Questao5.Application.Queries.Requests;
using Questao5.Application.Queries.Responses;

namespace Questao5.Application.Handlers
{
    public sealed class GetBalanceQueryHandler : IRequestHandler<GetBalanceQuery, GetBalanceResponse>
    {
        public const int MaximumAccountIdLength = 37;

        private readonly IBalanceQueryStore balanceQueryStore;
        private readonly TimeProvider timeProvider;

        public GetBalanceQueryHandler(IBalanceQueryStore balanceQueryStore, TimeProvider timeProvider)
        {
            this.balanceQueryStore = balanceQueryStore;
            this.timeProvider = timeProvider;
        }

        public Task<GetBalanceResponse> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // O titular vem do token já validado; um valor inválido aqui é falha de integração, não de negócio.
            if (!Guid.TryParseExact(request.AccountHolderId, "D", out var accountHolderId))
            {
                throw new ArgumentException(
                    "A identificação do correntista deve ser um UUID válido.",
                    nameof(request));
            }

            var accountId = request.AccountId?.Trim();

            if (string.IsNullOrEmpty(accountId) || accountId.Length > MaximumAccountIdLength)
            {
                throw AccountRuleViolations.InvalidAccount();
            }

            var balance = balanceQueryStore.GetBalance(accountHolderId.ToString("D"), accountId, cancellationToken);

            // O instante da consulta é obtido somente depois de a leitura consistente terminar.
            var queriedAt = timeProvider.GetUtcNow();

            return Task.FromResult(new GetBalanceResponse(
                balance.AccountNumber,
                balance.HolderName,
                queriedAt,
                balance.Balance));
        }
    }
}
