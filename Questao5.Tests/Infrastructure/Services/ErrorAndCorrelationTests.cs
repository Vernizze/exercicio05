using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Questao5.Infrastructure.Services.Correlation;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class ErrorAndCorrelationTests
{
    [Fact]
    public async Task Success_WithoutExternalCorrelationId_GeneratesAndReturnsIdentifier()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/security-tests/success",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        response.EnsureSuccessStatusCode();
        var headerValue = GetCorrelationHeader(response);
        Assert.NotEmpty(headerValue);
        Assert.Equal(headerValue, document.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Success_WithValidExternalCorrelationId_PreservesNormalizedIdentifier()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/security-tests/success");
        request.Headers.Add(CorrelationConstants.HeaderName, "  client-request_123.abc  ");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Equal("client-request_123.abc", GetCorrelationHeader(response));
    }

    [Fact]
    public async Task Success_WithUnsafeExternalCorrelationId_ReplacesIdentifier()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/security-tests/success");
        request.Headers.TryAddWithoutValidation(CorrelationConstants.HeaderName, "unsafe value/with-delimiters");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var headerValue = GetCorrelationHeader(response);
        Assert.NotEqual("unsafe value/with-delimiters", headerValue);
        Assert.Matches("^[A-Za-z0-9._-]{1,64}$", headerValue);
    }

    [Fact]
    public async Task Success_WithMultipleExternalCorrelationIds_ReplacesIdentifier()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/security-tests/success");
        request.Headers.Add(CorrelationConstants.HeaderName, ["first-id", "second-id"]);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var headerValue = GetCorrelationHeader(response);
        Assert.NotEqual("first-id", headerValue);
        Assert.NotEqual("second-id", headerValue);
        Assert.Matches("^[A-Za-z0-9._-]{1,64}$", headerValue);
    }

    [Fact]
    public async Task Validation_WithInvalidBody_ReturnsCorrelatedProblemDetails()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/security-tests/validation",
            new { value = "" },
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            GetCorrelationHeader(response),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task BusinessError_ReturnsStableCodeAndCorrelationId()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(
            "/security-tests/business-error",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("TEST_BUSINESS_RULE", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            GetCorrelationHeader(response),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
    }

    [Fact]
    public async Task UnexpectedError_DoesNotExposeSensitiveDetailsAndProducesSafeLog()
    {
        const string correlationId = "unexpected-error-test";
        using var factory = new SecurityWebApplicationFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/security-tests/unexpected-error");
        request.Headers.Add(CorrelationConstants.HeaderName, correlationId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(correlationId, GetCorrelationHeader(response));
        Assert.Equal(
            correlationId,
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        Assert.DoesNotContain("sensitive-value", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.OrdinalIgnoreCase);

        var logEntry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 9000);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, logEntry.Level);
        Assert.Contains(correlationId, logEntry.Message, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", logEntry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-value", logEntry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source", logEntry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", logEntry.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient(SecurityWebApplicationFactory factory)
    {
        return factory.CreateAuthenticatedClient();
    }

    private static string GetCorrelationHeader(HttpResponseMessage response)
    {
        return Assert.Single(response.Headers.GetValues(CorrelationConstants.HeaderName));
    }
}