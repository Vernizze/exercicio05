using System.Globalization;
using Questao5.Application.Queries.Responses;
using Questao5.Infrastructure.Services.Controllers.Models;

namespace Questao5.Tests.Infrastructure.Services;

public sealed class GetBalanceHttpResponseTests
{
    [Fact]
    public void From_UtcInstant_UsesRoundTripFormatWithZeroOffset()
    {
        var response = new GetBalanceResponse(
            456,
            "Eva Woodward",
            new DateTimeOffset(2026, 10, 1, 23, 59, 58, TimeSpan.Zero),
            115.25m);

        var httpResponse = GetBalanceHttpResponse.From(response);

        Assert.Equal(456, httpResponse.NumeroContaCorrente);
        Assert.Equal("Eva Woodward", httpResponse.NomeTitular);
        Assert.Equal("2026-10-01T23:59:58.0000000+00:00", httpResponse.DataHoraConsulta);
        Assert.Equal(115.25m, httpResponse.SaldoAtual);
    }

    [Fact]
    public void From_InstantWithLocalOffsetAcrossDateBoundary_IsConvertedToUtc()
    {
        var response = new GetBalanceResponse(
            456,
            "Eva Woodward",
            new DateTimeOffset(2026, 12, 31, 21, 30, 0, TimeSpan.FromHours(-3)),
            0.00m);

        var httpResponse = GetBalanceHttpResponse.From(response);

        Assert.Equal("2027-01-01T00:30:00.0000000+00:00", httpResponse.DataHoraConsulta);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void From_AnyProcessCulture_ProducesTheSameInstantText(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var response = new GetBalanceResponse(
            456,
            "Eva Woodward",
            new DateTimeOffset(2026, 10, 1, 23, 59, 58, TimeSpan.Zero),
            115.25m);

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            var httpResponse = GetBalanceHttpResponse.From(response);

            Assert.Equal("2026-10-01T23:59:58.0000000+00:00", httpResponse.DataHoraConsulta);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
