using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Questao5.Infrastructure.Services.Security
{
    public static class JwtAuthenticationExtensions
    {
        public const string SubjectClaimType = "sub";

        private const string MissingTokenReason = "MissingToken";
        private const string InvalidTokenReason = "InvalidToken";
        private const string ExpiredTokenReason = "ExpiredToken";
        private const string InvalidSubjectReason = "InvalidSubject";

        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            JwtAuthenticationOptions jwtOptions)
        {
            ArgumentNullException.ThrowIfNull(jwtOptions);
            jwtOptions.Validate();

            services.AddSingleton<SecurityLogger>();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.MetadataAddress = jwtOptions.MetadataAddress!;
                    options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = SubjectClaimType
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = ValidateSubject,
                        OnChallenge = WriteUnauthenticatedResponseAsync
                    };
                });
            services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build());

            return services;
        }

        private static Task ValidateSubject(TokenValidatedContext context)
        {
            var subject = context.Principal?.FindFirst(SubjectClaimType)?.Value;

            if (!Guid.TryParseExact(subject, "D", out _))
            {
                context.Fail(new InvalidTokenSubjectException());
            }

            return Task.CompletedTask;
        }

        private static async Task WriteUnauthenticatedResponseAsync(JwtBearerChallengeContext context)
        {
            context.HandleResponse();

            var httpContext = context.HttpContext;
            var securityLogger = httpContext.RequestServices.GetRequiredService<SecurityLogger>();
            securityLogger.AuthenticationFailed(
                httpContext.TraceIdentifier,
                GetFailureReason(context.AuthenticateFailure));

            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;

            var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Autenticação necessária.",
                    Detail = "A solicitação não possui credenciais válidas.",
                    Extensions =
                    {
                        ["code"] = "UNAUTHENTICATED"
                    }
                }
            }).ConfigureAwait(false);
        }

        private static string GetFailureReason(Exception? failure)
        {
            return failure switch
            {
                null => MissingTokenReason,
                SecurityTokenExpiredException => ExpiredTokenReason,
                InvalidTokenSubjectException => InvalidSubjectReason,
                _ => InvalidTokenReason
            };
        }
    }
}
