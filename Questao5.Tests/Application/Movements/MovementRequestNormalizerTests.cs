using Questao5.Application.Commands.Requests;
using Questao5.Application.Exceptions;
using Questao5.Application.Movements;

namespace Questao5.Tests.Application.Movements;

public sealed class MovementRequestNormalizerTests
{
    [Fact]
    public void Normalize_ValidRequest_ReturnsDeterministicCanonicalRepresentation()
    {
        var command = new CreateMovementCommand(
            " D743ABE7-40ED-4A7D-B0A5-8F876E916A07 ",
            " fa99d033-7067-ed11-96c6-7c5dfa4a16c9 ",
            125.5m,
            "C");

        var result = MovementRequestNormalizer.Normalize(command);

        Assert.Equal("d743abe7-40ed-4a7d-b0a5-8f876e916a07", result.RequestId);
        Assert.Equal("FA99D033-7067-ED11-96C6-7C5DFA4A16C9", result.AccountId);
        Assert.Equal(125.5m, result.Amount);
        Assert.Equal('C', result.MovementType);
        Assert.Equal(
            "v1|conta=FA99D033-7067-ED11-96C6-7C5DFA4A16C9|valor=125.50|tipo=C",
            result.CanonicalRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000000000)]
    public void Normalize_InvalidAmount_ThrowsInvalidValue(decimal amount)
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            MovementRequestNormalizer.Normalize(CreateCommand(amount: amount)));

        Assert.Equal("INVALID_VALUE", exception.Code);
    }

    [Fact]
    public void Normalize_AmountWithMoreThanTwoDecimalPlaces_ThrowsInvalidValue()
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            MovementRequestNormalizer.Normalize(CreateCommand(amount: 1.001m)));

        Assert.Equal("INVALID_VALUE", exception.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("c")]
    [InlineData("X")]
    [InlineData("CC")]
    public void Normalize_InvalidMovementType_ThrowsInvalidType(string movementType)
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            MovementRequestNormalizer.Normalize(CreateCommand(movementType: movementType)));

        Assert.Equal("INVALID_TYPE", exception.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("d743abe740ed4a7db0a58f876e916a07")]
    public void Normalize_InvalidRequestId_ThrowsInvalidRequestId(string requestId)
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            MovementRequestNormalizer.Normalize(CreateCommand(requestId: requestId)));

        Assert.Equal("INVALID_REQUEST_ID", exception.Code);
    }

    [Fact]
    public void Normalize_AccountLongerThanSchemaLimit_ThrowsInvalidAccount()
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            MovementRequestNormalizer.Normalize(CreateCommand(accountId: new string('A', 38))));

        Assert.Equal("INVALID_ACCOUNT", exception.Code);
    }

    private static CreateMovementCommand CreateCommand(
        string requestId = "d743abe7-40ed-4a7d-b0a5-8f876e916a07",
        string accountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9",
        decimal amount = 10.25m,
        string movementType = "C")
    {
        return new CreateMovementCommand(requestId, accountId, amount, movementType);
    }
}