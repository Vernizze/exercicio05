using Questao5.Application.Queries.Responses;
using System.Globalization;

namespace Questao5.Controllers.Models
{
    /// <summary>
    /// Saldo atual de uma conta corrente.
    /// </summary>
    /// <param name="NumeroContaCorrente" example="456">Número da conta corrente.</param>
    /// <param name="NomeTitular" example="Eva Woodward">Nome do titular da conta corrente.</param>
    /// <param name="DataHoraConsulta" example="2026-10-01T23:59:58.0000000+00:00">
    /// Data e hora da resposta da consulta, em UTC, no formato ISO 8601 de ida e volta.
    /// </param>
    /// <param name="SaldoAtual" example="115.25">
    /// Saldo atual: soma dos créditos menos soma dos débitos. É 0.00 quando a conta não possui movimentos
    /// e pode ser negativo.
    /// </param>
    public sealed record GetBalanceHttpResponse(
        long NumeroContaCorrente,
        string NomeTitular,
        string DataHoraConsulta,
        decimal SaldoAtual)
    {
        public static GetBalanceHttpResponse From(GetBalanceResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            // Instante em UTC, formato round-trip e cultura invariável: independe do servidor.
            var queriedAt = response.QueriedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

            return new GetBalanceHttpResponse(
                response.AccountNumber,
                response.HolderName,
                queriedAt,
                response.Balance);
        }
    }
}
