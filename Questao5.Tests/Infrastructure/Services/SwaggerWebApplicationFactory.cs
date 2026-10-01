using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Questao5.Tests.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Services;

/// <summary>
/// Sobe a aplicação em Development, único ambiente em que o documento OpenAPI é publicado.
/// </summary>
internal sealed class SwaggerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly TemporarySqliteDatabase database = new();

    public async Task<JsonDocument> GetOpenApiDocumentAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/swagger/v1/swagger.json", cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

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
