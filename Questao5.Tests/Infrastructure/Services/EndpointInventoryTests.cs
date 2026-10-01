using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Questao5.Tests.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class EndpointInventoryTests
{
    [Fact]
    public async Task Swagger_exposes_only_the_movement_endpoint()
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
        var paths = document.RootElement.GetProperty("paths");
        var path = Assert.Single(paths.EnumerateObject());
        var operation = Assert.Single(path.Value.EnumerateObject());

        Assert.Equal("/api/v1/movimentos", path.Name);
        Assert.Equal("post", operation.Name);
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

    private sealed class SwaggerWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly TemporarySqliteDatabase database = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            // Program.cs lê DatabaseName antes de ConfigureAppConfiguration ser aplicado.
            builder.UseSetting("DatabaseName", database.ConnectionString);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseName"] = database.ConnectionString
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                database.Dispose();
            }
        }
    }
}
