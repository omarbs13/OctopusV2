# Contrato: casos de uso y puertos de Productos (cambios)

Amplía `specs/001-pos-foundation/contracts/use-cases.md` y `ports.md`. Lo que no aparece aquí
no cambia.

## Errores de negocio

Se agrega un error a los existentes (`ValidationFailed`, `Duplicate`, `NotFound`, `Conflict`):

| Error | Datos | Significado |
|---|---|---|
| `InvalidImage` | `Reason`: `TooLarge`, `UnsupportedFormat`, `Corrupt`, `DimensionsTooLarge` | El archivo no se puede usar como imagen de producto |

Campos de `FieldError`: se agrega `UnitCode`. Los errores de imagen no son de campo; el editor
los muestra junto al área de la imagen.

## Mensajes de validación (`ProductMessages`)

| Clave | Texto |
|---|---|
| `NameRequired` | El nombre es obligatorio. *(sin cambios)* |
| `SkuRequired` | El SKU es obligatorio. *(sin cambios)* |
| `PriceRequired` | El precio es obligatorio. |
| `PriceNotPositive` | El precio debe ser mayor que 0. ➕ |
| `PriceTooManyDecimals` | El precio admite máximo 2 decimales. ➕ |
| `PriceTooLarge` | El precio máximo es $999,999.99. ➕ |
| `PriceFormat` | Capture el precio como 1234.50 o 1,234.50. ✏️ |
| `UnitRequired` | La unidad de medida es obligatoria. ➕ |
| `ImageTooLarge` | La imagen pesa más de 5 MB. Elija un archivo más pequeño. ➕ |
| `ImageUnsupportedFormat` | Formato no admitido. Use una imagen JPG, PNG o WEBP. ➕ |
| `ImageCorrupt` | No se pudo leer la imagen; el archivo está dañado o no es una imagen válida. ➕ |
| `ImageDimensionsTooLarge` | La imagen tiene dimensiones demasiado grandes para procesarse. ➕ |

Los mensajes de nombre, SKU y código de barras de la fundación no cambian (FR-018).

## Money (Domain)

- `Money.Parse(string? text) → MoneyParseResult` reemplaza a `TryParse`.
- `MoneyParseResult` contiene `Money? Value` y `MoneyParseError? Error`, donde el error vale
  `Empty`, `Format`, `TooManyDecimals` o `TooLarge`.
- Acepta, tras recortar espacios, `1234.5`, `1234.50`, `1,234.50` y `999,999.99`.
- Rechaza `12,50`, `1,23.45`, `12,3456`, `$10`, `-1` y `1 234`.
- Nunca redondea.
- `ToEditableString()` sigue sin separador de miles ("1234.50").

## SearchProducts ✏️

- **Entrada**: `SearchProductsQuery(string? Text, bool IncludeInactive, int Page = 1, Guid?
  LocateProductId = null)`
- **Salida**: `Result<ProductPage>` (ver [data-model.md](../data-model.md))
- **Reglas**:
  - El filtro de texto de la fundación (FR-017 de 001) no cambia.
  - `IncludeInactive = false` devuelve solo activos. `true` devuelve activos e inactivos. Nunca
    devuelve borrados.
  - Orden `NameSearch, Sku`.
  - Una página menor que 1 se trata como 1. Una página mayor que `TotalPages` devuelve la última.
  - Si `LocateProductId` es visible con el texto y el filtro dados, se devuelve la página que lo
    contiene, ignorando `Page`. Si no es visible, se usa `Page`.
- Se elimina `SearchProductsHandler.Limit`.

## GetProduct ✏️

- `ProductDto` agrega `UnitCode` y `Image` (`byte[]?`, la imagen optimizada).

## ListUnitsOfMeasure ➕

- **Entrada**: ninguna.
- **Salida**: `IReadOnlyList<UnitOfMeasureDto(Code, Name)>` ordenada por `SortOrder`.
- Sale del catálogo de Domain y no consulta la base.

## PrepareProductImage ➕

`Application/Products/PrepareProductImage/`

- **Entrada**: `PrepareProductImageCommand(Stream Content, long Length)`
- **Salida**: `Result<PreparedProductImage>` o `InvalidImage(Reason)`
- **Reglas**:
  1. Si `Length` es mayor que 5 MB, devuelve `TooLarge` sin leer el contenido.
  2. Delega en `IImageProcessor`.
  3. Nunca lanza una excepción por un archivo inválido; las excepciones quedan para fallas
     inesperadas.
- No guarda nada.

## CreateProduct / UpdateProduct ✏️

- `CreateProductCommand(Name, Sku, Barcode, PriceText, UnitCode, ProductImageChange Image)`
- `UpdateProductCommand(Id, ExpectedVersion, Name, Sku, Barcode, PriceText, UnitCode, IsActive,
  ProductImageChange Image)`
- Validación (FR-014 a FR-020): los errores de todos los campos se devuelven juntos.
  - Nombre, SKU y código de barras: igual que en la fundación.
  - Precio: `PriceRequired`, luego `PriceFormat`, `PriceTooManyDecimals` o `PriceTooLarge`, y
    luego `PriceNotPositive`.
  - Unidad: `UnitRequired` si falta o no existe en el catálogo.
- Imagen:
  - Con `Replace`, se aplica `product.SetImage`. Con `Remove`, se aplica `product.RemoveImage()`.
    Con `Keep`, no se carga ni se toca la imagen.
  - Todo se guarda en la misma transacción que los datos (FR-027). Un fallo de guardado no deja
    ni datos ni imagen a medias.
- Con `Remove` en un producto sin imagen, no se hace nada y no es un error.

## Puertos ✏️/➕

### IProductRepository ✏️

```text
Task<Product?> GetAsync(Guid id, bool includeImage, CancellationToken)
Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken)   // ProductSearch: + Page, PageSize; − Limit
Task<int?> LocatePageAsync(ProductSearch search, Guid productId, CancellationToken) // null si no es visible
```

Las implementaciones de prueba (`InMemoryProductRepository`) se actualizan igual.

### IImageProcessor ➕ (Application; implementación `SkiaImageProcessor` en Infrastructure)

```text
ImageProcessingResult Process(Stream content)
  // ImageProcessingResult(byte[]? Content, byte[]? Thumbnail, int Width, int Height, InvalidImageReason? Error)
  // Éxito: Content (WEBP ≤ 800 px), Thumbnail (WEBP ≤ 128 px), Width, Height
  // Error: UnsupportedFormat | Corrupt | DimensionsTooLarge
```

- Devuelve un resultado público sin validación. Solo `PrepareProductImageHandler` construye
  `PreparedProductImage` (constructor `internal`) a partir de él; no se usa `InternalsVisibleTo`.

- Identifica el formato por la firma de los bytes, no por la extensión.
- Aplica la orientación EXIF.
- No amplía imágenes pequeñas.
- Conserva la transparencia.
- En un WEBP animado usa el primer cuadro.
- Es síncrono y puro (sin E/S más allá del `Stream`); el caso de uso lo ejecuta fuera del hilo de
  la interfaz.

### IDiagnosticsExporter (sin cambio de firma)

`ZipDiagnosticsExporter` elimina el contenido de `ProductImages` de la copia temporal y la
compacta antes de comprimirla (FR-029).
