using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.QueryStore
{
    public sealed class TitularidadeContaQueryStore : ITitularidadeContaQueryStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public TitularidadeContaQueryStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public TitularidadeConta? ObterPorConta(string idContaCorrente)
        {
            var row = connection.QuerySingleOrDefault<Row>(
                """
                SELECT idcontacorrente, idcorrentista
                FROM titularidade_conta
                WHERE idcontacorrente = @IdContaCorrente;
                """,
                new { IdContaCorrente = idContaCorrente },
                transaction);

            return row is null ? null : new TitularidadeConta(row.IdContaCorrente, row.IdCorrentista);
        }

        private sealed class Row
        {
            public string IdContaCorrente { get; init; } = string.Empty;

            public string IdCorrentista { get; init; } = string.Empty;
        }
    }
}
