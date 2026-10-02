using Pos.Domain.Common;

namespace Pos.Domain.Audit;

/// <summary>
/// Cambio de un campo dentro de una entrada de auditoría (018), con los valores ya formateados para
/// mostrarse: congelan cómo se veía el dato en ese momento (FR-005). <c>Before</c> es nulo en las
/// altas y <c>After</c> en las eliminaciones.
/// </summary>
public sealed record AuditFieldChange
{
    public const int FieldMaxLength = 80;
    public const int ValueMaxLength = 2000;

    public AuditFieldChange(string field, string? before, string? after)
    {
        if (string.IsNullOrWhiteSpace(field) || field.Trim().Length > FieldMaxLength)
        {
            throw new DomainException("El campo del cambio de auditoría no es válido.");
        }

        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            throw new DomainException("Un cambio de auditoría debe tener valores distintos antes y después.");
        }

        Field = field.Trim();
        Before = Limit(before);
        After = Limit(after);
    }

    public string Field { get; private set; }

    public string? Before { get; private set; }

    public string? After { get; private set; }

    private static string? Limit(string? value) =>
        value is { Length: > ValueMaxLength } ? string.Concat(value.AsSpan(0, ValueMaxLength - 1), "…") : value;
}
