using System.Globalization;

namespace Pos.Application.Audit;

/// <summary>Textos del rango y de los filtros de la bitácora para el archivo exportado y su registro (018).</summary>
public static class AuditFilterTexts
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>"01/10/2026 - 31/10/2026" en fechas locales; el límite superior es exclusivo.</summary>
    public static string Period(AuditFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var from = filter.FromUtc is { } f ? DateOnly.FromDateTime(f.ToLocalTime()) : (DateOnly?)null;
        var to = filter.ToUtcExclusive is { } t ? DateOnly.FromDateTime(t.ToLocalTime()).AddDays(-1) : (DateOnly?)null;
        return (from, to) switch
        {
            ({ } a, { } b) when a == b => a.ToString("dd/MM/yyyy", Culture),
            ({ } a, { } b) => $"{a.ToString("dd/MM/yyyy", Culture)} - {b.ToString("dd/MM/yyyy", Culture)}",
            _ => "Todas las fechas",
        };
    }

    /// <summary>Un texto por filtro aplicado además del rango; <paramref name="userName"/> es el nombre del usuario filtrado.</summary>
    public static IReadOnlyList<string> Filters(AuditFilter filter, string? userName)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var texts = new List<string>();
        if (filter.UserId is not null)
        {
            texts.Add($"Usuario: {userName}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            texts.Add($"Evento: {AuditActions.Describe(filter.Action)}");
        }

        if (filter.Entity is { } entity)
        {
            texts.Add($"Entidad: {AuditEntityGroups.Describe(entity)}");
        }

        if (filter.Record is { } record)
        {
            texts.Add($"Registro: {record.EntityType} {record.EntityId}");
        }

        return texts;
    }
}
