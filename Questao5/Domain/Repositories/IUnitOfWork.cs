namespace Questao5.Domain.Repositories
{
    /// <summary>
    /// Unidade de trabalho: uma conexão e uma transação compartilhadas por todos os repositórios.
    /// É o que mantém movimento, saldo e idempotência gravados juntos ou desfeitos juntos.
    /// Descartada sem <see cref="Commit"/>, desfaz tudo o que foi escrito.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        IContaCorrenteQueryStore ContaCorrenteQuery { get; }

        IContaCorrenteCommandStore ContaCorrenteCommand { get; }

        ITitularidadeContaQueryStore TitularidadeContaQuery { get; }

        ITitularidadeContaCommandStore TitularidadeContaCommand { get; }

        IMovimentoQueryStore MovimentoQuery { get; }

        IMovimentoCommandStore MovimentoCommand { get; }

        IIdempotenciaQueryStore IdempotenciaQuery { get; }

        IIdempotenciaCommandStore IdempotenciaCommand { get; }

        ISaldoContaQueryStore SaldoContaQuery { get; }

        ISaldoContaCommandStore SaldoContaCommand { get; }

        void Commit();
    }

    public interface IUnitOfWorkFactory
    {
        /// <summary>
        /// Abre uma transação imediata: a reserva de escritor é obtida antes das leituras que decidem a gravação.
        /// </summary>
        IUnitOfWork BeginWrite();

        /// <summary>
        /// Abre uma transação de leitura: todas as leituras enxergam o mesmo snapshot, sem disputar a escrita.
        /// </summary>
        IUnitOfWork BeginRead();
    }
}
