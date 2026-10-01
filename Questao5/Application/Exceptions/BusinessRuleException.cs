namespace Questao5.Application.Exceptions
{
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string code, string message)
            : base(message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            Code = code;
        }

        public string Code { get; }
    }
}