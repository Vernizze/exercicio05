using Dapper;
using Questao5.Application.Commands.Responses;
using Questao5.Application.Exceptions;
using Questao5.Application.Movements;
using Questao5.Infrastructure.Sqlite;
using System.Globalization;

namespace Questao5.Infrastructure.Database.CommandStore
{
    public sealed class MovementStore : IMovementStore
    {
        private readonly ISqliteConnectionFactory connectionFactory;
        private readonly IMovementIdGenerator movementIdGenerator;
        private readonly TimeProvider timeProvider;

        public MovementStore(
            ISqliteConnectionFactory connectionFactory,
            IMovementIdGenerator movementIdGenerator,
            TimeProvider timeProvider)
        {
            this.connectionFactory = connectionFactory;
            this.movementIdGenerator = movementIdGenerator;
            this.timeProvider = timeProvider;
        }

        public CreateMovementResponse Create(
            NormalizedMovementRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            using var connection = connectionFactory.OpenConnection();
            using var transaction = connection.BeginTransaction(deferred: false);

            cancellationToken.ThrowIfCancellationRequested();

            var idempotency = connection.QuerySingleOrDefault<IdempotencyRecord>(
                """
                SELECT requisicao AS Request, resultado AS Result
                FROM idempotencia
                WHERE chave_idempotencia = @RequestId;
                """,
                new { request.RequestId },
                transaction);

            if (idempotency is not null)
            {
                if (!string.Equals(idempotency.Request, request.CanonicalRequest, StringComparison.Ordinal))
                {
                    throw new IdempotencyConflictException();
                }

                return new CreateMovementResponse(ParseMovementId(idempotency.Result), IsReplay: true);
            }

            var account = connection.QuerySingleOrDefault<AccountRecord>(
                """
                SELECT idcontacorrente AS Id, ativo AS Active
                FROM contacorrente
                WHERE idcontacorrente = @AccountId COLLATE NOCASE;
                """,
                new { request.AccountId },
                transaction);

            if (account is null)
            {
                throw new BusinessRuleException(
                    "INVALID_ACCOUNT",
                    "A conta corrente informada não está cadastrada.");
            }

            if (account.Active != 1)
            {
                throw new BusinessRuleException(
                    "INACTIVE_ACCOUNT",
                    "A conta corrente informada está inativa.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var movementId = movementIdGenerator.Create();
            var movementDate = timeProvider.GetUtcNow().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

            connection.Execute(
                """
                INSERT INTO movimento(
                    idmovimento,
                    idcontacorrente,
                    datamovimento,
                    tipomovimento,
                    valor)
                VALUES (
                    @MovementId,
                    @AccountId,
                    @MovementDate,
                    @MovementType,
                    @Amount);
                """,
                new
                {
                    MovementId = movementId,
                    AccountId = account.Id,
                    MovementDate = movementDate,
                    MovementType = request.MovementType.ToString(),
                    Amount = Convert.ToDouble(request.Amount, CultureInfo.InvariantCulture)
                },
                transaction);

            cancellationToken.ThrowIfCancellationRequested();

            var result = $"v1|idMovimento={movementId}";

            connection.Execute(
                """
                INSERT INTO idempotencia(chave_idempotencia, requisicao, resultado)
                VALUES (@RequestId, @CanonicalRequest, @Result);
                """,
                new
                {
                    request.RequestId,
                    request.CanonicalRequest,
                    Result = result
                },
                transaction);

            transaction.Commit();
            return new CreateMovementResponse(movementId, IsReplay: false);
        }

        private static string ParseMovementId(string? result)
        {
            const string prefix = "v1|idMovimento=";

            if (result is null ||
                !result.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(result[prefix.Length..], "D", out var movementId))
            {
                throw new InvalidOperationException("O resultado idempotente persistido é inválido.");
            }

            return movementId.ToString("D");
        }

        private sealed record IdempotencyRecord(string? Request, string? Result);

        private sealed record AccountRecord(string Id, long Active);
    }
}