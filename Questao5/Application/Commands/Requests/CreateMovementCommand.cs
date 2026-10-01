using MediatR;
using Questao5.Application.Commands.Responses;

namespace Questao5.Application.Commands.Requests
{
    public sealed record CreateMovementCommand(
        string RequestId,
        string AccountId,
        decimal Amount,
        string MovementType) : IRequest<CreateMovementResponse>;
}