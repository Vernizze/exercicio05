using Microsoft.Data.Sqlite;
using Questao5.Domain.Repositories;
using Questao5.Infrastructure.Sqlite;

namespace Questao5.Infrastructure.Database
{
    public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly ISqliteConnectionFactory connectionFactory;

        public UnitOfWorkFactory(ISqliteConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public IUnitOfWork BeginWrite()
        {
            return Begin(deferred: false);
        }

        public IUnitOfWork BeginRead()
        {
            return Begin(deferred: true);
        }

        private UnitOfWork Begin(bool deferred)
        {
            var connection = connectionFactory.OpenConnection();
            SqliteTransaction? transaction = null;

            try
            {
                transaction = connection.BeginTransaction(deferred);
                return new UnitOfWork(connection, transaction, ownsTransaction: true);
            }
            catch
            {
                transaction?.Dispose();
                connection.Dispose();
                throw;
            }
        }
    }
}
