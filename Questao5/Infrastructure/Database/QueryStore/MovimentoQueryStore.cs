using Dapper;
using Microsoft.Data.Sqlite;
using Questao5.Domain.Entities;
using Questao5.Domain.Enumerators;
using Questao5.Domain.Repositories;
using Questao5.Infrastructure.Sqlite;
using System.Globalization;

namespace Questao5.Infrastructure.Database.QueryStore
{
    public sealed class MovimentoQueryStore : IMovimentoQueryStore
    {
        private readonly SqliteConnection connection;
        private readonly SqliteTransaction transaction;

        public MovimentoQueryStore(SqliteConnection connection, SqliteTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public IEnumerable<Movimento> ListarTodos()
        {
            // Sem SUM(valor): cada linha é lida e cada REAL legado é convertido e validado individualmente.
            var rows = connection.Query<Row>(
                """
                SELECT idmovimento, idcontacorrente, datamovimento, tipomovimento, valor
                FROM movimento;
                """,
                transaction: transaction,
                buffered: false);

            foreach (var row in rows)
            {
                yield return ToEntity(row);
            }
        }

        private static Movimento ToEntity(Row row)
        {
            if (!TipoMovimentoExtensions.TentarConverter(row.TipoMovimento, out var tipoMovimento))
            {
                throw new InvalidDatabaseSchemaException(
                    "A tabela 'movimento' contém tipo de movimento incompatível.");
            }

            return new Movimento(
                ToText(row.IdMovimento),
                ToText(row.IdContaCorrente),
                ToText(row.DataMovimento),
                tipoMovimento,
                ToDecimal(row.Valor));
        }

        // As colunas legadas têm afinidades que não garantem texto (idcontacorrente é INTEGER(10)).
        private static string ToText(object? value)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static decimal ToDecimal(object? persistedAmount)
        {
            if (persistedAmount is not double value || !double.IsFinite(value))
            {
                throw new InvalidDatabaseSchemaException(
                    "A tabela 'movimento' contém valor monetário não numérico ou não finito.");
            }

            decimal amount;

            try
            {
                amount = Convert.ToDecimal(value);
            }
            catch (OverflowException)
            {
                throw new InvalidDatabaseSchemaException(
                    "A tabela 'movimento' contém valor monetário fora do limite suportado.");
            }

            if (amount <= 0 || amount > Movimento.ValorMaximo || decimal.Round(amount, 2) != amount)
            {
                throw new InvalidDatabaseSchemaException(
                    "A tabela 'movimento' contém valor monetário fora do contrato.");
            }

            return amount;
        }

        private sealed class Row
        {
            public object? IdMovimento { get; init; }

            public object? IdContaCorrente { get; init; }

            public object? DataMovimento { get; init; }

            public string? TipoMovimento { get; init; }

            public object? Valor { get; init; }
        }
    }
}
