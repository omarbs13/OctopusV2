using Pos.Domain.Products;

namespace Pos.Application.Scanner.InspectScan;

/// <summary>
/// Resultado de inspeccionar una lectura (021). <see cref="NormalizedText"/> queda vacío si la lectura está
/// vacía; <see cref="Length"/> son los caracteres recibidos, sin el terminador.
/// </summary>
public sealed record ScanInspectionDto(string NormalizedText, BarcodeFormat Format, int Length, ScanProductDto? Product);

/// <summary>Producto con ese código de barras, activo o inactivo.</summary>
public sealed record ScanProductDto(string Name, string Sku, bool IsActive);
