# Contrato: casos de uso y puertos de Application

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

## ➕ `CheckStockAlerts` (`Application/Inventory/CheckStockAlerts/`)

```csharp
public sealed record StockAlertCheck(int UrgentCount, int AlertCount, bool NotifyUrgent, bool NotifyAlert);

public sealed class CheckStockAlertsHandler
{
    Task<Result<StockAlertCheck>> HandleAsync(CancellationToken cancellationToken);
}
```

- **Permiso**: `ViewInventory` (con la licencia del módulo Inventario). Sin permiso → `Forbidden`;
  sin licencia → `ModuleNotLicensed`. El monitor no muestra nada en ambos casos.
- **Usuario**: `ICurrentUser.UserId`; con `Guid.Empty` (sin sesión) → `Forbidden`.
- **Fecha**: `today = ReportPeriodResolver.ToLocalDate(IClock.UtcNow)`.
- **Transacción** (`IWriteTransactions.BeginAsync`):
  1. `candidates = store.GetCandidatesAsync()`; nivel de cada uno con `StockAlertRule.Evaluate`.
  2. `acks = store.GetAcknowledgedAsync(userId, today)` → por producto, el conjunto de niveles.
  3. `NotifyUrgent` = algún `Urgent` con `StockAlertDedup.IsPending`; ídem `NotifyAlert`.
  4. Si `NotifyUrgent`, registra **todos** los `Urgent` de hoy que no tengan registro `Urgent`;
     si `NotifyAlert`, registra todos los `Alert` sin registro `Alert`.
  5. `store.PurgeBefore(today.AddDays(-7))`, `SaveChangesAsync`, confirma.
- **Resultado**: los conteos son los totales actuales de cada nivel (coinciden con la tarjeta).
- **Conflicto** (`SaveOutcome.Conflict` por el índice único, p. ej. dos revisiones simultáneas):
  devuelve éxito con `Notify* = false`; la otra revisión ya notificó.

## ➕ `IStockAlertStore` (`Application/Inventory/IStockAlertStore.cs`)

```csharp
public sealed record StockAlertCandidate(Guid ProductId, long OnHandThousandths, long? MinimumThousandths, long? ReorderPointThousandths);

public interface IStockAlertStore
{
    /// Productos activos, no borrados, que controlan inventario y tienen al menos un umbral;
    /// existencia actual (0 sin fila de existencia).
    Task<IReadOnlyList<StockAlertCandidate>> GetCandidatesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<(Guid ProductId, StockAlertLevel Level)>> GetAcknowledgedAsync(
        Guid userId, DateOnly localDate, CancellationToken cancellationToken);

    void Add(StockAlertAcknowledgement acknowledgement);

    /// Marca para borrar las filas con LocalDate anterior a la fecha indicada (todas las personas).
    Task PurgeBeforeAsync(DateOnly localDate, CancellationToken cancellationToken);

    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}
```

Implementación: `Infrastructure/Inventory/StockAlertStore.cs`, registrada en
`Infrastructure/DependencyInjection.cs`.

## ✏️ `GetStockAlerts` / `IInventoryRepository.CountAlertsAsync`

```csharp
public sealed record StockAlertCounts(long Low, long Out, long Alert, long Urgent);
```

`Alert` y `Urgent` usan el predicado SQL `FilterByAlertLevel`, equivalente a `StockAlertRule`, solo
sobre productos activos. Firma del caso de uso sin cambios.

## ✏️ `GetInventoryReport`

- `InventoryReportQuery.Filter` acepta `StockFilter.Alert` y `StockFilter.Urgent`.
- `InventoryReportRow` agrega `ReorderPointThousandths` y `Level`; `InventoryCounts` agrega
  `Alert` y `Urgent`.
- Filtro `Urgent` = filas con `Level == Urgent`; `Alert` = `Level == Alert`. Los productos
  inactivos tienen `Level = None`.

## ✏️ `CreateProduct` / `UpdateProduct`

- Comandos: `string? ReorderPointText`.
- Validación (campo `ReorderPoint`):
  - formato y decimales con los mensajes de `InventoryMessages` y el sujeto "El punto de reorden";
  - 0 permitido;
  - ignorado si `TracksInventory = false`;
  - con mínimo capturado y válido, si `reorden >= mínimo`: "El punto de reorden debe ser menor que
    la existencia mínima.".
- La auditoría del producto incluye "Punto de reorden".
