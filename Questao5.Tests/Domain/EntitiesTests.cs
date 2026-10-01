using Questao5.Domain.Entities;
using Questao5.Domain.Enumerators;

namespace Questao5.Tests.Domain;

public sealed class EntitiesTests
{
    private const string AccountId = "FA99D033-7067-ED11-96C6-7C5DFA4A16C9";
    private const string MovementId = "34e56aa7-703f-46d8-8f65-100f2795a87b";
    private const string RequestId = "d743abe7-40ed-4a7d-b0a5-8f876e916a07";

    [Theory]
    [InlineData(0L, "0.00")]
    [InlineData(1L, "0.01")]
    [InlineData(10L, "0.10")]
    [InlineData(1050L, "10.50")]
    [InlineData(-2575L, "-25.75")]
    [InlineData(999999999999L, "9999999999.99")]
    [InlineData(9223372036854775807L, "92233720368547758.07")]
    public void SaldoConta_Saldo_IsBuiltFromCentsWithTwoDecimalPlaces(long cents, string expected)
    {
        var saldo = new SaldoConta(AccountId, cents, versao: 0);

        Assert.Equal(expected, saldo.Saldo.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2, saldo.Saldo.Scale);
    }

    [Fact]
    public void SaldoConta_Aplicar_AddsCreditsSubtractsDebitsAndCountsMovements()
    {
        var saldo = new SaldoConta(AccountId, saldoCentavos: 0, versao: 0);

        saldo.Aplicar(CreateMovement(TipoMovimento.Credito, 100.10m));
        saldo.Aplicar(CreateMovement(TipoMovimento.Debito, 30.05m));
        saldo.Aplicar(CreateMovement(TipoMovimento.Credito, 0.01m));

        Assert.Equal(7006, saldo.SaldoCentavos);
        Assert.Equal(3, saldo.Versao);
        Assert.Equal(70.06m, saldo.Saldo);
    }

    [Theory]
    [InlineData(long.MaxValue, TipoMovimento.Credito)]
    [InlineData(long.MinValue, TipoMovimento.Debito)]
    public void SaldoConta_Aplicar_OverflowFailsInsteadOfWrapping(long cents, TipoMovimento tipoMovimento)
    {
        var saldo = new SaldoConta(AccountId, cents, versao: 0);

        Assert.Throws<OverflowException>(() => saldo.Aplicar(CreateMovement(tipoMovimento, 0.01m)));
        Assert.Equal(cents, saldo.SaldoCentavos);
        Assert.Equal(0, saldo.Versao);
    }

    [Fact]
    public void SaldoConta_ParaCentavos_RejectsMoreThanTwoDecimalPlaces()
    {
        Assert.Equal(12550, SaldoConta.ParaCentavos(125.50m));
        Assert.Throws<InvalidOperationException>(() => SaldoConta.ParaCentavos(1.001m));
    }

    [Theory]
    [InlineData("04b276dc-0f45-4efc-bffc-911110198733", true)]
    [InlineData("04B276DC-0F45-4EFC-BFFC-911110198733", true)]
    [InlineData("06dc3a47-fb77-4589-9e18-076f3860d1d2", false)]
    [InlineData("", false)]
    public void TitularidadeConta_PertenceA_ComparesTheAccountHolderIgnoringCase(string idCorrentista, bool expected)
    {
        var titularidade = new TitularidadeConta(AccountId, "04b276dc-0f45-4efc-bffc-911110198733");

        Assert.Equal(expected, titularidade.PertenceA(idCorrentista));
    }

    [Fact]
    public void Movimento_Criar_UsesUtcDateInTheLegacyFormat()
    {
        var localInstant = new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.FromHours(-3));

        var movimento = Movimento.Criar(MovementId, AccountId, localInstant, TipoMovimento.Debito, 10.25m);

        Assert.Equal("01/01/2027", movimento.DataMovimento);
        Assert.Equal(TipoMovimento.Debito, movimento.TipoMovimento);
        Assert.Equal(10.25m, movimento.Valor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(1.001)]
    [InlineData(10000000000)]
    public void Movimento_InvalidValue_IsRejected(decimal valor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Movimento.Criar(MovementId, AccountId, DateTimeOffset.UtcNow, TipoMovimento.Credito, valor));
    }

    [Fact]
    public void Idempotencia_Registrar_StoresOnlyTheMinimalVersionedResult()
    {
        var idempotencia = Idempotencia.Registrar(RequestId, "v2|titular=t|conta=c|valor=1.00|tipo=C", MovementId);

        Assert.Equal(RequestId, idempotencia.ChaveIdempotencia);
        Assert.Equal($"v1|idMovimento={MovementId}", idempotencia.Resultado);
        Assert.Equal(MovementId, idempotencia.ObterIdMovimento());
        Assert.True(idempotencia.CorrespondeA("v2|titular=t|conta=c|valor=1.00|tipo=C"));
        Assert.False(idempotencia.CorrespondeA("v2|titular=t|conta=c|valor=1.00|tipo=D"));
        Assert.False(idempotencia.CorrespondeA("V2|TITULAR=T|CONTA=C|VALOR=1.00|TIPO=C"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1|idMovimento=not-a-uuid")]
    [InlineData("v2|idMovimento=34e56aa7-703f-46d8-8f65-100f2795a87b")]
    public void Idempotencia_ObterIdMovimento_InvalidPersistedResultFails(string? resultado)
    {
        var idempotencia = new Idempotencia(RequestId, "requisicao", resultado);

        Assert.Throws<InvalidOperationException>(() => idempotencia.ObterIdMovimento());
    }

    [Theory]
    [InlineData("C", true, TipoMovimento.Credito)]
    [InlineData("D", true, TipoMovimento.Debito)]
    [InlineData("c", false, default(TipoMovimento))]
    [InlineData("", false, default(TipoMovimento))]
    [InlineData(null, false, default(TipoMovimento))]
    public void TipoMovimento_TentarConverter_AcceptsOnlyTheTwoPersistedCodes(
        string? codigo,
        bool expectedSuccess,
        TipoMovimento expected)
    {
        var success = TipoMovimentoExtensions.TentarConverter(codigo, out var tipoMovimento);

        Assert.Equal(expectedSuccess, success);

        if (expectedSuccess)
        {
            Assert.Equal(expected, tipoMovimento);
            Assert.Equal(codigo, tipoMovimento.ParaCodigo());
        }
    }

    private static Movimento CreateMovement(TipoMovimento tipoMovimento, decimal valor)
    {
        return Movimento.Criar(Guid.NewGuid().ToString("D"), AccountId, DateTimeOffset.UtcNow, tipoMovimento, valor);
    }
}
