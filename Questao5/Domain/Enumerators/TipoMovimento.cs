namespace Questao5.Domain.Enumerators
{
    /// <summary>
    /// Tipo do movimento, com o mesmo código gravado na coluna <c>movimento.tipomovimento</c>.
    /// </summary>
    public enum TipoMovimento
    {
        Credito = 'C',
        Debito = 'D'
    }

    public static class TipoMovimentoExtensions
    {
        public static string ParaCodigo(this TipoMovimento tipoMovimento)
        {
            return tipoMovimento switch
            {
                TipoMovimento.Credito => "C",
                TipoMovimento.Debito => "D",
                _ => throw new ArgumentOutOfRangeException(nameof(tipoMovimento), tipoMovimento, "Tipo de movimento inválido.")
            };
        }

        public static bool TentarConverter(string? codigo, out TipoMovimento tipoMovimento)
        {
            switch (codigo)
            {
                case "C":
                    tipoMovimento = TipoMovimento.Credito;
                    return true;
                case "D":
                    tipoMovimento = TipoMovimento.Debito;
                    return true;
                default:
                    tipoMovimento = default;
                    return false;
            }
        }
    }
}
