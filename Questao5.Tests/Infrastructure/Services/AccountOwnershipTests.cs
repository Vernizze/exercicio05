using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Questao5.Infrastructure.Services.Security;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class AccountOwnershipTests
{
    private const string EvaAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string EvaSubject = "04b276dc-0f45-4efc-bffc-911110198733";
    private const string TevinAccountId = "382D323D-7067-ED11-8866-7D5DFA4A16C9";
    private const string TevinSubject = "06dc3a47-fb77-4589-9e18-076f3860d1d2";
    private const string AmeenaInactiveAccountId = "F475F943-7067-ED11-A06B-7E5DFA4A16C9";

    [Fact]
    public async Task Create_AccountOfAnotherHolder_ReturnsSameBodyAsMissingAccountAndPersistsNothing()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(TevinSubject);

        using var missing = await PostMovementAsync(client, accountId: "missing-account");
        using var denied = await PostMovementAsync(client, accountId: EvaAccountId);
        using var missingDocument = await ReadJsonAsync(missing);
        using var deniedDocument = await ReadJsonAsync(denied);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal("INVALID_ACCOUNT", deniedDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(WithoutCorrelation(missingDocument), WithoutCorrelation(deniedDocument));
        Assert.Equal(0L, CountRows(factory, "movimento"));
        Assert.Equal(0L, CountRows(factory, "idempotencia"));
    }

    [Fact]
    public async Task Create_AccountOfAnotherHolder_EmitsEvent5301WithoutIdentifiersInClearText()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(TevinSubject);

        using var response = await PostMovementAsync(client, accountId: EvaAccountId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5301);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(SecurityFingerprint.Create(TevinSubject), entry.Message, StringComparison.Ordinal);
        Assert.Contains(SecurityFingerprint.ForAccount(EvaAccountId), entry.Message, StringComparison.Ordinal);

        var combinedLogs = string.Join('\n', factory.LoggerProvider.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(TevinSubject, combinedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EvaAccountId, combinedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5103);
    }

    [Fact]
    public async Task Create_InactiveAccountOfAnotherHolder_ReturnsInvalidAccountInsteadOfInactive()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await PostMovementAsync(client, accountId: AmeenaInactiveAccountId);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_ACCOUNT", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_MissingAccount_DoesNotEmitOwnershipEvent()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var response = await PostMovementAsync(client, accountId: "missing-account");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5301);
        Assert.Contains(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5103);
    }

    [Fact]
    public async Task Create_OwnAccount_UpdatesBalanceProjection()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(EvaSubject);

        using var credit = await PostMovementAsync(client, accountId: EvaAccountId, amount: 125.50m, movementType: "C");
        using var debit = await PostMovementAsync(client, accountId: EvaAccountId, amount: 25.25m, movementType: "D");

        Assert.Equal(HttpStatusCode.OK, credit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, debit.StatusCode);
        Assert.Equal(
            10025L,
            ExecuteScalar(
                factory,
                $"SELECT saldo_centavos FROM saldo_conta WHERE idcontacorrente = '{EvaAccountId}';"));
    }

    [Fact]
    public async Task Create_SameIdempotencyKeyFromAnotherHolder_Returns409WithoutOriginalResult()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var evaClient = factory.CreateAuthenticatedClient(EvaSubject);
        using var tevinClient = factory.CreateAuthenticatedClient(TevinSubject);
        var requestId = Guid.NewGuid().ToString("D");

        using var original = await PostMovementAsync(evaClient, requestId, EvaAccountId);
        using var originalDocument = await ReadJsonAsync(original);
        using var reused = await PostMovementAsync(tevinClient, requestId, TevinAccountId);
        var reusedBody = await reused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.DoesNotContain(
            originalDocument.RootElement.GetProperty("idMovimento").GetString()!,
            reusedBody,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1L, CountRows(factory, "movimento"));
    }

    [Fact]
    public async Task Create_EndpointLimit_IsCountedPerAccountHolder()
    {
        using var factory = CreateFactoryWithEndpointLimit(1);
        using var evaClient = factory.CreateAuthenticatedClient(EvaSubject);
        using var tevinClient = factory.CreateAuthenticatedClient(TevinSubject);

        using var evaFirst = await PostMovementAsync(evaClient, accountId: EvaAccountId);
        using var evaSecond = await PostMovementAsync(evaClient, accountId: EvaAccountId);
        using var tevinFirst = await PostMovementAsync(tevinClient, accountId: TevinAccountId);

        Assert.Equal(HttpStatusCode.OK, evaFirst.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, evaSecond.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tevinFirst.StatusCode);
    }

    [Fact]
    public async Task Create_UnauthenticatedRequests_ConsumeEndpointLimitByIpWithoutAffectingAccountHolders()
    {
        using var factory = CreateFactoryWithEndpointLimit(1);
        using var anonymousClient = factory.CreateAnonymousClient();
        using var evaClient = factory.CreateAuthenticatedClient(EvaSubject);

        using var firstAnonymous = await PostMovementAsync(anonymousClient, accountId: EvaAccountId);
        using var secondAnonymous = await PostMovementAsync(anonymousClient, accountId: EvaAccountId);
        using var authenticated = await PostMovementAsync(evaClient, accountId: EvaAccountId);

        Assert.Equal(HttpStatusCode.Unauthorized, firstAnonymous.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondAnonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
    }

    private static SecurityWebApplicationFactory CreateFactoryWithEndpointLimit(int endpointPermitLimit)
    {
        return new SecurityWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Movement:EndpointPermitLimit"] = endpointPermitLimit.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    private static Task<HttpResponseMessage> PostMovementAsync(
        HttpClient client,
        string? requestId = null,
        string accountId = EvaAccountId,
        decimal amount = 10.25m,
        string movementType = "C")
    {
        return client.PostAsJsonAsync(
            "/api/v1/movimentos",
            new
            {
                idRequisicao = requestId ?? Guid.NewGuid().ToString("D"),
                idContaCorrente = accountId,
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

    private static long CountRows(SecurityWebApplicationFactory factory, string tableName)
    {
        return ExecuteScalar(factory, $"SELECT COUNT(*) FROM {tableName};");
    }

    private static long ExecuteScalar(SecurityWebApplicationFactory factory, string sql)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = factory.DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (long)command.ExecuteScalar()!;
    }
}
