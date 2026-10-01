using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Questao5.Application.Queries.Requests;
using Questao5.Application.Queries.Responses;

namespace Questao5.Tests.Infrastructure.Services;

/// <summary>
/// Substitui o handler da consulta de saldo para exercitar o pipeline HTTP (timeout, concorrência, erro interno)
/// sem depender do banco.
/// </summary>
internal sealed class ControlledBalanceHandler : IRequestHandler<GetBalanceQuery, GetBalanceResponse>
{
    private readonly Func<GetBalanceQuery, CancellationToken, GetBalanceResponse> handle;

    public ControlledBalanceHandler(Func<GetBalanceQuery, CancellationToken, GetBalanceResponse> handle)
    {
        this.handle = handle;
    }

    public static void Register(
        IServiceCollection services,
        Func<GetBalanceQuery, CancellationToken, GetBalanceResponse> handle)
    {
        services.RemoveAll<IRequestHandler<GetBalanceQuery, GetBalanceResponse>>();
        services.AddSingleton<IRequestHandler<GetBalanceQuery, GetBalanceResponse>>(
            new ControlledBalanceHandler(handle));
    }

    public Task<GetBalanceResponse> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(handle(request, cancellationToken));
    }
}
