using Questao5.Domain.Enumerators;
using System.Globalization;

namespace Questao5.Domain.Entities
{
    /// <summary>
    /// Movimento de crédito ou débito de uma conta (tabela <c>movimento</c> recebida do proponente).
    /// </summary>
    public sealed class Movimento
    {
        public const decimal ValorMaximo = 9999999999.99m;

        /// <summary>Formato legado da coluna <c>datamovimento</c>.</summary>
        public const string FormatoData = "dd/MM/yyyy";

        public Movimento(
            string idMovimento,
            string idContaCorrente,
            string dataMovimento,
            TipoMovimento tipoMovimento,
            decimal valor)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idMovimento);
            ArgumentException.ThrowIfNullOrWhiteSpace(idContaCorrente);
            ArgumentNullException.ThrowIfNull(dataMovimento);

            if (valor <= 0 || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(valor),
                    "O valor do movimento deve ser positivo, ter no máximo duas casas e respeitar o limite.");
            }

            IdMovimento = idMovimento;
            IdContaCorrente = idContaCorrente;
            DataMovimento = dataMovimento;
            TipoMovimento = tipoMovimento;
            Valor = valor;
        }

        public string IdMovimento { get; }

        public string IdContaCorrente { get; }

        /// <summary>Data do movimento no formato persistido (<c>dd/MM/yyyy</c> para os movimentos criados pela API).</summary>
        public string DataMovimento { get; }

        public TipoMovimento TipoMovimento { get; }

        public decimal Valor { get; }

        /// <summary>
        /// Cria um movimento novo, com a data em UTC no formato legado e cultura invariável.
        /// </summary>
        public static Movimento Criar(
            string idMovimento,
            string idContaCorrente,
            DateTimeOffset instante,
            TipoMovimento tipoMovimento,
            decimal valor)
        {
            var dataMovimento = instante.ToUniversalTime().ToString(FormatoData, CultureInfo.InvariantCulture);

            return new Movimento(idMovimento, idContaCorrente, dataMovimento, tipoMovimento, valor);
        }
    }
}
