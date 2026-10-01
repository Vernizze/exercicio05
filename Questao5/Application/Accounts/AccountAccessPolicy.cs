using Questao5.Application.Exceptions;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Application.Accounts
{
    /// <summary>
    /// Regra única de acesso a uma conta, usada pela movimentação e pela consulta de saldo.
    /// </summary>
    public static class AccountAccessPolicy
    {
        /// <summary>
        /// Valida, nesta ordem, que a conta existe, pertence ao correntista e está ativa.
        /// Conta de outro correntista, ou sem titular, é rejeitada com a mesma resposta de conta inexistente.
        /// </summary>
        public static ContaCorrente EnsureAccessible(
            IUnitOfWork unitOfWork,
            string accountId,
            string accountHolderId)
        {
            ArgumentNullException.ThrowIfNull(unitOfWork);

            var conta = unitOfWork.ContaCorrenteQuery.ObterPorId(accountId)
                ?? throw AccountRuleViolations.InvalidAccount();
            var titularidade = unitOfWork.TitularidadeContaQuery.ObterPorConta(conta.IdContaCorrente);

            // Conta sem titularidade não pertence a ninguém (falha fechada).
            if (titularidade is null || !titularidade.PertenceA(accountHolderId))
            {
                throw AccountRuleViolations.OwnershipDenied();
            }

            if (!conta.Ativo)
            {
                throw AccountRuleViolations.InactiveAccount();
            }

            return conta;
        }
    }
}
