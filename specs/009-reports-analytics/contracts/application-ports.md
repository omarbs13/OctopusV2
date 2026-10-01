# Contratos de Application: Reportes y análisis

Firmas de referencia; los nombres definitivos se fijan al implementar. Todos los casos de uso
devuelven `Result<T>` y verifican el permiso con `IAccessControl` antes de leer.

## Lectores (Application define, Infrastructure implementa)

```csharp
public interface ISalesReportReader
{
    Task<SalesReport> GetAsync(SalesReportWindow window, SalesReportQuery query, CancellationToken ct);
}

public interface ICashCountReportReader
{
    Task<CashCountReport> GetAsync(ReportWindow window, Guid? cashierId, CancellationToken ct);
}

public interface IInventoryReportReader
{
    Task<InventoryReport> GetAsync(DateTime endUtcExclusive, InventoryReportQuery query, CancellationToken ct);
}

/// <summary>Ventana UTC [FromUtc, ToUtcExclusive) más el rango local que la originó.</summary>
public sealed record ReportWindow(ReportPeriod Period, DateTime FromUtc, DateTime ToUtcExclusive);
```

`ReportPeriodResolver` convierte un `ReportPeriod` en `ReportWindow` con la zona horaria local
(misma conversión que `DayWindow` en 005). No se usan llamadas directas a `TimeZoneInfo.Local` fuera
de ese tipo, para poder probarlo.

## Casos de uso

| Caso de uso | Permiso | Entrada | Salida | Errores de negocio |
|-------------|---------|---------|--------|--------------------|
| `GetSalesReport` | `ViewReports` | `SalesReportQuery` | `SalesReport` | período inválido |
| `GetCashCountReport` | `ViewReports` | `CashCountReportQuery` | `CashCountReport` | período inválido |
| `GetInventoryReport` | `ViewInventory` | `InventoryReportQuery` | `InventoryReport` | fecha inválida |
| `GetMyShiftSummary` | `OperateShift` | `Guid? shiftId` (propio) | `MyShiftSummary` | turno inexistente o ajeno: "no encontrado" |
| `ListMyShifts` | `OperateShift` | (ninguna) | los 10 turnos más recientes del usuario | — |
| `GetReportAlerts` | `ViewReports` | (ninguna) | `ReportAlerts` | — |
| `GetReportSettings` | `ViewReports` | (ninguna) | `ReportSettings` | — |
| `SaveReportSettings` | `ManageSettings` | umbral en puntos base | éxito | fuera de 1–10,000 |
| `SetProductCritical` | `ManageProducts` | `ProductId`, `IsCritical` | éxito | producto inexistente |
| `ExportReport` | el del reporte | `ExportRequest` | `ExportedFile` | "Sin datos en este período", sin permiso |

`ExportRequest`: `ReportKind` (`Sales`, `CashCount`, `Inventory`, `MyShift`), `ExportFormat` (`Pdf`,
`Xlsx`) y los parámetros del reporte correspondiente (sin paginar). Excel solo aplica a `Sales`,
`CashCount` e `Inventory` (la especificación de "Mi turno" pide solo PDF).

Reglas comunes:

- Un reporte nunca modifica datos ni abre transacción de escritura.
- Ninguna caché: cada llamada consulta.
- `ExportReport` registra `REPORT_EXPORTED` en la bitácora después de generar el archivo.
- `SaveReportSettings` registra `REPORT_SETTINGS_CHANGED`.

## Puertos de exportación (Application define, Infrastructure implementa)

```csharp
public interface IPdfReportWriter  { byte[] Write(ReportDocument document); }
public interface IXlsxReportWriter { byte[] Write(ReportDocument document); }
public interface IChartRenderer    { byte[] RenderPng(ChartSpec chart, int widthPx, int heightPx); }
```

- `PdfReportWriter` dibuja `ChartSpec` directamente en el lienzo del PDF, sin pasar por PNG.
- `ReportDocument` no contiene datos sensibles de turnos abiertos: esa omisión se aplica al armarlo
  en Application, no en los escritores.

## Bitácora

Acciones nuevas en `AuditActions`: `REPORT_EXPORTED` ("Reporte exportado") y
`REPORT_SETTINGS_CHANGED` ("Configuración de reportes modificada"). El detalle guarda reporte,
formato, período y filtros, nunca los datos exportados.

## Cambios en contratos existentes

- `Permission`: valor nuevo `ViewReports`.
- `Product`: propiedad `IsCritical`; `ProductDto` y el formulario de producto la exponen.
- `IProductRepository`: sin cambios de firma.
