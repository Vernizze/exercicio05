using Questao5.Domain.Enumerators;

namespace Questao5.Domain.Entities
{
    /// <summary>
    /// Saldo consolidado de uma conta, em centavos inteiros (tabela <c>saldo_conta</c>).
    /// </summary>
    public sealed class SaldoConta
    {
        public SaldoConta(string idContaCorrente, long saldoCentavos, long versao)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idContaCorrente);

            IdContaCorrente = idContaCorrente;
            SaldoCentavos = saldoCentavos;
            Versao = versao;
        }

        public string IdContaCorrente { get; }

        public long SaldoCentavos { get; private set; }

        /// <summary>Quantidade de movimentos já aplicados ao saldo.</summary>
        public long Versao { get; private set; }

        /// <summary>
        /// Saldo em <c>decimal</c>, construído diretamente dos centavos com escala 2 (0.00, 10.50), sem ponto flutuante.
        /// </summary>
        public decimal Saldo
        {
            get
            {
                if (SaldoCentavos == long.MinValue)
                {
                    throw new InvalidOperationException("O saldo persistido está fora do limite suportado.");
                }

                var magnitude = (ulong)Math.Abs(SaldoCentavos);
                return new decimal(
                    unchecked((int)(magnitude & 0xFFFFFFFF)),
                    unchecked((int)(magnitude >> 32)),
                    0,
                    SaldoCentavos < 0,
                    2);
            }
        }

        public static long ParaCentavos(decimal valor)
        {
            var centavos = valor * 100m;

            if (decimal.Truncate(centavos) != centavos)
            {
                throw new InvalidOperationException("O valor monetário possui mais de duas casas decimais.");
            }

            return decimal.ToInt64(centavos);
        }

        /// <summary>
        /// Soma um crédito ou subtrai um débito, com aritmética inteira verificada, e conta mais um movimento.
        /// </summary>
        public void Aplicar(Movimento movimento)
        {
            ArgumentNullException.ThrowIfNull(movimento);

            var centavos = ParaCentavos(movimento.Valor);

            SaldoCentavos = movimento.TipoMovimento == TipoMovimento.Credito
                ? checked(SaldoCentavos + centavos)
                : checked(SaldoCentavos - centavos);
            Versao = checked(Versao + 1);
        }
    }
}
