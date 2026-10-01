using System.Security.Cryptography;
using System.Text;

namespace Questao5.Infrastructure.Services.Security
{
    /// <summary>
    /// Deriva um identificador curto e estável para correlacionar eventos sem registrar o valor em claro.
    /// </summary>
    public static class SecurityFingerprint
    {
        public static string Create(string normalizedValue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedValue);

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedValue));
            return Convert.ToHexString(hash)[..16];
        }

        public static string ForAccount(string accountId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
            return Create(accountId.Trim().ToUpperInvariant());
        }
    }
}
