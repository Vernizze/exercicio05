using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class EndpointInventoryTests
{
    [Fact]
    public async Task Swagger_exposes_only_the_movement_and_balance_endpoints()
    {
        using var factory = new SwaggerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var operations = document.RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["GET /api/v1/contas/{idContaCorrente}/saldo", "POST /api/v1/movimentos"],
            operations);
    }

    [Fact]
    public async Task Swagger_documents_the_balance_contract_and_its_authentication_failure()
    {
        using var factory = new SwaggerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));

        var responses = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/contas/{idContaCorrente}/saldo")
            .GetProperty("get")
            .GetProperty("responses");
        var schema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("GetBalanceHttpResponse")
            .GetProperty("properties");

        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("401", out _));
        Assert.Equal(
            ["dataHoraConsulta", "nomeTitular", "numeroContaCorrente", "saldoAtual"],
            schema.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Swagger_declares_bearer_security_for_the_movement_endpoint()
    {
        using var factory = new SwaggerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));

        var scheme = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/movimentos")
            .GetProperty("post");

        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.True(document.RootElement.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
    }
}
