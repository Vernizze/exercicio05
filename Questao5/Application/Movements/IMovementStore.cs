using Questao5.Application.Commands.Responses;

namespace Questao5.Application.Movements
{
    public interface IMovementStore
    {
        CreateMovementResponse Create(
            NormalizedMovementRequest request,
            CancellationToken cancellationToken);
    }
}