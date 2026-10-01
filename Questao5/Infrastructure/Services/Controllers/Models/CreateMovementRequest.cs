using Questao5.Application.Movements;
using System.ComponentModel.DataAnnotations;

namespace Questao5.Infrastructure.Services.Controllers.Models
{
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

        [Required]
        [RegularExpression("^[CD]$")]
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

            if (Valor.HasValue &&
                (Valor.Value <= 0 ||
                 Valor.Value > MovementRequestNormalizer.MaximumAmount ||
                 GetScale(Valor.Value) > 2))
            {
                yield return new ValidationResult(
                    "O valor deve ser positivo, possuir no máximo duas casas decimais e respeitar o limite permitido.",
                    [nameof(Valor)]);
            }
        }

        private static int GetScale(decimal value)
        {
            return (decimal.GetBits(value)[3] >> 16) & 0x7F;
        }
    }
}