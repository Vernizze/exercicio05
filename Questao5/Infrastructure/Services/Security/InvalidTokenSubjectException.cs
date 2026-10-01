namespace Questao5.Infrastructure.Services.Security
{
    public sealed class InvalidTokenSubjectException : Exception
    {
        public InvalidTokenSubjectException()
            : base("O token não identifica um correntista válido.")
        {
        }
    }
}
