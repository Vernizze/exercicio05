using Microsoft.OpenApi;
using Questao5.Infrastructure.Services.Correlation;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Questao5.Infrastructure.Services.OpenApi
{
    /// <summary>
    /// Acrescenta a cada operação o que os comentários XML não cobrem: exemplos nomeados das respostas
    /// de erro e os headers de resposta.
    /// </summary>
    public sealed class ResponseDocumentationOperationFilter : IOperationFilter
    {
        private const string ProblemContentType = "application/problem+json";

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(context);

            if (operation.Responses is null)
            {
                return;
            }

            var isMovement = HttpMethods.IsPost(context.ApiDescription.HttpMethod ?? string.Empty);

            foreach (var (statusCodeText, response) in operation.Responses)
            {
                if (response is not OpenApiResponse documentedResponse ||
                    !int.TryParse(statusCodeText, NumberStyles.None, CultureInfo.InvariantCulture, out var statusCode))
                {
                    continue;
                }

                AddHeaders(documentedResponse, statusCode, isMovement);

                var examples = isMovement
                    ? ProblemExamples.ForMovement(statusCode)
                    : ProblemExamples.ForBalance(statusCode);

                if (examples.Count > 0 &&
                    documentedResponse.Content is not null &&
                    documentedResponse.Content.TryGetValue(ProblemContentType, out var mediaType))
                {
                    mediaType.Examples = examples.ToDictionary(
                        example => example.Name,
                        IOpenApiExample (example) => new OpenApiExample
                        {
                            Summary = example.Summary,
                            Value = example.Value
                        },
                        StringComparer.Ordinal);
                }
            }
        }

        private static void AddHeaders(OpenApiResponse response, int statusCode, bool isMovement)
        {
            response.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.OrdinalIgnoreCase);

            response.Headers[CorrelationConstants.HeaderName] = Header(
                "Identificador de correlação da requisição. Repete o valor enviado pelo cliente, quando válido, " +
                "ou é gerado pela API; aparece também no campo correlationId dos erros.",
                ProblemExamples.CorrelationIdExample);

            switch (statusCode)
            {
                case StatusCodes.Status401Unauthorized:
                    response.Headers["WWW-Authenticate"] = Header(
                        "Esquema de autenticação exigido.",
                        "Bearer");
                    break;
                case StatusCodes.Status429TooManyRequests:
                    response.Headers["Retry-After"] = Header(
                        "Segundos até que uma nova tentativa seja aceita. Enviado quando o limite excedido é de frequência.",
                        "42");
                    break;
                case StatusCodes.Status200OK when !isMovement:
                    response.Headers["Cache-Control"] = Header(
                        "A resposta com o saldo não deve ser armazenada em cache.",
                        "no-store");
                    break;
            }
        }

        private static OpenApiHeader Header(string description, string example)
        {
            return new OpenApiHeader
            {
                Description = description,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                Example = JsonValue.Create(example)
            };
        }
    }
}
