using Questao5.Infrastructure.Services.Security;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class SecurityFingerprintTests
{
    [Fact]
    public void ForAccount_EquivalentIdentifierForms_ProduceTheSameTruncatedFingerprint()
    {
        var canonical = SecurityFingerprint.ForAccount("FA99D033-7067-ED11-96C6-7C5DFA4A16C9");
        var equivalent = SecurityFingerprint.ForAccount("  fa99d033-7067-ed11-96c6-7c5dfa4a16c9 ");

        Assert.Equal(canonical, equivalent);
        Assert.Matches("^[0-9A-F]{16}$", canonical);
    }

    [Fact]
    public void ForAccount_DifferentAccounts_ProduceDifferentFingerprints()
    {
        var first = SecurityFingerprint.ForAccount("FA99D033-7067-ED11-96C6-7C5DFA4A16C9");
        var second = SecurityFingerprint.ForAccount("382D323D-7067-ED11-8866-7D5DFA4A16C9");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_DoesNotContainTheOriginalValue()
    {
        const string subject = "04b276dc-0f45-4efc-bffc-911110198733";

        var fingerprint = SecurityFingerprint.Create(subject);

        Assert.DoesNotContain(subject, fingerprint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(16, fingerprint.Length);
    }
}
