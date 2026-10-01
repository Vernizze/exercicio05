using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Infrastructure.Database.CommandStore
{
    public sealed class SaldoContaCommandStore : ISaldoContaCommandStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public SaldoContaCommandStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public void Inserir(SaldoConta saldoConta)
        {
            ArgumentNullException.ThrowIfNull(saldoConta);

            connection.Execute(
                """
                INSERT INTO saldo_conta(idcontacorrente, saldo_centavos, versao)
                VALUES (@IdContaCorrente, @SaldoCentavos, @Versao);
                """,
                new { saldoConta.IdContaCorrente, saldoConta.SaldoCentavos, saldoConta.Versao },
                transaction);
        }

        public void Atualizar(SaldoConta saldoConta)
        {
            ArgumentNullException.ThrowIfNull(saldoConta);

            // Chamado dentro da transação imediata, que garante escritor único: gravar o saldo e a versão
            // calculados pela entidade não perde atualização concorrente.
            var affectedRows = connection.Execute(
                """
                UPDATE saldo_conta
                SET saldo_centavos = @SaldoCentavos,
                    versao = @Versao
                WHERE idcontacorrente = @IdContaCorrente;
                """,
                new { saldoConta.IdContaCorrente, saldoConta.SaldoCentavos, saldoConta.Versao },
                transaction);

            if (affectedRows != 1)
            {
                throw new InvalidOperationException("O saldo consolidado da conta não foi atualizado.");
            }
        }
    }
}
