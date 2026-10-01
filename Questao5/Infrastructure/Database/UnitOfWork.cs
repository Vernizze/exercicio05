using Microsoft.Data.Sqlite;
using Questao5.Domain.Repositories;
using Questao5.Infrastructure.Database.CommandStore;
using Questao5.Infrastructure.Database.QueryStore;

namespace Questao5.Infrastructure.Database
{
    /// <summary>
    /// Entrega todos os repositórios ligados à mesma conexão e à mesma transação SQLite.
    /// </summary>
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;
        private readonly bool ownsTransaction;

        /// <param name="connection">Conexão aberta.</param>
        /// <param name="transaction">Transação em curso nessa conexão.</param>
        /// <param name="ownsTransaction">
        /// Quando verdadeiro, a unidade de trabalho encerra a transação e a conexão ao ser descartada.
        /// Quando falso, apenas usa uma transação controlada por quem a criou (a migração do banco).
        /// </param>
        public UnitOfWork(SqliteConnection connection, SqliteTransaction transaction, bool ownsTransaction)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(transaction);

            this.connection = connection;
            this.transaction = transaction;
            this.ownsTransaction = ownsTransaction;

            ContaCorrenteQuery = new ContaCorrenteQueryStore(connection, transaction);
            ContaCorrenteCommand = new ContaCorrenteCommandStore(connection, transaction);
            TitularidadeContaQuery = new TitularidadeContaQueryStore(connection, transaction);
            TitularidadeContaCommand = new TitularidadeContaCommandStore(connection, transaction);
            MovimentoQuery = new MovimentoQueryStore(connection, transaction);
            MovimentoCommand = new MovimentoCommandStore(connection, transaction);
            IdempotenciaQuery = new IdempotenciaQueryStore(connection, transaction);
            IdempotenciaCommand = new IdempotenciaCommandStore(connection, transaction);
            SaldoContaQuery = new SaldoContaQueryStore(connection, transaction);
            SaldoContaCommand = new SaldoContaCommandStore(connection, transaction);
        }

        public IContaCorrenteQueryStore ContaCorrenteQuery { get; }

        public IContaCorrenteCommandStore ContaCorrenteCommand { get; }

        public ITitularidadeContaQueryStore TitularidadeContaQuery { get; }

        public ITitularidadeContaCommandStore TitularidadeContaCommand { get; }

        public IMovimentoQueryStore MovimentoQuery { get; }

        public IMovimentoCommandStore MovimentoCommand { get; }

        public IIdempotenciaQueryStore IdempotenciaQuery { get; }

        public IIdempotenciaCommandStore IdempotenciaCommand { get; }

        public ISaldoContaQueryStore SaldoContaQuery { get; }

        public ISaldoContaCommandStore SaldoContaCommand { get; }

        public void Commit()
        {
            if (!ownsTransaction)
            {
                throw new InvalidOperationException(
                    "A transação desta unidade de trabalho é confirmada por quem a criou.");
            }

            transaction.Commit();
        }

        public void Dispose()
        {
            if (!ownsTransaction)
            {
                return;
            }

            // Descartar a transação sem Commit desfaz tudo o que foi escrito.
            transaction.Dispose();
            connection.Dispose();
        }
    }
}
