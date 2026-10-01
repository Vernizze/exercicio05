using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.QueryStore
{
    public sealed class IdempotenciaQueryStore : IIdempotenciaQueryStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public IdempotenciaQueryStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public Idempotencia? ObterPorChave(string chaveIdempotencia)
        {
            var row = connection.QuerySingleOrDefault<Row>(
                """
                SELECT chave_idempotencia AS ChaveIdempotencia, requisicao, resultado
                FROM idempotencia
                WHERE chave_idempotencia = @ChaveIdempotencia;
                """,
                new { ChaveIdempotencia = chaveIdempotencia },
                transaction);

            return row is null ? null : new Idempotencia(row.ChaveIdempotencia, row.Requisicao, row.Resultado);
        }

        private sealed class Row
        {
            public string ChaveIdempotencia { get; init; } = string.Empty;

            public string? Requisicao { get; init; }

            public string? Resultado { get; init; }
        }
    }
}
