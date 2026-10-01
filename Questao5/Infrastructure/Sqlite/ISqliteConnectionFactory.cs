using Microsoft.Data.Sqlite;

namespace Questao5.Infrastructure.Sqlite
{
    public interface ISqliteConnectionFactory
    {
        SqliteConnection OpenConnection();
    }
}