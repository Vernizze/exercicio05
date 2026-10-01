using Dapper;
using Microsoft.Data.Sqlite;
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
                SELECT c.idcontacorrente AS Id,
                       c.ativo AS Active,
                       t.idcorrentista AS HolderId
                FROM contacorrente c
                LEFT JOIN titularidade_conta t ON t.idcontacorrente = c.idcontacorrente
                WHERE c.idcontacorrente = @AccountId COLLATE NOCASE;
                """,
                new { request.AccountId },
                transaction);

            if (account is null)
            {
                throw AccountRuleViolations.InvalidAccount();
            }

            // Conta sem titularidade não pertence a ninguém (falha fechada).
            if (!string.Equals(account.HolderId, request.AccountHolderId, StringComparison.OrdinalIgnoreCase))
            {
                throw AccountRuleViolations.OwnershipDenied();
            }

            if (account.Active != 1)
            {
                throw AccountRuleViolations.InactiveAccount();
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

            UpdateBalanceProjection(connection, transaction, account.Id, request);

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

        private static void UpdateBalanceProjection(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string accountId,
            NormalizedMovementRequest request)
        {
            // A transação imediata garante escritor único: ler e regravar o saldo aqui não perde atualização.
            var currentCents = connection.QuerySingleOrDefault<long?>(
                "SELECT saldo_centavos FROM saldo_conta WHERE idcontacorrente = @AccountId;",
                new { AccountId = accountId },
                transaction)
                ?? throw new InvalidOperationException("A projeção de saldo da conta não existe.");
            var amountCents = BalanceProjection.ToCents(request.Amount);
            var newCents = request.MovementType == 'C'
                ? checked(currentCents + amountCents)
                : checked(currentCents - amountCents);

            connection.Execute(
                """
                UPDATE saldo_conta
                SET saldo_centavos = @BalanceCents,
                    versao = versao + 1
                WHERE idcontacorrente = @AccountId;
                """,
                new { AccountId = accountId, BalanceCents = newCents },
                transaction);
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

        private sealed record AccountRecord(string Id, long Active, string? HolderId);
    }
}