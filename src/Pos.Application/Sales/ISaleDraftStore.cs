namespace Pos.Application.Sales;

/// <summary>Borrador durable de la venta en curso: una sola fila por instalación (research §7).</summary>
public interface ISaleDraftStore
{
    Task<StoredDraft?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Guarda el borrador; no hace nada si ya existe una venta con ese <paramref name="draftId"/>.</summary>
    Task SaveAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, CancellationToken cancellationToken);

    /// <summary>Marca el borrador para borrarlo con la unidad de trabajo del caso de uso.</summary>
    void Remove();

    Task DiscardAsync(CancellationToken cancellationToken);
}
