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
    [InlineData("missing-account", "INVALID_ACCOUNT")]
    [InlineData("F475F943-7067-ED11-A06B-7E5DFA4A16C9", "INACTIVE_ACCOUNT")]
    public async Task Create_InvalidAccount_ReturnsCorrelatedBusinessProblemDetails(
        string accountId,
        string expectedCode)
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

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
          "valor": 0,
          "tipoMovimento": "C"
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": 1.001,
          "tipoMovimento": "C"
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": 10000000000,
          "tipoMovimento": "C"
        }
        """,
        $$"""
        {
          "idRequisicao": "{{Guid.NewGuid():D}}",
          "idContaCorrente": "{{ActiveAccountId}}",
          "valor": 10.25,
          "tipoMovimento": "c"
        }
        """
    };

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