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

    private sealed class SwaggerWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly TemporarySqliteDatabase database = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
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
