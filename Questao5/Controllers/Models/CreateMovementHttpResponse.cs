namespace Questao5.Controllers.Models
{
    /// <summary>
    /// Resultado de uma movimentação confirmada ou de sua repetição idempotente.
    /// </summary>
    /// <param name="IdMovimento" example="34e56aa7-703f-46d8-8f65-100f2795a87b">
    /// Identificação do movimento gerado. Em uma repetição idêntica, é o mesmo identificador da primeira execução.
    /// </param>
    public sealed record CreateMovementHttpResponse(string IdMovimento);
}
