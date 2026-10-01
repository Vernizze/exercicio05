namespace Questao5.Domain.Entities
{
    /// <summary>
    /// Conta corrente, como na tabela <c>contacorrente</c> recebida do proponente.
    /// </summary>
    public sealed class ContaCorrente
    {
        public ContaCorrente(string idContaCorrente, long numero, string nome, bool ativo)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idContaCorrente);
            ArgumentNullException.ThrowIfNull(nome);

            IdContaCorrente = idContaCorrente;
            Numero = numero;
            Nome = nome;
            Ativo = ativo;
        }

        public string IdContaCorrente { get; }

        public long Numero { get; }

        /// <summary>Nome do titular da conta.</summary>
        public string Nome { get; }

        public bool Ativo { get; }
    }
}
