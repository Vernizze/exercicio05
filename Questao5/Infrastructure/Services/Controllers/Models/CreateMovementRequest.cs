using System.ComponentModel.DataAnnotations;

namespace Questao5.Infrastructure.Services.Controllers.Models
{
    /// <summary>
    /// Valida somente a estrutura da requisição. As regras de negócio sobre valor e tipo
    /// (INVALID_VALUE e INVALID_TYPE) ficam no normalizador, para chegarem ao cliente com o código do enunciado.
    /// </summary>
    public sealed class CreateMovementRequest : IValidatableObject
    {
        [Required]
        [RegularExpression(
            "^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$")]
        public string? IdRequisicao { get; init; }

        [Required]
        public string? IdContaCorrente { get; init; }

        [Required]
        public decimal? Valor { get; init; }

        // Texto vazio é um tipo presente e inválido (INVALID_TYPE), não um campo ausente.
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
