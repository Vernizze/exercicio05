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
    private readonly IReadOnlyDictionary<string, string?> configuration;
    private readonly Action<IServiceCollection>? configureServices;

    public SecurityWebApplicationFactory(
        IReadOnlyDictionary<string, string?>? configuration = null,
        Action<IServiceCollection>? configureServices = null)
    {
        this.configuration = configuration ?? new Dictionary<string, string?>();
        this.configureServices = configureServices;
        LoggerProvider = new TestLoggerProvider();
    }

    public TestLoggerProvider LoggerProvider { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var value in configuration)
        {
            builder.UseSetting(value.Key, value.Value);
        }

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var values = new Dictionary<string, string?>(configuration, StringComparer.OrdinalIgnoreCase)
            {
                ["DatabaseName"] = database.ConnectionString
            };
            configurationBuilder.AddInMemoryCollection(values);
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
            configureServices?.Invoke(services);
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