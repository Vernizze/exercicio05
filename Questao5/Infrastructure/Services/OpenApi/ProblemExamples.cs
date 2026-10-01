using System.Text.Json.Nodes;

namespace Questao5.Infrastructure.Services.OpenApi
{
    /// <summary>
    /// Exemplos das respostas de erro publicados no documento OpenAPI. Cada exemplo reproduz uma resposta
    /// real da API; os testes do documento comparam os exemplos com as respostas dos endpoints.
    /// </summary>
    public static class ProblemExamples
    {
        public const string CorrelationIdExample = "2dfe616b5f034b169b3e81745e189f68";

        private const string TraceIdExample = "00-2dfe616b5f034b169b3e81745e189f68-7e007dedbf755e22-00";
        private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
        private const string BusinessRuleTitle = "A solicitação não pôde ser processada.";

        public static IReadOnlyList<ProblemExample> ForMovement(int statusCode)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest =>
                [
                    InvalidValue,
                    InvalidType,
                    InvalidAccount,
                    InactiveAccount,
                    ValidationError("IdContaCorrente")
                ],
                StatusCodes.Status401Unauthorized => [Unauthenticated],
                StatusCodes.Status409Conflict => [IdempotencyConflict],
                StatusCodes.Status413PayloadTooLarge => [PayloadTooLarge],
                StatusCodes.Status415UnsupportedMediaType => [UnsupportedMediaType],
                StatusCodes.Status429TooManyRequests => [RateLimitExceeded],
                StatusCodes.Status500InternalServerError => [InternalError],
                StatusCodes.Status504GatewayTimeout =>
                [
                    Timeout("Não foi possível concluir a movimentação dentro do tempo permitido.")
                ],
                _ => []
            };
        }

        public static IReadOnlyList<ProblemExample> ForBalance(int statusCode)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => [InvalidAccount, InactiveAccount, ValidationError("idContaCorrente")],
                StatusCodes.Status401Unauthorized => [Unauthenticated],
                StatusCodes.Status429TooManyRequests => [RateLimitExceeded],
                StatusCodes.Status500InternalServerError => [InternalError],
                StatusCodes.Status504GatewayTimeout =>
                [
                    Timeout("Não foi possível concluir a consulta de saldo dentro do tempo permitido.")
                ],
                _ => []
            };
        }

        private static ProblemExample InvalidValue => Business(
            "INVALID_VALUE",
            "Valor não positivo, acima do limite ou com mais de duas casas decimais",
            "O valor da movimentação deve ser positivo, possuir no máximo duas casas decimais e respeitar o limite permitido.");

        private static ProblemExample InvalidType => Business(
            "INVALID_TYPE",
            "Tipo de movimento diferente de C ou D",
            "O tipo de movimento deve ser C para crédito ou D para débito.");

        private static ProblemExample InvalidAccount => Business(
            "INVALID_ACCOUNT",
            "Conta não cadastrada, ou pertencente a outro correntista",
            "A conta corrente informada não está cadastrada.");

        private static ProblemExample InactiveAccount => Business(
            "INACTIVE_ACCOUNT",
            "Conta do próprio correntista, mas inativa",
            "A conta corrente informada está inativa.");

        // O nome do campo em errors segue o nome do membro validado: propriedade do corpo ou parâmetro de rota.
        private static ProblemExample ValidationError(string fieldName)
        {
            return new ProblemExample(
                "VALIDATION_ERROR",
                "Erro estrutural: campo ausente ou em formato inválido (sem code)",
                new JsonObject
                {
                    ["type"] = BadRequestType,
                    ["title"] = "Um ou mais campos são inválidos.",
                    ["status"] = StatusCodes.Status400BadRequest,
                    ["errors"] = new JsonObject
                    {
                        [fieldName] = new JsonArray(
                            "A identificação da conta corrente deve possuir entre 1 e 37 caracteres.")
                    },
                    ["traceId"] = TraceIdExample,
                    ["correlationId"] = CorrelationIdExample
                });
        }

        private static ProblemExample Unauthenticated => Problem(
            "UNAUTHENTICATED",
            "Token ausente, expirado ou inválido",
            StatusCodes.Status401Unauthorized,
            "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            "Autenticação necessária.",
            "A solicitação não possui credenciais válidas.",
            "UNAUTHENTICATED");

        private static ProblemExample IdempotencyConflict => Problem(
            "IDEMPOTENCY_CONFLICT",
            "Chave de idempotência reutilizada com dados diferentes",
            StatusCodes.Status409Conflict,
            "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            "A chave de idempotência está em conflito.",
            "A chave de idempotência já foi utilizada com uma requisição diferente.",
            "IDEMPOTENCY_CONFLICT");

        private static ProblemExample PayloadTooLarge => Problem(
            "PAYLOAD_TOO_LARGE",
            "Corpo da requisição acima de 4 KiB",
            StatusCodes.Status413PayloadTooLarge,
            type: null,
            "O corpo da requisição excede o limite permitido.",
            "O corpo da requisição deve possuir no máximo 4 KiB.",
            code: null,
            includeTraceId: false);

        private static ProblemExample UnsupportedMediaType => Problem(
            "UNSUPPORTED_MEDIA_TYPE",
            "Conteúdo diferente de application/json",
            StatusCodes.Status415UnsupportedMediaType,
            "https://tools.ietf.org/html/rfc9110#section-15.5.16",
            "Unsupported Media Type",
            detail: null,
            code: null);

        private static ProblemExample RateLimitExceeded => Problem(
            "RATE_LIMIT_EXCEEDED",
            "Limite de frequência ou de concorrência excedido",
            StatusCodes.Status429TooManyRequests,
            type: null,
            "O limite de requisições foi excedido.",
            "A solicitação não pode ser processada neste momento.",
            "RATE_LIMIT_EXCEEDED");

        private static ProblemExample InternalError => Problem(
            "INTERNAL_ERROR",
            "Falha inesperada (sem code e sem detalhe interno)",
            StatusCodes.Status500InternalServerError,
            "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            "Ocorreu um erro interno.",
            "Não foi possível concluir a solicitação.",
            code: null);

        private static ProblemExample Timeout(string detail)
        {
            return Problem(
                "REQUEST_TIMEOUT",
                "Tempo limite da operação excedido",
                StatusCodes.Status504GatewayTimeout,
                "https://tools.ietf.org/html/rfc9110#section-15.6.5",
                "A operação excedeu o tempo limite.",
                detail,
                "REQUEST_TIMEOUT");
        }

        private static ProblemExample Business(string code, string summary, string detail)
        {
            return Problem(code, summary, StatusCodes.Status400BadRequest, BadRequestType, BusinessRuleTitle, detail, code);
        }

        private static ProblemExample Problem(
            string name,
            string summary,
            int status,
            string? type,
            string title,
            string? detail,
            string? code,
            bool includeTraceId = true)
        {
            // "type" e "traceId" só entram quando a resposta real os traz: o 429 não possui "type"
            // e o 413, escrito diretamente pelo filtro de tamanho do corpo, não possui nenhum dos dois.
            var value = new JsonObject();

            if (type is not null)
            {
                value["type"] = type;
            }

            value["title"] = title;
            value["status"] = status;

            if (detail is not null)
            {
                value["detail"] = detail;
            }

            if (code is not null)
            {
                value["code"] = code;
            }

            if (includeTraceId)
            {
                value["traceId"] = TraceIdExample;
            }

            value["correlationId"] = CorrelationIdExample;

            return new ProblemExample(name, summary, value);
        }
    }

    public sealed record ProblemExample(string Name, string Summary, JsonObject Value);
}
