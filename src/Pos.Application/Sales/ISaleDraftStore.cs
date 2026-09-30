namespace Pos.Application.Sales;

/// <summary>
/// Borrador durable de la venta en curso: una fila por usuario, siempre la del usuario conectado
/// (005 research §7; 007 Historia 8). Es la "venta conservada" al cerrar sesión.
/// </summary>
public interface ISaleDraftStore
{
    Task<StoredDraft?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Guarda el borrador; no hace nada si ya existe una venta con ese <paramref name="draftId"/>.</summary>
    Task SaveAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, CancellationToken cancellationToken);

    /// <summary>Marca el borrador para borrarlo con la unidad de trabajo del caso de uso.</summary>
    void Remove();

    Task DiscardAsync(CancellationToken cancellationToken);

    /// <summary>Indica si el usuario tiene una venta conservada (para avisar antes de desactivarlo).</summary>
    Task<bool> HasForAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Borra el borrador de otro usuario con la unidad de trabajo del caso de uso (al desactivarlo).</summary>
    /// <returns><c>true</c> si el usuario tenía un borrador.</returns>
    Task<bool> RemoveForAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Reasigna el borrador de un usuario a otro; no hace nada si el origen no tiene o el destino ya tiene.</summary>
    Task ReassignAsync(Guid fromUserId, Guid toUserId, CancellationToken cancellationToken);
}
