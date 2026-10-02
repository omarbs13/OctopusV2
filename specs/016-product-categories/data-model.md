# Data Model: Categorías de productos

**Funcionalidad**: `016-product-categories` | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

## Category (nuevo agregado, `Pos.Domain/Categories`)

| Campo | Tipo C# | Columna SQLite | Reglas |
|---|---|---|---|
| `Id` | `Guid` | `Id TEXT PK` | `Guid.CreateVersion7()` |
| `Name` | `string` | `Name TEXT(50) NOT NULL` | Recortado, 1–50 caracteres (FR-002) |
| `NameKey` | `string` | `NameKey TEXT(50) NOT NULL` | `TextNormalizer.ForSearch(Name)`; único entre no borradas (FR-003) |
| `Description` | `string?` | `Description TEXT(200) NULL` | Recortada, hasta 200; vacía → nula |
| `IsActive` | `bool` | `IsActive INTEGER NOT NULL` | `true` al crear |
| `CreatedAt` / `CreatedBy` | `DateTime` / `Guid` | NOT NULL | UTC; los asigna la persistencia |
| `UpdatedAt` / `UpdatedBy` | `DateTime` / `Guid` | NOT NULL | UTC; los asigna la persistencia |
| `DeletedAt` | `DateTime?` | NULL | Borrado lógico (FR-007) |
| `Version` | `int` | NOT NULL, token de concurrencia | Inicia en 1 |

Métodos del dominio:

- `static Category Create(string name, string? description)`: valida y normaliza.
- `void Update(string name, string? description)`: mismas validaciones.
- `void Deactivate()` / `void Activate()`: idempotentes.
- `void Delete(DateTime utcNow)`: exige UTC, como `Product.Delete`.
- `static string NormalizeName(string?)`, `static string? NormalizeDescription(string?)`,
  `static bool IsValidName(string)`, `static bool IsValidDescription(string?)`.

La regla "no se elimina con productos" y la de "confirmar si tiene productos" las aplican los casos de
uso dentro de la transacción (research §4), porque dependen del conteo de productos.

Índices:

- `IX_Categories_NameKey` único, filtro `"DeletedAt" IS NULL`.
- `IX_Categories_IsActive_NameKey` (`IsActive`, `NameKey`), filtro `"DeletedAt" IS NULL`: listado
  ordenado y opciones activas.

### Estados

```text
           Create
             │
             ▼
  ┌──────► Activa ──── Deactivate (confirmar si tiene productos) ───► Inactiva
  │          │                                                          │
  └──────────┼──────────────────── Activate ───────────────────────────┘
             │                                                          │
             └── Delete (solo sin productos no borrados) ──► Borrada ◄──┘
```

Una categoría borrada no vuelve; su nombre queda libre para una nueva.

## Product (cambia, `Pos.Domain/Products/Product.cs`)

| Campo nuevo | Tipo C# | Columna SQLite | Reglas |
|---|---|---|---|
| `CategoryId` | `Guid?` | `CategoryId TEXT NULL` | Cero o una categoría (FR-009). Sin clave foránea (research §2) |

- `Create(...)` y `Update(...)` reciben `Guid? categoryId`. El dominio solo guarda el valor; si la
  categoría está activa lo valida el caso de uso (FR-011), porque requiere consultar otra entidad.
- Índice `IX_Products_CategoryId` (`CategoryId`), filtro `"DeletedAt" IS NULL`: conteo por categoría,
  filtro del listado y bloqueo de eliminación.
- Migración: `AddColumn` nulo; los productos existentes quedan "Sin categoría" (SC-005).

## Venta, línea, devolución y movimiento de inventario (sin cambios de esquema)

- `SaleLine.ProductId` → `Products.CategoryId` al consultar (categoría vigente, FR-019).
- Importe neto por línea = `SaleLine.AmountCents − Σ SaleReturnLine.AmountCents` (research §9).
- Unidades netas por línea = `SaleLine.QuantityThousandths − SaleLine.ReturnedQuantity`.
- Solo ventas `Status = COMPLETED`.

## Valores de Application

### CategoryFilter (`Pos.Application/Categories`)

```text
CategoryFilter
  Kind: All | Uncategorized | Only
  CategoryId: Guid?   (solo con Only)
  static All, static Uncategorized, static Only(Guid)
```

### DTOs

| DTO | Campos |
|---|---|
| `CategoryListItemDto` | `Id`, `Name`, `Description?`, `IsActive`, `ProductCount` (no borrados), `Version` |
| `CategoryDto` | `Id`, `Name`, `Description?`, `IsActive`, `ProductCount`, `Version` |
| `CategoryOptionDto` | `Id`, `Name`, `IsActive` |
| `ProductDto` (+) | `CategoryId?`, `CategoryName?`, `CategoryIsActive` |
| `ProductListItemDto` (+) | `CategoryName?` (nulo = "Sin categoría"), `CategoryIsActive` |
| `SalesReport` (+) | `Categories: IReadOnlyList<CategorySales>`, `Totals.PaymentsBreakdownAvailable` |
| `CategorySales` | `CategoryId?` (nulo = sin categoría), `Name`, `IsActive`, `UnitsThousandths`, `AmountCents`, `ShareBasisPoints`, `Products: IReadOnlyList<ProductSales>` |
| `ProductSales` | `ProductId`, `Name`, `Sku`, `UnitName`, `DecimalPlaces`, `UnitsThousandths`, `AmountCents` |
| `InventoryReportRow` (+) | `CategoryName?`, `CategoryIsActive` |

### Consultas que cambian

| Consulta | Campo nuevo |
|---|---|
| `SearchProductsQuery` | `CategoryFilter Category = CategoryFilter.All` |
| `ProductSearch` (puerto) | `CategoryFilter Category` |
| `SalesReportQuery` | `CategoryFilter Category = CategoryFilter.All` |
| `InventoryReportQuery` | `CategoryFilter Category = CategoryFilter.All` |
| `InventoryReportSort` | + `Category` |
| `CreateProductCommand` / `UpdateProductCommand` | `Guid? CategoryId` |

## Invariantes verificadas por pruebas

1. `Σ CategorySales.AmountCents = SalesReport(sin filtro de categoría).Totals.TotalCents` (SC-002).
2. Para cada categoría, `Σ Products.AmountCents = AmountCents` y `Σ Products.UnitsThousandths =
   UnitsThousandths` (SC-003).
3. Ningún producto no borrado tiene `CategoryId` de una categoría borrada (research §4).
4. No hay dos categorías no borradas con el mismo `NameKey` (FR-003).
