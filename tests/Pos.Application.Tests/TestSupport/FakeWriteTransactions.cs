using Pos.Application.Abstractions;

namespace Pos.Application.Tests.TestSupport;

/// <summary>Transacción de escritura que no hace nada; cuenta los commits.</summary>
public sealed class FakeWriteTransactions : IWriteTransactions
{
    public int Commits { get; private set; }

    public Task<IWriteTransaction> BeginAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IWriteTransaction>(new Transaction(this));

    private sealed class Transaction(FakeWriteTransactions owner) : IWriteTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            owner.Commits++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
