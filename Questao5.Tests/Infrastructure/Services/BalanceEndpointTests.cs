using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Questao5.Application.Balances;
using Questao5.Application.Handlers;
using Questao5.Application.Queries.Requests;
using Questao5.Application.Queries.Responses;
using Questao5.Infrastructure.Services.Correlation;
using Questao5.Infrastructure.Services.Security;

namespace Questao5.Tests.Infrastructure.Services;

public sealed partial class BalanceEndpointTests
{
    private const string EvaAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string EvaSubject = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string TevinSubject = "06dc3a47-fb77-4589-9e18-076f3860d1d2";
    private const string AmeenaInactiveAccountId = "F475F943-7067-ED11-A06B-7E5DFA4A16C9";
    private const string AmeenaSubject = "cf18e8e5-35f2-498d-a77d-4dd6828316d4";
    private const string AccountWithInternalSpaceId = "B6BAFC09 -6967-ED11-A567-055DFA4A16C9";
    private const string KatherineSubject = "7d85c0f1-c90c-49e6-a2c7-0fb988c3d943";
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 1, 23, 59, 58, TimeSpan.Zero);

    [Fact]
    public async Task Get_AccountWithoutMovements_ReturnsZeroBalanceAndAccountData()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(456, root.GetProperty("numeroContaCorrente").GetInt64());
        Assert.Equal("Eva Woodward", root.GetProperty("nomeTitular").GetString());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("saldoAtual").ValueKind);
        Assert.Equal(0m, root.GetProperty("saldoAtual").GetDecimal());
        Assert.Equal("0.00", root.GetProperty("saldoAtual").GetRawText());
    }

    [Fact]
    public async Task Get_AfterCreditsAndDebits_ReturnsCreditsMinusDebits()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);
        await PostMovementAsync(client, 100.10m, "C");
        await PostMovementAsync(client, 0.20m, "C");
        await PostMovementAsync(client, 50.05m, "D");

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50.25m, document.RootElement.GetProperty("saldoAtual").GetDecimal());
    }

    [Fact]
    public async Task Get_DebitsGreaterThanCredits_ReturnsNegativeBalance()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);
        await PostMovementAsync(client, 10.00m, "C");
        await PostMovementAsync(client, 35.75m, "D");

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(-25.75m, document.RootElement.GetProperty("saldoAtual").GetDecimal());
    }

    [Fact]
    public async Task Get_RepeatedIdempotentMovement_IsCountedOnlyOnce()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);
        var requestId = Guid.NewGuid().ToString("D");
        await PostMovementAsync(client, 10.25m, "C", requestId);
        await PostMovementAsync(client, 10.25m, "C", requestId);
        await PostMovementAsync(client, 10.25m, "C", requestId);

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(10.25m, document.RootElement.GetProperty("saldoAtual").GetDecimal());
    }

    [Fact]
    public async Task Get_Success_ReturnsOnlyContractFieldsWithNoStoreAndCorrelation()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(
            ["dataHoraConsulta", "nomeTitular", "numeroContaCorrente", "saldoAtual"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.NotEmpty(Assert.Single(response.Headers.GetValues(CorrelationConstants.HeaderName)));
    }

    [Fact]
    public async Task Get_Success_ReturnsUtcRoundTripInstantCloseToNow()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);
        var before = DateTimeOffset.UtcNow;

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);
        var after = DateTimeOffset.UtcNow;
        var text = document.RootElement.GetProperty("dataHoraConsulta").GetString()!;
        var instant = DateTimeOffset.ParseExact(
            text,
            "O",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind);

        Assert.Matches(RoundTripUtcPattern(), text);
        Assert.Equal(TimeSpan.Zero, instant.Offset);
        Assert.InRange(instant, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task Get_WithFixedClock_ReturnsExactCanonicalInstant()
    {
        using var factory = new SecurityWebApplicationFactory(configureServices: services =>
        {
            services.RemoveAll<IRequestHandler<GetBalanceQuery, GetBalanceResponse>>();
            services.AddTransient<IRequestHandler<GetBalanceQuery, GetBalanceResponse>>(provider =>
                new GetBalanceQueryHandler(
                    provider.GetRequiredService<IBalanceQueryStore>(),
                    new FixedTimeProvider(FixedUtcNow)));
        });
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(
            "2026-10-01T23:59:58.0000000+00:00",
            document.RootElement.GetProperty("dataHoraConsulta").GetString());
    }

    [Theory]
    [InlineData("fa99d033-7067-ed11-96c6-7c5dfa4a16c9", EvaSubject, 456)]
    [InlineData("%20%20FA99D033-7067-ED11-96C6-7C5DFA4A16C9%20", EvaSubject, 456)]
    [InlineData("B6BAFC09%20-6967-ED11-A567-055DFA4A16C9", KatherineSubject, 123)]
    public async Task Get_EquivalentIdentifierForms_FindThePersistedAccount(
        string routeSegment,
        string subject,
        int expectedNumber)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(subject);

        using var response = await client.GetAsync(
            $"/api/v1/contas/{routeSegment}/saldo",
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedNumber, document.RootElement.GetProperty("numeroContaCorrente").GetInt64());
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401WithoutAccountData()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await GetBalanceAsync(client, EvaAccountId);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("UNAUTHENTICATED", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Eva Woodward", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5200);
    }

    [Fact]
    public async Task Get_MissingAccount_ReturnsInvalidAccountAndEvent5201()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await GetBalanceAsync(client, "missing-account");
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("INVALID_ACCOUNT", document.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("detail").GetString()));
        Assert.Equal(
            Assert.Single(response.Headers.GetValues(CorrelationConstants.HeaderName)),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5201);
        Assert.Contains("RuleCode: INVALID_ACCOUNT;", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5301);
    }

    [Fact]
    public async Task Get_OwnInactiveAccount_ReturnsInactiveAccount()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(AmeenaSubject);

        using var response = await GetBalanceAsync(client, AmeenaInactiveAccountId);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INACTIVE_ACCOUNT", document.RootElement.GetProperty("code").GetString());
        Assert.False(document.RootElement.TryGetProperty("saldoAtual", out _));
    }

    [Theory]
    [InlineData(EvaAccountId)]
    [InlineData(AmeenaInactiveAccountId)]
    public async Task Get_AccountOfAnotherHolder_ReturnsSameBodyAsMissingAccountAndEvent5301(string accountId)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(TevinSubject);

        using var missing = await GetBalanceAsync(client, "missing-account");
        using var denied = await GetBalanceAsync(client, accountId);
        using var missingDocument = await ReadJsonAsync(missing);
        using var deniedDocument = await ReadJsonAsync(denied);
        var deniedBody = deniedDocument.RootElement.GetRawText();

        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("INVALID_ACCOUNT", deniedDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(WithoutCorrelation(missingDocument), WithoutCorrelation(deniedDocument));
        Assert.DoesNotContain("Woodward", deniedBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lynn", deniedBody, StringComparison.OrdinalIgnoreCase);

        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5301);
        Assert.Contains(SecurityFingerprint.Create(TevinSubject), entry.Message, StringComparison.Ordinal);
        Assert.Contains(SecurityFingerprint.ForAccount(accountId), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5200);
    }

    [Theory]
    [InlineData("%20")]
    [InlineData("%20%20%20")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Get_StructurallyInvalidIdentifier_ReturnsCorrelatedValidationProblem(string routeSegment)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await client.GetAsync(
            $"/api/v1/contas/{routeSegment}/saldo",
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(
            Assert.Single(response.Headers.GetValues(CorrelationConstants.HeaderName)),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
    }

    [Fact]
    public async Task Get_MissingAccountSegment_DoesNotMatchTheRoute()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await client.GetAsync(
            "/api/v1/contas/saldo",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Success_EmitsEvent5200WithoutSensitiveData()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);
        await PostMovementAsync(client, 987.65m, "C");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/contas/{EvaAccountId}/saldo");
        request.Headers.Add(CorrelationConstants.HeaderName, "balance-log-test");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5200);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("CorrelationId: balance-log-test;", entry.Message, StringComparison.Ordinal);
        Assert.Contains(SecurityFingerprint.ForAccount(EvaAccountId), entry.Message, StringComparison.Ordinal);

        var balanceLogs = string.Join(
            '\n',
            factory.LoggerProvider.Entries
                .Where(entry => entry.EventId.Id is >= 5200 and <= 5203)
                .Select(entry => entry.Message));
        Assert.DoesNotContain(EvaAccountId, balanceLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EvaSubject, balanceLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Woodward", balanceLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("987.65", balanceLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("987,65", balanceLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("98765", balanceLogs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_UnexpectedFailure_Returns500AndEvent5202WithoutExceptionMessage()
    {
        const string sensitiveMessage = "SELECT saldo FROM C:\\secret\\database.db";
        using var factory = CreateFactoryWithStore(
            new DelegatingBalanceQueryStore((_, _, _) => throw new InvalidOperationException(sensitiveMessage)));
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await GetBalanceAsync(client, EvaAccountId);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(sensitiveMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5202);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(typeof(InvalidOperationException).FullName!, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_EndpointFrequencyExceeded_Returns429AndEvent5203PerAccountHolder()
    {
        using var factory = CreateFactoryWithLimits(endpointPermitLimit: 2);
        using var evaClient = factory.CreateAuthenticatedClient(EvaSubject);
        using var katherineClient = factory.CreateAuthenticatedClient(KatherineSubject);

        using var first = await GetBalanceAsync(evaClient, EvaAccountId);
        using var second = await GetBalanceAsync(evaClient, "missing-account");
        using var rejected = await GetBalanceAsync(evaClient, EvaAccountId);
        using var otherHolder = await GetBalanceAsync(katherineClient, AccountWithInternalSpaceId);
        using var document = await ReadJsonAsync(rejected);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter is not null);
        Assert.Equal("RATE_LIMIT_EXCEEDED", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            Assert.Single(rejected.Headers.GetValues(CorrelationConstants.HeaderName)),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        Assert.Equal(HttpStatusCode.OK, otherHolder.StatusCode);
        Assert.Contains(
            factory.LoggerProvider.Entries,
            entry => entry.EventId.Id == 5203 && entry.Message.Contains("LimitName: Balance;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_BalanceLimit_DoesNotConsumeTheMovementLimit()
    {
        using var factory = CreateFactoryWithLimits(endpointPermitLimit: 1);
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var firstBalance = await GetBalanceAsync(client, EvaAccountId);
        using var rejectedBalance = await GetBalanceAsync(client, EvaAccountId);
        using var movement = await PostMovementAsync(client, 10.25m, "C");

        Assert.Equal(HttpStatusCode.OK, firstBalance.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedBalance.StatusCode);
        Assert.Equal(HttpStatusCode.OK, movement.StatusCode);
    }

    [Fact]
    public async Task Get_ConcurrencyExceededWithoutQueue_Returns429()
    {
        using var release = new ManualResetEventSlim(initialState: false);
        var twoEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        using var factory = CreateFactoryWithStore(
            new DelegatingBalanceQueryStore((_, _, cancellationToken) =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                {
                    twoEntered.TrySetResult();
                }

                release.Wait(cancellationToken);
                return new AccountBalance(456, "Eva Woodward", 0.00m);
            }),
            concurrencyPermitLimit: 2);
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        var first = Task.Run(() => GetBalanceAsync(client, EvaAccountId), TestContext.Current.CancellationToken);
        var second = Task.Run(() => GetBalanceAsync(client, EvaAccountId), TestContext.Current.CancellationToken);
        await twoEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        using var rejected = await GetBalanceAsync(client, EvaAccountId);
        release.Set();
        using var firstResponse = await first;
        using var secondResponse = await second;

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Get_OperationTimeout_CancelsReadAndReturnsCorrelated504()
    {
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var factory = CreateFactoryWithStore(
            new DelegatingBalanceQueryStore((_, _, cancellationToken) =>
            {
                cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));

                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult();
                }

                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Fluxo inalcançável.");
            }),
            timeoutSeconds: 1);
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await GetBalanceAsync(client, EvaAccountId);
        using var document = await ReadJsonAsync(response);
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("REQUEST_TIMEOUT", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            Assert.Single(response.Headers.GetValues(CorrelationConstants.HeaderName)),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        Assert.Contains(
            factory.LoggerProvider.Entries,
            entry => entry.EventId.Id == 5203 &&
                entry.Message.Contains("LimitName: BalanceTimeout;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_DoesNotChangePersistedState()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);
        await PostMovementAsync(client, 10.25m, "C");
        var before = Snapshot(factory);

        using var first = await GetBalanceAsync(client, EvaAccountId);
        using var second = await GetBalanceAsync(client, "missing-account");
        using var third = await GetBalanceAsync(client, AmeenaInactiveAccountId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(before, Snapshot(factory));
    }

    [Fact]
    public async Task Get_ConcurrentWithMovements_AlwaysObservesAConfirmedBalance()
    {
        // Abaixo dos limites de concorrência (8) para que nenhuma requisição seja rejeitada com 429.
        const int movementCount = 6;
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        var movements = Enumerable.Range(0, movementCount)
            .Select(_ => PostMovementAsync(client, 1.00m, "C"))
            .ToArray();
        var readings = Enumerable.Range(0, movementCount)
            .Select(async _ =>
            {
                using var response = await GetBalanceAsync(client, EvaAccountId);
                using var document = await ReadJsonAsync(response);
                return (response.StatusCode, Balance: document.RootElement.GetProperty("saldoAtual").GetDecimal());
            })
            .ToArray();

        var movementResponses = await Task.WhenAll(movements);
        var observed = await Task.WhenAll(readings);
        using var finalResponse = await GetBalanceAsync(client, EvaAccountId);
        using var finalDocument = await ReadJsonAsync(finalResponse);

        Assert.All(movementResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.All(observed, reading =>
        {
            Assert.Equal(HttpStatusCode.OK, reading.StatusCode);
            Assert.InRange(reading.Balance, 0m, movementCount);
            Assert.Equal(decimal.Truncate(reading.Balance), reading.Balance);
        });
        Assert.Equal(movementCount, finalDocument.RootElement.GetProperty("saldoAtual").GetDecimal());

        foreach (var response in movementResponses)
        {
            response.Dispose();
        }
    }

    private static SecurityWebApplicationFactory CreateFactoryWithLimits(int endpointPermitLimit)
    {
        return new SecurityWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Balance:EndpointPermitLimit"] = endpointPermitLimit.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    private static SecurityWebApplicationFactory CreateFactoryWithStore(
        IBalanceQueryStore store,
        int timeoutSeconds = 5,
        int concurrencyPermitLimit = 8)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Balance:TimeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Balance:ConcurrencyPermitLimit"] = concurrencyPermitLimit.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };

        return new SecurityWebApplicationFactory(
            configuration,
            services =>
            {
                services.RemoveAll<IBalanceQueryStore>();
                services.AddSingleton(store);
            });
    }

    private static Task<HttpResponseMessage> GetBalanceAsync(HttpClient client, string accountId)
    {
        return client.GetAsync(
            $"/api/v1/contas/{Uri.EscapeDataString(accountId)}/saldo",
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> PostMovementAsync(
        HttpClient client,
        decimal amount,
        string movementType,
        string? requestId = null)
    {
        return client.PostAsJsonAsync(
            "/api/v1/movimentos",
            new
            {
                idRequisicao = requestId ?? Guid.NewGuid().ToString("D"),
                idContaCorrente = EvaAccountId,
                valor = amount,
                tipoMovimento = movementType
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(body);
    }

    private static string WithoutCorrelation(JsonDocument document)
    {
        var properties = document.RootElement.EnumerateObject()
            .Where(property => property.Name is not "correlationId" and not "traceId")
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={property.Value.GetRawText()}");

        return string.Join('|', properties);
    }

    private static string Snapshot(SecurityWebApplicationFactory factory)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = factory.DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT (SELECT COUNT(*) FROM contacorrente) || '|' ||
                   (SELECT COUNT(*) FROM movimento) || '|' ||
                   (SELECT COUNT(*) FROM idempotencia) || '|' ||
                   (SELECT COUNT(*) FROM titularidade_conta) || '|' ||
                   (SELECT group_concat(idcontacorrente || ':' || saldo_centavos || ':' || versao, ';')
                    FROM (SELECT * FROM saldo_conta ORDER BY idcontacorrente));
            """;

        return (string)command.ExecuteScalar()!;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}\+00:00$")]
    private static partial Regex RoundTripUtcPattern();

    private sealed class DelegatingBalanceQueryStore : IBalanceQueryStore
    {
        private readonly Func<string, string, CancellationToken, AccountBalance> getBalance;

        public DelegatingBalanceQueryStore(Func<string, string, CancellationToken, AccountBalance> getBalance)
        {
            this.getBalance = getBalance;
        }

        public AccountBalance GetBalance(
            string accountHolderId,
            string accountId,
            CancellationToken cancellationToken)
        {
            return getBalance(accountHolderId, accountId, cancellationToken);
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
}
