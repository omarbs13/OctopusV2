# Data Model: Alertas inteligentes de bajo stock

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md)

Leyenda: ➕ nuevo, ✏️ modificado.

## Domain

### ✏️ `Product` (`Domain/Products/Product.cs`)

| Miembro | Tipo / columna | Regla |
|---|---|---|
| ➕ `ReorderPointThousandths` | `long?`, `Products.ReorderPoint INTEGER NULL` | FR-001. Nulo si no controla inventario (FR-003) |
| ➕ `ReorderPoint` | `Quantity?` (calculada) | |
| ✏️ `Create(...)`, `Update(...)` | parámetro `Quantity? reorderPoint = null` | Pasan a `ApplyInventory` |
| ➕ `IsValidReorderPoint(Quantity? reorder, Quantity? minimum, UnitOfMeasure unit)` | `static bool` | Nulo válido; decimales de la unidad; ≤ `MaxCaptureThousandths`; si hay mínimo, `reorder < minimum` (FR-002) |

`ApplyInventory(tracks, minimum, reorder)` lanza `DomainException` ("El punto de reorden no es
válido para la unidad del producto o no es menor que la existencia mínima.") si
`tracks && !IsValidReorderPoint(...)`.

### ➕ `StockAlertLevel` (`Domain/Inventory/StockAlertLevel.cs`)

```text
None = 0, Alert = 1, Urgent = 2
```

Valores persistidos como entero en `StockAlertAcknowledgements.Level`; no se renumeran.

### ➕ `StockAlertRule` (`Domain/Inventory/StockAlertLevel.cs`)

`Evaluate(StockLevel onHand, Quantity? minimum, Quantity? reorderPoint) → StockAlertLevel`

| Condición (en orden) | Resultado |
|---|---|
| `reorderPoint` no nulo y `onHand <= reorderPoint` | `Urgent` |
| `minimum` no nulo y `onHand <= minimum` | `Alert` |
| otro caso | `None` |

Independiente de `StockStatusRule`: un producto en 0 o negativo es `Out` y además puede ser
`Urgent` o `Alert`. Los productos inactivos no se evalúan (se tratan como `None` en los lectores).

### ➕ `StockAlertDedup` (`Domain/Inventory/StockAlertDedup.cs`)

`IsPending(StockAlertLevel level, IReadOnlySet<StockAlertLevel> acknowledgedToday) → bool`

| Nivel actual | Pendiente si |
|---|---|
| `Urgent` | `acknowledgedToday` no contiene `Urgent` |
| `Alert` | `acknowledgedToday` está vacío |
| `None` | nunca |

### ➕ `StockAlertAcknowledgement` (`Domain/Inventory/StockAlertAcknowledgement.cs`)

Registro técnico de lo ya notificado a un usuario en un día (research §4). Sin columnas de
auditoría ni borrado lógico (Complexity Tracking del plan).

| Miembro | Columna | Regla |
|---|---|---|
| `Id` | `Guid` PK, GUID v7 | `Guid.CreateVersion7()` |
| `UserId` | `Guid NOT NULL` | Usuario de la sesión |
| `ProductId` | `Guid NOT NULL` | Sin clave foránea (como `CategoryId`) |
| `Level` | `INTEGER NOT NULL` | `Alert` o `Urgent`; `None` no se registra |
| `LocalDate` | `TEXT NOT NULL` (`DateOnly`, `yyyy-MM-dd`) | Día calendario local del equipo |
| `CreatedAt` | `TEXT NOT NULL` (UTC) | Instante de la notificación |

- Índice único `IX_StockAlertAcknowledgements_User_Date_Level_Product (UserId, LocalDate, Level, ProductId)`.
- Fábrica `Create(Guid userId, Guid productId, StockAlertLevel level, DateOnly localDate, DateTime utcNow)`:
  rechaza `None`, `Guid.Empty` y fechas no UTC.
- Purga: filas con `LocalDate < hoy - 7 días`, en la misma transacción de la revisión.

## Application

### ✏️ Productos

- `CreateProductCommand` / `UpdateProductCommand`: ➕ `string? ReorderPointText`.
- `ProductRules`: ➕ `ParseReorderPoint(tracks, text, unitCode)` y regla de relación con el mínimo;
  campo `ProductFields.ReorderPoint = "ReorderPoint"`; mensajes en `InventoryMessages` con sujeto
  "El punto de reorden" y `ProductMessages.ReorderPointNotBelowMinimum` = "El punto de reorden debe
  ser menor que la existencia mínima.".
- `ProductDto`: ➕ `long? ReorderPointThousandths`.
- `ProductAuditFields`: ➕ "Punto de reorden" (con los decimales de la unidad).

### ✏️ Inventario

- `StockFilter`: ➕ `Alert`, `Urgent` (al final; los valores existentes no cambian).
- `StockAlertCounts`: ➕ `long Alert`, `long Urgent`.
- `IInventoryRepository.CountAlertsAsync`: llena también `Alert` y `Urgent` (solo activos).
- ➕ `IStockAlertStore` (puerto, ver [contracts/application-ports.md](contracts/application-ports.md)).
- ➕ `CheckStockAlerts` → `StockAlertCheck(int UrgentCount, int AlertCount, bool NotifyUrgent, bool NotifyAlert)`.

### ✏️ Reportes

- `InventoryReportRow`: ➕ `long? ReorderPointThousandths`, `StockAlertLevel Level`.
- `InventoryCounts`: ➕ `int Alert`, `int Urgent`.
- Exportación del reporte de inventario: ➕ columna "Punto de reorden".

## Persistencia

| Tabla | Cambio | SQL esperado |
|---|---|---|
| `Products` | ➕ `ReorderPoint INTEGER NULL` | `ALTER TABLE "Products" ADD "ReorderPoint" INTEGER NULL;` (sin reconstrucción) |
| ➕ `StockAlertAcknowledgements` | tabla nueva + índice único | `CREATE TABLE` + `CREATE UNIQUE INDEX` |

Migración `LowStockAlerts`; `Version` 0.16.0; base de ejemplo `v0.16.0.db`.

## Transiciones del nivel (por producto y por usuario, en un día)

```text
None ──(baja a ≤ mínimo)──► Alert  : notifica "alerta" si no hubo ningún registro hoy
Alert ─(baja a ≤ reorden)─► Urgent : notifica "urgente" si no hubo registro Urgent hoy
Urgent ─(sube a ≤ mínimo)─► Alert  : no notifica (ya hubo registro hoy)
* ──(sube a > mínimo)────► None    : no notifica; los registros del día se conservan
nuevo día                          : sin registros → vuelve a notificar el nivel vigente
```
