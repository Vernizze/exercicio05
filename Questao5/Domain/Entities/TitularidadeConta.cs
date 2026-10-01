namespace Questao5.Domain.Entities
{
    /// <summary>
    /// Vínculo entre uma conta e o correntista que é seu titular (tabela <c>titularidade_conta</c>).
    /// </summary>
    public sealed class TitularidadeConta
    {
        public TitularidadeConta(string idContaCorrente, string idCorrentista)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idContaCorrente);
            ArgumentException.ThrowIfNullOrWhiteSpace(idCorrentista);

            IdContaCorrente = idContaCorrente;
            IdCorrentista = idCorrentista;
        }

        public string IdContaCorrente { get; }

        public string IdCorrentista { get; }

        public bool PertenceA(string idCorrentista)
        {
            return string.Equals(IdCorrentista, idCorrentista, StringComparison.OrdinalIgnoreCase);
        }
    }
}
