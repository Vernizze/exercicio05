using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using System.Globalization;

namespace Questao5.Application.Movements
{
    public static class MovementRequestNormalizer
    {
        public const decimal MaximumAmount = 9999999999.99m;

        public static NormalizedMovementRequest Normalize(CreateMovementCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!Guid.TryParseExact(command.RequestId?.Trim(), "D", out var requestId))
            {
                throw new BusinessRuleException(
                    "INVALID_REQUEST_ID",
                    "A identificação da requisição deve ser um UUID válido.");
            }

            var accountId = command.AccountId?.Trim();

            if (string.IsNullOrEmpty(accountId) || accountId.Length > 37)
            {
                throw new BusinessRuleException(
                    "INVALID_ACCOUNT",
                    "A identificação da conta corrente é inválida.");
            }

            if (command.Amount <= 0 ||
                command.Amount > MaximumAmount ||
                GetScale(command.Amount) > 2)
            {
                throw new BusinessRuleException(
                    "INVALID_VALUE",
                    "O valor da movimentação deve ser positivo, possuir no máximo duas casas decimais e respeitar o limite permitido.");
            }

            if (command.MovementType is not "C" and not "D")
            {
                throw new BusinessRuleException(
                    "INVALID_TYPE",
                    "O tipo de movimento deve ser C para crédito ou D para débito.");
            }

            var normalizedRequestId = requestId.ToString("D");
            var normalizedAccountId = accountId.ToUpperInvariant();
            var canonicalRequest = string.Create(
                CultureInfo.InvariantCulture,
                $"v1|conta={normalizedAccountId}|valor={command.Amount:F2}|tipo={command.MovementType}");

            return new NormalizedMovementRequest(
                normalizedRequestId,
                normalizedAccountId,
                command.Amount,
                command.MovementType[0],
                canonicalRequest);
        }

        private static int GetScale(decimal value)
        {
            return (decimal.GetBits(value)[3] >> 16) & 0x7F;
        }
    }

    public sealed record NormalizedMovementRequest(
        string RequestId,
        string AccountId,
        decimal Amount,
        char MovementType,
        string CanonicalRequest);
}