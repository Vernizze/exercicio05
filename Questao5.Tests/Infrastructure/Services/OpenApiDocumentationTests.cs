using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Questao5.Application.Balances;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class OpenApiDocumentationTests : IDisposable
{
    private readonly List<SecurityWebApplicationFactory> factories = [];

    private const string MovementPath = "/api/v1/movimentos";
    private const string BalancePath = "/api/v1/contas/{idContaCorrente}/saldo";
    private const string ProblemContentType = "application/problem+json";
    private const string EvaAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string AmeenaInactiveAccountId = "F475F943-7067-ED11-A06B-7E5DFA4A16C9";
    private const string AmeenaSubject = "cf18e8e5-35f2-498d-a77d-4dd6828316d4";

    private static readonly string[] MovementStatusCodes =
        ["200", "400", "401", "409", "413", "415", "429", "500", "504"];

    private static readonly string[] BalanceStatusCodes = ["200", "400", "401", "429", "500", "504"];

    [Fact]
    public async Task Document_DescribesTheApiAndItsAuthentication()
    {
        using var document = await GetDocumentAsync();
        var info = document.RootElement.GetProperty("info");

        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("title").GetString()));
        Assert.Equal("v1", info.GetProperty("version").GetString());
        Assert.Contains("Bearer", info.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("X-Correlation-ID", info.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MovementPath, "post")]
    [InlineData(BalancePath, "get")]
    public async Task Operation_HasSummaryDescriptionAndEveryPossibleStatus(string path, string method)
    {
        using var document = await GetDocumentAsync();
        var operation = GetOperation(document, path, method);
        var expectedStatusCodes = method == "post" ? MovementStatusCodes : BalanceStatusCodes;

        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("description").GetString()));
        Assert.Equal(
            expectedStatusCodes,
            operation.GetProperty("responses").EnumerateObject().Select(response => response.Name).Order());
    }

    [Theory]
    [InlineData(MovementPath, "post")]
    [InlineData(BalancePath, "get")]
    public async Task EveryResponse_HasDescriptionContentTypeAndCorrelationHeader(string path, string method)
    {
        using var document = await GetDocumentAsync();
        var responses = GetOperation(document, path, method).GetProperty("responses");

        foreach (var response in responses.EnumerateObject())
        {
            var expectedContentType = response.Name == "200" ? "application/json" : ProblemContentType;
            var content = Assert.Single(response.Value.GetProperty("content").EnumerateObject());

            Assert.False(string.IsNullOrWhiteSpace(response.Value.GetProperty("description").GetString()));
            Assert.Equal(expectedContentType, content.Name);
            Assert.True(content.Value.GetProperty("schema").TryGetProperty("$ref", out _));
            AssertDocumentedHeader(response.Value, "X-Correlation-ID");
        }

        AssertDocumentedHeader(responses.GetProperty("401"), "WWW-Authenticate");
        AssertDocumentedHeader(responses.GetProperty("429"), "Retry-After");
    }

    [Fact]
    public async Task BalanceSuccess_DocumentsNoStoreHeaderAndRouteParameter()
    {
        using var document = await GetDocumentAsync();
        var operation = GetOperation(document, BalancePath, "get");
        var parameter = Assert.Single(operation.GetProperty("parameters").EnumerateArray());

        AssertDocumentedHeader(operation.GetProperty("responses").GetProperty("200"), "Cache-Control");
        Assert.Equal("idContaCorrente", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
        Assert.False(string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
        Assert.Equal(EvaAccountId, parameter.GetProperty("example").GetString());
    }

    [Fact]
    public async Task MovementRequestBody_IsDescribedAndReferencesTheDocumentedSchema()
    {
        using var document = await GetDocumentAsync();
        var requestBody = GetOperation(document, MovementPath, "post").GetProperty("requestBody");

        Assert.False(string.IsNullOrWhiteSpace(requestBody.GetProperty("description").GetString()));
        Assert.Equal(
            "#/components/schemas/CreateMovementRequest",
            requestBody.GetProperty("content").GetProperty("application/json")
                .GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Theory]
    [InlineData("CreateMovementRequest", "idContaCorrente,idRequisicao,tipoMovimento,valor")]
    [InlineData("CreateMovementHttpResponse", "idMovimento")]
    [InlineData("GetBalanceHttpResponse", "dataHoraConsulta,nomeTitular,numeroContaCorrente,saldoAtual")]
    [InlineData("ProblemDetails", "code,correlationId,detail,errors,instance,status,title,traceId,type")]
    public async Task Schema_EveryAttributeHasDescriptionAndExample(string schemaName, string expectedProperties)
    {
        using var document = await GetDocumentAsync();
        var schema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(schemaName);
        var properties = schema.GetProperty("properties").EnumerateObject().ToArray();

        Assert.False(string.IsNullOrWhiteSpace(schema.GetProperty("description").GetString()));
        Assert.Equal(
            expectedProperties.Split(','),
            properties.Select(property => property.Name).Order(StringComparer.Ordinal));

        foreach (var property in properties)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString()),
                $"{schemaName}.{property.Name} sem descrição.");

            // "instance" é o único atributo sem exemplo: a API nunca o preenche.
            if (schemaName != "ProblemDetails" || property.Name != "instance")
            {
                Assert.True(
                    property.Value.TryGetProperty("example", out _),
                    $"{schemaName}.{property.Name} sem exemplo.");
            }
        }
    }

    [Theory]
    [InlineData("CreateMovementHttpResponse", "idMovimento")]
    [InlineData("GetBalanceHttpResponse", "nomeTitular")]
    [InlineData("GetBalanceHttpResponse", "dataHoraConsulta")]
    public async Task ResponseSchema_NonNullableTextIsNotDocumentedAsNullable(string schemaName, string propertyName)
    {
        using var document = await GetDocumentAsync();
        var property = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schemaName).GetProperty("properties").GetProperty(propertyName);

        Assert.False(property.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean());
    }

    [Theory]
    [InlineData(MovementPath, "post", "400", "INACTIVE_ACCOUNT,INVALID_ACCOUNT,INVALID_TYPE,INVALID_VALUE,VALIDATION_ERROR")]
    [InlineData(MovementPath, "post", "401", "UNAUTHENTICATED")]
    [InlineData(MovementPath, "post", "409", "IDEMPOTENCY_CONFLICT")]
    [InlineData(MovementPath, "post", "413", "PAYLOAD_TOO_LARGE")]
    [InlineData(MovementPath, "post", "415", "UNSUPPORTED_MEDIA_TYPE")]
    [InlineData(MovementPath, "post", "429", "RATE_LIMIT_EXCEEDED")]
    [InlineData(MovementPath, "post", "500", "INTERNAL_ERROR")]
    [InlineData(MovementPath, "post", "504", "REQUEST_TIMEOUT")]
    [InlineData(BalancePath, "get", "400", "INACTIVE_ACCOUNT,INVALID_ACCOUNT,VALIDATION_ERROR")]
    [InlineData(BalancePath, "get", "401", "UNAUTHENTICATED")]
    [InlineData(BalancePath, "get", "429", "RATE_LIMIT_EXCEEDED")]
    [InlineData(BalancePath, "get", "500", "INTERNAL_ERROR")]
    [InlineData(BalancePath, "get", "504", "REQUEST_TIMEOUT")]
    public async Task ErrorResponse_HasNamedExamplesForEverySituation(
        string path,
        string method,
        string statusCode,
        string expectedExamples)
    {
        using var document = await GetDocumentAsync();
        var examples = GetExamples(document, path, method, statusCode);

        Assert.Equal(
            expectedExamples.Split(','),
            examples.EnumerateObject().Select(example => example.Name).Order(StringComparer.Ordinal));
        Assert.All(
            examples.EnumerateObject(),
            example => Assert.False(string.IsNullOrWhiteSpace(example.Value.GetProperty("summary").GetString())));
    }

    [Theory]
    [InlineData(MovementPath, "400", "INVALID_VALUE")]
    [InlineData(MovementPath, "400", "INVALID_TYPE")]
    [InlineData(MovementPath, "400", "INVALID_ACCOUNT")]
    [InlineData(MovementPath, "400", "INACTIVE_ACCOUNT")]
    [InlineData(MovementPath, "400", "VALIDATION_ERROR")]
    [InlineData(MovementPath, "401", "UNAUTHENTICATED")]
    [InlineData(MovementPath, "409", "IDEMPOTENCY_CONFLICT")]
    [InlineData(MovementPath, "413", "PAYLOAD_TOO_LARGE")]
    [InlineData(MovementPath, "415", "UNSUPPORTED_MEDIA_TYPE")]
    [InlineData(MovementPath, "429", "RATE_LIMIT_EXCEEDED")]
    [InlineData(MovementPath, "500", "INTERNAL_ERROR")]
    [InlineData(MovementPath, "504", "REQUEST_TIMEOUT")]
    [InlineData(BalancePath, "400", "INVALID_ACCOUNT")]
    [InlineData(BalancePath, "400", "INACTIVE_ACCOUNT")]
    [InlineData(BalancePath, "400", "VALIDATION_ERROR")]
    [InlineData(BalancePath, "401", "UNAUTHENTICATED")]
    [InlineData(BalancePath, "429", "RATE_LIMIT_EXCEEDED")]
    [InlineData(BalancePath, "500", "INTERNAL_ERROR")]
    [InlineData(BalancePath, "504", "REQUEST_TIMEOUT")]
    public async Task ErrorExample_MatchesTheRealResponse(string path, string statusCode, string exampleName)
    {
        using var document = await GetDocumentAsync();
        var method = path == MovementPath ? "post" : "get";
        var example = JsonNode.Parse(
            GetExamples(document, path, method, statusCode).GetProperty(exampleName).GetProperty("value").GetRawText())!
            .AsObject();

        using var response = path == MovementPath
            ? await ProduceMovementErrorAsync(exampleName)
            : await ProduceBalanceErrorAsync(exampleName);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var actual = JsonNode.Parse(body)!.AsObject();

        Assert.Equal(statusCode, ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(ProblemContentType, response.Content.Headers.ContentType?.MediaType);

        // O exemplo documenta exatamente os atributos devolvidos; só os identificadores variam por requisição.
        Assert.Equal(
            example.Select(property => property.Key).Order(StringComparer.Ordinal),
            actual.Select(property => property.Key).Order(StringComparer.Ordinal));

        foreach (var (name, expectedValue) in example)
        {
            if (name is "traceId" or "correlationId")
            {
                continue;
            }

            Assert.True(
                JsonNode.DeepEquals(expectedValue, actual[name]),
                $"O atributo '{name}' do exemplo {exampleName} diverge da resposta real: {actual[name]?.ToJsonString()}");
        }
    }

    private async Task<HttpResponseMessage> ProduceMovementErrorAsync(string exampleName)
    {
        switch (exampleName)
        {
            case "INVALID_VALUE":
                return await PostMovementAsync(Track(new SecurityWebApplicationFactory()), amount: 0m);
            case "INVALID_TYPE":
                return await PostMovementAsync(Track(new SecurityWebApplicationFactory()), movementType: "X");
            case "INVALID_ACCOUNT":
                return await PostMovementAsync(Track(new SecurityWebApplicationFactory()), accountId: "missing-account");
            case "INACTIVE_ACCOUNT":
                return await PostMovementAsync(
                    Track(new SecurityWebApplicationFactory()),
                    accountId: AmeenaInactiveAccountId,
                    subject: AmeenaSubject);
            case "VALIDATION_ERROR":
                return await PostMovementAsync(Track(new SecurityWebApplicationFactory()), accountId: new string('A', 38));
            case "UNAUTHENTICATED":
                return await PostMovementAsync(Track(new SecurityWebApplicationFactory()), subject: null);
            case "IDEMPOTENCY_CONFLICT":
                {
                    var factory = Track(new SecurityWebApplicationFactory());
                    var requestId = Guid.NewGuid().ToString("D");
                    using var first = await PostMovementAsync(factory, requestId: requestId, amount: 10m);
                    return await PostMovementAsync(factory, requestId: requestId, amount: 20m);
                }

            case "PAYLOAD_TOO_LARGE":
                {
                    var factory = Track(new SecurityWebApplicationFactory());
                    var client = factory.CreateAuthenticatedClient();
                    using var content = new StringContent(
                        $$"""{"padding":"{{new string('A', 5000)}}"}""",
                        Encoding.UTF8,
                        "application/json");
                    return await client.PostAsync(MovementPath, content, TestContext.Current.CancellationToken);
                }

            case "UNSUPPORTED_MEDIA_TYPE":
                {
                    var factory = Track(new SecurityWebApplicationFactory());
                    var client = factory.CreateAuthenticatedClient();
                    using var content = new StringContent("texto", Encoding.UTF8, "text/plain");
                    return await client.PostAsync(MovementPath, content, TestContext.Current.CancellationToken);
                }

            case "RATE_LIMIT_EXCEEDED":
                {
                    var factory = Track(new SecurityWebApplicationFactory(
                        new Dictionary<string, string?> { ["Movement:EndpointPermitLimit"] = "1" }));
                    using var first = await PostMovementAsync(factory);
                    return await PostMovementAsync(factory);
                }

            case "INTERNAL_ERROR":
                return await PostMovementAsync(CreateMovementFactory(
                    (_, _) => throw new InvalidOperationException("falha interna de teste")));
            case "REQUEST_TIMEOUT":
                return await PostMovementAsync(CreateMovementFactory(
                    async (_, cancellationToken) =>
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                        throw new InvalidOperationException("Fluxo inalcançável.");
                    },
                    timeoutSeconds: 1));
            default:
                throw new ArgumentOutOfRangeException(nameof(exampleName), exampleName, "Exemplo sem cenário.");
        }
    }

    private async Task<HttpResponseMessage> ProduceBalanceErrorAsync(string exampleName)
    {
        switch (exampleName)
        {
            case "INVALID_ACCOUNT":
                return await GetBalanceAsync(Track(new SecurityWebApplicationFactory()), "missing-account");
            case "INACTIVE_ACCOUNT":
                return await GetBalanceAsync(
                    Track(new SecurityWebApplicationFactory()),
                    AmeenaInactiveAccountId,
                    AmeenaSubject);
            case "VALIDATION_ERROR":
                return await GetBalanceAsync(Track(new SecurityWebApplicationFactory()), new string('A', 38));
            case "UNAUTHENTICATED":
                return await GetBalanceAsync(Track(new SecurityWebApplicationFactory()), EvaAccountId, subject: null);
            case "RATE_LIMIT_EXCEEDED":
                {
                    var factory = Track(new SecurityWebApplicationFactory(
                        new Dictionary<string, string?> { ["Balance:EndpointPermitLimit"] = "1" }));
                    using var first = await GetBalanceAsync(factory, EvaAccountId);
                    return await GetBalanceAsync(factory, EvaAccountId);
                }

            case "INTERNAL_ERROR":
                {
                    var store = Substitute.For<IBalanceQueryStore>();
                    store.GetBalance(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                        .Throws(new InvalidOperationException("falha interna de teste"));
                    return await GetBalanceAsync(CreateBalanceFactory(store), EvaAccountId);
                }

            case "REQUEST_TIMEOUT":
                {
                    var store = Substitute.For<IBalanceQueryStore>();
                    store.GetBalance(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                        .Returns(call =>
                        {
                            var cancellationToken = call.Arg<CancellationToken>();
                            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                            cancellationToken.ThrowIfCancellationRequested();
                            return new AccountBalance(456, "Eva Woodward", 0.00m);
                        });
                    return await GetBalanceAsync(CreateBalanceFactory(store, timeoutSeconds: 1), EvaAccountId);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(exampleName), exampleName, "Exemplo sem cenário.");
        }
    }

    private SecurityWebApplicationFactory CreateMovementFactory(
        Func<Questao5.Application.Commands.Requests.CreateMovementCommand, CancellationToken,
            Task<Questao5.Application.Commands.Responses.CreateMovementResponse>> handler,
        int timeoutSeconds = 5)
    {
        return Track(new SecurityWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Movement:TimeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            services =>
            {
                services.RemoveAll<IMediator>();
                services.AddSingleton<IMediator>(new ControlledMovementMediator(handler));
            }));
    }

    private SecurityWebApplicationFactory CreateBalanceFactory(IBalanceQueryStore store, int timeoutSeconds = 5)
    {
        return Track(new SecurityWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Balance:TimeoutSeconds"] = timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            services =>
            {
                services.RemoveAll<IBalanceQueryStore>();
                services.AddSingleton(store);
            }));
    }

    private static Task<HttpResponseMessage> PostMovementAsync(
        SecurityWebApplicationFactory factory,
        string? requestId = null,
        string accountId = EvaAccountId,
        decimal amount = 10.25m,
        string movementType = "C",
        string? subject = TestTokenIssuer.DefaultSubject)
    {
        var client = subject is null ? factory.CreateAnonymousClient() : factory.CreateAuthenticatedClient(subject);

        return client.PostAsJsonAsync(
            MovementPath,
            new
            {
                idRequisicao = requestId ?? Guid.NewGuid().ToString("D"),
                idContaCorrente = accountId,
                valor = amount,
                tipoMovimento = movementType
            },
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> GetBalanceAsync(
        SecurityWebApplicationFactory factory,
        string routeSegment,
        string? subject = TestTokenIssuer.DefaultSubject)
    {
        var client = subject is null ? factory.CreateAnonymousClient() : factory.CreateAuthenticatedClient(subject);

        return client.GetAsync($"/api/v1/contas/{routeSegment}/saldo", TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        foreach (var factory in factories)
        {
            factory.Dispose();
        }
    }

    private SecurityWebApplicationFactory Track(SecurityWebApplicationFactory factory)
    {
        factories.Add(factory);
        return factory;
    }

    private static async Task<JsonDocument> GetDocumentAsync()
    {
        using var factory = new SwaggerWebApplicationFactory();
        return await factory.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
    }

    private static JsonElement GetOperation(JsonDocument document, string path, string method)
    {
        return document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);
    }

    private static JsonElement GetExamples(JsonDocument document, string path, string method, string statusCode)
    {
        return GetOperation(document, path, method)
            .GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty(ProblemContentType)
            .GetProperty("examples");
    }

    private static void AssertDocumentedHeader(JsonElement response, string headerName)
    {
        var header = response.GetProperty("headers").GetProperty(headerName);

        Assert.False(string.IsNullOrWhiteSpace(header.GetProperty("description").GetString()));
        Assert.True(header.TryGetProperty("example", out _));
    }
}
