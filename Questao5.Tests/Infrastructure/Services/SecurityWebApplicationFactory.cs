using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
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
        TokenIssuer = new TestTokenIssuer();
    }

    public TestLoggerProvider LoggerProvider { get; }

    public TestTokenIssuer TokenIssuer { get; }

    public string DatabasePath => database.DatabasePath;

    public HttpClient CreateAnonymousClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public HttpClient CreateAuthenticatedClient(string subject = TestTokenIssuer.DefaultSubject)
    {
        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            TokenIssuer.CreateToken(subject));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs lê a configuração antes de ConfigureAppConfiguration ser aplicado;
        // somente UseSetting garante que estes valores sejam vistos na inicialização.
        var values = new Dictionary<string, string?>(configuration, StringComparer.OrdinalIgnoreCase)
        {
            ["DatabaseName"] = database.ConnectionString,
            ["Jwt:MetadataAddress"] = TestTokenIssuer.MetadataAddress,
            ["Jwt:Issuer"] = TestTokenIssuer.Issuer,
            ["Jwt:Audience"] = TestTokenIssuer.Audience
        };

        foreach (var value in values)
        {
            builder.UseSetting(value.Key, value.Value);
        }

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
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

            // Substitui a busca do JWKS do emissor por chaves locais, sem depender de rede ou contêiner.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var issuerConfiguration = new OpenIdConnectConfiguration { Issuer = TestTokenIssuer.Issuer };
                issuerConfiguration.SigningKeys.Add(TokenIssuer.SigningKey);
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(issuerConfiguration);
            });
            configureServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            TokenIssuer.Dispose();
            database.Dispose();
        }
    }
}
