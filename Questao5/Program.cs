using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Questao5.Application.Balances;
using Questao5.Application.Movements;
using Questao5.Domain.Repositories;
using Questao5.Infrastructure.Database;
using Questao5.Infrastructure.Services.Balances;
using Questao5.Infrastructure.Services.Correlation;
using Questao5.Infrastructure.Services.Errors;
using Questao5.Infrastructure.Services.Identifiers;
using Questao5.Infrastructure.Services.Movements;
using Questao5.Infrastructure.Services.OpenApi;
using Questao5.Infrastructure.Services.Security;
using Questao5.Infrastructure.Sqlite;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

const string ReconcileBalancesArgument = "--reconciliar-saldos";

var builder = WebApplication.CreateBuilder(args);
var movementOptions = builder.Configuration
    .GetSection(MovementOperationalOptions.SectionName)
    .Get<MovementOperationalOptions>() ?? new MovementOperationalOptions();

if (movementOptions.TimeoutSeconds <= 0 ||
    movementOptions.EndpointPermitLimit <= 0 ||
    movementOptions.GlobalPermitLimit <= 0 ||
    movementOptions.WindowSeconds <= 0 ||
    movementOptions.ConcurrencyPermitLimit <= 0)
{
    throw new InvalidOperationException("Os limites operacionais de movimentação devem ser maiores que zero.");
}

var balanceOptions = builder.Configuration
    .GetSection(BalanceOperationalOptions.SectionName)
    .Get<BalanceOperationalOptions>() ?? new BalanceOperationalOptions();

if (balanceOptions.TimeoutSeconds <= 0 ||
    balanceOptions.EndpointPermitLimit <= 0 ||
    balanceOptions.WindowSeconds <= 0 ||
    balanceOptions.ConcurrencyPermitLimit <= 0)
{
    throw new InvalidOperationException("Os limites operacionais da consulta de saldo devem ser maiores que zero.");
}

var jwtOptions = builder.Configuration
    .GetSection(JwtAuthenticationOptions.SectionName)
    .Get<JwtAuthenticationOptions>() ?? new JwtAuthenticationOptions();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetailsFactory = context.HttpContext.RequestServices
                .GetRequiredService<ProblemDetailsFactory>();
            var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                context.HttpContext,
                context.ModelState,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Um ou mais campos são inválidos.");

            problemDetails.Extensions[CorrelationConstants.ProblemDetailsExtensionName] =
                context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions[CorrelationConstants.ProblemDetailsExtensionName] =
            context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddJwtAuthentication(jwtOptions);
builder.Services.AddSingleton(movementOptions);
builder.Services.AddSingleton<MovementLogger>();
builder.Services.AddSingleton(balanceOptions);
builder.Services.AddSingleton<BalanceLogger>();
builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy(BalancePolicyNames.RequestTimeout, new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(balanceOptions.TimeoutSeconds),
        TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
        WriteTimeoutResponse = async context =>
        {
            var balanceLogger = context.RequestServices.GetRequiredService<BalanceLogger>();
            balanceLogger.LimitExceeded(context.TraceIdentifier, "BalanceTimeout");

            var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status504GatewayTimeout,
                    Title = "A operação excedeu o tempo limite.",
                    Detail = "Não foi possível concluir a consulta de saldo dentro do tempo permitido.",
                    Extensions =
                    {
                        ["code"] = "REQUEST_TIMEOUT"
                    }
                }
            }).ConfigureAwait(false);
        }
    });
    options.AddPolicy(MovementPolicyNames.RequestTimeout, new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(movementOptions.TimeoutSeconds),
        TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
        WriteTimeoutResponse = async context =>
        {
            var movementLogger = context.RequestServices.GetRequiredService<MovementLogger>();
            movementLogger.LimitExceeded(context.TraceIdentifier, "MovementTimeout");

            var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status504GatewayTimeout,
                    Title = "A operação excedeu o tempo limite.",
                    Detail = "Não foi possível concluir a movimentação dentro do tempo permitido.",
                    Extensions =
                    {
                        ["code"] = "REQUEST_TIMEOUT"
                    }
                }
            }).ConfigureAwait(false);
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    var globalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartition(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = movementOptions.GlobalPermitLimit,
                Window = TimeSpan.FromSeconds(movementOptions.WindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    var movementFrequencyLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        IsMovementRequest(context)
            ? RateLimitPartition.GetFixedWindowLimiter(
                GetIdentityPartition(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = movementOptions.EndpointPermitLimit,
                    Window = TimeSpan.FromSeconds(movementOptions.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                })
            : RateLimitPartition.GetNoLimiter("NonMovement"));
    var movementConcurrencyLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        IsMovementRequest(context)
            ? RateLimitPartition.GetConcurrencyLimiter(
                "Movement",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = movementOptions.ConcurrencyPermitLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                })
            : RateLimitPartition.GetNoLimiter("NonMovement"));
    var balanceFrequencyLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        IsBalanceRequest(context)
            ? RateLimitPartition.GetFixedWindowLimiter(
                GetIdentityPartition(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = balanceOptions.EndpointPermitLimit,
                    Window = TimeSpan.FromSeconds(balanceOptions.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                })
            : RateLimitPartition.GetNoLimiter("NonBalance"));
    var balanceConcurrencyLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        IsBalanceRequest(context)
            ? RateLimitPartition.GetConcurrencyLimiter(
                "Balance",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = balanceOptions.ConcurrencyPermitLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                })
            : RateLimitPartition.GetNoLimiter("NonBalance"));

    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        globalLimiter,
        movementFrequencyLimiter,
        movementConcurrencyLimiter,
        balanceFrequencyLimiter,
        balanceConcurrencyLimiter);
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;

        if (IsBalanceRequest(httpContext))
        {
            var balanceLogger = httpContext.RequestServices.GetRequiredService<BalanceLogger>();
            balanceLogger.LimitExceeded(httpContext.TraceIdentifier, "Balance");
        }
        else
        {
            var limitName = IsMovementRequest(httpContext) ? "Movement" : "Global";
            var movementLogger = httpContext.RequestServices.GetRequiredService<MovementLogger>();
            movementLogger.LimitExceeded(httpContext.TraceIdentifier, limitName);
        }

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "O limite de requisições foi excedido.",
                Detail = "A solicitação não pode ser processada neste momento.",
                Extensions =
                {
                    ["code"] = "RATE_LIMIT_EXCEEDED"
                }
            }
        }).ConfigureAwait(false);
    };
});

builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMovementIdGenerator, GuidMovementIdGenerator>();
builder.Services.AddSingleton<IBalanceReconciler, BalanceReconciler>();

builder.Services.AddSingleton(new DatabaseConfig(builder.Configuration["DatabaseName"]));
builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
builder.Services.AddSingleton<IDatabaseBootstrap, DatabaseBootstrap>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Questão 5 — API de conta corrente",
        Version = "v1",
        Description =
            "Movimentação e consulta de saldo de conta corrente.\n\n" +
            "**Autenticação.** Os dois endpoints exigem um JWT no header `Authorization: Bearer <token>`. " +
            "A claim `sub` identifica o correntista, e cada conta só pode ser movimentada ou consultada pelo seu titular.\n\n" +
            "**Erros.** As respostas de erro usam `application/problem+json`. Falhas de regra de negócio trazem " +
            "`code` com o tipo da falha e `detail` com a mensagem; erros estruturais trazem `errors` por campo.\n\n" +
            "**Correlação.** Toda resposta devolve o header `X-Correlation-ID`. O cliente pode enviar o seu " +
            "(até 64 caracteres entre letras, números, ponto, hífen e sublinhado); caso contrário, a API gera um.\n\n" +
            "**Idempotência.** Na movimentação, `idRequisicao` é a chave de idempotência: repetir a mesma " +
            "requisição devolve o mesmo resultado, sem criar outro movimento."
    });

    var xmlDocumentationPath = Path.Combine(
        AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    options.IncludeXmlComments(xmlDocumentationPath, includeControllerXmlComments: true);
    options.SupportNonNullableReferenceTypes();
    options.OperationFilter<ResponseDocumentationOperationFilter>();
    options.SchemaFilter<ProblemDetailsSchemaFilter>();

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT emitido pelo emissor configurado; a claim sub identifica o correntista."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = []
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseRateLimiter();
app.UseRequestTimeouts();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Services.GetRequiredService<IDatabaseBootstrap>().Setup();

// Reconciliação sob demanda: compara a projeção de saldo com os movimentos e encerra sem iniciar o servidor.
if (args.Contains(ReconcileBalancesArgument, StringComparer.Ordinal))
{
    var reconciliation = app.Services.GetRequiredService<IBalanceReconciler>().Reconcile();

    foreach (var accountFingerprint in reconciliation.DivergentAccountFingerprints)
    {
        Console.Error.WriteLine($"Saldo divergente. AccountFingerprint: {accountFingerprint}");
    }

    Console.Out.WriteLine(reconciliation.IsConsistent
        ? "Reconciliação concluída: nenhuma divergência."
        : $"Reconciliação concluída: {reconciliation.DivergentAccountFingerprints.Count} conta(s) divergente(s).");

    return reconciliation.IsConsistent ? 0 : 1;
}

app.Run();

return 0;

static string GetClientPartition(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

// O limite específico é contado por correntista autenticado; sem identidade válida, por IP.
static string GetIdentityPartition(HttpContext context)
{
    return context.User.TryGetAccountHolderId(out var accountHolderId)
        ? $"sub:{accountHolderId}"
        : $"ip:{GetClientPartition(context)}";
}

static bool IsBalanceRequest(HttpContext context)
{
    var path = context.Request.Path.Value;

    return HttpMethods.IsGet(context.Request.Method) &&
        path is not null &&
        path.StartsWith("/api/v1/contas/", StringComparison.OrdinalIgnoreCase) &&
        path.EndsWith("/saldo", StringComparison.OrdinalIgnoreCase);
}

static bool IsMovementRequest(HttpContext context)
{
    return HttpMethods.IsPost(context.Request.Method) &&
        context.Request.Path.Equals("/api/v1/movimentos", StringComparison.OrdinalIgnoreCase);
}

public partial class Program;

