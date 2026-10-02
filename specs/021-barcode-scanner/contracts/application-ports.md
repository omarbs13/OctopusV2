# Contrato: dominio, casos de uso y puertos

**Feature**: `021-barcode-scanner` | **Plan**: [../plan.md](../plan.md)

## Domain

```csharp
namespace Pos.Domain.Products;

public enum BarcodeFormat { Empty, Ean13, Ean8, Code128OrCode39, Unrecognized }

public static class Barcode
{
    public const int MaxLength = 48;
    public static string? Normalize(string? raw);
    public static bool IsValidForCatalog(string? normalized);
    public static BarcodeFormat Classify(string? raw);
    public static bool HasValidEanCheckDigit(string digits);
}
```

`Product.NormalizeBarcode` e `IsValidBarcode` delegan en `Barcode.Normalize` e `IsValidForCatalog`
(misma firma pública que hoy). `Product.BarcodeMinLength = 1`, `BarcodeMaxLength = Barcode.MaxLength`.

## Puerto `IProductRepository` (cambia)

| Miembro | Cambio |
|---|---|
| `FindForSaleAsync(string code, …)` | Normaliza el código de barras con `Barcode.Normalize` (mayúsculas, sin `*…*`) |
| `ExistsWithCodeAsync(string code, …)` | Igual (lo usa 015 para que un cupón no repita el código de un producto) |
| `BarcodeExistsAsync(string barcode, …)` | Sin cambios; recibe el código ya normalizado |
| **Nuevo** `Task<Product?> FindByBarcodeAsync(string normalizedBarcode, CancellationToken)` | Producto no borrado con ese código (activo o inactivo); usa `IX_Products_Barcode` |

El doble de pruebas `InMemoryProductRepository` implementa el método nuevo.

## Caso de uso `FindProductsForSale` (cambia)

```csharp
public sealed record FindProductsForSaleQuery(string Text, bool FromScanner = false);

public sealed record ProductLookup(
    LookupKind Kind,
    IReadOnlyList<SaleProductDto> Items,
    CouponLookupDto? Coupon = null,
    BarcodeFormat? Format = null);   // nuevo: solo con Kind == None
```

| Situación | Resultado |
|---|---|
| Texto vacío | `ValidationFailed` (sin cambios) |
| Una coincidencia exacta de SKU o código | `ExactMatch` con 1 producto (incluye inactivo/borrado con `NotSellableReason`) |
| Código de barras de uno y SKU de otro | `NameMatches` con ambos → selector (FR-013) |
| Cupón (015) | `Coupon` |
| `FromScanner == false` y hay coincidencias por nombre | `NameMatches` (005) |
| Nada (o escaneo sin coincidencia exacta ni cupón) | `None` con `Format = Barcode.Classify(Text)` |

Permiso: `Sell` (sin cambios). No escribe datos.

## Caso de uso nuevo `InspectScan` (`Pos.Application/Scanner/InspectScan/`)

```csharp
public sealed record InspectScanQuery(string RawText);

public sealed record ScanInspectionDto(
    string NormalizedText,      // vacío si la lectura está vacía
    BarcodeFormat Format,
    int Length,                 // caracteres de RawText, sin terminador
    ScanProductDto? Product);

public sealed record ScanProductDto(string Name, string Sku, bool IsActive);

public sealed class InspectScanHandler
{
    Task<Result<ScanInspectionDto>> HandleAsync(InspectScanQuery query, CancellationToken cancellationToken);
}
```

- Requiere una sesión iniciada (`ICurrentUser` autenticado). No pide permiso de rol (FR-015). Sin
  sesión → `Forbidden`.
- Busca el producto con `FindByBarcodeAsync(Barcode.Normalize(RawText))` solo si el formato no es
  `Empty` ni `Unrecognized`, o si el código normalizado cumple la regla del catálogo.
- No escribe datos, no toca la venta ni el borrador y no escribe bitácora (FR-019).
- Se registra en `DependencyInjection` de Application como los demás casos de uso.

## Mensajes (`ProductMessages`)

`BarcodeFormat` cambia a: "El código de barras admite hasta 48 letras, dígitos, espacios interiores y
los símbolos - . $ / + %."
