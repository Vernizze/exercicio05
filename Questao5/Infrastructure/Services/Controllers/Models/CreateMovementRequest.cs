using System.ComponentModel.DataAnnotations;

namespace Questao5.Infrastructure.Services.Controllers.Models
{
    /// <summary>
    /// Requisição de movimentação de conta corrente.
    /// </summary>
    /// <remarks>
    /// Valida somente a estrutura da requisição. As regras de negócio sobre valor e tipo
    /// (INVALID_VALUE e INVALID_TYPE) ficam no normalizador, para chegarem ao cliente com o código do enunciado.
    /// </remarks>
    public sealed class CreateMovementRequest : IValidatableObject
    {
        /// <summary>
        /// Identificação da requisição, usada como chave de idempotência. UUID no formato canônico de 36 caracteres.
        /// Repetir a mesma requisição devolve o movimento original, sem criar outro.
        /// </summary>
        /// <example>d743abe7-40ed-4a7d-b0a5-8f876e916a07</example>
        [Required]
        [RegularExpression(
            "^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$")]
        public string? IdRequisicao { get; init; }

        /// <summary>
        /// Identificação da conta corrente a movimentar, com 1 a 37 caracteres.
        /// A conta deve pertencer ao correntista do token.
        /// </summary>
        /// <example>FA99D033-7067-ED11-96C6-7C5DFA4A16C9</example>
        [Required]
        public string? IdContaCorrente { get; init; }

        /// <summary>
        /// Valor a movimentar: maior que zero, com no máximo duas casas decimais e até 9999999999.99.
        /// </summary>
        /// <example>125.50</example>
        [Required]
        public decimal? Valor { get; init; }

        // Texto vazio é um tipo presente e inválido (INVALID_TYPE), não um campo ausente.

        /// <summary>
        /// Tipo do movimento: C para crédito ou D para débito.
        /// </summary>
        /// <example>C</example>
        [Required(AllowEmptyStrings = true)]
        public string? TipoMovimento { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var normalizedAccountId = IdContaCorrente?.Trim();

            if (string.IsNullOrEmpty(normalizedAccountId) || normalizedAccountId.Length > 37)
            {
                yield return new ValidationResult(
                    "A identificação da conta corrente deve possuir entre 1 e 37 caracteres.",
                    [nameof(IdContaCorrente)]);
            }
        }
    }
}
