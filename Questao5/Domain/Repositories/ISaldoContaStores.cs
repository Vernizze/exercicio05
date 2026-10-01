using Questao5.Domain.Entities;

namespace Questao5.Domain.Repositories
{
    public interface ISaldoContaQueryStore
    {
        SaldoConta? ObterPorConta(string idContaCorrente);

        IReadOnlyList<SaldoConta> ListarTodos();
    }

    public interface ISaldoContaCommandStore
    {
        void Inserir(SaldoConta saldoConta);

        void Atualizar(SaldoConta saldoConta);
    }
}
