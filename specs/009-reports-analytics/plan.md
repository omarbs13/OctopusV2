# Plan de implementación: Reportes y análisis

**Rama**: `009-reports-analytics` | **Fecha**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Entrada**: especificación en `specs/009-reports-analytics/spec.md`

## Resumen

Tres reportes de solo lectura (Ventas, Arqueo, Inventario), la vista "Mi turno" del Cajero y la
exportación a PDF y Excel. Todo se calcula bajo demanda con consultas agregadas en SQL.

1. **Lectores de reportes**: tres interfaces de lectura en Application (`ISalesReportReader`,
   `ICashCountReportReader`, `IInventoryReportReader`) implementadas en Infrastructure con
   consultas agregadas sin seguimiento de cambios (research §1, §2).
2. **Reglas en Domain**: `ReportPeriod` (presets, período anterior), `VariationMath` (variación en
   puntos base, nula si el anterior es cero) y `CashDifferenceRule` (porcentaje y alerta)
   (research §3).
3. **Inventario histórico**: la existencia al cierre del período es el `ResultingStock` del último
   movimiento anterior al límite; sin movimientos vale 0 (research §4).
4. **Permisos**: permiso nuevo `ViewReports` (solo administrador); Inventario usa `ViewInventory`;
   "Mi turno" usa `OperateShift` y filtra por el usuario actual en el caso de uso (research §5).
5. **Exportación**: los casos de uso arman un `ReportDocument` neutral (aquí se omiten los datos
   sensibles de turnos abiertos); Infrastructure lo convierte a PDF con SkiaSharp y a XLSX con
   ClosedXML (research §6, §7).
6. **Gráficas**: un solo renderizador SkiaSharp (línea, barras, pastel) produce la imagen que se
   muestra en pantalla, se dibuja en el PDF y se incrusta en el XLSX (research §8).
7. **Umbral de alerta**: preferencia guardada con `IPreferencesStore`, 5 % por defecto
   (research §9). **Producto crítico**: columna nueva `Products.IsCritical` (research §10).
8. **Interfaz**: grupo "Reportes" con tres páginas, página "Mi turno" y tarjeta "Alertas" en
   Inicio ([contracts/ui.md](contracts/ui.md)).

Hay una migración, `ReportsAnalytics`: agrega `Products.IsCritical` (booleana, valor 0 por defecto)
sin reconstruir la tabla, más índices de lectura (research §2). Se agrega una dependencia nueva,
ClosedXML (research §7).

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation, SkiaSharp 3.119.4). **Nueva: ClosedXML 0.105.1** (MIT), solo en
`Pos.Infrastructure`.

**Storage**: SQLite. Migración `ReportsAnalytics`: `Products.IsCritical` y los índices de research §2.
`Version` 0.6.0 → 0.7.0. Se conserva la base de ejemplo `v0.7.0.db`, generada antes de la migración
según `docs/migraciones.md`.

**Testing**: xUnit v3 con la política mínima de la constitución v1.2.0 (research §12).

**Target Platform**: Windows y Linux de escritorio; SkiaSharp y ClosedXML no usan APIs de plataforma.

**Project Type**: aplicación de escritorio (Avalonia) en capas.

**Performance Goals**: cada reporte en menos de 2 s con 10,000 ventas en el período (SC-002); PDF de
hasta 10,000 ventas en menos de 10 s (SC-006).

**Constraints**: solo lectura, sin caché ni pre-cálculo, sin red, fechas locales en pantalla y UTC
en la base, importes con `Money`.

**Scale/Scope**: una caja, un negocio; decenas de miles de ventas y miles de productos.

## Constitution Check

*Compuerta antes de la investigación; se vuelve a evaluar tras el diseño.*

| Principio | Cumplimiento |
|-----------|--------------|
| I. La venta nunca se detiene | Los reportes solo leen; las consultas son agregadas y de corta duración, no toman transacción de escritura. El PDF/XLSX se genera en segundo plano (`OperationRunner`). Un error al exportar muestra mensaje y se registra sin cerrar la app. |
| II. Capas | Reglas y DTOs en Domain/Application; lectores (EF), PDF, XLSX y gráficas en Infrastructure. Las interfaces están en Application. No hay acceso a `DbContext` desde Desktop. Pruebas de arquitectura sin cambios. |
| III. Lógica en el núcleo | Variación, porcentaje de diferencia, alerta, estado de inventario y omisión de datos sensibles viven en Domain/Application. Los ViewModels solo invocan casos de uso. |
| IV. Integridad de datos | Sin tablas nuevas. `Products.IsCritical` con migración versionada y sin reconstrucción de tabla. Fechas en UTC y conversión a local al mostrar. Dinero con `Money`/centavos. Base de ejemplo nueva y prueba de migración. |
| V. Multiplataforma | SkiaSharp ya es dependencia (con assets para Linux); ClosedXML es administrado. La fuente del PDF se resuelve con un respaldo (research §6). |
| VI. Calidad verificable | Pruebas mínimas según research §12; persistencia contra SQLite real. |
| VII. Simplicidad | Sin repositorios genéricos ni MediatR. Un solo renderizador de gráficas. Una dependencia nueva, justificada en research §7. Sin caché. |
| VIII. Diagnóstico | Logging de fallos de exportación con reporte y período, sin datos exportados. |
| IX. Seguridad | Permiso `ViewReports`; Cajero sin acceso a ventas ni arqueo globales (verificado en el caso de uso, no solo en el menú). Exportaciones en la bitácora (FR-027). |

**Resultado**: sin violaciones. Sin entradas en Complejidad.

**Re-evaluación tras el diseño**: sin cambios; el diseño no agrega capas ni dependencias más allá de
ClosedXML.

## Project Structure

### Documentación

```text
specs/009-reports-analytics/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── application-ports.md
    └── ui.md
```

### Código fuente

```text
src/Pos.Domain/Reports/
├── ReportPeriod.cs            # rango local, presets, período anterior
├── ReportPreset.cs
├── VariationMath.cs
└── CashDifferenceRule.cs      # porcentaje en puntos base y alerta
src/Pos.Domain/Users/Permission.cs   # + ViewReports
src/Pos.Domain/Products/Product.cs   # + IsCritical

src/Pos.Application/Reports/
├── ReportFields.cs / ReportMessages.cs
├── IReportSettingsStore.cs + GetReportSettings / SaveReportSettings
├── ISalesReportReader.cs      + GetSalesReport/
├── ICashCountReportReader.cs  + GetCashCountReport/
├── IInventoryReportReader.cs  + GetInventoryReport/
├── GetMyShiftSummary/
├── GetReportAlerts/
├── SetProductCritical/
├── Export/                    # ExportReport, ReportDocument, IPdfReportWriter, IXlsxReportWriter, IChartRenderer
└── ReportPeriodResolver.cs    # presets -> ventana UTC con la zona local

src/Pos.Infrastructure/Reports/
├── SalesReportReader.cs
├── CashCountReportReader.cs
├── InventoryReportReader.cs
├── ChartRenderer.cs           # SkiaSharp
├── PdfReportWriter.cs         # SkiaSharp (SKDocument)
└── XlsxReportWriter.cs        # ClosedXML
src/Pos.Infrastructure/Persistence/Migrations/…_ReportsAnalytics.cs

src/Pos.Desktop/Reports/
├── ReportsModule.cs
├── SalesReportView / ViewModel
├── CashCountReportView / ViewModel
├── InventoryReportView / ViewModel
├── PeriodPickerView / ViewModel   # presets y rango personalizado (componente)
├── MyShiftView / ViewModel
└── AlertsCard.cs

tests/Pos.Domain.Tests/Reports/
tests/Pos.Application.Tests/Reports/
tests/Pos.Infrastructure.Tests/Reports/
```

**Decisión de estructura**: se sigue la organización por funcionalidad de 005 a 008; "Reportes" es una
carpeta nueva en cada capa, más un grupo de menú propio.

## Complexity Tracking

Sin violaciones que justificar.
