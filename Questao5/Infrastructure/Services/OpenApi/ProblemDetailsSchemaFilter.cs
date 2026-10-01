using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Questao5.Infrastructure.Services.OpenApi
{
    /// <summary>
    /// Documenta os atributos do corpo de erro, inclusive as extensões que a API acrescenta ao Problem Details.
    /// </summary>
    public sealed class ProblemDetailsSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(context);

            if (context.Type != typeof(ProblemDetails) || schema is not OpenApiSchema problemSchema)
            {
                return;
            }

            problemSchema.Description =
                "Corpo de erro no formato Problem Details (RFC 9457), com as extensões code, correlationId e traceId.";
            problemSchema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

            Describe(problemSchema, "type", JsonSchemaType.String,
                "URI que identifica a categoria do erro.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1");
            Describe(problemSchema, "title", JsonSchemaType.String,
                "Resumo do erro.",
                "A solicitação não pôde ser processada.");
            Describe(problemSchema, "status", JsonSchemaType.Integer,
                "Código de status HTTP da resposta.",
                StatusCodes.Status400BadRequest);
            Describe(problemSchema, "detail", JsonSchemaType.String,
                "Mensagem descritiva da falha.",
                "A conta corrente informada não está cadastrada.");
            Describe(problemSchema, "instance", JsonSchemaType.String,
                "Referência da ocorrência do erro. Não é preenchida por esta API.",
                example: null);
            Describe(problemSchema, "code", JsonSchemaType.String,
                "Tipo da falha: INVALID_ACCOUNT, INACTIVE_ACCOUNT, INVALID_VALUE, INVALID_TYPE, IDEMPOTENCY_CONFLICT, " +
                "UNAUTHENTICATED, RATE_LIMIT_EXCEEDED ou REQUEST_TIMEOUT. Ausente em erros estruturais e internos.",
                "INVALID_ACCOUNT");
            Describe(problemSchema, "correlationId", JsonSchemaType.String,
                "Identificador de correlação da requisição, igual ao header X-Correlation-ID.",
                ProblemExamples.CorrelationIdExample);
            Describe(problemSchema, "traceId", JsonSchemaType.String,
                "Identificador de rastreamento gerado pelo servidor.",
                "00-2dfe616b5f034b169b3e81745e189f68-7e007dedbf755e22-00");

            problemSchema.Properties["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "Presente somente em erros estruturais: mensagens de validação agrupadas por campo.",
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                Example = new JsonObject
                {
                    ["IdContaCorrente"] = new JsonArray(
                        "A identificação da conta corrente deve possuir entre 1 e 37 caracteres.")
                }
            };
        }

        private static void Describe(
            OpenApiSchema problemSchema,
            string propertyName,
            JsonSchemaType type,
            string description,
            JsonNode? example)
        {
            problemSchema.Properties![propertyName] = new OpenApiSchema
            {
                Type = type | JsonSchemaType.Null,
                Description = description,
                Example = example
            };
        }
    }
}
