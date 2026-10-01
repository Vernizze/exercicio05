using Questao5.Domain.Entities;

namespace Questao5.Domain.Repositories
{
    public interface ITitularidadeContaQueryStore
    {
        TitularidadeConta? ObterPorConta(string idContaCorrente);
    }

    public interface ITitularidadeContaCommandStore
    {
        /// <summary>Registra o titular somente quando a conta ainda não possui um; nunca sobrescreve.</summary>
        void InserirSeAusente(TitularidadeConta titularidadeConta);
    }
}
