using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Questao5.Tests.Infrastructure.Services;

internal sealed class TestTokenIssuer : IDisposable
{
    public const string Issuer = "https://issuer.test/default";
    public const string MetadataAddress = "https://issuer.test/default/.well-known/openid-configuration";
    public const string Audience = "questao5-api";

    // Titular da conta 456 (FA99D033-7067-ED11-96C6-7C5DFA4A16C9), usada pela maioria dos testes HTTP.
    public const string DefaultSubject = "04b276dc-0f45-4efc-bffc-911110198733";

    private readonly RSA rsa = RSA.Create(2048);

    public TestTokenIssuer()
    {
        SigningKey = new RsaSecurityKey(rsa) { KeyId = "test-signing-key" };
    }

    public RsaSecurityKey SigningKey { get; }

    public string CreateToken(
        string? subject = DefaultSubject,
        string issuer = Issuer,
        string audience = Audience,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        SecurityKey? signingKey = null,
        string algorithm = SecurityAlgorithms.RsaSha256)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new Dictionary<string, object>();

        if (subject is not null)
        {
            claims["sub"] = subject;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = (notBefore ?? now).UtcDateTime,
            NotBefore = (notBefore ?? now.AddMinutes(-1)).UtcDateTime,
            Expires = (expires ?? now.AddMinutes(5)).UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, algorithm)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static string CreateUnsignedToken(string subject = DefaultSubject)
    {
        var now = DateTimeOffset.UtcNow;
        var header = JsonSerializer.Serialize(new { alg = "none", typ = "JWT" });
        var payload = JsonSerializer.Serialize(new
        {
            sub = subject,
            iss = Issuer,
            aud = Audience,
            nbf = now.AddMinutes(-1).ToUnixTimeSeconds(),
            exp = now.AddMinutes(5).ToUnixTimeSeconds()
        });

        return $"{Encode(header)}.{Encode(payload)}.";
    }

    public void Dispose()
    {
        rsa.Dispose();
    }

    private static string Encode(string value)
    {
        return Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(value));
    }
}
