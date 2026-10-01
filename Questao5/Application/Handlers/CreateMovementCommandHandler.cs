using MediatR;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Commands.Responses;
using Questao5.Application.Movements;

namespace Questao5.Application.Handlers
{
    public sealed class CreateMovementCommandHandler : IRequestHandler<CreateMovementCommand, CreateMovementResponse>
    {
        private readonly IMovementStore movementStore;

        public CreateMovementCommandHandler(IMovementStore movementStore)
        {
            this.movementStore = movementStore;
        }

        public Task<CreateMovementResponse> Handle(
            CreateMovementCommand request,
            CancellationToken cancellationToken)
        {
            var normalizedRequest = MovementRequestNormalizer.Normalize(request);
            var response = movementStore.Create(normalizedRequest, cancellationToken);
            return Task.FromResult(response);
        }
    }
}