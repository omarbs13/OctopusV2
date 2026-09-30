namespace Pos.Domain.Sales;

/// <summary>
/// Borrador de la venta en curso: una fila por usuario (<c>UserId</c>) que se sobrescribe y se borra
/// al registrar la venta. Es la "venta conservada" al cerrar sesión (007, Historia 8). Es un dato
/// temporal, no una entidad de negocio (005, research §7).
/// </summary>
public sealed class SaleDraft
{
    private SaleDraft()
    {
        LinesJson = "[]";
    }

    public Guid UserId { get; private set; }

    public Guid DraftId { get; private set; }

    /// <summary><c>[{productId, quantityThousandths, unitPriceCents}]</c> en orden de captura.</summary>
    public string LinesJson { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static SaleDraft Create(Guid userId, Guid draftId, string linesJson, DateTime utcNow) =>
        new() { UserId = userId, DraftId = draftId, LinesJson = linesJson, UpdatedAt = utcNow };

    public void Replace(Guid draftId, string linesJson, DateTime utcNow)
    {
        DraftId = draftId;
        LinesJson = linesJson;
        UpdatedAt = utcNow;
    }
}
