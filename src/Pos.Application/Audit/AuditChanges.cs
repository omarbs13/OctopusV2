using Pos.Domain.Audit;

namespace Pos.Application.Audit;

/// <summary>Campo de una instantánea de auditoría con su valor ya formateado (018, research §2).</summary>
public sealed record AuditField(string Field, string? Value);

/// <summary>Calcula los cambios de campo entre instantáneas de auditoría (018, research §2).</summary>
public static class AuditChanges
{
    /// <summary>Solo los campos con valor distinto, en el orden de la instantánea.</summary>
    public static IReadOnlyList<AuditFieldChange> Compare(IReadOnlyList<AuditField> before, IReadOnlyList<AuditField> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Count != after.Count)
        {
            throw new ArgumentException("Las instantáneas no tienen los mismos campos.", nameof(after));
        }

        var changes = new List<AuditFieldChange>();
        for (var i = 0; i < before.Count; i++)
        {
            if (!string.Equals(before[i].Field, after[i].Field, StringComparison.Ordinal))
            {
                throw new ArgumentException("Las instantáneas no tienen los mismos campos en el mismo orden.", nameof(after));
            }

            if (!string.Equals(before[i].Value, after[i].Value, StringComparison.Ordinal))
            {
                changes.Add(new AuditFieldChange(before[i].Field, before[i].Value, after[i].Value));
            }
        }

        return changes;
    }

    /// <summary>Valores iniciales de un alta: todos los campos con valor, con el anterior nulo.</summary>
    public static IReadOnlyList<AuditFieldChange> Created(IReadOnlyList<AuditField> after)
    {
        ArgumentNullException.ThrowIfNull(after);
        return [.. after.Where(f => f.Value is not null).Select(f => new AuditFieldChange(f.Field, null, f.Value))];
    }

    /// <summary>Últimos valores de una eliminación: todos los campos con valor, con el nuevo nulo.</summary>
    public static IReadOnlyList<AuditFieldChange> Removed(IReadOnlyList<AuditField> before)
    {
        ArgumentNullException.ThrowIfNull(before);
        return [.. before.Where(f => f.Value is not null).Select(f => new AuditFieldChange(f.Field, f.Value, null))];
    }

    public static bool HasChanges(IReadOnlyList<AuditFieldChange>? changes) => changes is { Count: > 0 };
}
