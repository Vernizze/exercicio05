using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questao5.Tests.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Services;

internal sealed class SecurityWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly TemporarySqliteDatabase database = new();

    public SecurityWebApplicationFactory()
    {
        LoggerProvider = new TestLoggerProvider();
    }

    public TestLoggerProvider LoggerProvider { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DatabaseName"] = database.ConnectionString
            });
        });
        builder.ConfigureLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddProvider(LoggerProvider);
        });
        builder.ConfigureServices(services =>
        {
            services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    var testAssembly = typeof(SecurityTestController).Assembly;

                    if (manager.ApplicationParts.All(part => part.Name != testAssembly.GetName().Name))
                    {
                        manager.ApplicationParts.Add(new AssemblyPart(testAssembly));
                    }
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