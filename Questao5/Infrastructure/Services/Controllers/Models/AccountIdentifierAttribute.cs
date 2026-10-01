using Questao5.Application.Handlers;
using System.ComponentModel.DataAnnotations;

namespace Questao5.Infrastructure.Services.Controllers.Models
{
    /// <summary>
    /// Identificação de conta corrente: entre 1 e 37 caracteres depois de removidos os espaços externos.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class AccountIdentifierAttribute : ValidationAttribute
    {
        public AccountIdentifierAttribute()
            : base("A identificação da conta corrente deve possuir entre 1 e 37 caracteres.")
        {
        }

        public override bool IsValid(object? value)
        {
            var normalized = (value as string)?.Trim();

            return !string.IsNullOrEmpty(normalized) &&
                normalized.Length <= GetBalanceQueryHandler.MaximumAccountIdLength;
        }
    }
}
