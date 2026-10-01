namespace Questao5.Domain.Entities
{
    /// <summary>
    /// Registro de uma requisição já executada (tabela <c>idempotencia</c> recebida do proponente).
    /// </summary>
    public sealed class Idempotencia
    {
        private const string PrefixoResultado = "v1|idMovimento=";

        public Idempotencia(string chaveIdempotencia, string? requisicao, string? resultado)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(chaveIdempotencia);

            ChaveIdempotencia = chaveIdempotencia;
            Requisicao = requisicao;
            Resultado = resultado;
        }

        public string ChaveIdempotencia { get; }

        /// <summary>Representação canônica e versionada da requisição original.</summary>
        public string? Requisicao { get; }

        /// <summary>Resultado mínimo e versionado da execução original.</summary>
        public string? Resultado { get; }

        /// <summary>
        /// Registra a execução bem-sucedida de uma requisição, guardando somente o identificador do movimento.
        /// </summary>
        public static Idempotencia Registrar(string chaveIdempotencia, string requisicaoCanonica, string idMovimento)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requisicaoCanonica);
            ArgumentException.ThrowIfNullOrWhiteSpace(idMovimento);

            return new Idempotencia(chaveIdempotencia, requisicaoCanonica, $"{PrefixoResultado}{idMovimento}");
        }

        /// <summary>
        /// Indica se a requisição recebida é a mesma que foi executada. A comparação é ordinal e exata.
        /// </summary>
        public bool CorrespondeA(string requisicaoCanonica)
        {
            return string.Equals(Requisicao, requisicaoCanonica, StringComparison.Ordinal);
        }

        public string ObterIdMovimento()
        {
            if (Resultado is null ||
                !Resultado.StartsWith(PrefixoResultado, StringComparison.Ordinal) ||
                !Guid.TryParseExact(Resultado[PrefixoResultado.Length..], "D", out var idMovimento))
            {
                throw new InvalidOperationException("O resultado idempotente persistido é inválido.");
            }

            return idMovimento.ToString("D");
        }
    }
}
