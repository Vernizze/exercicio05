using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.QueryStore
{
    public sealed class SaldoContaQueryStore : ISaldoContaQueryStore
    {
        private const string Colunas = "idcontacorrente, saldo_centavos AS SaldoCentavos, versao";

        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public SaldoContaQueryStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public SaldoConta? ObterPorConta(string idContaCorrente)
        {
            var row = connection.QuerySingleOrDefault<Row>(
                $"SELECT {Colunas} FROM saldo_conta WHERE idcontacorrente = @IdContaCorrente;",
                new { IdContaCorrente = idContaCorrente },
                transaction);

            return row is null ? null : ToEntity(row);
        }

        public IReadOnlyList<SaldoConta> ListarTodos()
        {
            return connection.Query<Row>(
                    $"SELECT {Colunas} FROM saldo_conta ORDER BY idcontacorrente;",
                    transaction: transaction)
                .Select(ToEntity)
                .ToArray();
        }

        private static SaldoConta ToEntity(Row row)
        {
            // O SQLite aceita um REAL em coluna INTEGER: saldo ou versão não inteiros são falha, nunca arredondados.
            if (row.SaldoCentavos is not long saldoCentavos || row.Versao is not long versao)
            {
                throw new InvalidOperationException("O saldo consolidado da conta não é inteiro.");
            }

            return new SaldoConta(row.IdContaCorrente, saldoCentavos, versao);
        }

        private sealed class Row
        {
            public string IdContaCorrente { get; init; } = string.Empty;

            public object? SaldoCentavos { get; init; }

            public object? Versao { get; init; }
        }
    }
}
