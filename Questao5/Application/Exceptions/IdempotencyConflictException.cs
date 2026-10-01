namespace Questao5.Application.Exceptions
{
    public sealed class IdempotencyConflictException : Exception
    {
        public const string ErrorCode = "IDEMPOTENCY_CONFLICT";

        public IdempotencyConflictException()
            : base("A chave de idempotência já foi utilizada com uma requisição diferente.")
        {
        }

        public string Code => ErrorCode;
    }
}