using Questao5.Infrastructure.Services.Security;

namespace Questao5.Infrastructure.Services.Movements
{
    public static class IdempotencyFingerprint
    {
        public static string Create(string normalizedRequestId)
        {
            return SecurityFingerprint.Create(normalizedRequestId);
        }
    }
}
