using System.Security.Claims;

namespace Questao5.Infrastructure.Services.Security
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Identificador do correntista autenticado, no formato UUID canônico em minúsculas.
        /// </summary>
        public static string GetAccountHolderId(this ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            return TryGetAccountHolderId(principal, out var accountHolderId)
                ? accountHolderId
                : throw new InvalidOperationException("A requisição não possui correntista autenticado.");
        }

        public static bool TryGetAccountHolderId(this ClaimsPrincipal principal, out string accountHolderId)
        {
            ArgumentNullException.ThrowIfNull(principal);

            var subject = principal.FindFirst(JwtAuthenticationExtensions.SubjectClaimType)?.Value;

            if (principal.Identity?.IsAuthenticated == true &&
                Guid.TryParseExact(subject, "D", out var parsedSubject))
            {
                accountHolderId = parsedSubject.ToString("D");
                return true;
            }

            accountHolderId = string.Empty;
            return false;
        }
    }
}
