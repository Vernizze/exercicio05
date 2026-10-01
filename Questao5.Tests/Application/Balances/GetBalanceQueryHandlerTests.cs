using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Questao5.Application.Balances;
using Questao5.Application.Exceptions;
using Questao5.Application.Handlers;
using Questao5.Application.Queries.Requests;

namespace Questao5.Tests.Application.Balances;

public sealed class GetBalanceQueryHandlerTests
{
    private const string AccountHolderId = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string AccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 1, 23, 59, 58, TimeSpan.Zero);

    private readonly IBalanceQueryStore store = Substitute.For<IBalanceQueryStore>();
    private readonly TimeProvider timeProvider = Substitute.For<TimeProvider>();

    public GetBalanceQueryHandlerTests()
    {
        timeProvider.GetUtcNow().Returns(FixedUtcNow);
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsStoredBalanceWithQueryInstant()
    {
        store.GetBalance(AccountHolderId, AccountId, Arg.Any<CancellationToken>())
            .Returns(new AccountBalance(456, "Eva Woodward", 115.25m));
        var handler = CreateHandler();

        var response = await handler.Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken);

        Assert.Equal(456, response.AccountNumber);
        Assert.Equal("Eva Woodward", response.HolderName);
        Assert.Equal(115.25m, response.Balance);
        Assert.Equal(FixedUtcNow, response.QueriedAt);
    }

    [Fact]
    public async Task Handle_ValidQuery_NormalizesIdentifiersBeforeReading()
    {
        store.GetBalance(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AccountBalance(456, "Eva Woodward", 0.00m));
        var handler = CreateHandler();

        await handler.Handle(
            new GetBalanceQuery(AccountHolderId.ToUpperInvariant(), $"  {AccountId}  "),
            TestContext.Current.CancellationToken);

        store.Received(1).GetBalance(AccountHolderId, AccountId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_ValidQuery_ObtainsInstantOnlyAfterTheReadCompletes()
    {
        store.GetBalance(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AccountBalance(456, "Eva Woodward", 0.00m));
        var handler = CreateHandler();

        await handler.Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            store.GetBalance(AccountHolderId, AccountId, Arg.Any<CancellationToken>());
            timeProvider.GetUtcNow();
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("eva.woodward")]
    [InlineData("04b276dc0f454efcbffc911110198733")]
    public async Task Handle_InvalidAccountHolder_ThrowsArgumentExceptionWithoutReading(string accountHolderId)
    {
        var handler = CreateHandler();

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new GetBalanceQuery(accountHolderId, AccountId),
            TestContext.Current.CancellationToken));

        store.DidNotReceive().GetBalance(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Handle_StructurallyInvalidAccount_ThrowsInvalidAccountWithoutReading(string accountId)
    {
        var handler = CreateHandler();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new GetBalanceQuery(AccountHolderId, accountId),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
        store.DidNotReceive().GetBalance(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StoreRejection_PropagatesWithoutObtainingInstant()
    {
        store.GetBalance(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(AccountRuleViolations.OwnershipDenied());
        var handler = CreateHandler();

        await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => handler.Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken));

        timeProvider.DidNotReceive().GetUtcNow();
    }

    private GetBalanceQueryHandler CreateHandler()
    {
        return new GetBalanceQueryHandler(store, timeProvider);
    }
}
