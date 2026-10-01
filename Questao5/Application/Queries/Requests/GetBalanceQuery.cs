using MediatR;
using Questao5.Application.Queries.Responses;

namespace Questao5.Application.Queries.Requests
{
    public sealed record GetBalanceQuery(
        string AccountHolderId,
        string AccountId) : IRequest<GetBalanceResponse>;
}
