using System.Security.Cryptography;

namespace Questao5.Tests.Infrastructure.Sqlite;

public sealed class ReferenceDatabaseTests
{
    private const string ExpectedSha256 = "E355A3EDB7D73E7784A7467513248D530C27CC989C624A35D13A188B2AA8DD3C";

    [Fact]
    public void Fixture_HasExpectedHash()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-database.sqlite");

        using var stream = File.OpenRead(fixturePath);
        var actualHash = Convert.ToHexString(SHA256.HashData(stream));

        Assert.Equal(ExpectedSha256, actualHash);
    }
}