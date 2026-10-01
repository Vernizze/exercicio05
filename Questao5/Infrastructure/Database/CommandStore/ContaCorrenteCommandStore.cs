using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.CommandStore
{
    public sealed class ContaCorrenteCommandStore : IContaCorrenteCommandStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public ContaCorrenteCommandStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public void InserirSeAusente(ContaCorrente contaCorrente)
        {
            ArgumentNullException.ThrowIfNull(contaCorrente);

            connection.Execute(
                """
                INSERT INTO contacorrente(idcontacorrente, numero, nome, ativo)
                VALUES (@IdContaCorrente, @Numero, @Nome, @Ativo)
                ON CONFLICT(idcontacorrente) DO NOTHING;
                """,
                new
                {
                    contaCorrente.IdContaCorrente,
                    contaCorrente.Numero,
                    contaCorrente.Nome,
                    Ativo = contaCorrente.Ativo ? 1 : 0
                },
                transaction);
        }
    }
}
