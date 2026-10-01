using MediatR;
using Questao5.Application.Accounts;
using Questao5.Application.Commands.Requests;
using Questao5.Application.Commands.Responses;
using Questao5.Application.Exceptions;
using Questao5.Application.Movements;
using Questao5.Domain.Entities;
using Questao5.Domain.Repositories;

namespace Questao5.Application.Handlers
{
    public sealed class CreateMovementCommandHandler : IRequestHandler<CreateMovementCommand, CreateMovementResponse>
    {
        private readonly IUnitOfWorkFactory unitOfWorkFactory;
        private readonly IMovementIdGenerator movementIdGenerator;
        private readonly TimeProvider timeProvider;

        public CreateMovementCommandHandler(
            IUnitOfWorkFactory unitOfWorkFactory,
            IMovementIdGenerator movementIdGenerator,
            TimeProvider timeProvider)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.movementIdGenerator = movementIdGenerator;
            this.timeProvider = timeProvider;
        }

        public Task<CreateMovementResponse> Handle(
            CreateMovementCommand request,
            CancellationToken cancellationToken)
        {
            // Valor e tipo são validados aqui, antes de qualquer acesso ao banco.
            var movement = MovementRequestNormalizer.Normalize(request);
            cancellationToken.ThrowIfCancellationRequested();

            // Uma única transação imediata: movimento, saldo e idempotência são confirmados ou desfeitos juntos.
            using var unitOfWork = unitOfWorkFactory.BeginWrite();
            cancellationToken.ThrowIfCancellationRequested();

            var idempotencia = unitOfWork.IdempotenciaQuery.ObterPorChave(movement.RequestId);

            if (idempotencia is not null)
            {
                if (!idempotencia.CorrespondeA(movement.CanonicalRequest))
                {
                    throw new IdempotencyConflictException();
                }

                // Repetição idêntica: devolve o resultado original sem revalidar a conta e sem escrever nada.
                return Task.FromResult(new CreateMovementResponse(idempotencia.ObterIdMovimento(), IsReplay: true));
            }

            var conta = AccountAccessPolicy.EnsureAccessible(unitOfWork, movement.AccountId, movement.AccountHolderId);
            cancellationToken.ThrowIfCancellationRequested();

            var movimento = Movimento.Criar(
                movementIdGenerator.Create(),
                conta.IdContaCorrente,
                timeProvider.GetUtcNow(),
                movement.MovementType,
                movement.Amount);
            unitOfWork.MovimentoCommand.Inserir(movimento);

            var saldo = unitOfWork.SaldoContaQuery.ObterPorConta(conta.IdContaCorrente)
                ?? throw new InvalidOperationException("O saldo consolidado da conta não existe.");
            saldo.Aplicar(movimento);
            unitOfWork.SaldoContaCommand.Atualizar(saldo);
            cancellationToken.ThrowIfCancellationRequested();

            unitOfWork.IdempotenciaCommand.Inserir(
                Idempotencia.Registrar(movement.RequestId, movement.CanonicalRequest, movimento.IdMovimento));
            unitOfWork.Commit();

            return Task.FromResult(new CreateMovementResponse(movimento.IdMovimento, IsReplay: false));
        }
    }
}
