using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Questao5.Infrastructure.Services.Correlation;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class MovementEndpointTests
{
    private const string ActiveAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";

    [Theory]
    [InlineData("C")]
    [InlineData("D")]
    public async Task Create_ValidRequest_ReturnsMovementIdAndCorrelationId(string movementType)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        var request = CreateRequest(movementType: movementType);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            request,
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(Guid.TryParseExact(
            document.RootElement.GetProperty("idMovimento").GetString(),
            "D",
            out _));
        Assert.NotEmpty(GetCorrelationHeader(response));
    }

    [Fact]
    public async Task Create_ValidRequest_PersistsOnlyInTheIsolatedTestDatabase()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            CreateRequest(),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = factory.DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM movimento;";

        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public async Task Create_RepeatedIdenticalRequest_ReturnsOriginalMovementId()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        var request = CreateRequest();

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            request,
            TestContext.Current.CancellationToken);
        using var firstDocument = await ReadJsonAsync(firstResponse);
        using var repeatedResponse = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            request,
            TestContext.Current.CancellationToken);
        using var repeatedDocument = await ReadJsonAsync(repeatedResponse);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(
            firstDocument.RootElement.GetProperty("idMovimento").GetString(),
            repeatedDocument.RootElement.GetProperty("idMovimento").GetString());
        Assert.NotEqual(GetCorrelationHeader(firstResponse), GetCorrelationHeader(repeatedResponse));
    }

    [Fact]
    public async Task Create_SameKeyWithDifferentPayload_ReturnsCorrelatedConflictProblemDetails()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        var requestId = Guid.NewGuid().ToString("D");
        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            CreateRequest(requestId: requestId, amount: 10m),
            TestContext.Current.CancellationToken);

        using var conflictResponse = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            CreateRequest(requestId: requestId, amount: 20m),
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(conflictResponse);

        firstResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("IDEMPOTENCY_CONFLICT", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            GetCorrelationHeader(conflictResponse),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
    }

    [Theory]
    [InlineData("missing-account", TestTokenIssuer.DefaultSubject, "INVALID_ACCOUNT")]
    [InlineData("F475F943-7067-ED11-A06B-7E5DFA4A16C9", "cf18e8e5-35f2-498d-a77d-4dd6828316d4", "INACTIVE_ACCOUNT")]
    public async Task Create_InvalidAccount_ReturnsCorrelatedBusinessProblemDetails(
        string accountId,
        string subject,
        string expectedCode)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(subject);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            CreateRequest(accountId: accountId),
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            GetCorrelationHeader(response),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Create_InvalidJsonContract_ReturnsCorrelatedValidationProblemDetails(string body)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            "/api/v1/movimentos",
            content,
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
        Assert.Equal(
            GetCorrelationHeader(response),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.00")]
    [InlineData("-1")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000")]
    public async Task Create_InvalidValue_ReturnsInvalidValueCodeWithoutPersisting(string valueLiteral)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await PostRawMovementAsync(client, valueLiteral, "C");

        await AssertBusinessRejectionAsync(factory, response, "INVALID_VALUE");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("c")]
    [InlineData("d")]
    [InlineData("X")]
    [InlineData("CC")]
    [InlineData("credito")]
    public async Task Create_InvalidType_ReturnsInvalidTypeCodeWithoutPersisting(string movementType)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await PostRawMovementAsync(client, "10.25", movementType);

        await AssertBusinessRejectionAsync(factory, response, "INVALID_TYPE");
    }

    [Fact]
    public async Task Create_InvalidValueAndType_ReturnsInvalidValueFirst()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await PostRawMovementAsync(client, "0", "X");

        await AssertBusinessRejectionAsync(factory, response, "INVALID_VALUE");
    }

    [Theory]
    [InlineData("missing-account")]
    [InlineData("382D323D-7067-ED11-8866-7D5DFA4A16C9")]
    [InlineData("F475F943-7067-ED11-A06B-7E5DFA4A16C9")]
    public async Task Create_InvalidValueForUnavailableAccount_ReturnsInvalidValueWithoutRevealingTheAccount(
        string accountId)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await PostRawMovementAsync(client, "-5", "C", accountId: accountId);

        await AssertBusinessRejectionAsync(factory, response, "INVALID_VALUE");
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5301);
    }

    [Theory]
    [InlineData("0", "C")]
    [InlineData("10.25", "X")]
    public async Task Create_RejectedValueOrType_DoesNotReserveTheIdempotencyKey(
        string valueLiteral,
        string movementType)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        var requestId = Guid.NewGuid().ToString("D");

        using var rejected = await PostRawMovementAsync(client, valueLiteral, movementType, requestId);
        using var accepted = await PostRawMovementAsync(client, "10.25", "C", requestId);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(1L, CountRows(factory, "movimento"));
    }

    [Fact]
    public async Task Create_InvalidValue_DoesNotEchoTheReceivedValueOrLogIt()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await PostRawMovementAsync(client, "-98765.43", "C");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("98765", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            factory.LoggerProvider.Entries,
            entry => entry.EventId.Id == 5103 && entry.Message.Contains("98765", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_AccountWithExternalSpacesWithinNormalizedLimit_IsAccepted()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/movimentos",
            CreateRequest(accountId: $"  {ActiveAccountId}  "),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnsupportedMediaType_Returns415()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        using var content = new StringContent("valid-looking-but-not-json", Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(
            "/api/v1/movimentos",
            content,
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            GetCorrelationHeader(response),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
    }

    [Fact]
    public async Task Create_BodyAboveFourKiB_IsRejected()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        var body = $$"""
            {
              "idRequisicao": "{{Guid.NewGuid():D}}",
              "idContaCorrente": "{{ActiveAccountId}}",
              "valor": 10.25,
              "tipoMovimento": "C",
              "padding": "{{new string('A', 4096)}}"
            }
            """;
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            "/api/v1/movimentos",
            content,
            TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            GetCorrelationHeader(response),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
    }

    public static TheoryData<string> InvalidBodies => new()
    {
        "{}",
        "{invalid-json",
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": 10.25,
          "tipoMovimento": "C",
          "propriedadeDesconhecida": true
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():N}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": 10.25,
          "tipoMovimento": "C"
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "tipoMovimento": "C"
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": null,
          "tipoMovimento": "C"
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": "dez",
          "tipoMovimento": "C"
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": 10.25
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": 10.25,
          "tipoMovimento": null
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "   ",
          "valor": 10.25,
          "tipoMovimento": "C"
        }
        """
    };

    private static async Task<HttpResponseMessage> PostRawMovementAsync(
        HttpClient client,
        string valueLiteral,
        string movementType,
        string? requestId = null,
        string accountId = ActiveAccountId)
    {
        var body = $$"""
            {
              "idRequisicao": "{{requestId ?? Guid.NewGuid().ToString("D")}}",
              "idContaCorrente": "{{accountId}}",
              "valor": {{valueLiteral}},
              "tipoMovimento": "{{movementType}}"
            }
            """;
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        return await client.PostAsync(
            "/api/v1/movimentos",
            content,
            TestContext.Current.CancellationToken);
    }

    private static async Task AssertBusinessRejectionAsync(
        SecurityWebApplicationFactory factory,
        HttpResponseMessage response,
        string expectedCode)
    {
        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedCode, root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.False(root.TryGetProperty("errors", out _));
        Assert.Equal(
            GetCorrelationHeader(response),
            root.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        Assert.Equal(0L, CountRows(factory, "movimento"));
        Assert.Equal(0L, CountRows(factory, "idempotencia"));
        Assert.Contains(
            factory.LoggerProvider.Entries,
            entry => entry.EventId.Id == 5103 &&
                entry.Message.Contains($"RuleCode: {expectedCode};", StringComparison.Ordinal));
    }

    private static long CountRows(SecurityWebApplicationFactory factory, string tableName)
    {
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = factory.DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";

        return (long)command.ExecuteScalar()!;
    }

    private static object CreateRequest(
        string? requestId = null,
        string accountId = ActiveAccountId,
        decimal amount = 10.25m,
        string movementType = "C")
    {
        return new
        {
            idRequisicao = requestId ?? Guid.NewGuid().ToString("D"),
            idContaCorrente = accountId,
            valor = amount,
            tipoMovimento = movementType
        };
    }

    private static HttpClient CreateClient(SecurityWebApplicationFactory factory)
    {
        return factory.CreateAuthenticatedClient();
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