namespace Pos.Domain.Sales;

/// <summary>
/// Borrador de la venta en curso: una sola fila por instalación (<c>Slot</c> = 1) que se sobrescribe y
/// se borra al registrar la venta. Es un dato temporal, no una entidad de negocio (research §7).
/// </summary>
public sealed class SaleDraft
{
    public const int SingleSlot = 1;

    private SaleDraft()
    {
        LinesJson = "[]";
    }

    public int Slot { get; private set; } = SingleSlot;

    public Guid DraftId { get; private set; }

    /// <summary><c>[{productId, quantityThousandths, unitPriceCents}]</c> en orden de captura.</summary>
    public string LinesJson { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public static SaleDraft Create(Guid draftId, string linesJson, DateTime utcNow) =>
        new() { DraftId = draftId, LinesJson = linesJson, UpdatedAt = utcNow };

    public void Replace(Guid draftId, string linesJson, DateTime utcNow)
    {
        DraftId = draftId;
        LinesJson = linesJson;
        UpdatedAt = utcNow;
    }
}
