using MediatR;
using Questao5.Application.Accounts;
using Questao5.Application.Exceptions;
using Questao5.Application.Queries.Requests;
using Questao5.Application.Queries.Responses;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Application.Handlers
{
    public sealed class GetBalanceQueryHandler : IRequestHandler<GetBalanceQuery, GetBalanceResponse>
    {
        public const int MaximumAccountIdLength = 37;

        private readonly IUnitOfWorkFactory unitOfWorkFactory;
        private readonly TimeProvider timeProvider;

        public GetBalanceQueryHandler(IUnitOfWorkFactory unitOfWorkFactory, TimeProvider timeProvider)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
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

            cancellationToken.ThrowIfCancellationRequested();

            ContaCorrente conta;
            SaldoConta saldo;

            // Transação de leitura: conta, titularidade e saldo vêm do mesmo snapshot,
            // e a consulta não disputa a reserva de escritor com a movimentação.
            using (var unitOfWork = unitOfWorkFactory.BeginRead())
            {
                conta = AccountAccessPolicy.EnsureAccessible(unitOfWork, accountId, accountHolderId.ToString("D"));
                saldo = unitOfWork.SaldoContaQuery.ObterPorConta(conta.IdContaCorrente)
                    ?? throw new InvalidOperationException("O saldo consolidado da conta não existe.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // O instante da consulta é obtido somente depois de a leitura consistente terminar.
            var queriedAt = timeProvider.GetUtcNow();

            return Task.FromResult(new GetBalanceResponse(conta.Numero, conta.Nome, queriedAt, saldo.Saldo));
        }
    }
}
