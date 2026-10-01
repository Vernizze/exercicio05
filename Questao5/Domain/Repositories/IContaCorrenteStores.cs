using Questao5.Domain.Entities;

namespace Questao5.Domain.Repositories
{
    public interface IContaCorrenteQueryStore
    {
        /// <summary>
        /// Localiza a conta sem diferenciar maiúsculas de minúsculas; a entidade traz o identificador persistido.
        /// </summary>
        ContaCorrente? ObterPorId(string idContaCorrente);

        IReadOnlyList<ContaCorrente> ListarTodas();
    }

    public interface IContaCorrenteCommandStore
    {
        /// <summary>Insere a conta somente quando o identificador ainda não existe.</summary>
        void InserirSeAusente(ContaCorrente contaCorrente);
    }
}
