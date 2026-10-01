using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Questao5.Infrastructure.Services.Correlation;

namespace Questao5.Controllers.Filters
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MovementRequestBodySizeLimitAttribute : Attribute, IAsyncResourceFilter, IOrderedFilter
    {
        public const long MaximumBodySize = 4 * 1024;

        public int Order => int.MinValue;

        public async Task OnResourceExecutionAsync(
            ResourceExecutingContext context,
            ResourceExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            if (context.HttpContext.Request.ContentLength is > MaximumBodySize)
            {
                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "O corpo da requisição excede o limite permitido.",
                    Detail = "O corpo da requisição deve possuir no máximo 4 KiB."
                };
                problemDetails.Extensions[CorrelationConstants.ProblemDetailsExtensionName] =
                    context.HttpContext.TraceIdentifier;

                context.Result = new ObjectResult(problemDetails)
                {
                    StatusCode = StatusCodes.Status413PayloadTooLarge,
                    ContentTypes = { "application/problem+json" }
                };
                return;
            }

            await next().ConfigureAwait(false);
        }
    }
}