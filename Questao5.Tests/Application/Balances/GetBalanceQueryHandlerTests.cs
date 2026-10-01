using NSubstitute;
using Questao5.Application.Exceptions;
using Questao5.Application.Handlers;
using Questao5.Application.Queries.Requests;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Tests.Application.Balances;

public sealed class GetBalanceQueryHandlerTests
{
    private const string AccountHolderId = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string OtherAccountHolderId = "06dc3a47-fb77-4589-9e18-076f3860d1d2";
    private const string AccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 1, 23, 59, 58, TimeSpan.Zero);

    private readonly IUnitOfWorkFactory unitOfWorkFactory = Substitute.For<IUnitOfWorkFactory>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider timeProvider = Substitute.For<TimeProvider>();

    public GetBalanceQueryHandlerTests()
    {
        unitOfWorkFactory.BeginRead().Returns(unitOfWork);
        timeProvider.GetUtcNow().Returns(FixedUtcNow);
    }

    [Fact]
    public async Task Handle_OwnActiveAccount_ReturnsAccountDataBalanceAndQueryInstant()
    {
        ArrangeAccount(balanceCents: 11525);

        var response = await CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken);

        Assert.Equal(456, response.AccountNumber);
        Assert.Equal("Eva Woodward", response.HolderName);
        Assert.Equal(115.25m, response.Balance);
        Assert.Equal(FixedUtcNow, response.QueriedAt);
    }

    [Fact]
    public async Task Handle_Query_UsesAReadUnitOfWorkAndNeverWrites()
    {
        ArrangeAccount();

        await CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken);

        unitOfWorkFactory.Received(1).BeginRead();
        unitOfWorkFactory.DidNotReceive().BeginWrite();
        unitOfWork.DidNotReceive().Commit();
        unitOfWork.Received(1).Dispose();
        unitOfWork.MovimentoCommand.DidNotReceive().Inserir(Arg.Any<Movimento>());
        unitOfWork.SaldoContaCommand.DidNotReceive().Atualizar(Arg.Any<SaldoConta>());
    }

    [Fact]
    public async Task Handle_Query_NormalizesIdentifiersBeforeReading()
    {
        ArrangeAccount();

        var response = await CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId.ToUpperInvariant(), $"  {AccountId}  "),
            TestContext.Current.CancellationToken);

        Assert.Equal(456, response.AccountNumber);
        unitOfWork.ContaCorrenteQuery.Received(1).ObterPorId(AccountId);
    }

    [Fact]
    public async Task Handle_Query_ObtainsInstantOnlyAfterTheReadIsFinished()
    {
        ArrangeAccount();

        await CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            unitOfWork.ContaCorrenteQuery.ObterPorId(AccountId);
            unitOfWork.TitularidadeContaQuery.ObterPorConta(AccountId);
            unitOfWork.SaldoContaQuery.ObterPorConta(AccountId);
            unitOfWork.Dispose();
            timeProvider.GetUtcNow();
        });
    }

    [Fact]
    public async Task Handle_AccountWithoutMovements_ReturnsZeroWithTwoDecimalPlaces()
    {
        ArrangeAccount(balanceCents: 0);

        var response = await CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken);

        Assert.Equal(0m, response.Balance);
        Assert.Equal("0.00", response.Balance.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("")]
    [InlineData("eva.woodward")]
    [InlineData("04b276dc0f454efcbffc911110198733")]
    public async Task Handle_InvalidAccountHolder_ThrowsArgumentExceptionWithoutOpeningTheDatabase(
        string accountHolderId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateHandler().Handle(
            new GetBalanceQuery(accountHolderId, AccountId),
            TestContext.Current.CancellationToken));

        unitOfWorkFactory.DidNotReceive().BeginRead();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Handle_StructurallyInvalidAccount_ThrowsInvalidAccountWithoutOpeningTheDatabase(
        string accountId)
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, accountId),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
        unitOfWorkFactory.DidNotReceive().BeginRead();
    }

    [Fact]
    public async Task Handle_MissingAccount_ThrowsInvalidAccountWithoutCheckingOwnership()
    {
        unitOfWork.ContaCorrenteQuery.ObterPorId(AccountId).Returns((ContaCorrente?)null);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
        Assert.IsNotType<AccountOwnershipDeniedException>(exception);
        unitOfWork.TitularidadeContaQuery.DidNotReceive().ObterPorConta(Arg.Any<string>());
        unitOfWork.Received(1).Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_AccountOfAnotherHolder_ThrowsOwnershipDeniedEvenWhenInactive(bool active)
    {
        ArrangeAccount(active: active, accountHolderId: OtherAccountHolderId);

        var exception = await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
        unitOfWork.SaldoContaQuery.DidNotReceive().ObterPorConta(Arg.Any<string>());
        timeProvider.DidNotReceive().GetUtcNow();
    }

    [Fact]
    public async Task Handle_AccountWithoutHolder_ThrowsOwnershipDenied()
    {
        ArrangeAccount();
        unitOfWork.TitularidadeContaQuery.ObterPorConta(AccountId).Returns((TitularidadeConta?)null);

        await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_OwnInactiveAccount_ThrowsInactiveAccount()
    {
        ArrangeAccount(active: false);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken));

        Assert.Equal("INACTIVE_ACCOUNT", exception.Code);
        unitOfWork.SaldoContaQuery.DidNotReceive().ObterPorConta(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_MissingConsolidatedBalance_FailsInsteadOfReturningZero()
    {
        ArrangeAccount();
        unitOfWork.SaldoContaQuery.ObterPorConta(AccountId).Returns((SaldoConta?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            TestContext.Current.CancellationToken));

        unitOfWork.Received(1).Dispose();
    }

    [Fact]
    public async Task Handle_CancelledRequest_ThrowsWithoutOpeningTheDatabase()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateHandler().Handle(
            new GetBalanceQuery(AccountHolderId, AccountId),
            cancellationTokenSource.Token));

        unitOfWorkFactory.DidNotReceive().BeginRead();
    }

    private void ArrangeAccount(
        bool active = true,
        string accountHolderId = AccountHolderId,
        long balanceCents = 0)
    {
        unitOfWork.ContaCorrenteQuery.ObterPorId(AccountId)
            .Returns(new ContaCorrente(AccountId, 456, "Eva Woodward", active));
        unitOfWork.TitularidadeContaQuery.ObterPorConta(AccountId)
            .Returns(new TitularidadeConta(AccountId, accountHolderId));
        unitOfWork.SaldoContaQuery.ObterPorConta(AccountId)
            .Returns(new SaldoConta(AccountId, balanceCents, versao: 0));
    }

    private GetBalanceQueryHandler CreateHandler()
    {
        return new GetBalanceQueryHandler(unitOfWorkFactory, timeProvider);
    }
}
