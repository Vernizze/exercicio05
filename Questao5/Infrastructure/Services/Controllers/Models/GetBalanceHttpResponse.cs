using Questao5.Application.Queries.Responses;
using System.Globalization;

namespace Questao5.Infrastructure.Services.Controllers.Models
{
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
