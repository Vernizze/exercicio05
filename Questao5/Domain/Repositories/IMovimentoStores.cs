using Questao5.Domain.Entities;

namespace Questao5.Domain.Repositories
{
    public interface IMovimentoQueryStore
    {
        /// <summary>
        /// Percorre todos os movimentos, um a um, validando cada valor persistido. Usado no preenchimento
        /// inicial e na reconciliação do saldo consolidado, nunca na consulta de saldo.
        /// </summary>
        IEnumerable<Movimento> ListarTodos();
    }

    public interface IMovimentoCommandStore
    {
        void Inserir(Movimento movimento);
    }
}
