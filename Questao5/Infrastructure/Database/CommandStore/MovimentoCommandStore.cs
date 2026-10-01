using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Enumerators;
using Questao5.Domain.Repositories;
using System.Globalization;

namespace Questao5.Infrastructure.Database.CommandStore
{
    public sealed class MovimentoCommandStore : IMovimentoCommandStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public MovimentoCommandStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public void Inserir(Movimento movimento)
        {
            ArgumentNullException.ThrowIfNull(movimento);

            connection.Execute(
                """
                INSERT INTO movimento(idmovimento, idcontacorrente, datamovimento, tipomovimento, valor)
                VALUES (@IdMovimento, @IdContaCorrente, @DataMovimento, @TipoMovimento, @Valor);
                """,
                new
                {
                    movimento.IdMovimento,
                    movimento.IdContaCorrente,
                    movimento.DataMovimento,
                    TipoMovimento = movimento.TipoMovimento.ParaCodigo(),

                    // A coluna legada é REAL: o decimal validado só vira ponto flutuante aqui, na persistência.
                    Valor = Convert.ToDouble(movimento.Valor, CultureInfo.InvariantCulture)
                },
                transaction);
        }
    }
}
