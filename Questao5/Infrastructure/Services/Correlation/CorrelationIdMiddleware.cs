using System.Diagnostics;

namespace Questao5.Infrastructure.Services.Correlation
{
    public sealed class CorrelationIdMiddleware
    {
        private readonly RequestDelegate next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var correlationId = GetCorrelationId(context);
            context.TraceIdentifier = correlationId;
            Activity.Current?.SetTag("correlation.id", correlationId);

            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            await next(context).ConfigureAwait(false);
        }

        private static string GetCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(CorrelationConstants.HeaderName, out var values) &&
                values.Count == 1)
            {
                var candidate = values[0]?.Trim();

                if (IsValid(candidate))
                {
                    return candidate!;
                }
            }

            return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        }

        private static bool IsValid(string? candidate)
        {
            if (string.IsNullOrEmpty(candidate) || candidate.Length > CorrelationConstants.MaximumLength)
            {
                return false;
            }

            foreach (var character in candidate)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')
                {
                    return false;
                }
            }

            return true;
        }
    }
}