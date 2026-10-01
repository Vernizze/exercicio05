using Dapper;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using Questao5.Application.Handlers;
using Questao5.Application.Movements;
using Questao5.Infrastructure.Database.CommandStore;
using Questao5.Infrastructure.Database.QueryStore;
using Questao5.Infrastructure.Sqlite;
using Questao5.Tests.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Database;

public sealed class BalanceQueryStoreTests : IDisposable
{
    private const string ActiveAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string ActiveAccountHolderId = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string InactiveAccountId = "F475F943-7067-ED11-A06B-7E5DFA4A16C9";
    private const string InactiveAccountHolderId = "cf18e8e5-35f2-498d-a77d-4dd6828316d4";
    private const string OtherAccountHolderId = "06dc3a47-fb77-4589-9e18-076f3860d1d2";

    private readonly TemporarySqliteDatabase database = new();
    private readonly SqliteConnectionFactory connectionFactory;
    private readonly BalanceQueryStore store;

    public BalanceQueryStoreTests()
    {
        connectionFactory = new SqliteConnectionFactory(new DatabaseConfig(database.ConnectionString));
        new DatabaseBootstrap(connectionFactory).Setup();
        store = new BalanceQueryStore(connectionFactory);
    }

    [Fact]
    public void GetBalance_AccountWithoutMovements_ReturnsZeroWithTwoDecimalPlaces()
    {
        var balance = store.GetBalance(ActiveAccountHolderId, ActiveAccountId, TestContext.Current.CancellationToken);

        Assert.Equal(456, balance.AccountNumber);
        Assert.Equal("Eva Woodward", balance.HolderName);
        Assert.Equal(0m, balance.Balance);
        Assert.Equal(2, balance.Balance.Scale);
    }

    [Theory]
    [InlineData(11525L, "115.25")]
    [InlineData(1L, "0.01")]
    [InlineData(10L, "0.10")]
    [InlineData(1050L, "10.50")]
    [InlineData(-2575L, "-25.75")]
    [InlineData(999999999999L, "9999999999.99")]
    [InlineData(9223372036854775807L, "92233720368547758.07")]
    public void GetBalance_PersistedCents_AreConvertedToDecimalWithoutFloatingPoint(
        long persistedCents,
        string expectedBalance)
    {
        SetProjection(ActiveAccountId, persistedCents);

        var balance = store.GetBalance(ActiveAccountHolderId, ActiveAccountId, TestContext.Current.CancellationToken);

        Assert.Equal(
            decimal.Parse(expectedBalance, System.Globalization.CultureInfo.InvariantCulture),
            balance.Balance);
        Assert.Equal(
            expectedBalance,
            balance.Balance.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GetBalance_AfterMovements_ReturnsCreditsMinusDebits()
    {
        var movementStore = new MovementStore(connectionFactory, new GuidGenerator(), TimeProvider.System);
        var handler = new CreateMovementCommandHandler(movementStore);

        foreach (var (amount, movementType) in new[] { (100.10m, "C"), (0.20m, "C"), (50.05m, "D") })
        {
            await handler.Handle(
                new CreateMovementCommand(
                    Guid.NewGuid().ToString("D"),
                    ActiveAccountHolderId,
                    ActiveAccountId,
                    amount,
                    movementType),
                TestContext.Current.CancellationToken);
        }

        var balance = store.GetBalance(ActiveAccountHolderId, ActiveAccountId, TestContext.Current.CancellationToken);

        Assert.Equal(50.25m, balance.Balance);
    }

    [Fact]
    public async Task GetBalance_ManySmallMovements_AccumulatesExactlyWithoutFloatingPointDrift()
    {
        // 0,10 não tem representação binária exata: somado 200 vezes em double não resulta em 20,00.
        const int movementCount = 200;
        var handler = new CreateMovementCommandHandler(
            new MovementStore(connectionFactory, new GuidGenerator(), TimeProvider.System));

        for (var index = 0; index < movementCount; index++)
        {
            await handler.Handle(
                new CreateMovementCommand(
                    Guid.NewGuid().ToString("D"),
                    ActiveAccountHolderId,
                    ActiveAccountId,
                    0.10m,
                    "C"),
                TestContext.Current.CancellationToken);
        }

        var balance = store.GetBalance(ActiveAccountHolderId, ActiveAccountId, TestContext.Current.CancellationToken);

        Assert.Equal(20.00m, balance.Balance);
        Assert.True(new BalanceReconciler(connectionFactory).Reconcile().IsConsistent);
    }

    [Fact]
    public async Task GetBalance_CreditExactlyCancelledByDebit_ReturnsZeroWithTwoDecimalPlaces()
    {
        var handler = new CreateMovementCommandHandler(
            new MovementStore(connectionFactory, new GuidGenerator(), TimeProvider.System));

        foreach (var movementType in new[] { "C", "D" })
        {
            await handler.Handle(
                new CreateMovementCommand(
                    Guid.NewGuid().ToString("D"),
                    ActiveAccountHolderId,
                    ActiveAccountId,
                    9999999999.99m,
                    movementType),
                TestContext.Current.CancellationToken);
        }

        var balance = store.GetBalance(ActiveAccountHolderId, ActiveAccountId, TestContext.Current.CancellationToken);

        Assert.Equal(0m, balance.Balance);
        Assert.Equal("0.00", balance.Balance.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void GetBalance_AccountIdentifierInDifferentCase_FindsPersistedAccount()
    {
        var balance = store.GetBalance(
            ActiveAccountHolderId.ToUpperInvariant(),
            ActiveAccountId.ToLowerInvariant(),
            TestContext.Current.CancellationToken);

        Assert.Equal(456, balance.AccountNumber);
    }

    [Fact]
    public void GetBalance_MissingAccount_ThrowsInvalidAccount()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => store.GetBalance(
            ActiveAccountHolderId,
            "missing-account",
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
    }

    [Theory]
    [InlineData(ActiveAccountId)]
    [InlineData(InactiveAccountId)]
    public void GetBalance_AccountOfAnotherHolder_ThrowsOwnershipDeniedIdenticalToMissingAccount(string accountId)
    {
        var missing = Assert.Throws<BusinessRuleException>(() => store.GetBalance(
            OtherAccountHolderId,
            "missing-account",
            TestContext.Current.CancellationToken));
        var denied = Assert.Throws<AccountOwnershipDeniedException>(() => store.GetBalance(
            OtherAccountHolderId,
            accountId,
            TestContext.Current.CancellationToken));

        Assert.Equal(missing.Code, denied.Code);
        Assert.Equal(missing.Message, denied.Message);
    }

    [Fact]
    public void GetBalance_AccountWithoutHolder_ThrowsOwnershipDenied()
    {
        Execute("DELETE FROM titularidade_conta WHERE idcontacorrente = @AccountId;", ActiveAccountId);

        Assert.Throws<AccountOwnershipDeniedException>(() => store.GetBalance(
            ActiveAccountHolderId,
            ActiveAccountId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetBalance_OwnInactiveAccount_ThrowsInactiveAccount()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => store.GetBalance(
            InactiveAccountHolderId,
            InactiveAccountId,
            TestContext.Current.CancellationToken));

        Assert.Equal("INACTIVE_ACCOUNT", exception.Code);
        Assert.IsNotType<AccountOwnershipDeniedException>(exception);
    }

    [Fact]
    public void GetBalance_MissingProjectionRow_FailsInsteadOfReturningZero()
    {
        Execute("DELETE FROM saldo_conta WHERE idcontacorrente = @AccountId;", ActiveAccountId);

        Assert.Throws<InvalidOperationException>(() => store.GetBalance(
            ActiveAccountHolderId,
            ActiveAccountId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetBalance_NonIntegerPersistedBalance_FailsInsteadOfRounding()
    {
        Execute("UPDATE saldo_conta SET saldo_centavos = 10.5 WHERE idcontacorrente = @AccountId;", ActiveAccountId);

        Assert.Throws<InvalidOperationException>(() => store.GetBalance(
            ActiveAccountHolderId,
            ActiveAccountId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetBalance_CancelledRequest_ThrowsBeforeReading()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        Assert.Throws<OperationCanceledException>(() => store.GetBalance(
            ActiveAccountHolderId,
            ActiveAccountId,
            cancellationTokenSource.Token));
    }

    [Fact]
    public void GetBalance_DoesNotChangePersistedState()
    {
        SetProjection(ActiveAccountId, 11525);
        var before = Snapshot();

        store.GetBalance(ActiveAccountHolderId, ActiveAccountId, TestContext.Current.CancellationToken);
        Assert.Throws<AccountOwnershipDeniedException>(() => store.GetBalance(
            OtherAccountHolderId,
            ActiveAccountId,
            TestContext.Current.CancellationToken));

        Assert.Equal(before, Snapshot());
    }

    public void Dispose()
    {
        database.Dispose();
    }

    private void SetProjection(string accountId, long balanceCents)
    {
        using var connection = connectionFactory.OpenConnection();
        connection.Execute(
            "UPDATE saldo_conta SET saldo_centavos = @BalanceCents WHERE idcontacorrente = @AccountId;",
            new { AccountId = accountId, BalanceCents = balanceCents });
    }

    private void Execute(string sql, string accountId)
    {
        using var connection = connectionFactory.OpenConnection();
        connection.Execute(sql, new { AccountId = accountId });
    }

    private string Snapshot()
    {
        using var connection = connectionFactory.OpenConnection();
        var counts = connection.QuerySingle<(long Accounts, long Movements, long Keys, long Holders)>(
            """
            SELECT (SELECT COUNT(*) FROM contacorrente),
                   (SELECT COUNT(*) FROM movimento),
                   (SELECT COUNT(*) FROM idempotencia),
                   (SELECT COUNT(*) FROM titularidade_conta);
            """);
        var projection = connection.Query<(string AccountId, long BalanceCents, long Version)>(
            "SELECT idcontacorrente, saldo_centavos, versao FROM saldo_conta ORDER BY idcontacorrente;");

        return $"{counts}|{string.Join(';', projection)}";
    }

    private sealed class GuidGenerator : IMovementIdGenerator
    {
        public string Create()
        {
            return Guid.NewGuid().ToString("D");
        }
    }
}
