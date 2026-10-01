using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.QueryStore
{
    public sealed class ContaCorrenteQueryStore : IContaCorrenteQueryStore
    {
        private const string Colunas = "idcontacorrente, numero, nome, ativo";

        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public ContaCorrenteQueryStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public ContaCorrente? ObterPorId(string idContaCorrente)
        {
            var row = connection.QuerySingleOrDefault<Row>(
                $"""
                SELECT {Colunas}
                FROM contacorrente
                WHERE idcontacorrente = @IdContaCorrente COLLATE NOCASE;
                """,
                new { IdContaCorrente = idContaCorrente },
                transaction);

            return row is null ? null : ToEntity(row);
        }

        public IReadOnlyList<ContaCorrente> ListarTodas()
        {
            return connection.Query<Row>(
                    $"SELECT {Colunas} FROM contacorrente ORDER BY idcontacorrente;",
                    transaction: transaction)
                .Select(ToEntity)
                .ToArray();
        }

        private static ContaCorrente ToEntity(Row row)
        {
            return new ContaCorrente(row.IdContaCorrente, row.Numero, row.Nome, row.Ativo == 1);
        }

        private sealed class Row
        {
            public string IdContaCorrente { get; init; } = string.Empty;

            public long Numero { get; init; }

            public string Nome { get; init; } = string.Empty;

            public long Ativo { get; init; }
        }
    }
}
