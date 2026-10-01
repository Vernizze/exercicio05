using Dapper;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using Questao5.Application.Handlers;
using Questao5.Application.Movements;
using Questao5.Infrastructure.Database.CommandStore;
using Questao5.Infrastructure.Sqlite;
using Questao5.Tests.Infrastructure.Sqlite;
using System.Collections.Concurrent;

namespace Questao5.Tests.Infrastructure.Database;

public sealed class MovementStoreTests
{
    private const string ActiveAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string InactiveAccountId = "F475F943-7067-ED11-A06B-7E5DFA4A16C9";
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
            $"v1|conta={ActiveAccountId}|valor=125.50|tipo={movementType}",
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
    [InlineData("missing-account", "INVALID_ACCOUNT")]
    [InlineData(InactiveAccountId, "INACTIVE_ACCOUNT")]
    public async Task Handle_InvalidAccountState_ThrowsBusinessRuleAndDoesNotReserveKey(
        string accountId,
        string expectedCode)
    {
        using var context = CreateContext();
        var handler = context.CreateHandler(Guid.NewGuid().ToString("D"));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            CreateCommand(Guid.NewGuid().ToString("D"), accountId, 10.25m, "C"),
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
        string movementType)
    {
        return new CreateMovementCommand(requestId, accountId, amount, movementType);
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