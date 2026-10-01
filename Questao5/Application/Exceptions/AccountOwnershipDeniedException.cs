namespace Questao5.Application.Exceptions
{
    /// <summary>
    /// Conta existente que não pertence ao correntista autenticado. Para o cliente é indistinguível
    /// de conta não cadastrada; o tipo próprio existe somente para o registro interno do motivo real.
    /// </summary>
    public sealed class AccountOwnershipDeniedException : BusinessRuleException
    {
        public AccountOwnershipDeniedException(string code, string message)
            : base(code, message)
        {
        }
    }
}
