using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Questao5.Application.Commands.Responses;
using Questao5.Application.Exceptions;
using Questao5.Infrastructure.Services.Correlation;
using Questao5.Infrastructure.Services.Movements;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class MovementOperationalTests
{
    private const string ActiveAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";

    [Fact]
    public async Task Create_EndpointFrequencyExceeded_Returns429RetryAfterAndEvent5105()
    {
        using var factory = CreateFactory(endpointPermitLimit: 2);
        using var client = CreateClient(factory);

        using var first = await PostMovementAsync(client);
        using var second = await PostMovementAsync(client);
        using var rejected = await PostMovementAsync(client);
        using var document = await ReadJsonAsync(rejected);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter is not null);
        Assert.Equal("RATE_LIMIT_EXCEEDED", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            GetCorrelationHeader(rejected),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        Assert.Contains(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5105);
    }

    [Fact]
    public async Task Request_GlobalFrequencyExceeded_Returns429RetryAfterAndEvent5105()
    {
        using var factory = CreateFactory(globalPermitLimit: 2);
        using var client = CreateClient(factory);

        using var first = await client.GetAsync(
            "/security-tests/success",
            TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(
            "/security-tests/success",
            TestContext.Current.CancellationToken);
        using var rejected = await client.GetAsync(
            "/security-tests/success",
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(rejected);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter is not null);
        Assert.Equal("RATE_LIMIT_EXCEEDED", document.RootElement.GetProperty("code").GetString());
        Assert.Contains(
            factory.LoggerProvider.Entries,
            entry => entry.EventId.Id == 5105 && entry.Message.Contains("Global", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_ConcurrencyExceededWithoutQueue_Returns429()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        using var factory = CreateFactory(
            concurrencyPermitLimit: 2,
            handler: async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                {
                    twoEntered.TrySetResult();
                }

                await release.Task.WaitAsync(cancellationToken);
                return new CreateMovementResponse(Guid.NewGuid().ToString("D"), IsReplay: false);
            });
        using var client = CreateClient(factory);

        var first = PostMovementAsync(client);
        var second = PostMovementAsync(client);
        await twoEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        using var rejected = await PostMovementAsync(client);
        release.TrySetResult();
        using var firstResponse = await first;
        using var secondResponse = await second;

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Contains(
            factory.LoggerProvider.Entries,
            entry => entry.EventId.Id == 5105 && entry.Message.Contains("Movement", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_OperationTimeout_CancelsHandlerAndReturnsCorrelated504()
    {
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var factory = CreateFactory(
            timeoutSeconds: 1,
            handler: async (_, cancellationToken) =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult();
                    throw;
                }

                throw new InvalidOperationException("Fluxo inalcançável.");
            });
        using var client = CreateClient(factory);

        using var response = await PostMovementAsync(client);
        using var document = await ReadJsonAsync(response);
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("REQUEST_TIMEOUT", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            GetCorrelationHeader(response),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        Assert.Contains(
            factory.LoggerProvider.Entries,
            entry => entry.EventId.Id == 5105 && entry.Message.Contains("MovementTimeout", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_Outcomes_EmitStableEventsAndSafeFingerprint()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        var requestId = Guid.NewGuid().ToString("D");
        var expectedFingerprint = IdempotencyFingerprint.Create(requestId);

        using var created = await PostMovementAsync(client, requestId: requestId);
        using var replayed = await PostMovementAsync(client, requestId: requestId);
        using var conflict = await PostMovementAsync(client, requestId: requestId, amount: 20.75m);
        using var rejected = await PostMovementAsync(client, accountId: Guid.NewGuid().ToString("D"));

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var movementEntries = factory.LoggerProvider.Entries
            .Where(entry => entry.EventId.Id is >= 5100 and <= 5104)
            .ToArray();
        Assert.Contains(movementEntries, entry => entry.EventId.Id == 5100);
        Assert.Contains(movementEntries, entry => entry.EventId.Id == 5101);
        Assert.Contains(movementEntries, entry => entry.EventId.Id == 5102);
        Assert.Contains(movementEntries, entry => entry.EventId.Id == 5103);
        Assert.All(
            movementEntries.Where(entry => entry.EventId.Id is 5100 or 5101 or 5102),
            entry => Assert.Contains(expectedFingerprint, entry.Message, StringComparison.Ordinal));

        var combinedLogs = string.Join('\n', movementEntries.Select(entry => entry.Message));
        Assert.DoesNotContain(requestId, combinedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ActiveAccountId, combinedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("20.75", combinedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("tipoMovimento", combinedLogs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_UnexpectedFailure_EmitsEvent5104WithoutExceptionMessage()
    {
        const string sensitiveMessage = "SQL path=C:\\secret\\database.db account=123";
        using var factory = CreateFactory(
            handler: (_, _) => throw new InvalidOperationException(sensitiveMessage));
        using var client = CreateClient(factory);

        using var response = await PostMovementAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5104);
        Assert.Contains(typeof(InvalidOperationException).FullName!, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IdempotencyFingerprint_SameNormalizedKey_IsStableAndTruncated()
    {
        const string requestId = "11111111-2222-3333-4444-555555555555";

        var first = IdempotencyFingerprint.Create(requestId);
        var second = IdempotencyFingerprint.Create(requestId);

        Assert.Equal(first, second);
        Assert.Equal(16, first.Length);
        Assert.Matches("^[0-9A-F]{16}$", first);
    }

    private static SecurityWebApplicationFactory CreateFactory(
        int timeoutSeconds = 5,
        int endpointPermitLimit = 30,
        int globalPermitLimit = 120,
        int concurrencyPermitLimit = 8,
        Func<Questao5.Application.Commands.Requests.CreateMovementCommand, CancellationToken, Task<CreateMovementResponse>>? handler = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Movement:TimeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Movement:EndpointPermitLimit"] = endpointPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Movement:GlobalPermitLimit"] = globalPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Movement:WindowSeconds"] = "60",
            ["Movement:ConcurrencyPermitLimit"] = concurrencyPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        return new SecurityWebApplicationFactory(
            configuration,
            handler is null
                ? null
                : services =>
                {
                    services.RemoveAll<IMediator>();
                    services.AddSingleton<IMediator>(new ControlledMovementMediator(handler));
                });
    }

    private static HttpClient CreateClient(SecurityWebApplicationFactory factory)
    {
        return factory.CreateAuthenticatedClient();
    }

    private static Task<HttpResponseMessage> PostMovementAsync(
        HttpClient client,
        string? requestId = null,
        string accountId = ActiveAccountId,
        decimal amount = 10.25m)
    {
        return client.PostAsJsonAsync(
            "/api/v1/movimentos",
            new
            {
                idRequisicao = requestId ?? Guid.NewGuid().ToString("D"),
                idContaCorrente = accountId,
                valor = amount,
                tipoMovimento = "C"
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(body);
    }

    private static string GetCorrelationHeader(HttpResponseMessage response)
    {
        return Assert.Single(response.Headers.GetValues(CorrelationConstants.HeaderName));
    }
}