# Modelo de datos: Reportes y análisis

Solo hay un cambio de esquema. Todo lo demás son modelos de lectura (DTOs) sin tabla propia.

## Cambio de esquema

| Tabla | Cambio | Detalle |
|-------|--------|---------|
| `Products` | Columna nueva `IsCritical` | booleana, no nula, valor por defecto 0; `AddColumn`, sin reconstruir la tabla |
| `InventoryMovements` | Índice nuevo `IX_InventoryMovements_Product_CreatedAt` | (`ProductId`, `CreatedAt`) |

Migración `ReportsAnalytics`. Versión de la aplicación 0.6.0 → 0.7.0. Se revisa el SQL generado para
confirmar que no reconstruye `Products`.

## Preferencias (sin tabla)

| Clave | Tipo | Valor por defecto | Regla |
|-------|------|-------------------|-------|
| `CashDifferenceAlertBasisPoints` | entero | 500 (5 %) | de 1 a 10,000 |

## Domain

### `ReportPeriod`
- `FromDate`, `ToDate`: `DateOnly` locales, inclusivos. `ToDate ≥ FromDate`; máximo 366 días.
- `Previous()`: mismo número de días, termina el día anterior a `FromDate`.
- Fábricas por preset (`Today`, `Yesterday`, `Last7Days`, `ThisMonth`, `PreviousMonth`, `Custom`) que
  reciben la fecha local actual.

### `VariationMath` y `CashDifferenceRule`
Funciones puras en puntos base (centésimas de por ciento); `null` cuando no se puede calcular
(ver research §3).

### `Permission.ViewReports`
Nuevo valor del enumerado; solo administrador.

### `Product.IsCritical`
Booleana. Método `MarkCritical(bool)`; no exige movimientos ni altera otros campos.

## Application: modelos de lectura

Todos los importes son `long` en centavos en los DTOs (como en 008) y se convierten a `Money` para
mostrar. Las fechas llegan en UTC y se convierten a local al presentar.

### Ventas
- `SalesReportQuery`: `ReportPeriod`, `Guid? CashierId`, `bool Compare`, orden (`Folio`, `Date`,
  `Cashier`, `Total`; ascendente o descendente), `Page`, `PageSize` (100).
- `SalesReport`:
  - `Totals`: `SalesCount`, `TotalCents`, `AverageTicketCents` (total / ventas, redondeo a media
    hacia arriba; 0 sin ventas), `CashCents`, `CardCents`, `TransferCents`.
  - `Comparison?`: `PreviousTotals` y `VariationBasisPoints?`.
  - `Days`: lista `DayTotal` (ya existente en 005) con un elemento por cada día del período, incluidos
    los días sin ventas (total 0).
  - `Rows`: `SalesReportRow` (`SaleId`, `FolioText`, `CreatedAtUtc`, `CashierName`, `TotalCents`).
  - `TotalRows`, `Page`, `PageSize`.

### Arqueo
- `CashCountReportQuery`: `ReportPeriod`, `Guid? CashierId`.
- `CashCountReport`:
  - `Rows`: `CashCountRow` — `ShiftId`, `FolioText`, `CashierName`, `OpenedAtUtc`, `ClosedAtUtc?`,
    `OpeningFloatCents`, `TotalSoldCents`, `DepositsCents`, `WithdrawalsCents`, `IsOpen`,
    `ExpectedCashCents?`, `CountedCashCents?`, `DifferenceCents?`, `DifferenceBasisPoints?`, `IsAlert`.
    Para turnos abiertos los cuatro campos opcionales son siempre `null`.
  - `Totals`: `ClosedShifts`, `TotalSoldCents`, `AccumulatedDifferenceCents` (solo turnos cerrados).
  - `ThresholdBasisPoints`: el umbral usado.
- El total vendido del turno cerrado sale de la instantánea (`CashShift.TotalSoldCents`); el de turnos
  abiertos se calcula con `GetShiftTotalsAsync` (008).

### Inventario
- `InventoryReportQuery`: `DateOnly AsOfDate` (el fin del período), `StockFilter`, `SearchText`,
  orden (`Name`, `Sku`, `OnHand`), `Page`, `PageSize` (100).
- `InventoryReport`: `Counts` (`Total`, `Active`, `Low`, `Out`, `Normal`), `Rows`
  (`InventoryReportRow`: `ProductId`, `Name`, `Sku`, `OnHandThousandths`, `MinimumThousandths?`,
  `UnitName`, `DecimalPlaces`, `Status`), `TotalRows`, `Page`, `PageSize`.
- Las tarjetas y la gráfica no dependen del filtro ni de la búsqueda; la tabla sí.

### Mi turno
- `MyShiftSummary`: `ShiftId`, `FolioText`, `OpenedAtUtc`, `ClosedAtUtc?`, `OpeningFloatCents`,
  `SalesCount`, `TotalSoldCents`, `DepositsCents`, `WithdrawalsCents`, y solo si está cerrado
  `ExpectedCashCents`, `CountedCashCents`, `DifferenceCents`. Lista de movimientos del turno.

### Alertas
- `ReportAlerts`: `CashAlerts` (hasta 10 turnos cerrados en los últimos 7 días con alerta:
  cajero, fecha, diferencia) y `CriticalLowStock` (hasta 10 productos críticos con estado baja o
  agotada), más el total de cada uno.

### Exportación
- `ReportDocument`: `Title`, `PeriodText`, `FilterTexts`, `Metrics` (pares etiqueta y valor),
  `Tables` (columnas con tipo: texto, entero, importe en centavos, fecha UTC, cantidad; filas),
  `Charts` (`ChartSpec`), `Business` (nombre, dirección, teléfono o nulo), `GeneratedAtUtc`,
  `GeneratedBy`.
- `ChartSpec`: tipo (`Line`, `Bars`, `Pie`), título, series de (etiqueta, valor `long`), formato de
  valor. Las barras admiten valores negativos.
- `ExportedFile`: `FileName`, `ContentType`, `Bytes`.

## Relaciones

Los reportes leen `Sales`, `SalePayments`, `CashShifts`, `CashMovements`, `Users`, `Products`,
`ProductStocks` e `InventoryMovements`; ninguna escritura. Estados de venta cancelada y de turno
abierto se toman de las enumeraciones existentes (`SaleStatus`, `CashShiftStatus`).
