namespace Pos.Application.Abstractions;

/// <summary>
/// Transacción de escritura sobre la unidad de trabajo del ámbito actual. Toma el candado de
/// escritura al iniciar, así que serializa a los escritores concurrentes (research §5).
/// </summary>
public interface IWriteTransactions
{
    Task<IWriteTransaction> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>Si se descarta sin <see cref="CommitAsync"/>, revierte todo lo guardado dentro de ella.</summary>
public interface IWriteTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
