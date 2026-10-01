using Dapper;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using Questao5.Application.Handlers;
using Questao5.Application.Movements;
using Questao5.Application.Balances;
using Questao5.Infrastructure.Database.CommandStore;
using Questao5.Infrastructure.Database.QueryStore;
using Questao5.Infrastructure.Services.Security;
using Questao5.Infrastructure.Sqlite;
using Questao5.Tests.Infrastructure.Sqlite;
using System.Collections.Concurrent;

namespace Questao5.Tests.Infrastructure.Database;

public sealed class MovementStoreTests
{
    private const string ActiveAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string InactiveAccountId = "F475F943-7067-ED11-A06B-7E5DFA4A16C9";
    private const string ActiveAccountHolderId = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string InactiveAccountHolderId = "cf18e8e5-35f2-498d-a77d-4dd6828316d4";
    private const string OtherAccountHolderId = "06dc3a47-fb77-4589-9e18-076f3860d1d2";
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 1, 23, 59, 58, TimeSpan.Zero);

    [Theory]
    [InlineData("C")]
    [InlineData("D")]
    public async Task Handle_ValidMovement_PersistsMovementAndIdempotencyAtomically(string movementType)
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        var movementId = Guid.NewGuid().ToString("D");
        var handler = context.CreateHandler(movementId);

        var response = await handler.Handle(
            CreateCommand(requestId, ActiveAccountId.ToLowerInvariant(), 125.50m, movementType),
            TestContext.Current.CancellationToken);

        Assert.Equal(movementId, response.MovementId);
        using var connection = context.ConnectionFactory.OpenConnection();
        var movement = connection.QuerySingle<PersistedMovement>(
            """
            SELECT idmovimento AS MovementId,
                   idcontacorrente AS AccountId,
                   datamovimento AS MovementDate,
                   tipomovimento AS MovementType,
                   valor AS Amount
            FROM movimento;
            """);
        var idempotency = connection.QuerySingle<PersistedIdempotency>(
            """
            SELECT chave_idempotencia AS RequestId,
                   requisicao AS Request,
                   resultado AS Result
            FROM idempotencia;
            """);

        Assert.Equal(movementId, movement.MovementId);
        Assert.Equal(ActiveAccountId, movement.AccountId);
        Assert.Equal("01/10/2026", movement.MovementDate);
        Assert.Equal(movementType, movement.MovementType);
        Assert.Equal(125.5, movement.Amount, 10);
        Assert.Equal(requestId, idempotency.RequestId);
        Assert.Equal(
            $"v2|titular={ActiveAccountHolderId}|conta={ActiveAccountId}|valor=125.50|tipo={movementType}",
            idempotency.Request);
        Assert.Equal($"v1|idMovimento={movementId}", idempotency.Result);
    }

    [Fact]
    public async Task Handle_RepeatedIdenticalRequest_ReturnsOriginalMovementWithoutDuplication()
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        var originalMovementId = Guid.NewGuid().ToString("D");
        var handler = context.CreateHandler(originalMovementId, Guid.NewGuid().ToString("D"));
        var command = CreateCommand(requestId, ActiveAccountId, 10.25m, "C");

        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        var repeated = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(originalMovementId, first.MovementId);
        Assert.Equal(originalMovementId, repeated.MovementId);
        Assert.Equal(1, context.CountRows("movimento"));
        Assert.Equal(1, context.CountRows("idempotencia"));
    }

    [Fact]
    public async Task Handle_RepeatedRequestAfterAccountDeactivation_ReturnsOriginalMovement()
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        var originalMovementId = Guid.NewGuid().ToString("D");
        var handler = context.CreateHandler(originalMovementId);
        var command = CreateCommand(requestId, ActiveAccountId, 10.25m, "C");
        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        context.Execute(
            "UPDATE contacorrente SET ativo = 0 WHERE idcontacorrente = @AccountId;",
            new { AccountId = ActiveAccountId });

        var repeated = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(first.MovementId, repeated.MovementId);
        Assert.Equal(1, context.CountRows("movimento"));
    }

    [Fact]
    public async Task Handle_SameKeyWithDifferentPayload_ThrowsConflictWithoutChangingOriginalData()
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        var originalMovementId = Guid.NewGuid().ToString("D");
        var handler = context.CreateHandler(originalMovementId);
        await handler.Handle(
            CreateCommand(requestId, ActiveAccountId, 10.25m, "C"),
            TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<IdempotencyConflictException>(() => handler.Handle(
            CreateCommand(requestId, ActiveAccountId, 10.26m, "C"),
            TestContext.Current.CancellationToken));

        Assert.Equal(IdempotencyConflictException.ErrorCode, exception.Code);
        Assert.Equal(1, context.CountRows("movimento"));
        Assert.Equal(1, context.CountRows("idempotencia"));
    }

    [Theory]
    [InlineData("missing-account", ActiveAccountHolderId, "INVALID_ACCOUNT")]
    [InlineData(InactiveAccountId, InactiveAccountHolderId, "INACTIVE_ACCOUNT")]
    public async Task Handle_InvalidAccountState_ThrowsBusinessRuleAndDoesNotReserveKey(
        string accountId,
        string accountHolderId,
        string expectedCode)
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), accountId, 10.25m, "C", accountHolderId),
            TestContext.Current.CancellationToken));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(0, context.CountRows("movimento"));
        Assert.Equal(0, context.CountRows("idempotencia"));
    }

    [Fact]
    public async Task Handle_IdempotencyInsertFailure_RollsBackMovementAndKey()
    {
        using var context = CreateContext();
        context.Execute(
            """
            CREATE TRIGGER fail_idempotency_insert
            BEFORE INSERT ON idempotencia
            BEGIN
                SELECT RAISE(ABORT, 'forced idempotency failure');
            END;
            """);
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        await Assert.ThrowsAnyAsync<Exception>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10.25m, "D"),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, context.CountRows("movimento"));
        Assert.Equal(0, context.CountRows("idempotencia"));
    }

    [Fact]
    public async Task Handle_ConcurrentIdenticalRequests_CreateOneMovementAndReturnSameResult()
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        var movementIds = Enumerable.Range(0, 6)
            .Select(_ => Guid.NewGuid().ToString("D"))
            .ToArray();
        var handler = context.CreateHandler(movementIds);
        var command = CreateCommand(requestId, ActiveAccountId, 50m, "C");

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(
            () => handler.Handle(command, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        Assert.Single(responses.Select(response => response.MovementId).Distinct(StringComparer.Ordinal));
        Assert.Equal(1, context.CountRows("movimento"));
        Assert.Equal(1, context.CountRows("idempotencia"));
    }

    [Fact]
    public async Task Handle_ConcurrentConflictingRequests_CreateOneMovementAndRejectTheOther()
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        var handler = context.CreateHandler(
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"));
        using var start = new ManualResetEventSlim(initialState: false);

        var tasks = new[]
        {
            Task.Run(() => ExecuteAfterSignal(
                start,
                handler,
                CreateCommand(requestId, ActiveAccountId, 10m, "C"))),
            Task.Run(() => ExecuteAfterSignal(
                start,
                handler,
                CreateCommand(requestId, ActiveAccountId, 20m, "D")))
        };
        start.Set();

        var outcomes = await Task.WhenAll(tasks);

        Assert.Single(outcomes, outcome => outcome is "success");
        Assert.Single(outcomes, outcome => outcome is IdempotencyConflictException.ErrorCode);
        Assert.Equal(1, context.CountRows("movimento"));
        Assert.Equal(1, context.CountRows("idempotencia"));
    }

    [Fact]
    public async Task Handle_CancelledRequest_DoesNotCreateState()
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10m, "C"),
            cancellationTokenSource.Token));

        Assert.Equal(0, context.CountRows("movimento"));
        Assert.Equal(0, context.CountRows("idempotencia"));
    }

    [Fact]
    public async Task Handle_AccountOfAnotherHolder_ThrowsOwnershipDeniedIdenticalToMissingAccount()
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), "missing-account", 10.25m, "C"),
            TestContext.Current.CancellationToken));
        var denied = await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10.25m, "C", OtherAccountHolderId),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", denied.Code);
        Assert.Equal(missing.Code, denied.Code);
        Assert.Equal(missing.Message, denied.Message);
        Assert.Equal(0, context.CountRows("movimento"));
        Assert.Equal(0, context.CountRows("idempotencia"));
        Assert.Equal((0L, 0L), context.GetProjection(ActiveAccountId));
    }

    [Fact]
    public async Task Handle_InactiveAccountOfAnotherHolder_ThrowsOwnershipDeniedInsteadOfInactive()
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        var exception = await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), InactiveAccountId, 10.25m, "C"),
            TestContext.Current.CancellationToken));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
    }

    [Fact]
    public async Task Handle_AccountWithoutHolder_ThrowsOwnershipDenied()
    {
        using var context = CreateContext();
        context.Execute(
            "DELETE FROM titularidade_conta WHERE idcontacorrente = @AccountId;",
            new { AccountId = ActiveAccountId });
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        await Assert.ThrowsAsync<AccountOwnershipDeniedException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10.25m, "C"),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, context.CountRows("movimento"));
    }

    [Theory]
    [InlineData(ActiveAccountId)]
    [InlineData("382D323D-7067-ED11-8866-7D5DFA4A16C9")]
    public async Task Handle_SameKeyFromAnotherHolder_ThrowsConflictWithoutRevealingOrChangingData(
        string secondAccountId)
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));
        await handler.Handle(
            CreateCommand(requestId, ActiveAccountId, 10.25m, "C"),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => handler.Handle(
            CreateCommand(requestId, secondAccountId, 10.25m, "C", OtherAccountHolderId),
            TestContext.Current.CancellationToken));

        Assert.Equal(1, context.CountRows("movimento"));
        Assert.Equal(1, context.CountRows("idempotencia"));
        Assert.Equal((1025L, 1L), context.GetProjection(ActiveAccountId));
        Assert.Equal((0L, 0L), context.GetProjection("382D323D-7067-ED11-8866-7D5DFA4A16C9"));
    }

    [Fact]
    public async Task Handle_KeyWithVersionOneRecord_ThrowsConflict()
    {
        using var context = CreateContext();
        var requestId = Guid.NewGuid().ToString("D");
        context.Execute(
            """
            INSERT INTO idempotencia(chave_idempotencia, requisicao, resultado)
            VALUES (@RequestId, @Request, @Result);
            """,
            new
            {
                RequestId = requestId,
                Request = $"v1|conta={ActiveAccountId}|valor=10.25|tipo=C",
                Result = $"v1|idMovimento={Guid.NewGuid():D}"
            });
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() => handler.Handle(
            CreateCommand(requestId, ActiveAccountId, 10.25m, "C"),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, context.CountRows("movimento"));
        Assert.Equal((0L, 0L), context.GetProjection(ActiveAccountId));
    }

    [Fact]
    public async Task Handle_CreditsAndDebits_UpdateBalanceProjectionInCentsAndVersion()
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"));

        await handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 100.10m, "C"),
            TestContext.Current.CancellationToken);
        await handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 30.05m, "D"),
            TestContext.Current.CancellationToken);
        await handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 0.01m, "C"),
            TestContext.Current.CancellationToken);
        Assert.Equal((7006L, 3L), context.GetProjection(ActiveAccountId));

        await handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 9999999999.99m, "D"),
            TestContext.Current.CancellationToken);

        Assert.Equal((7006L - 999999999999L, 4L), context.GetProjection(ActiveAccountId));
        Assert.True(context.Reconcile().IsConsistent);
    }

    [Fact]
    public async Task Handle_RepeatedIdenticalRequest_DoesNotChangeBalanceProjection()
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));
        var command = CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10.25m, "C");

        await handler.Handle(command, TestContext.Current.CancellationToken);
        await handler.Handle(command, TestContext.Current.CancellationToken);
        await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal((1025L, 1L), context.GetProjection(ActiveAccountId));
    }

    [Fact]
    public async Task Handle_IdempotencyInsertFailure_RollsBackBalanceProjection()
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));
        await handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 50m, "C"),
            TestContext.Current.CancellationToken);
        context.Execute(
            """
            CREATE TRIGGER fail_idempotency_insert
            BEFORE INSERT ON idempotencia
            BEGIN
                SELECT RAISE(ABORT, 'forced idempotency failure');
            END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10.25m, "D"),
            TestContext.Current.CancellationToken));

        Assert.Equal(1, context.CountRows("movimento"));
        Assert.Equal((5000L, 1L), context.GetProjection(ActiveAccountId));
    }

    [Fact]
    public async Task Handle_MissingBalanceProjection_FailsWithoutPersistingMovement()
    {
        using var context = CreateContext();
        context.Execute(
            "DELETE FROM saldo_conta WHERE idcontacorrente = @AccountId;",
            new { AccountId = ActiveAccountId });
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10.25m, "C"),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, context.CountRows("movimento"));
        Assert.Equal(0, context.CountRows("idempotencia"));
    }

    [Fact]
    public async Task Handle_ConcurrentDistinctMovements_DoNotLoseBalanceUpdates()
    {
        const int movementCount = 20;
        using var context = CreateContext();
        var handler = context.CreateHandler(
            Enumerable.Range(0, movementCount).Select(_ => Guid.NewGuid().ToString("D")).ToArray());

        await Task.WhenAll(Enumerable.Range(0, movementCount).Select(index => Task.Run(
            () => handler.Handle(
                CreateCommand(
                    Guid.NewGuid().ToString("D"),
                    ActiveAccountId,
                    1.01m,
                    index % 4 == 0 ? "D" : "C"),
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        // 15 créditos e 5 débitos de 1,01.
        Assert.Equal((1010L, 20L), context.GetProjection(ActiveAccountId));
        Assert.Equal(movementCount, context.CountRows("movimento"));
        Assert.True(context.Reconcile().IsConsistent);
    }

    [Fact]
    public async Task Reconcile_ProjectionChangedOutsideTheStore_ReportsDivergentAccountFingerprint()
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));
        await handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), ActiveAccountId, 10.25m, "C"),
            TestContext.Current.CancellationToken);
        Assert.True(context.Reconcile().IsConsistent);

        context.Execute(
            "UPDATE saldo_conta SET saldo_centavos = saldo_centavos + 1 WHERE idcontacorrente = @AccountId;",
            new { AccountId = ActiveAccountId });

        var result = context.Reconcile();

        Assert.False(result.IsConsistent);
        Assert.Equal(
            SecurityFingerprint.ForAccount(ActiveAccountId),
            Assert.Single(result.DivergentAccountFingerprints));
    }

    [Fact]
    public void Reconcile_MovementWrittenOutsideTheStore_ReportsDivergence()
    {
        using var context = CreateContext();
        context.Execute(
            """
            INSERT INTO movimento(idmovimento, idcontacorrente, datamovimento, tipomovimento, valor)
            VALUES (@MovementId, @AccountId, '01/10/2026', 'C', 10.25);
            """,
            new { MovementId = Guid.NewGuid().ToString("D"), AccountId = ActiveAccountId });

        var result = context.Reconcile();

        Assert.False(result.IsConsistent);
        Assert.DoesNotContain(ActiveAccountId, result.DivergentAccountFingerprints);
    }

    private static async Task<string> ExecuteAfterSignal(
        ManualResetEventSlim start,
        CreateMovementCommandHandler handler,
        CreateMovementCommand command)
    {
        start.Wait(TestContext.Current.CancellationToken);

        try
        {
            await handler.Handle(command, TestContext.Current.CancellationToken);
            return "success";
        }
        catch (IdempotencyConflictException exception)
        {
            return exception.Code;
        }
    }

    private static CreateMovementCommand CreateCommand(
        string requestId,
        string accountId,
        decimal amount,
        string movementType,
        string accountHolderId = ActiveAccountHolderId)
    {
        return new CreateMovementCommand(requestId, accountHolderId, accountId, amount, movementType);
    }

    private static MovementTestContext CreateContext()
    {
        return new MovementTestContext();
    }

    private sealed class MovementTestContext : IDisposable
    {
        private readonly TemporarySqliteDatabase database = new();

        public MovementTestContext()
        {
            ConnectionFactory = new SqliteConnectionFactory(new DatabaseConfig(database.ConnectionString));
            new DatabaseBootstrap(ConnectionFactory).Setup();
        }

        public SqliteConnectionFactory ConnectionFactory { get; }

        public CreateMovementCommandHandler CreateHandler(params string[] movementIds)
        {
            var store = new MovementStore(
                ConnectionFactory,
                new QueueMovementIdGenerator(movementIds),
                new FixedTimeProvider(FixedUtcNow));
            return new CreateMovementCommandHandler(store);
        }

        public int CountRows(string tableName)
        {
            using var connection = ConnectionFactory.OpenConnection();
            return connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {tableName};");
        }

        public (long BalanceCents, long Version) GetProjection(string accountId)
        {
            using var connection = ConnectionFactory.OpenConnection();
            return connection.QuerySingle<(long, long)>(
                "SELECT saldo_centavos, versao FROM saldo_conta WHERE idcontacorrente = @AccountId;",
                new { AccountId = accountId });
        }

        public BalanceReconciliationResult Reconcile()
        {
            return new BalanceReconciler(ConnectionFactory).Reconcile();
        }

        public void Execute(string sql, object? parameters = null)
        {
            using var connection = ConnectionFactory.OpenConnection();
            connection.Execute(sql, parameters);
        }

        public void Dispose()
        {
            database.Dispose();
        }
    }

    private sealed class QueueMovementIdGenerator : IMovementIdGenerator
    {
        private readonly ConcurrentQueue<string> movementIds;

        public QueueMovementIdGenerator(IEnumerable<string> movementIds)
        {
            this.movementIds = new ConcurrentQueue<string>(movementIds);
        }

        public string Create()
        {
            if (!movementIds.TryDequeue(out var movementId))
            {
                throw new InvalidOperationException("Não há identificador de movimento configurado para o teste.");
            }

            return movementId;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed record PersistedMovement(
        string MovementId,
        string AccountId,
        string MovementDate,
        string MovementType,
        double Amount);

    private sealed record PersistedIdempotency(
        string RequestId,
        string Request,
        string Result);
}