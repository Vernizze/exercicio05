using MediatR;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Commands.Responses;

namespace Questao5.Tests.Infrastructure.Services;

internal sealed class ControlledMovementMediator : IMediator
{
    private readonly Func<CreateMovementCommand, CancellationToken, Task<CreateMovementResponse>> handler;

    public ControlledMovementMediator(
        Func<CreateMovementCommand, CancellationToken, Task<CreateMovementResponse>> handler)
    {
        this.handler = handler;
    }

    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        if (request is CreateMovementCommand movementCommand &&
            typeof(TResponse) == typeof(CreateMovementResponse))
        {
            var response = await handler(movementCommand, cancellationToken).ConfigureAwait(false);
            return (TResponse)(object)response;
        }

        throw new NotSupportedException("O mediador controlado aceita somente CreateMovementCommand.");
    }

    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (request is CreateMovementCommand movementCommand)
        {
            return await handler(movementCommand, cancellationToken).ConfigureAwait(false);
        }

        throw new NotSupportedException("O mediador controlado aceita somente CreateMovementCommand.");
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task Publish<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public IAsyncEnumerable<object?> CreateStream(
        object request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}