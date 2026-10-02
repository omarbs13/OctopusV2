using Pos.Application.Audit;
using Pos.Domain.Categories;

namespace Pos.Application.Categories;

/// <summary>Instantánea de auditoría de la categoría (018, research §6).</summary>
public static class CategoryAuditFields
{
    public const string State = "Estado";

    public static IReadOnlyList<AuditField> Snapshot(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return
        [
            new("Nombre", category.Name),
            new("Descripción", category.Description),
            new(State, AuditFormat.ActiveState(category.IsActive)),
        ];
    }
}
