namespace Questao5.Infrastructure.Services.Security
{
    public sealed class JwtAuthenticationOptions
    {
        public const string SectionName = "Jwt";

        public string? MetadataAddress { get; set; }

        public string? Issuer { get; set; }

        public string? Audience { get; set; }

        public bool RequireHttpsMetadata { get; set; } = true;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            {
                throw new InvalidOperationException(
                    "A configuração de autenticação exige Jwt:Issuer e Jwt:Audience.");
            }

            if (!Uri.TryCreate(MetadataAddress, UriKind.Absolute, out var metadataUri) ||
                (metadataUri.Scheme != Uri.UriSchemeHttps && metadataUri.Scheme != Uri.UriSchemeHttp))
            {
                throw new InvalidOperationException(
                    "A configuração de autenticação exige Jwt:MetadataAddress como URL absoluta.");
            }

            if (RequireHttpsMetadata && metadataUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "Jwt:MetadataAddress deve usar HTTPS quando Jwt:RequireHttpsMetadata está habilitado.");
            }
        }
    }
}
