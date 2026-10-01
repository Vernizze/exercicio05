using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.CommandStore
{
    public sealed class IdempotenciaCommandStore : IIdempotenciaCommandStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public IdempotenciaCommandStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public void Inserir(Idempotencia idempotencia)
        {
            ArgumentNullException.ThrowIfNull(idempotencia);

            connection.Execute(
                """
                INSERT INTO idempotencia(chave_idempotencia, requisicao, resultado)
                VALUES (@ChaveIdempotencia, @Requisicao, @Resultado);
                """,
                new { idempotencia.ChaveIdempotencia, idempotencia.Requisicao, idempotencia.Resultado },
                transaction);
        }
    }
}
