# Contrato: casos de uso de Productos y diagnóstico

Casos de uso públicos de `Pos.Application`. Son la única vía por la que Desktop modifica o consulta
datos. Cada uno es una clase simple con un método `HandleAsync`, sin MediatR (Principio VII).

## Errores de negocio comunes

`Result` / `Result<T>` contiene un valor o un `Error`:

| Error | Datos | Significado |
|---|---|---|
| `ValidationFailed` | `IReadOnlyList<FieldError(Field, Message)>` | Datos de entrada inválidos; los mensajes están en español |
| `Duplicate` | `Field` (`Sku` o `Barcode`) | Otro producto no borrado ya usa ese valor |
| `NotFound` | — | El producto no existe o está borrado |
| `Conflict` | — | Otro guardado modificó el producto después de que se abrió |

Los nombres de campo son `Name`, `Sku`, `Barcode` y `Price`, iguales a las propiedades de los
comandos, para que Desktop muestre cada error junto a su control.

## CreateProduct

`Application/Products/CreateProduct/`

- **Entrada**: `CreateProductCommand(string Name, string Sku, string? Barcode, string PriceText)`
- **Salida**: `Result<ProductDto>`
- **Reglas**:
  - Recorta el nombre y pasa el SKU a mayúsculas.
  - Valida FR-010 a FR-013.
  - Verifica que el SKU y el código de barras sean únicos entre los productos no borrados.
  - Crea el producto activo y lo guarda en una sola transacción.
- **Errores**: `ValidationFailed` y `Duplicate`.

## UpdateProduct

`Application/Products/UpdateProduct/`

- **Entrada**: `UpdateProductCommand(Guid Id, int ExpectedVersion, string Name, string Sku,
  string? Barcode, string PriceText, bool IsActive)`
- **Salida**: `Result<ProductDto>`
- **Reglas**: las mismas validaciones que CreateProduct. La unicidad excluye al propio producto.
  Si la versión en la base es distinta de `ExpectedVersion`, se devuelve `Conflict` y no se guarda
  nada.
- **Errores**: `ValidationFailed`, `Duplicate`, `NotFound` y `Conflict`.

## DeleteProduct

`Application/Products/DeleteProduct/`

- **Entrada**: `DeleteProductCommand(Guid Id, int ExpectedVersion)`
- **Salida**: `Result`
- **Reglas**: es un borrado lógico (`DeletedAt = IClock.UtcNow`). La confirmación ocurre en la UI
  antes de invocar este caso de uso.
- **Errores**: `NotFound` y `Conflict`.

## GetProduct

`Application/Products/GetProduct/`

- **Entrada**: `Guid Id`
- **Salida**: `Result<ProductDto>`. Se usa para abrir el editor y para "recargar datos actuales"
  después de un `Conflict`.
- **Errores**: `NotFound`.

## SearchProducts

`Application/Products/SearchProducts/`

- **Entrada**: `SearchProductsQuery(string? Text, bool IncludeInactive)`
- **Salida**: `SearchProductsResult(IReadOnlyList<ProductListItemDto> Items, bool HasMore)`
- **Reglas** (FR-016 y FR-017):
  - Nunca incluye productos borrados. Solo incluye inactivos si `IncludeInactive` es verdadero.
  - Si el texto está vacío, devuelve todos los productos visibles.
  - El nombre (sin acentos ni mayúsculas) y el SKU se buscan por coincidencia parcial.
  - El código de barras se busca por igualdad si el texto cumple `^\d{8,14}$`, y por coincidencia
    parcial en los demás casos.
  - Los resultados se ordenan por nombre normalizado, con un máximo de 200 y `HasMore` cuando hay
    más.
  - Los comodines `%` y `_` del texto se escapan.

## DTOs

```text
ProductDto(Guid Id, string Name, string Sku, string? Barcode, long PriceCents,
           bool IsActive, int Version, DateTime CreatedAtUtc, DateTime UpdatedAtUtc)
ProductListItemDto(Guid Id, string Name, string Sku, string? Barcode, long PriceCents,
                   bool IsActive, int Version)
```

El precio viaja en centavos. Para editarlo, Desktop lo muestra como `0.00`, con punto y sin
separador de miles, y para listarlo lo formatea como moneda es-MX.

## ExportDiagnostics

`Application/Diagnostics/ExportDiagnostics/`

- **Entrada**: `ExportDiagnosticsCommand(string DestinationFilePath)`
- **Salida**: `Result<string>` con la ruta final.
- **Reglas**: FR-028. Nunca deja un archivo incompleto en el destino.
- **Errores**: `ExportFailed(Message)`, por ejemplo por falta de permisos o disco lleno.

## GetAppInfo

`Application/Diagnostics/GetAppInfo/`

- **Salida**: `AppInfoDto(string Version, string DataDirectory, string OperatingSystem)`
  (FR-027).
