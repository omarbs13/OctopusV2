namespace Pos.Application.Business;

/// <summary>Línea del encabezado del negocio; <paramref name="IsTitle"/> marca el nombre comercial.</summary>
public sealed record BusinessHeaderLine(string Text, bool IsTitle);

/// <summary>
/// Encabezado del negocio que comparten reportes PDF/XLSX y tickets (023, FR-020 a FR-025). El orden
/// canónico y la omisión de los datos vacíos se deciden aquí, no en los escritores ni en las vistas.
/// </summary>
/// <param name="Name">Nombre comercial (obligatorio en BusinessProfile).</param>
/// <param name="Address">Dirección. Nulo si está vacío.</param>
/// <param name="Phone">Teléfono. Nulo si está vacío.</param>
/// <param name="TaxId">RFC. Nulo si está vacío.</param>
/// <param name="Logo">El logo optimizado de BusinessProfile; nulo si no existe.</param>
public sealed record BusinessHeader(string Name, string? Address, string? Phone, string? TaxId, byte[]? Logo)
{
    /// <summary>Aviso en lugar del encabezado cuando no se han capturado los datos del negocio (FR-025).</summary>
    public const string MissingText = "Datos del negocio no capturados";

    /// <summary>Líneas en orden canónico: nombre, dirección, teléfono y RFC, sin las que no existen (FR-023, FR-024).</summary>
    public IReadOnlyList<BusinessHeaderLine> Lines
    {
        get
        {
            var lines = new List<BusinessHeaderLine>(4) { new(Name, IsTitle: true) };
            if (Address is not null)
            {
                lines.Add(new BusinessHeaderLine(Address, IsTitle: false));
            }

            if (Phone is not null)
            {
                lines.Add(new BusinessHeaderLine($"Tel. {Phone}", IsTitle: false));
            }

            if (TaxId is not null)
            {
                lines.Add(new BusinessHeaderLine($"RFC: {TaxId}", IsTitle: false));
            }

            return lines;
        }
    }

    /// <summary>Encabezado a partir de los datos del negocio; nulo si no se han capturado.</summary>
    public static BusinessHeader? From(BusinessProfileDto? profile) =>
        profile is null
            ? null
            : new BusinessHeader(
                profile.TradeName.Trim(),
                Clean(profile.Address),
                Clean(profile.Phone),
                Clean(profile.TaxId),
                profile.Logo);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
