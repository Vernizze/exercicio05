using Questao5.Domain.Entities;
using Questao5.Domain.Enumerators;
using Questao5.Domain.Repositories;

namespace Questao5.Application.Balances
{
    /// <summary>
    /// Preenchimento inicial e reconciliação do saldo consolidado (<c>saldo_conta</c>) a partir dos movimentos.
    /// </summary>
    public static class BalanceProjection
    {
        /// <summary>
        /// Cria o saldo das contas que ainda não o possuem, reconstruindo o valor a partir dos movimentos
        /// persistidos. Saldos existentes não são alterados.
        /// </summary>
        public static void Backfill(IUnitOfWork unitOfWork)
        {
            ArgumentNullException.ThrowIfNull(unitOfWork);

            var existingBalances = unitOfWork.SaldoContaQuery.ListarTodos()
                .Select(saldo => saldo.IdContaCorrente)
                .ToHashSet(StringComparer.Ordinal);
            var accountsWithoutBalance = unitOfWork.ContaCorrenteQuery.ListarTodas()
                .Where(conta => !existingBalances.Contains(conta.IdContaCorrente))
                .ToArray();

            if (accountsWithoutBalance.Length == 0)
            {
                return;
            }

            var totals = ComputeFromMovements(unitOfWork);

            foreach (var conta in accountsWithoutBalance)
            {
                totals.TryGetValue(conta.IdContaCorrente, out var total);
                unitOfWork.SaldoContaCommand.Inserir(
                    new SaldoConta(conta.IdContaCorrente, total.BalanceCents, total.MovementCount));
            }
        }

        /// <summary>
        /// Compara o saldo consolidado com o recalculado dos movimentos e devolve as contas divergentes.
        /// </summary>
        public static IReadOnlyList<string> FindDivergentAccounts(IUnitOfWork unitOfWork)
        {
            ArgumentNullException.ThrowIfNull(unitOfWork);

            var totals = ComputeFromMovements(unitOfWork);
            var balances = unitOfWork.SaldoContaQuery.ListarTodos()
                .ToDictionary(saldo => saldo.IdContaCorrente, StringComparer.Ordinal);
            var divergentAccounts = new List<string>();

            foreach (var conta in unitOfWork.ContaCorrenteQuery.ListarTodas())
            {
                totals.TryGetValue(conta.IdContaCorrente, out var expected);

                if (!balances.TryGetValue(conta.IdContaCorrente, out var saldo) ||
                    saldo.SaldoCentavos != expected.BalanceCents ||
                    saldo.Versao != expected.MovementCount)
                {
                    divergentAccounts.Add(conta.IdContaCorrente);
                }
            }

            return divergentAccounts;
        }

        private static Dictionary<string, AccountTotal> ComputeFromMovements(IUnitOfWork unitOfWork)
        {
            var balances = new Dictionary<string, (decimal Balance, long Count)>(StringComparer.Ordinal);

            // Cada movimento é acumulado em decimal; o repositório já converteu e validou o REAL legado.
            foreach (var movimento in unitOfWork.MovimentoQuery.ListarTodos())
            {
                balances.TryGetValue(movimento.IdContaCorrente, out var current);

                balances[movimento.IdContaCorrente] = movimento.TipoMovimento == TipoMovimento.Credito
                    ? (current.Balance + movimento.Valor, checked(current.Count + 1))
                    : (current.Balance - movimento.Valor, checked(current.Count + 1));
            }

            return balances.ToDictionary(
                pair => pair.Key,
                pair => new AccountTotal(SaldoConta.ParaCentavos(pair.Value.Balance), pair.Value.Count),
                StringComparer.Ordinal);
        }

        private readonly record struct AccountTotal(long BalanceCents, long MovementCount);
    }
}
