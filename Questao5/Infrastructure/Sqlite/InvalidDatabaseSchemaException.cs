namespace Questao5.Infrastructure.Sqlite
{
    public sealed class InvalidDatabaseSchemaException : InvalidOperationException
    {
        public InvalidDatabaseSchemaException(string message)
            : base(message)
        {
        }
    }
}