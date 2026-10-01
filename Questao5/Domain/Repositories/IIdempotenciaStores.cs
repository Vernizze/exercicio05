using Questao5.Domain.Entities;

namespace Questao5.Domain.Repositories
{
    public interface IIdempotenciaQueryStore
    {
        Idempotencia? ObterPorChave(string chaveIdempotencia);
    }

    public interface IIdempotenciaCommandStore
    {
        void Inserir(Idempotencia idempotencia);
    }
}
