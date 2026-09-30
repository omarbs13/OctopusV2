# Data Model: Mejoras al catálogo de Productos

Cambios al modelo de la fundación (`specs/001-pos-foundation/data-model.md`). Una sola migración
nueva: `ProductCatalogImprovements`.

## Producto (`Pos.Domain.Products.Product`), ampliado

| Atributo | Tipo | Reglas | Cambio |
|---|---|---|---|
| `UnitCode` | texto(3) | Obligatorio; clave de `UnitOfMeasure.All` | ➕ |
| `Image` | `ProductImage?` | 0 o 1 imagen; no la carga el listado | ➕ |
| `Price` | `Money` | **Mayor que 0**, máximo 999,999.99 | ✏️ antes ≥ 0 |
| Demás atributos | — | Sin cambios (nombre, SKU, código de barras, estado, auditoría, `DeletedAt`, `Version`) | — |

**Operaciones de dominio**:

- `Create(name, sku, barcode, price, unitCode)` y `Update(name, sku, barcode, price, unitCode,
  isActive)` lanzan `DomainException` si el precio es 0 o la unidad no existe. Las demás
  validaciones de la fundación se conservan.
- `SetImage(ProductImage image)` reemplaza la imagen. `RemoveImage()` la quita.
- Cualquiera de las dos operaciones marca el producto como modificado, así que `Version` y
  `UpdatedAt/UpdatedBy` cambian en la misma transacción que la imagen.
- Constante nueva: `ImageMaxBytes = 5 * 1024 * 1024` (5 MB).
- `Image` se carga solo cuando se pide explícitamente (`IProductRepository.GetAsync(id,
  includeImage)`). Por eso el caso de uso de edición la carga cuando el cambio es `Replace` o
  `Remove`, y así un `null` nunca se confunde con "no cargada".

**Productos existentes** (clarificación 2):

- Los que tienen precio 0 se materializan sin validación y se listan normalmente.
- `Update` rechaza guardarlos mientras el precio sea 0.

## Unidad de medida (`Pos.Domain.Products.UnitOfMeasure`), nuevo

Catálogo fijo e inmutable (clarificación 1). Es la fuente de verdad en Domain y se persiste
con `HasData`.

| Code (PK) | Name | SortOrder |
|---|---|---|
| H87 | Pieza | 1 |
| KGM | Kilogramo | 2 |
| GRM | Gramo | 3 |
| LTR | Litro | 4 |
| MLT | Mililitro | 5 |
| MTR | Metro | 6 |
| XBX | Caja | 7 |
| XPK | Paquete | 8 |

- `UnitOfMeasure.Default` = H87. Es el valor inicial de un producto nuevo y el valor que la
  migración asigna a los productos existentes.
- No tiene auditoría ni borrado lógico: es un catálogo fijo, no una entidad de negocio editable.

## Imagen de producto (`Pos.Domain.Products.ProductImage`), nuevo

Pertenece al agregado Producto, en una relación 1 a 0..1.

| Atributo | Tipo | Reglas |
|---|---|---|
| `ProductId` | GUID | PK y FK a `Products.Id` |
| `Content` | bytes | WEBP, lado mayor ≤ 800 px, sin ampliar |
| `Thumbnail` | bytes | WEBP, lado mayor ≤ 128 px |
| `Width`, `Height` | entero | Dimensiones de `Content` |
| `ContentType` | texto | `image/webp` |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | UTC / GUID | Los asigna `AuditingInterceptor`, igual que en Producto |

- La imagen se conserva si el producto se borra lógicamente, junto con su registro.
- Al reemplazarla se actualiza la fila; al quitarla se borra la fila. No quedan datos huérfanos
  (FR-030).

## Imagen preparada (`Pos.Application.Products.PreparedProductImage`), nuevo, no persistente

Resultado de `PrepareProductImage`, con constructor `internal`: solo Application puede crearlo.
Contiene `Content`, `Thumbnail`, `Width` y `Height` ya optimizados. Viaja dentro de
`ProductImageChange.Replace`.

`ProductImageChange` es una unión cerrada:

- `Keep`: valor por defecto.
- `Replace(PreparedProductImage)`.
- `Remove`.

## Página de productos (`Pos.Application.Products.ProductPage`), nuevo, no persistente

| Atributo | Reglas |
|---|---|
| `Items` | Hasta 100 `ProductListItemDto` ordenados por `NameSearch, Sku` |
| `TotalCount` | Productos que cumplen el texto y el filtro; nunca incluye borrados |
| `Page` | Base 1; se ajusta a la última página válida |
| `PageSize` | 100 |
| `TotalPages` | `max(1, ceil(TotalCount / 100))` |

Reemplaza a `ProductSearchPage(Items, HasMore)`.

`ProductListItemDto` agrega `UnitCode`, `UnitName` (para mostrar sin consultar Domain desde la vista) y `Thumbnail` (`byte[]?`). `ProductDto` agrega
`UnitCode` e `Image` (`byte[]?`, la imagen optimizada, para la vista previa del editor).

## Esquema SQLite (migración `ProductCatalogImprovements`)

```text
UnitsOfMeasure
  Code       TEXT(3)  PK
  Name       TEXT(50) NOT NULL
  SortOrder  INTEGER  NOT NULL
  -- 8 filas con HasData

Products (reconstruida por EF Core para la FK)
  + UnitCode TEXT(3) NOT NULL DEFAULT 'H87' REFERENCES UnitsOfMeasure(Code)
  IX_Products_NameSearch → (NameSearch, Sku) WHERE DeletedAt IS NULL   -- según medición (research §2)
  IX_Products_Sku, IX_Products_Barcode: sin cambios (filtrados)

ProductImages
  ProductId  TEXT  PK, REFERENCES Products(Id) ON DELETE CASCADE
  Content    BLOB  NOT NULL
  Thumbnail  BLOB  NOT NULL
  Width      INTEGER NOT NULL
  Height     INTEGER NOT NULL
  ContentType TEXT(20) NOT NULL
  CreatedAt, CreatedBy, UpdatedAt, UpdatedBy  NOT NULL
```

**Revisión obligatoria del SQL** (constitución, Principio IV):

- Confirmar que la reconstrucción de `Products` copia todas las filas, incluidas las borradas.
- Confirmar que conserva `Version`, `DeletedAt` y los tres índices filtrados.
- Confirmar que llena `UnitCode` con `'H87'`.

**Base de ejemplo**:

- Se genera `v0.2.0.db` con productos que tienen unidades distintas, uno con imagen, uno inactivo,
  uno borrado y uno con precio 0.
- Se amplía `SampleDatabaseUpgradeTests` para verificar que `v0.1.0.db` migra con
  `UnitCode = 'H87'` y sin imágenes.

## Transiciones de estado

Sin cambios respecto a la fundación: activo ⇄ inactivo por edición y borrado lógico sin retorno.
La imagen no tiene estados propios: existe o no existe.
