# Contrato: casos de uso y puertos de Inventario

Amplía `specs/003-product-catalog-improvements/contracts/use-cases.md`. Todo caso de uso devuelve
un `Result` o un `Result<T>`. Las violaciones de reglas se devuelven como `ValidationFailed`, con
un `FieldError` por campo; no se agrega ningún tipo de `Error` nuevo.

## Puertos (Application) ➕

### `IWriteTransactions` (`Pos.Application.Abstractions`)

```text
Task<IWriteTransaction> BeginAsync(CancellationToken)
IWriteTransaction : IAsyncDisposable
    Task CommitAsync(CancellationToken)
```

- La transacción toma el candado de escritura al iniciar (`BEGIN IMMEDIATE`, research §5).
- Si se descarta sin `CommitAsync`, revierte.
- Comparte la unidad de trabajo (el `DbContext` del ámbito) con los repositorios.

### `IInventoryRepository` (`Pos.Application.Inventory`)

| Miembro | Descripción |
|---|---|
| `Task<ProductStock?> GetStockAsync(Guid productId, ct)` | Existencia con seguimiento de cambios, o nula si el producto no tiene movimientos |
| `Task<bool> HasMovementsAsync(Guid productId, ct)` | Si hay fila en `ProductStocks` |
| `void AddStock(ProductStock)` / `void AddMovement(InventoryMovement)` | No existen `Update` ni `Remove` de movimientos |
| `Task<SaveOutcome> SaveChangesAsync(ct)` | `Saved`, o `Conflict` si falla la concurrencia o el índice único de la secuencia |
| `Task<StockPage> SearchStockAsync(StockSearch, ct)` | Existencias paginadas, ordenadas por `NameSearch` y `Sku` |
| `Task<StockAlertCounts> CountAlertsAsync(ct)` | `(long Low, long Out)`; solo productos activos que controlan inventario y no borrados. Usa el mismo predicado que `SearchStockAsync` |
| `Task<MovementPage> SearchMovementsAsync(MovementSearch, ct)` | Historial paginado |

Los tipos que usa el repositorio:

- `StockSearch(string? NameText, string? SkuText, string? BarcodeText, bool BarcodeExact, StockFilter Filter, bool IncludeInactive, int Page, int PageSize)`.
  - `StockFilter` vale `All`, `Normal`, `Low` u `Out`.
- `StockItemDto(Guid ProductId, string Name, string Sku, string? Barcode, string UnitCode, string UnitName, int DecimalPlaces, long OnHandThousandths, long? MinimumThousandths, StockStatus Status, bool IsActive)`.
- `MovementSearch(Guid? ProductId, MovementType? Type, DateTime? FromUtc, DateTime? ToUtcExclusive, int Page, int PageSize)`.
- `MovementDto(Guid Id, DateTime CreatedAtUtc, Guid ProductId, string ProductName, string ProductSku, string UnitName, int DecimalPlaces, MovementType Type, long QuantityThousandths, long ResultingStockThousandths, string? Reason, string? Reference, Guid CreatedBy, string CreatedByName)`.
  - `CreatedByName` lo resuelve el caso de uso (research §10).
- `StockPage` y `MovementPage`: la misma forma que `ProductPage`, con `Items`, `TotalCount`,
  `Page`, `PageSize` y `TotalPages`, y `DefaultPageSize = 100`.

### `IProductRepository` ✏️

- `SearchAsync` amplía `ProductListItemDto` con:
  - `bool TracksInventory`.
  - `long? OnHandThousandths`: nulo si el producto no controla inventario.
  - `int DecimalPlaces`.
- `ProductDto` agrega:
  - `bool TracksInventory`.
  - `long? MinimumStockThousandths`.
  - `long? OnHandThousandths`: solo lectura (FR-005).
  - `bool HasMovements`.
  - `int DecimalPlaces`.

### `SystemUser` (`Pos.Application.Abstractions`) ➕

Contiene `Id` y `DisplayName = "Sistema"`. `SystemCurrentUser` (Infrastructure) lo usa en lugar
de su constante propia.

## Casos de uso

### RegisterMovement ➕ (`Inventory/RegisterMovement`)

- **Entrada**: `RegisterMovementCommand(Guid ProductId, MovementType Type, string QuantityText, string? Reason, string? Reference)`.
- **Salida**: `Result<MovementDto>`.
- **Flujo**:
  1. El validador revisa la forma: tipo válido, cantidad capturada, motivo de hasta 250
     caracteres y referencia de hasta 50.
  2. Abre `IWriteTransactions.BeginAsync`.
  3. Carga el producto; si no existe, devuelve `NotFound`. Carga la unidad y el `ProductStock`
     (o usa `ProductStock.Start`).
  4. Valida las reglas del negocio y devuelve `ValidationFailed` con estos campos y mensajes:

     | Campo | Mensaje |
     |---|---|
     | `ProductId` | "Este producto no controla inventario." / "El producto está inactivo; actívelo para registrar movimientos." |
     | `Type` | "Este producto ya tiene movimientos; registre un ajuste en lugar de inventario inicial." |
     | `Quantity` | "Capture la cantidad." / "La cantidad debe ser mayor que 0." / "{Unidad} no admite decimales." / "La cantidad admite máximo 3 decimales." / "La cantidad máxima es 9,999,999.999." / "La existencia actual es {n} {unidad}; el ajuste la dejaría por debajo de cero." (FR-011) / "La existencia excedería el máximo permitido." |
     | `Reason` | "El motivo es obligatorio en los ajustes." |

  5. Llama a `stock.Record(...)`, agrega el movimiento, guarda y confirma.
  6. Ante un `Conflict`, devuelve `Conflict`. El formulario muestra "La existencia cambió mientras
     capturaba; revise y vuelva a intentar." y recarga la existencia.
- **Atomicidad**: cualquier excepción antes de `CommitAsync` revierte el movimiento y la
  existencia juntos (criterio de aceptación 1).

### SearchStock ➕ (`Inventory/SearchStock`)

- **Entrada**: `SearchStockQuery(string? Text, StockFilter Filter, bool IncludeInactive, int Page = 1)`.
- **Salida**: `Result<StockPage>`.
- El texto se normaliza igual que en `SearchProducts`: nombre sin acentos, SKU en mayúsculas y
  código de barras exacto si el texto es un código completo.
- También alimenta los selectores de producto de Movimientos y del formulario "Registrar
  movimiento". El formulario lo invoca con `IncludeInactive = false`. El filtro de Movimientos lo
  invoca con `IncludeInactive = true`, para poder elegir productos inactivos con historial.
- Solo lista productos no borrados con `TracksInventory = true`. Por omisión, solo activos
  (FR-016, FR-017).

### GetStockAlerts ➕ (`Inventory/GetStockAlerts`)

- **Salida**: `Result<StockAlertCounts>` para las tarjetas de Inicio (FR-021).

### SearchMovements ➕ (`Inventory/SearchMovements`)

- **Entrada**: `SearchMovementsQuery(Guid? ProductId, MovementType? Type, DateTime? FromUtc, DateTime? ToUtcExclusive, int Page = 1)`.
  - La pantalla convierte las fechas locales elegidas a UTC: desde las 00:00 del día inicial
    hasta las 00:00 del día siguiente al final.
- **Salida**: `Result<MovementPage>`, ordenada por `CreatedAt` descendente y después por `Id`
  descendente (FR-018).
- Si `FromUtc > ToUtcExclusive`, devuelve `ValidationFailed` en el campo `DateRange`: "La fecha
  inicial no puede ser posterior a la final."

### CreateProduct / UpdateProduct ✏️

- Los comandos agregan `bool TracksInventory` y `string? MinimumStockText`.
- `ProductRules` agrega estas validaciones:
  - `MinimumStock`: se interpreta con `Quantity.Parse` y los decimales de la unidad elegida. Se
    permite 0. Si `TracksInventory = false`, se ignora.
- `UpdateProduct` abre `IWriteTransactions` y, dentro de ella:
  - Consulta `HasMovementsAsync`.
  - Si hay movimientos, rechaza con `ValidationFailed` en estos casos:
    - `UnitCode`: "No se puede cambiar la unidad de un producto con movimientos de inventario."
    - `TracksInventory`: "No se puede dejar de controlar el inventario de un producto con
      movimientos."
  - Después guarda con la concurrencia optimista existente.

### GetProduct / SearchProducts ✏️

Devuelven los campos nuevos de `ProductDto` y `ProductListItemDto`.

## Mensajes

Viven en `InventoryMessages` (Application), en español; ver las tablas de arriba. Los mensajes de
`MinimumStock` usan los mismos textos que `Quantity`, con el sujeto "La existencia mínima".
