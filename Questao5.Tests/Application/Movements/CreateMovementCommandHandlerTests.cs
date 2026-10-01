using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Commands.Responses;
using Questao5.Application.Exceptions;
using Questao5.Application.Handlers;
using Questao5.Application.Movements;

namespace Questao5.Tests.Application.Movements;

public sealed class CreateMovementCommandHandlerTests
{
    private const string RequestId = "d743abe7-40ed-4a7d-b0a5-8f876e916a07";
    private const string AccountHolderId = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string AccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string MovementId = "34e56aa7-703f-46d8-8f65-100f2795a87b";

    private readonly IMovementStore store = Substitute.For<IMovementStore>();

    [Fact]
    public async Task Handle_ValidCommand_SendsNormalizedRequestToTheStore()
    {
        NormalizedMovementRequest? received = null;
        store.Create(Arg.Do<NormalizedMovementRequest>(request => received = request), Arg.Any<CancellationToken>())
            .Returns(new CreateMovementResponse(MovementId, IsReplay: false));
        var handler = new CreateMovementCommandHandler(store);

        await handler.Handle(
            new CreateMovementCommand(
                $" {RequestId.ToUpperInvariant()} ",
                AccountHolderId.ToUpperInvariant(),
                $" {AccountId.ToLowerInvariant()} ",
                125.5m,
                "C"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(received);
        Assert.Equal(RequestId, received.RequestId);
        Assert.Equal(AccountHolderId, received.AccountHolderId);
        Assert.Equal(AccountId, received.AccountId);
        Assert.Equal(125.5m, received.Amount);
        Assert.Equal('C', received.MovementType);
        Assert.Equal(
            $"v2|titular={AccountHolderId}|conta={AccountId}|valor=125.50|tipo=C",
            received.CanonicalRequest);
        store.Received(1).Create(Arg.Any<NormalizedMovementRequest>(), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ValidCommand_ReturnsTheStoreResponseUnchanged(bool isReplay)
    {
        var storeResponse = new CreateMovementResponse(MovementId, isReplay);
        store.Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>())
            .Returns(storeResponse);
        var handler = new CreateMovementCommandHandler(store);

        var response = await handler.Handle(CreateCommand(), TestContext.Current.CancellationToken);

        Assert.Same(storeResponse, response);
        Assert.Equal(MovementId, response.MovementId);
        Assert.Equal(isReplay, response.IsReplay);
    }

    [Theory]
    [InlineData(0, "C", "INVALID_VALUE")]
    [InlineData(-10.25, "C", "INVALID_VALUE")]
    [InlineData(1.001, "D", "INVALID_VALUE")]
    [InlineData(10.25, "X", "INVALID_TYPE")]
    [InlineData(10.25, "", "INVALID_TYPE")]
    [InlineData(0, "X", "INVALID_VALUE")]
    public async Task Handle_InvalidValueOrType_ThrowsBusinessRuleWithoutCallingTheStore(
        decimal amount,
        string movementType,
        string expectedCode)
    {
        var handler = new CreateMovementCommandHandler(store);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            CreateCommand(amount: amount, movementType: movementType),
            TestContext.Current.CancellationToken));

        Assert.Equal(expectedCode, exception.Code);
        store.DidNotReceive().Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidRequestId_ThrowsBusinessRuleWithoutCallingTheStore()
    {
        var handler = new CreateMovementCommandHandler(store);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            CreateCommand(requestId: "not-a-uuid"),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_REQUEST_ID", exception.Code);
        store.DidNotReceive().Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidAccountHolder_ThrowsArgumentExceptionWithoutCallingTheStore()
    {
        var handler = new CreateMovementCommandHandler(store);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            CreateCommand(accountHolderId: "eva.woodward"),
            TestContext.Current.CancellationToken));

        store.DidNotReceive().Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StoreBusinessRejection_PropagatesTheSameException()
    {
        var rejection = AccountRuleViolations.InactiveAccount();
        store.Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>())
            .Throws(rejection);
        var handler = new CreateMovementCommandHandler(store);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        Assert.Same(rejection, exception);
    }

    [Fact]
    public async Task Handle_StoreOwnershipDenial_PropagatesTheSpecificExceptionType()
    {
        store.Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>())
            .Throws(AccountRuleViolations.OwnershipDenied());
        var handler = new CreateMovementCommandHandler(store);

        var exception = await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => handler.Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
    }

    [Fact]
    public async Task Handle_StoreIdempotencyConflict_PropagatesTheConflict()
    {
        store.Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>())
            .Throws(new IdempotencyConflictException());
        var handler = new CreateMovementCommandHandler(store);

        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(() => handler.Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        Assert.Equal(IdempotencyConflictException.ErrorCode, exception.Code);
    }

    [Fact]
    public async Task Handle_CancellationToken_IsForwardedToTheStore()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        store.Create(Arg.Any<NormalizedMovementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateMovementResponse(MovementId, IsReplay: false));
        var handler = new CreateMovementCommandHandler(store);

        await handler.Handle(CreateCommand(), cancellationTokenSource.Token);

        store.Received(1).Create(Arg.Any<NormalizedMovementRequest>(), cancellationTokenSource.Token);
    }

    private static CreateMovementCommand CreateCommand(
        string requestId = RequestId,
        string accountHolderId = AccountHolderId,
        decimal amount = 10.25m,
        string movementType = "C")
    {
        return new CreateMovementCommand(requestId, accountHolderId, AccountId, amount, movementType);
    }
}
