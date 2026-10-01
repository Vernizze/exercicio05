using System.Security.Cryptography;
using System.Text;

namespace Questao5.Infrastructure.Services.Movements
{
    public static class IdempotencyFingerprint
    {
        public static string Create(string normalizedRequestId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRequestId);

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRequestId));
            return Convert.ToHexString(hash)[..16];
        }
    }
}