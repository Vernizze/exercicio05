using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using Questao5.Application.Handlers;
using Questao5.Application.Movements;
using Questao5.Domain.Entities;
using Questao5.Domain.Enumerators;
using Questao5.Domain.Repositories;

namespace Questao5.Tests.Application.Movements;

public sealed class CreateMovementCommandHandlerTests
{
    private const string RequestId = "d743abe7-40ed-4a7d-b0a5-8f876e916a07";
    private const string AccountHolderId = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string OtherAccountHolderId = "06dc3a47-fb77-4589-9e18-076f3860d1d2";
    private const string AccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string MovementId = "34e56aa7-703f-46d8-8f65-100f2795a87b";
    private const string CanonicalRequest =
        $"v2|titular={AccountHolderId}|conta={AccountId}|valor=125.50|tipo=C";

    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 1, 23, 59, 58, TimeSpan.Zero);

    private readonly IUnitOfWorkFactory unitOfWorkFactory = Substitute.For<IUnitOfWorkFactory>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMovementIdGenerator movementIdGenerator = Substitute.For<IMovementIdGenerator>();
    private readonly TimeProvider timeProvider = Substitute.For<TimeProvider>();

    public CreateMovementCommandHandlerTests()
    {
        unitOfWorkFactory.BeginWrite().Returns(unitOfWork);
        movementIdGenerator.Create().Returns(MovementId);
        timeProvider.GetUtcNow().Returns(FixedUtcNow);
        unitOfWork.IdempotenciaQuery.ObterPorChave(Arg.Any<string>()).Returns((Idempotencia?)null);
        ArrangeAccount();
    }

    [Fact]
    public async Task Handle_ValidCredit_PersistsMovementBalanceAndIdempotencyThenCommits()
    {
        Movimento? insertedMovement = null;
        SaldoConta? updatedBalance = null;
        Idempotencia? insertedIdempotency = null;
        unitOfWork.MovimentoCommand.Inserir(Arg.Do<Movimento>(movimento => insertedMovement = movimento));
        unitOfWork.SaldoContaCommand.Atualizar(Arg.Do<SaldoConta>(saldo => updatedBalance = saldo));
        unitOfWork.IdempotenciaCommand.Inserir(Arg.Do<Idempotencia>(registro => insertedIdempotency = registro));

        var response = await CreateHandler().Handle(CreateCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(MovementId, response.MovementId);
        Assert.False(response.IsReplay);

        Assert.NotNull(insertedMovement);
        Assert.Equal(MovementId, insertedMovement.IdMovimento);
        Assert.Equal(AccountId, insertedMovement.IdContaCorrente);
        Assert.Equal("01/10/2026", insertedMovement.DataMovimento);
        Assert.Equal(TipoMovimento.Credito, insertedMovement.TipoMovimento);
        Assert.Equal(125.50m, insertedMovement.Valor);

        Assert.NotNull(updatedBalance);
        Assert.Equal(12550, updatedBalance.SaldoCentavos);
        Assert.Equal(1, updatedBalance.Versao);

        Assert.NotNull(insertedIdempotency);
        Assert.Equal(RequestId, insertedIdempotency.ChaveIdempotencia);
        Assert.Equal(CanonicalRequest, insertedIdempotency.Requisicao);
        Assert.Equal($"v1|idMovimento={MovementId}", insertedIdempotency.Resultado);

        unitOfWork.Received(1).Commit();
        unitOfWork.Received(1).Dispose();
    }

    [Fact]
    public async Task Handle_ValidMovement_RunsEveryStepInOrderInsideOneWriteUnitOfWork()
    {
        await CreateHandler().Handle(CreateCommand(), TestContext.Current.CancellationToken);

        unitOfWorkFactory.Received(1).BeginWrite();
        unitOfWorkFactory.DidNotReceive().BeginRead();
        Received.InOrder(() =>
        {
            unitOfWork.IdempotenciaQuery.ObterPorChave(RequestId);
            unitOfWork.ContaCorrenteQuery.ObterPorId(AccountId);
            unitOfWork.TitularidadeContaQuery.ObterPorConta(AccountId);
            unitOfWork.MovimentoCommand.Inserir(Arg.Any<Movimento>());
            unitOfWork.SaldoContaQuery.ObterPorConta(AccountId);
            unitOfWork.SaldoContaCommand.Atualizar(Arg.Any<SaldoConta>());
            unitOfWork.IdempotenciaCommand.Inserir(Arg.Any<Idempotencia>());
            unitOfWork.Commit();
            unitOfWork.Dispose();
        });
    }

    [Fact]
    public async Task Handle_Debit_SubtractsFromTheConsolidatedBalance()
    {
        SaldoConta? updatedBalance = null;
        unitOfWork.SaldoContaQuery.ObterPorConta(AccountId).Returns(new SaldoConta(AccountId, 10000, 4));
        unitOfWork.SaldoContaCommand.Atualizar(Arg.Do<SaldoConta>(saldo => updatedBalance = saldo));

        await CreateHandler().Handle(
            CreateCommand(amount: 25.25m, movementType: "D"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(updatedBalance);
        Assert.Equal(7475, updatedBalance.SaldoCentavos);
        Assert.Equal(5, updatedBalance.Versao);
    }

    [Fact]
    public async Task Handle_IdentifiersInOtherForms_AreNormalizedAndThePersistedAccountIdIsUsed()
    {
        Movimento? insertedMovement = null;
        unitOfWork.MovimentoCommand.Inserir(Arg.Do<Movimento>(movimento => insertedMovement = movimento));

        await CreateHandler().Handle(
            new CreateMovementCommand(
                $" {RequestId.ToUpperInvariant()} ",
                AccountHolderId.ToUpperInvariant(),
                $" {AccountId.ToLowerInvariant()} ",
                125.5m,
                "C"),
            TestContext.Current.CancellationToken);

        unitOfWork.IdempotenciaQuery.Received(1).ObterPorChave(RequestId);
        unitOfWork.ContaCorrenteQuery.Received(1).ObterPorId(AccountId);
        Assert.NotNull(insertedMovement);
        Assert.Equal(AccountId, insertedMovement.IdContaCorrente);
    }

    [Fact]
    public async Task Handle_RepeatedIdenticalRequest_ReturnsOriginalResultWithoutReadingTheAccountOrWriting()
    {
        unitOfWork.IdempotenciaQuery.ObterPorChave(RequestId)
            .Returns(Idempotencia.Registrar(RequestId, CanonicalRequest, MovementId));

        var response = await CreateHandler().Handle(CreateCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(MovementId, response.MovementId);
        Assert.True(response.IsReplay);
        unitOfWork.ContaCorrenteQuery.DidNotReceive().ObterPorId(Arg.Any<string>());
        AssertNothingWritten();
    }

    [Theory]
    [InlineData(AccountHolderId, 10.00, "C")]
    [InlineData(AccountHolderId, 125.50, "D")]
    [InlineData(OtherAccountHolderId, 125.50, "C")]
    public async Task Handle_SameKeyWithDifferentRequestOrHolder_ThrowsConflictWithoutWriting(
        string accountHolderId,
        decimal amount,
        string movementType)
    {
        unitOfWork.IdempotenciaQuery.ObterPorChave(RequestId)
            .Returns(Idempotencia.Registrar(RequestId, CanonicalRequest, MovementId));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => CreateHandler().Handle(
            CreateCommand(accountHolderId: accountHolderId, amount: amount, movementType: movementType),
            TestContext.Current.CancellationToken));

        AssertNothingWritten();
    }

    [Theory]
    [InlineData(0, "C", "INVALID_VALUE")]
    [InlineData(-10.25, "C", "INVALID_VALUE")]
    [InlineData(1.001, "D", "INVALID_VALUE")]
    [InlineData(10.25, "X", "INVALID_TYPE")]
    [InlineData(10.25, "", "INVALID_TYPE")]
    [InlineData(0, "X", "INVALID_VALUE")]
    public async Task Handle_InvalidValueOrType_ThrowsBusinessRuleWithoutOpeningTheDatabase(
        decimal amount,
        string movementType,
        string expectedCode)
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(
            CreateCommand(amount: amount, movementType: movementType),
            TestContext.Current.CancellationToken));

        Assert.Equal(expectedCode, exception.Code);
        unitOfWorkFactory.DidNotReceive().BeginWrite();
    }

    [Fact]
    public async Task Handle_InvalidRequestId_ThrowsBusinessRuleWithoutOpeningTheDatabase()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(
            CreateCommand(requestId: "not-a-uuid"),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_REQUEST_ID", exception.Code);
        unitOfWorkFactory.DidNotReceive().BeginWrite();
    }

    [Fact]
    public async Task Handle_InvalidAccountHolder_ThrowsArgumentExceptionWithoutOpeningTheDatabase()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateHandler().Handle(
            CreateCommand(accountHolderId: "eva.woodward"),
            TestContext.Current.CancellationToken));

        unitOfWorkFactory.DidNotReceive().BeginWrite();
    }

    [Fact]
    public async Task Handle_MissingAccount_ThrowsInvalidAccountWithoutWriting()
    {
        unitOfWork.ContaCorrenteQuery.ObterPorId(AccountId).Returns((ContaCorrente?)null);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
        Assert.IsNotType<AccountOwnershipDeniedException>(exception);
        AssertNothingWritten();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_AccountOfAnotherHolder_ThrowsOwnershipDeniedEvenWhenInactive(bool active)
    {
        ArrangeAccount(active: active, accountHolderId: OtherAccountHolderId);

        var exception = await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => CreateHandler().Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
        AssertNothingWritten();
    }

    [Fact]
    public async Task Handle_OwnInactiveAccount_ThrowsInactiveAccountWithoutWriting()
    {
        ArrangeAccount(active: false);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateHandler().Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        Assert.Equal("INACTIVE_ACCOUNT", exception.Code);
        AssertNothingWritten();
    }

    [Fact]
    public async Task Handle_MissingConsolidatedBalance_FailsWithoutCommitting()
    {
        unitOfWork.SaldoContaQuery.ObterPorConta(AccountId).Returns((SaldoConta?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        unitOfWork.IdempotenciaCommand.DidNotReceive().Inserir(Arg.Any<Idempotencia>());
        unitOfWork.DidNotReceive().Commit();
        unitOfWork.Received(1).Dispose();
    }

    [Fact]
    public async Task Handle_FailureWhileRecordingIdempotency_PropagatesWithoutCommitting()
    {
        var failure = new InvalidOperationException("falha de persistência de teste");
        unitOfWork.IdempotenciaCommand
            .When(store => store.Inserir(Arg.Any<Idempotencia>()))
            .Throw(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().Handle(
            CreateCommand(),
            TestContext.Current.CancellationToken));

        Assert.Same(failure, exception);
        unitOfWork.DidNotReceive().Commit();
        unitOfWork.Received(1).Dispose();
    }

    [Fact]
    public async Task Handle_CancelledRequest_ThrowsWithoutOpeningTheDatabase()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateHandler().Handle(
            CreateCommand(),
            cancellationTokenSource.Token));

        unitOfWorkFactory.DidNotReceive().BeginWrite();
    }

    private void ArrangeAccount(bool active = true, string accountHolderId = AccountHolderId)
    {
        unitOfWork.ContaCorrenteQuery.ObterPorId(AccountId)
            .Returns(new ContaCorrente(AccountId, 456, "Eva Woodward", active));
        unitOfWork.TitularidadeContaQuery.ObterPorConta(AccountId)
            .Returns(new TitularidadeConta(AccountId, accountHolderId));
        unitOfWork.SaldoContaQuery.ObterPorConta(AccountId)
            .Returns(_ => new SaldoConta(AccountId, saldoCentavos: 0, versao: 0));
    }

    private void AssertNothingWritten()
    {
        unitOfWork.MovimentoCommand.DidNotReceive().Inserir(Arg.Any<Movimento>());
        unitOfWork.SaldoContaCommand.DidNotReceive().Atualizar(Arg.Any<SaldoConta>());
        unitOfWork.IdempotenciaCommand.DidNotReceive().Inserir(Arg.Any<Idempotencia>());
        unitOfWork.DidNotReceive().Commit();
        unitOfWork.Received(1).Dispose();
    }

    private CreateMovementCommandHandler CreateHandler()
    {
        return new CreateMovementCommandHandler(unitOfWorkFactory, movementIdGenerator, timeProvider);
    }

    private static CreateMovementCommand CreateCommand(
        string requestId = RequestId,
        string accountHolderId = AccountHolderId,
        decimal amount = 125.50m,
        string movementType = "C")
    {
        return new CreateMovementCommand(requestId, accountHolderId, AccountId, amount, movementType);
    }
}
