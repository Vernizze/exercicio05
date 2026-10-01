namespace Questao5.Application.Exceptions
{
    public static class AccountRuleViolations
    {
        public const string InvalidAccountCode = "INVALID_ACCOUNT";
        public const string InactiveAccountCode = "INACTIVE_ACCOUNT";

        private const string InvalidAccountMessage = "A conta corrente informada não está cadastrada.";
        private const string InactiveAccountMessage = "A conta corrente informada está inativa.";

        public static BusinessRuleException InvalidAccount()
        {
            return new BusinessRuleException(InvalidAccountCode, InvalidAccountMessage);
        }

        public static AccountOwnershipDeniedException OwnershipDenied()
        {
            return new AccountOwnershipDeniedException(InvalidAccountCode, InvalidAccountMessage);
        }

        public static BusinessRuleException InactiveAccount()
        {
            return new BusinessRuleException(InactiveAccountCode, InactiveAccountMessage);
        }
    }
}
