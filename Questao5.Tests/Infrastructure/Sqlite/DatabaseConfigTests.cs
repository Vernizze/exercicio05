using Questao5.Infrastructure.Sqlite;

namespace Questao5.Tests.Infrastructure.Sqlite;

public sealed class DatabaseConfigTests
{
    [Fact]
    public void Constructor_WithValidConnectionString_PreservesValue()
    {
        const string connectionString = "Data Source=test.sqlite;Foreign Keys=True";

        var config = new DatabaseConfig(connectionString);

        Assert.Equal(connectionString, config.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingConnectionString_ThrowsArgumentException(string? connectionString)
    {
        var exception = Assert.Throws<ArgumentException>(() => new DatabaseConfig(connectionString));

        Assert.Equal("name", exception.ParamName);
        Assert.Contains("DatabaseName", exception.Message, StringComparison.Ordinal);
    }
}