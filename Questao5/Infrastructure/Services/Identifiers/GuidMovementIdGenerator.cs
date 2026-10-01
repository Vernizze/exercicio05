using Questao5.Application.Movements;

namespace Questao5.Infrastructure.Services.Identifiers
{
    public sealed class GuidMovementIdGenerator : IMovementIdGenerator
    {
        public string Create()
        {
            return Guid.NewGuid().ToString("D");
        }
    }
}