using Pos.Domain.Common;

namespace Pos.Domain.Inventory;

/// <summary>
/// Registro técnico de que a un usuario ya se le notificó un producto en un nivel durante un día
/// local (022, research §4). No es una entidad de negocio: no lleva auditoría, versión ni borrado
/// lógico y se purga a los 7 días.
/// </summary>
public sealed class StockAlertAcknowledgement
{
    private StockAlertAcknowledgement()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid ProductId { get; private set; }

    public StockAlertLevel Level { get; private set; }

    /// <summary>Día calendario local del equipo en que se notificó.</summary>
    public DateOnly LocalDate { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static StockAlertAcknowledgement Create(
        Guid userId,
        Guid productId,
        StockAlertLevel level,
        DateOnly localDate,
        DateTime utcNow)
    {
        if (level == StockAlertLevel.None)
        {
            throw new DomainException("Solo se registran notificaciones de nivel alerta o urgente.");
        }

        if (userId == Guid.Empty || productId == Guid.Empty)
        {
            throw new DomainException("El usuario y el producto son obligatorios.");
        }

        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("La fecha de la notificación debe estar en UTC.");
        }

        return new StockAlertAcknowledgement
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ProductId = productId,
            Level = level,
            LocalDate = localDate,
            CreatedAt = utcNow,
        };
    }
}
