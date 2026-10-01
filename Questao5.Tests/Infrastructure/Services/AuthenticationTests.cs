using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Questao5.Infrastructure.Services.Correlation;
using Questao5.Infrastructure.Services.Security;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class AuthenticationTests
{
    private const string ActiveAccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";

    [Fact]
    public async Task Request_WithoutToken_Returns401ProblemDetailsWithChallengeAndCorrelation()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await PostMovementAsync(client);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.Equal("UNAUTHENTICATED", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            Assert.Single(response.Headers.GetValues(CorrelationConstants.HeaderName)),
            document.RootElement.GetProperty(CorrelationConstants.ProblemDetailsExtensionName).GetString());
        AssertAuthenticationFailure(factory, "MissingToken");
    }

    [Fact]
    public async Task Request_WithoutToken_DoesNotPersistMovement()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var anonymousClient = factory.CreateAnonymousClient();
        using var authenticatedClient = factory.CreateAuthenticatedClient();
        var requestId = Guid.NewGuid().ToString("D");

        using var rejected = await PostMovementAsync(anonymousClient, requestId);
        using var accepted = await PostMovementAsync(authenticatedClient, requestId);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Contains(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5100);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5101);
    }

    [Fact]
    public async Task Request_WithValidToken_IsAccepted()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();

        using var response = await PostMovementAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5300);
    }

    [Fact]
    public async Task Request_WithExpiredToken_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();
        var now = DateTimeOffset.UtcNow;
        var token = factory.TokenIssuer.CreateToken(
            notBefore: now.AddMinutes(-20),
            expires: now.AddMinutes(-10));

        await AssertRejectedAsync(factory, token, "ExpiredToken");
    }

    [Fact]
    public async Task Request_WithTokenNotYetValid_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();
        var now = DateTimeOffset.UtcNow;
        var token = factory.TokenIssuer.CreateToken(
            notBefore: now.AddMinutes(10),
            expires: now.AddMinutes(20));

        await AssertRejectedAsync(factory, token, "InvalidToken");
    }

    [Fact]
    public async Task Request_WithTokenSignedByAnotherKey_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var otherRsa = RSA.Create(2048);
        var token = factory.TokenIssuer.CreateToken(
            signingKey: new RsaSecurityKey(otherRsa) { KeyId = "test-signing-key" });

        await AssertRejectedAsync(factory, token, "InvalidToken");
    }

    [Fact]
    public async Task Request_WithWrongIssuer_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();
        var token = factory.TokenIssuer.CreateToken(issuer: "https://other-issuer.test/default");

        await AssertRejectedAsync(factory, token, "InvalidToken");
    }

    [Fact]
    public async Task Request_WithWrongAudience_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();
        var token = factory.TokenIssuer.CreateToken(audience: "other-api");

        await AssertRejectedAsync(factory, token, "InvalidToken");
    }

    [Fact]
    public async Task Request_WithAlgorithmOutsideAllowlist_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();
        var token = factory.TokenIssuer.CreateToken(algorithm: SecurityAlgorithms.RsaSha384);

        await AssertRejectedAsync(factory, token, "InvalidToken");
    }

    [Fact]
    public async Task Request_WithUnsignedToken_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();
        var token = TestTokenIssuer.CreateUnsignedToken();

        await AssertRejectedAsync(factory, token, "InvalidToken");
    }

    [Fact]
    public async Task Request_WithMalformedToken_Returns401()
    {
        using var factory = new SecurityWebApplicationFactory();

        await AssertRejectedAsync(factory, "not-a-jwt", "InvalidToken");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("eva.woodward")]
    [InlineData("04b276dc0f454efcbffc911110198733")]
    public async Task Request_WithTokenWithoutUuidSubject_Returns401(string? subject)
    {
        using var factory = new SecurityWebApplicationFactory();
        var token = factory.TokenIssuer.CreateToken(subject: subject);

        await AssertRejectedAsync(factory, token, "InvalidSubject");
    }

    [Fact]
    public async Task Rejection_DoesNotRevealReasonOrLogCredentials()
    {
        using var factory = new SecurityWebApplicationFactory();
        var now = DateTimeOffset.UtcNow;
        var token = factory.TokenIssuer.CreateToken(
            notBefore: now.AddMinutes(-20),
            expires: now.AddMinutes(-10));
        using var client = CreateClientWithToken(factory, token);

        using var response = await PostMovementAsync(client);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("expir", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExpiredToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("IDX", body, StringComparison.Ordinal);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).ToString());

        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5300);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain(token, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(TestTokenIssuer.DefaultSubject, entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IDX", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_WhenIssuerMetadataIsUnavailable_Returns401InsteadOfServerError()
    {
        using var factory = new SecurityWebApplicationFactory(configureServices: services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    "http://127.0.0.1:9/default/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = false })));
        var token = factory.TokenIssuer.CreateToken();

        await AssertRejectedAsync(factory, token, "InvalidToken");
    }

    [Fact]
    public async Task EveryEndpoint_RequiresAuthenticationByDefault()
    {
        using var factory = new SecurityWebApplicationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.GetAsync(
            "/security-tests/success",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:MetadataAddress", "")]
    [InlineData("Jwt:MetadataAddress", "not-a-url")]
    [InlineData("Jwt:MetadataAddress", "http://issuer.test/default/.well-known/openid-configuration")]
    public void Startup_WithInvalidJwtConfiguration_Fails(string key, string value)
    {
        // As duas fábricas são descartadas: a derivada não descarta o banco temporário da original.
        using var baseFactory = new SecurityWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void JwtOptions_HttpMetadataWithHttpsRequirementDisabled_IsAccepted()
    {
        var options = new JwtAuthenticationOptions
        {
            MetadataAddress = "http://issuer.test/default/.well-known/openid-configuration",
            Issuer = "http://issuer.test/default",
            Audience = "questao5-api",
            RequireHttpsMetadata = false
        };

        options.Validate();
    }

    private static async Task AssertRejectedAsync(
        SecurityWebApplicationFactory factory,
        string token,
        string expectedReason)
    {
        using var client = CreateClientWithToken(factory, token);

        using var response = await PostMovementAsync(client);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("UNAUTHENTICATED", document.RootElement.GetProperty("code").GetString());
        AssertAuthenticationFailure(factory, expectedReason);
        Assert.DoesNotContain(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5100);
    }

    private static void AssertAuthenticationFailure(SecurityWebApplicationFactory factory, string expectedReason)
    {
        var entry = Assert.Single(factory.LoggerProvider.Entries, entry => entry.EventId.Id == 5300);
        Assert.Contains($"Reason: {expectedReason};", entry.Message, StringComparison.Ordinal);
    }

    private static HttpClient CreateClientWithToken(SecurityWebApplicationFactory factory, string token)
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> PostMovementAsync(HttpClient client, string? requestId = null)
    {
        return client.PostAsJsonAsync(
            "/api/v1/movimentos",
            new
            {
                idRequisicao = requestId ?? Guid.NewGuid().ToString("D"),
                idContaCorrente = ActiveAccountId,
                valor = 10.25m,
                tipoMovimento = "C"
            },
            TestContext.Current.CancellationToken);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonDocument.Parse(body);
    }
}
