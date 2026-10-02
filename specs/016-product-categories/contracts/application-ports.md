# Contrato: casos de uso y puertos de Application

**Funcionalidad**: `016-product-categories` | **Modelo**: [../data-model.md](../data-model.md)

Todos los casos de uso devuelven `Result` / `Result<T>`; los errores de negocio son `Error`. Los
mensajes al operador viven en `CategoryMessages` (español).

## Casos de uso nuevos (`Pos.Application/Categories`)

| Caso de uso | Entrada | Permiso | Éxito | Errores |
|---|---|---|---|---|
| `SearchCategories` | `Text?`, `Status: Active \| Inactive \| All` | `ManageProducts` | `IReadOnlyList<CategoryListItemDto>` por `NameKey` | `Forbidden` |
| `GetCategory` | `Id` | `ManageProducts` | `CategoryDto` | `NotFound` |
| `CreateCategory` | `Name`, `Description?` | `ManageProducts` | `Guid` | `ValidationFailed`, `Duplicate("Name")` |
| `UpdateCategory` | `Id`, `Name`, `Description?`, `ExpectedVersion` | `ManageProducts` | — | `ValidationFailed`, `Duplicate("Name")`, `NotFound`, `Conflict` |
| `SetCategoryActive` | `Id`, `IsActive`, `ExpectedVersion`, `Confirmed` | `ManageProducts` | — | `ConfirmationRequired(ProductCount)`, `NotFound`, `Conflict` |
| `DeleteCategory` | `Id`, `ExpectedVersion` | `ManageProducts` | — | `CategoryInUse(ProductCount)`, `NotFound`, `Conflict` |
| `ListCategoryOptions` | `IncludeInactive` | `ViewProducts` | `IReadOnlyList<CategoryOptionDto>` por `NameKey` | `Forbidden` |

Reglas:

- `Duplicate("Name")` se muestra como "Ya existe una categoría con ese nombre".
- `ConfirmationRequired(n)` solo al desactivar con `n > 0` y `Confirmed = false`; la interfaz muestra
  "La categoría tiene {n} productos. Seguirán asignados a ella, pero no podrá asignarse a productos
  nuevos" y repite con `Confirmed = true`. Activar nunca pide confirmación.
- `CategoryInUse(n)`: "No se puede eliminar: la categoría tiene {n} productos. Reasígnalos o
  desactívala". `n` cuenta productos no borrados, activos o inactivos.
- `DeleteCategory`, en una sola transacción de escritura: cuenta, marca `DeletedAt` y limpia
  `CategoryId` de los productos borrados que la referencian (research §4).
- Cada éxito de alta, edición, activación, desactivación y eliminación escribe en `IAuditLog`
  (`CATEGORY_*`, entidad `Category`).

Errores nuevos en `Abstractions/Error.cs`:

```text
ConfirmationRequired(int ProductCount) : Error
CategoryInUse(int ProductCount) : Error
CategoryNotAssignable : Error   // categoría inactiva, borrada o inexistente al guardar un producto
```

## Casos de uso que cambian

| Caso de uso | Cambio |
|---|---|
| `CreateProduct` | `CategoryId?`. Si no es nulo, dentro de la transacción de escritura: la categoría existe, no está borrada y está activa; si no, `CategoryNotAssignable` ("La categoría elegida ya no está disponible. Elige otra"). |
| `UpdateProduct` | Igual, pero solo valida si `CategoryId` cambia respecto al guardado (FR-011). |
| `GetProduct` | Devuelve `CategoryId`, `CategoryName`, `CategoryIsActive`. |
| `SearchProducts` | `Category: CategoryFilter`; la fila trae `CategoryName` y `CategoryIsActive`. Se combina con texto e inactivos (FR-012). |
| `GetSalesReport` | `Category: CategoryFilter`; agrega `Categories` y calcula `ShareBasisPoints` con `ShareMath` (research §11–§12). |
| `GetInventoryReport` | `Category: CategoryFilter`; tarjetas y tabla filtradas; columna y orden por categoría (research §13). |
| `ExportReport` | Usa los mismos criterios; `ReportDocumentBuilder` agrega filtro, columna y sección (research §14). |

## Puertos

### Nuevo: `ICategoryRepository` (`Pos.Application/Categories`)

```text
Task<Category?> GetAsync(Guid id, CancellationToken)                       // no borrada, con seguimiento
Task<bool> NameExistsAsync(string nameKey, Guid? excludingId, CancellationToken)
Task<int> CountProductsAsync(Guid id, CancellationToken)                   // productos no borrados
Task<IReadOnlyList<CategoryListItemDto>> SearchAsync(string? nameKey, CategoryStatusFilter, CancellationToken)
Task<IReadOnlyList<CategoryOptionDto>> ListOptionsAsync(bool includeInactive, CancellationToken)
Task ClearFromDeletedProductsAsync(Guid id, CancellationToken)             // ExecuteUpdate
void Add(Category category)
Task<SaveOutcome> SaveChangesAsync(Category category, int? expectedVersion, CancellationToken)
```

### Cambian

- `IProductRepository.SearchAsync` / `LocatePageAsync`: respetan `ProductSearch.Category`.
- `ISalesReportReader.GetAsync`: respeta `SalesReportQuery.Category` y llena `SalesReport.Categories`
  (sin porcentajes; los pone el caso de uso).
- `IInventoryReportReader.GetAsync`: respeta `InventoryReportQuery.Category` y `InventoryReportSort.Category`.

## Domain nuevo

```text
Pos.Domain/Categories/Category.cs
Pos.Domain/Reports/ShareMath.cs    // BasisPoints(long part, long total): mitad hacia arriba, 0 si total = 0
```
