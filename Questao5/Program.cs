using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Questao5.Application.Movements;
using Questao5.Infrastructure.Database.CommandStore;
using Questao5.Infrastructure.Services.Correlation;
using Questao5.Infrastructure.Services.Errors;
using Questao5.Infrastructure.Services.Identifiers;
using Questao5.Infrastructure.Services.Movements;
using Questao5.Infrastructure.Services.Security;
using Questao5.Infrastructure.Sqlite;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

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
builder.Services.AddRequestTimeouts(options =>
{
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
                GetClientPartition(context),
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

    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        globalLimiter,
        movementFrequencyLimiter,
        movementConcurrencyLimiter);
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        var limitName = IsMovementRequest(httpContext) ? "Movement" : "Global";
        var movementLogger = httpContext.RequestServices.GetRequiredService<MovementLogger>();
        movementLogger.LimitExceeded(httpContext.TraceIdentifier, limitName);

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
builder.Services.AddSingleton<IMovementStore, MovementStore>();

builder.Services.AddSingleton(new DatabaseConfig(builder.Configuration["DatabaseName"]));
builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddSingleton<IDatabaseBootstrap, DatabaseBootstrap>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
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

app.Run();

static string GetClientPartition(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static bool IsMovementRequest(HttpContext context)
{
    return HttpMethods.IsPost(context.Request.Method) &&
        context.Request.Path.Equals("/api/v1/movimentos", StringComparison.OrdinalIgnoreCase);
}

public partial class Program;

