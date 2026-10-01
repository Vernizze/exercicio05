using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.CommandStore
{
    public sealed class TitularidadeContaCommandStore : ITitularidadeContaCommandStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public TitularidadeContaCommandStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public void InserirSeAusente(TitularidadeConta titularidadeConta)
        {
            ArgumentNullException.ThrowIfNull(titularidadeConta);

            connection.Execute(
                """
                INSERT INTO titularidade_conta(idcontacorrente, idcorrentista)
                VALUES (@IdContaCorrente, @IdCorrentista)
                ON CONFLICT(idcontacorrente) DO NOTHING;
                """,
                new { titularidadeConta.IdContaCorrente, titularidadeConta.IdCorrentista },
                transaction);
        }
    }
}
