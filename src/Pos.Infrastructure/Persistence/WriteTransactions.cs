using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pos.Application.Abstractions;

namespace Pos.Infrastructure.Persistence;

/// <summary>
/// Transacción de escritura sobre el <see cref="PosDbContext"/> del ámbito. Con aislamiento
/// serializable, Microsoft.Data.Sqlite inicia <c>BEGIN IMMEDIATE</c>: toma el candado de escritura
/// al empezar y serializa a los escritores (research §5).
/// </summary>
public sealed class WriteTransactions : IWriteTransactions
{
    private readonly PosDbContext _context;

    public WriteTransactions(PosDbContext context) => _context = context;

    public async Task<IWriteTransaction> BeginAsync(CancellationToken cancellationToken) =>
        new Transaction(await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken));

    private sealed class Transaction : IWriteTransaction
    {
        private readonly IDbContextTransaction _inner;

        public Transaction(IDbContextTransaction inner) => _inner = inner;

        public Task CommitAsync(CancellationToken cancellationToken) => _inner.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
