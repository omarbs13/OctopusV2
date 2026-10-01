# Tasks: Reportes y análisis

**Input**: documentos de diseño en `/specs/009-reports-analytics/` (spec.md, plan.md, research.md, data-model.md, contracts/application-ports.md, contracts/ui.md, quickstart.md)

**Prerequisites**: plan.md, spec.md

**Tests**: se aplica la política mínima de la constitución v1.2.0 (Principio VI, research §12): solo reglas con cálculos, permisos/omisión de datos sensibles, persistencia contra SQLite real, migración y prueba de humo de PDF/XLSX. No se escriben pruebas de ViewModels, vistas, gráficas ni diseño del PDF. Al implementar se ejecuta solo el proyecto de pruebas modificado (`dotnet test --verbosity quiet tests/<Proyecto>`); compilar con `dotnet build -v q`.

**Organization**: tareas agrupadas por historia de usuario para implementarlas y probarlas de forma independiente.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia de usuario a la que pertenece (US1…US7)
- Las rutas son relativas a la raíz del repositorio. Sigue los patrones de 005 (Sales), 007 (Users) y 008 (CashShifts): carpeta por funcionalidad en cada capa, casos de uso como clases simples que devuelven `Result<T>` y verifican el permiso con `IAccessControl`, textos en español en `Strings.resx` con prefijo `Reports_`, código en inglés.

## Path Conventions

- Capas: `src/Pos.Domain/`, `src/Pos.Application/`, `src/Pos.Infrastructure/`, `src/Pos.Desktop/`
- Pruebas: `tests/Pos.Domain.Tests/`, `tests/Pos.Application.Tests/`, `tests/Pos.Infrastructure.Tests/`, `tests/Pos.ArchitectureTests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dependencia nueva y esqueleto de carpetas

- [X] T001 Agregar `ClosedXML` versión 0.105.1 en `Directory.Packages.props` y la referencia `<PackageReference Include="ClosedXML" />` en `src/Pos.Infrastructure/Pos.Infrastructure.csproj` (solo Infrastructure; justificación en research §7)
- [X] T002 [P] Crear las carpetas `src/Pos.Domain/Reports/`, `src/Pos.Application/Reports/`, `src/Pos.Infrastructure/Reports/`, `src/Pos.Desktop/Reports/`, `tests/Pos.Domain.Tests/Reports/`, `tests/Pos.Application.Tests/Reports/` y `tests/Pos.Infrastructure.Tests/Reports/` (un archivo `.gitkeep` si hace falta hasta que tengan contenido)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: reglas de dominio, permiso, resolución de períodos, renderizador de gráficas, componentes compartidos de interfaz y navegación. Bloquea todas las historias.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

- [X] T003 [P] Crear `ReportPreset` (enum: `Today`, `Yesterday`, `Last7Days`, `ThisMonth`, `PreviousMonth`, `Custom`) en `src/Pos.Domain/Reports/ReportPreset.cs`
- [X] T004 [P] Crear `ReportPeriod` en `src/Pos.Domain/Reports/ReportPeriod.cs`: `FromDate`/`ToDate` como `DateOnly` locales inclusivos; rechaza `ToDate < FromDate` y rangos mayores a 366 días (devuelve `Result`/error de negocio, no excepción); `Previous()` devuelve el rango de igual número de días que termina el día anterior a `FromDate`; fábricas `Today`, `Yesterday`, `Last7Days` (incluye hoy), `ThisMonth`, `PreviousMonth`, `Custom` que reciben la fecha local actual (data-model.md, research §3)
- [X] T005 [P] Crear `VariationMath.PercentBasisPoints(long previous, long current)` en `src/Pos.Domain/Reports/VariationMath.cs`: entero con signo en centésimas de por ciento (1,250 = 12.50 %), `null` si `previous` es 0; nunca usa `double`
- [X] T006 [P] Crear `CashDifferenceRule` en `src/Pos.Domain/Reports/CashDifferenceRule.cs`: `PercentBasisPoints(differenceCents, expectedCents)` = `difference * 10000 / expected` redondeado a media hacia afuera de cero, `null` si el esperado es 0 o negativo; `IsAlert(basisPoints, thresholdBasisPoints)` = `abs(basisPoints) > threshold` (exactamente 5.00 % NO es alerta; `null` nunca es alerta)
- [X] T007 [P] Agregar el valor `ViewReports` al enumerado en `src/Pos.Domain/Users/Permission.cs` y asignarlo solo al administrador en `RolePermissions` (el Cajero NO lo recibe); agregar su nombre visible en español donde 007 centraliza los nombres de permisos
- [X] T008 [P] Pruebas de `VariationMath` en `tests/Pos.Domain.Tests/Reports/VariationMathTests.cs`: caso válido (previo 1000, actual 1125 → 1250) y período anterior en 0 → `null`
- [X] T009 [P] Pruebas de `CashDifferenceRule` en `tests/Pos.Domain.Tests/Reports/CashDifferenceRuleTests.cs`: diferencia −6,000 centavos sobre esperado 100,000 centavos (−60.00 sobre 1,000.00) → −600 y alerta con umbral 500; exactamente 500 no es alerta; esperado 0 → `null`
- [X] T010 [P] Pruebas de `ReportPeriod` en `tests/Pos.Domain.Tests/Reports/ReportPeriodTests.cs`: presets (hoy, últimos 7 días, este mes, mes anterior), `Previous()` de "últimos 7 días" y de "este mes" (mismos días de duración, no el mes calendario anterior) y rango inválido (fin antes de inicio; más de 366 días)
- [X] T011 Crear en `src/Pos.Application/Reports/` los tipos base: `ReportFields.cs` (constantes de nombres de campo), `ReportMessages.cs` (mensajes en español: período inválido, fecha inválida, "Sin datos en este período", no encontrado, umbral fuera de rango) y `ReportWindow` (record `ReportWindow(ReportPeriod Period, DateTime FromUtc, DateTime ToUtcExclusive)`) en `src/Pos.Application/Reports/ReportWindow.cs` Agregar además `SalesReportWindow(ReportWindow Current, ReportWindow? Previous)` en `src/Pos.Application/Reports/SalesReportWindow.cs` (`Previous` solo si `Compare` está activo)
- [X] T012 Crear `ReportPeriodResolver` en `src/Pos.Application/Reports/ReportPeriodResolver.cs`: convierte `ReportPeriod` a `ReportWindow` UTC `[FromUtc, ToUtcExclusive)` con la zona local, usando la misma conversión que `DayWindow` de 005 (reutilízala; no llamar `TimeZoneInfo.Local` fuera de este tipo). Registrar en DI donde 005 registra sus servicios de Application
- [X] T013 [P] Agregar a `src/Pos.Application/Reports/` los tipos de gráfica neutrales: `ChartSpec` (tipo `Line`/`Bars`/`Pie`, título, series de (etiqueta, valor `long`), formato de valor; las barras admiten negativos) en `ChartSpec.cs` y la interfaz `IChartRenderer { byte[] RenderPng(ChartSpec chart, int widthPx, int heightPx); }` en `IChartRenderer.cs` (contracts/application-ports.md)
- [X] T014 Implementar `ChartRenderer` (SkiaSharp) en `src/Pos.Infrastructure/Reports/ChartRenderer.cs`: línea, barras con signo (verde positivo / rojo negativo con signo en la etiqueta además del color) y pastel con conteo y porcentaje; exponer además un método interno de dibujo sobre un `SKCanvas` y un `SKRect` para que el PDF dibuje la misma gráfica en vectores (T053 lo usará). Registrar `IChartRenderer` en DI de Infrastructure
- [X] T015 [P] Crear `PeriodPickerView.axaml`/`.axaml.cs` y `PeriodPickerViewModel.cs` en `src/Pos.Desktop/Reports/`: botones de preset (Hoy, Ayer, Últimos 7 días, Este mes, Mes anterior, Personalizado); Personalizado muestra dos fechas y "Consultar" solo se habilita si el rango es válido, mostrando el motivo si no (fin anterior al inicio, más de 366 días); tras elegir un preset dispara el evento de consulta de inmediato; expone `ReportPeriod` actual (contracts/ui.md)
- [X] T016 [P] Crear un control/imagen reutilizable `ChartImageView` (`.axaml`/`.axaml.cs`/ViewModel) en `src/Pos.Desktop/Reports/` que muestra el PNG de `IChartRenderer` y lo vuelve a dibujar al cambiar el tamaño con un retardo corto (research §8)
- [X] T017 Crear `ReportsModule` en `src/Pos.Desktop/Reports/ReportsModule.cs` siguiendo `SalesModule`: grupo nuevo **Reportes** (orden 7, después de Ventas), visible si el usuario tiene al menos una página; las páginas se registran con `AddPage` en las tareas de cada historia. Registrar el módulo en el arranque/`Composition`. Agregar en `src/Pos.Desktop/Resources/Strings.resx` (y su `.Designer.cs` si existe) las cadenas comunes `Reports_*` (título del grupo, presets, "Sin datos en este período", "Exportar", etc.)
- [X] T018 Subir la versión a 0.7.0 en `Directory.Build.props` y generar la base de ejemplo `v0.7.0.db` **antes de tocar el modelo**: ejecutar el generador `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"` con el esquema de `CashShifts` (sin `Products.IsCritical` ni el índice nuevo; no realizar T019 antes), de modo que `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.7.0.db` tenga el esquema de 0.6.0. Ampliar `SampleData` con turnos cerrados (uno con diferencia) y uno abierto, y movimientos de inventario en fechas distintas, si aún no los tiene. Agregar el `.db` al commit y no modificarlo después (`docs/migraciones.md`, sección "Base de ejemplo de cada versión")
- [X] T019 Crear la migración EF Core `ReportsAnalytics` (Infrastructure, `src/Pos.Infrastructure/Persistence/Migrations/`; requiere T018 terminada): agregar `Product.IsCritical` (bool) en `src/Pos.Domain/Products/Product.cs` con método `MarkCritical(bool)` y configurarlo en la configuración EF de `Product` como booleana no nula con valor por defecto 0 (`AddColumn`, sin reconstruir `Products`); agregar el índice `IX_InventoryMovements_Product_CreatedAt` (`ProductId`, `CreatedAt`) a la configuración de `InventoryMovement`. Crear `ReportsAnalyticsMigrationTests` en `tests/Pos.Infrastructure.Tests/` siguiendo `CashShiftsMigrationTests` (falla si el SQL contiene `DROP TABLE` o `ef_temp_`) y ampliar `SampleDatabaseUpgradeTests` para verificar que tras migrar cada base de ejemplo `Products.IsCritical` vale 0 y el índice existe; ejecutar `dotnet test --verbosity quiet tests/Pos.Infrastructure.Tests`

**Checkpoint**: fundamentos listos — las historias pueden comenzar

---

## Phase 3: User Story 1 - Dashboard de ventas por período (Priority: P1) 🎯 MVP

**Goal**: el Administrador consulta total vendido, cantidad, ticket promedio, totales por forma de pago, gráfica por día, tabla ordenable/filtrable por cajero y comparativo con el período anterior, excluyendo ventas canceladas.

**Independent Test**: con ventas de varios días, cajeros y formas de pago (y una cancelada), abrir Reportes > Ventas como Administrador y comprobar que tarjetas, gráfica y tabla coinciden con las ventas completadas; como Cajero la opción no aparece y el caso de uso rechaza.

### Tests for User Story 1

- [X] T020 [P] [US1] Prueba de permisos en `tests/Pos.Application.Tests/Reports/GetSalesReportHandlerTests.cs`: un Cajero (sin `ViewReports`) es rechazado y no se invoca el lector; un período inválido devuelve error de negocio
- [X] T021 [P] [US1] Prueba con SQLite real en `tests/Pos.Infrastructure.Tests/Reports/SalesReportReaderTests.cs`: excluye ventas canceladas, efectivo + tarjeta + transferencia = total vendido, ticket promedio = total / ventas (media hacia arriba), `Days` incluye días sin ventas con 0, filtro por cajero, comparativo con período anterior en 0 → variación `null` (usa las fábricas/fixtures de pruebas existentes de 005/008)

### Implementation for User Story 1

- [X] T022 [P] [US1] Crear DTOs en `src/Pos.Application/Reports/GetSalesReport/SalesReportDtos.cs`: `SalesReportQuery` (`ReportPeriod`, `Guid? CashierId`, `bool Compare`, orden `Folio|Date|Cashier|Total` asc/desc, `Page`, `PageSize` = 100), `SalesReport` (`Totals` con `SalesCount`, `TotalCents`, `AverageTicketCents`, `CashCents`, `CardCents`, `TransferCents`; `Comparison?` con `PreviousTotals` y `VariationBasisPoints?`; `Days` reutilizando `DayTotal` de 005 con un elemento por cada día del período; `Rows` de `SalesReportRow` (`SaleId`, `FolioText`, `CreatedAtUtc`, `CashierName`, `TotalCents`); `TotalRows`, `Page`, `PageSize`). Importes `long` en centavos
- [X] T023 [P] [US1] Crear `ISalesReportReader` en `src/Pos.Application/Reports/ISalesReportReader.cs` con `Task<SalesReport> GetAsync(SalesReportWindow window, SalesReportQuery query, CancellationToken ct)` (contracts/application-ports.md; `SalesReportWindow` = ventana actual y, si `Compare`, la del período anterior)
- [X] T024 [US1] Crear el caso de uso `GetSalesReport` (handler + validador FluentValidation si 005 los usa así) en `src/Pos.Application/Reports/GetSalesReport/`: verifica `ViewReports` con `IAccessControl` antes de leer (modelo: `GetSalesDashboardHandler`), valida período, resuelve ventanas con `ReportPeriodResolver`, llama al lector y calcula `VariationBasisPoints` con `VariationMath` (la variación NO se calcula en Infrastructure ni en el ViewModel)
- [X] T025 [US1] Implementar `SalesReportReader` en `src/Pos.Infrastructure/Reports/SalesReportReader.cs` (EF Core, `AsNoTracking`, sin transacción de escritura): sumas y conteos en SQL con `GroupBy`+`Sum` sobre ventas completadas (`SaleStatus`) en `[FromUtc, ToUtcExclusive)`; totales por forma de pago con la misma fórmula que `GetShiftTotalsAsync` (008: `SalePayments.AmountCents` neto de cambio); ventas por día proyectando solo `CreatedAt` y `TotalCents` y agrupando en memoria por fecha local; tabla ordenada y paginada en SQL; cajeros dados de baja siguen apareciendo. Registrar en DI de Infrastructure
- [X] T026 [P] [US1] Crear `SalesReportViewModel` en `src/Pos.Desktop/Reports/SalesReportViewModel.cs`: usa `PeriodPickerViewModel`, filtro de cajero (lista de `ListCashiers` con "Todos"), interruptor "Comparar con el período anterior", ordenamiento por encabezado, paginación de 100, construcción del `ChartSpec` de línea, estado vacío "Sin datos en este período" y ejecución mediante `OperationRunner` (indicador de carga, errores con mensaje comprensible). Solo invoca el caso de uso
- [X] T027 [P] [US1] Crear `SalesReportView.axaml`/`.axaml.cs` en `src/Pos.Desktop/Reports/` según contracts/ui.md: filtros, 6 tarjetas (Total vendido, Ventas, Ticket promedio, Efectivo, Tarjeta, Transferencia), bloque de comparativo (X, Y, variación ± Z % o "No calculable"), gráfica de línea con `ChartImageView`, tabla con encabezados ordenables y paginador. Usar `MoneyConverter` y fechas en hora local; cadenas `Reports_Sales_*` en `Strings.resx`
- [X] T028 [US1] Registrar la página **Ventas** en `ReportsModule` (`src/Pos.Desktop/Reports/ReportsModule.cs`) con permiso `ViewReports`; ajustar DI de ViewModels/vistas

**Checkpoint**: Historia 1 funcional y verificable sola (MVP)

---

## Phase 4: User Story 2 - Dashboard de arqueo de caja (Priority: P1)

**Goal**: el Administrador ve el arqueo por turno con diferencia, porcentaje, alertas según umbral configurable, gráfica de barras con signo y totales; los turnos abiertos aparecen como "En curso" sin cifras de efectivo.

**Independent Test**: con turnos cerrados de distinta diferencia y uno abierto, abrir Reportes > Arqueo y comprobar −60.00 / −6 % con alerta, +2 % verde sin alerta, "En curso" sin cifras, totales sin el abierto; cambiar el umbral a 10 % quita la alerta.

### Tests for User Story 2

- [X] T029 [P] [US2] Prueba de permisos en `tests/Pos.Application.Tests/Reports/GetCashCountReportHandlerTests.cs`: Cajero rechazado; turno abierto llega con `ExpectedCashCents`, `CountedCashCents`, `DifferenceCents`, `DifferenceBasisPoints` = `null` e `IsAlert` = false
- [X] T030 [P] [US2] Prueba con SQLite real en `tests/Pos.Infrastructure.Tests/Reports/CashCountReportReaderTests.cs`: turno cerrado con esperado 1,000.00 y contado 940.00 → −6,000 centavos; turno abierto excluido de `ClosedShifts` y de `AccumulatedDifferenceCents`; turno asignado por fecha de apertura (cruza medianoche); filtro por cajero; esperado 0 → porcentaje `null`

### Implementation for User Story 2

- [X] T031 [P] [US2] Crear `ReportSettings` y `IReportSettingsStore` en `src/Pos.Application/Reports/` (`IReportSettingsStore.cs`, `ReportSettings.cs`): clave `CashDifferenceAlertBasisPoints`, entero, valor por defecto 500 (5 %), validación "de 1 a 10,000"; implementarla sobre `IPreferencesStore` igual que `SecuritySettings` de 007 en `src/Pos.Infrastructure/Reports/ReportSettingsStore.cs` y registrarla en DI
- [X] T032 [US2] Crear los casos de uso `GetReportSettings` (permiso `ViewReports`) y `SaveReportSettings` (permiso `ManageSettings`; umbral fuera de 1–10,000 → error de negocio; registra `REPORT_SETTINGS_CHANGED` en la bitácora) en `src/Pos.Application/Reports/GetReportSettings/` y `src/Pos.Application/Reports/SaveReportSettings/`; agregar `REPORT_SETTINGS_CHANGED` ("Configuración de reportes modificada") y `REPORT_EXPORTED` ("Reporte exportado") a `AuditActions` (donde viven las demás acciones de bitácora)
- [X] T033 [P] [US2] Crear DTOs en `src/Pos.Application/Reports/GetCashCountReport/CashCountReportDtos.cs`: `CashCountReportQuery` (`ReportPeriod`, `Guid? CashierId`), `CashCountRow` (`ShiftId`, `FolioText`, `CashierName`, `OpenedAtUtc`, `ClosedAtUtc?`, `OpeningFloatCents`, `TotalSoldCents`, `DepositsCents`, `WithdrawalsCents`, `IsOpen`, `ExpectedCashCents?`, `CountedCashCents?`, `DifferenceCents?`, `DifferenceBasisPoints?`, `IsAlert`), `CashCountReport` (`Rows`, `Totals` con `ClosedShifts`, `TotalSoldCents`, `AccumulatedDifferenceCents` solo de turnos cerrados, `ThresholdBasisPoints`)
- [X] T034 [P] [US2] Crear `ICashCountReportReader` en `src/Pos.Application/Reports/ICashCountReportReader.cs` con `Task<IReadOnlyList<CashCountRawRow>> GetAsync(ReportWindow window, Guid? cashierId, CancellationToken ct)`; `CashCountRawRow` (mismo archivo de DTOs de T033) trae `ShiftId`, `FolioText`, `CashierName`, `OpenedAtUtc`, `ClosedAtUtc?`, `OpeningFloatCents`, `TotalSoldCents`, `DepositsCents`, `WithdrawalsCents`, `IsOpen`, `ExpectedCashCents?`, `CountedCashCents?`, `DifferenceCents?` y **no** calcula porcentaje, alerta ni umbral: eso lo hace solo el caso de uso (T035)
- [X] T035 [US2] Crear el caso de uso `GetCashCountReport` en `src/Pos.Application/Reports/GetCashCountReport/`: verifica `ViewReports`, valida el período, llama al lector (`CashCountRawRow`), lee el umbral de `IReportSettingsStore`, completa porcentaje con `CashDifferenceRule.PercentBasisPoints` y alerta con `CashDifferenceRule.IsAlert` solo en turnos cerrados; para turnos abiertos fuerza los campos opcionales a `null`
- [X] T036 [US2] Implementar `CashCountReportReader` en `src/Pos.Infrastructure/Reports/CashCountReportReader.cs` (`AsNoTracking`): turnos por `OpenedAt` en la ventana (usa `IX_CashShifts_OpenedAt`); total vendido de turnos cerrados desde la instantánea `CashShift.TotalSoldCents`, de turnos abiertos con `GetShiftTotalsAsync` (008); ingresos y retiros desde `CashMovements`; efectivo esperado/contado/diferencia solo de turnos cerrados tomados de los datos de cierre de 008; cajeros inactivos siguen apareciendo. Registrar en DI
- [X] T037 [P] [US2] Crear `CashCountReportViewModel` en `src/Pos.Desktop/Reports/CashCountReportViewModel.cs`: período, filtro de cajero, campo de umbral en % (editable solo con `ManageSettings`; si no, solo lectura; guardar con `SaveReportSettings` y reconsultar), banner con cantidad de turnos en alerta, `ChartSpec` de barras (valor = diferencia por turno, sin barra para turnos abiertos), fila "En curso", estado vacío y `OperationRunner`
- [X] T038 [P] [US2] Crear `CashCountReportView.axaml`/`.axaml.cs` en `src/Pos.Desktop/Reports/` según contracts/ui.md (filtros, umbral, tarjetas Turnos cerrados / Total vendido / Diferencia acumulada, banner de alerta, gráfica de barras verde/roja con signo, tabla con 12 columnas, fila "En curso" con campos de efectivo vacíos). Cadenas `Reports_CashCount_*` en `Strings.resx`
- [X] T039 [US2] Registrar la página **Arqueo** en `ReportsModule` (`src/Pos.Desktop/Reports/ReportsModule.cs`) con permiso `ViewReports`

**Checkpoint**: Historias 1 y 2 funcionan de forma independiente

---

## Phase 5: User Story 3 - Dashboard de inventario (Priority: P1)

**Goal**: Administrador y Cajero ven el estado del inventario al cierre de una fecha (tarjetas, pastel, tabla filtrable/ordenable/buscable con 100 por página), sin costos.

**Independent Test**: con productos y movimientos en fechas distintas, consultar una fecha intermedia y comprobar las existencias vigentes a esa fecha; el Cajero ve la pantalla completa en solo lectura.

### Tests for User Story 3

- [X] T040 [P] [US3] Prueba de permisos en `tests/Pos.Application.Tests/Reports/GetInventoryReportHandlerTests.cs`: sin `ViewInventory` se rechaza; Administrador y Cajero (ambos con `ViewInventory`) pasan
- [X] T041 [P] [US3] Prueba con SQLite real en `tests/Pos.Infrastructure.Tests/Reports/InventoryReportReaderTests.cs` (consistencia de inventario, obligatoria): producto con existencia 10 y salida de 10 el día 20 → consultando al día 15 aparece con 10, al día 25 sin existencia; existencia igual al mínimo → "baja"; sin movimientos hasta la fecha → 0; existencia negativa (por ventas) → "sin existencia"; producto sin mínimo nunca "baja"; producto que no controla inventario o borrado no aparece; las tarjetas no cambian con el filtro de estado ni la búsqueda; paginación de 100

### Implementation for User Story 3

- [X] T042 [P] [US3] Crear DTOs en `src/Pos.Application/Reports/GetInventoryReport/InventoryReportDtos.cs`: `InventoryReportQuery` (`DateOnly AsOfDate`, `StockFilter` Todos/Normal/Baja/SinExistencia, `SearchText`, orden `Name|Sku|OnHand`, `Page`, `PageSize` = 100), `InventoryReport` (`Counts` con `Total`, `Active`, `Low`, `Out`, `Normal`; `Rows` de `InventoryReportRow` con `ProductId`, `Name`, `Sku`, `OnHandThousandths`, `MinimumThousandths?`, `UnitName`, `DecimalPlaces`, `Status`; `TotalRows`, `Page`, `PageSize`). Sin campos de costo, valuación ni margen (FR-014). Reutilizar `StockFilter`/estado de 004 si ya existen
- [X] T043 [P] [US3] Crear `IInventoryReportReader` en `src/Pos.Application/Reports/IInventoryReportReader.cs` con `Task<InventoryReport> GetAsync(DateTime endUtcExclusive, InventoryReportQuery query, CancellationToken ct)`
- [X] T044 [US3] Crear el caso de uso `GetInventoryReport` en `src/Pos.Application/Reports/GetInventoryReport/`: verifica `ViewInventory`, valida la fecha (inválida → error de negocio), convierte el fin del período al inicio del día siguiente en UTC con `ReportPeriodResolver` y llama al lector
- [X] T045 [US3] Implementar `InventoryReportReader` en `src/Pos.Infrastructure/Reports/InventoryReportReader.cs` (`AsNoTracking`): para cada producto que controla inventario, no está borrado y fue creado antes del límite, la existencia es `ResultingStockThousandths` del movimiento de mayor `Sequence` con `CreatedAt` < límite (subconsulta correlacionada apoyada en `IX_InventoryMovements_Product_CreatedAt`), 0 si no hay; estado con la misma regla que `StockStatusRule` de 004 (≤ 0 sin existencia; > 0 y ≤ mínimo baja; sin mínimo nunca baja); incluye activos e inactivos, "Activos" cuenta `IsActive`; tarjetas sobre el conjunto completo, tabla con filtro, búsqueda por nombre/SKU, orden y paginación en SQL. Registrar en DI
- [X] T046 [P] [US3] Crear `InventoryReportViewModel` y `InventoryReportView.axaml`/`.axaml.cs` en `src/Pos.Desktop/Reports/`: selector de período que toma solo la fecha final ("Al cierre del día"), filtro de estado, búsqueda, 4 tarjetas, gráfica de pastel (Normal/Baja/Sin existencia con conteo y porcentaje), tabla ordenable con paginador de 100, nota "Existencias al cierre de la fecha. Mínimos y datos del producto: valores actuales.", `QuantityConverter` para cantidades, estado vacío y `OperationRunner`. Cadenas `Reports_Inventory_*` en `Strings.resx`
- [X] T047 [US3] Registrar la página **Inventario** en `ReportsModule` (`src/Pos.Desktop/Reports/ReportsModule.cs`) con permiso `ViewInventory` (Administrador y Cajero)

**Checkpoint**: las tres pantallas de reporte funcionan

---

## Phase 6: User Story 4 - Exportación a PDF (Priority: P1)

**Goal**: cada dashboard exporta un PDF A4 horizontal con título, rango, filtros, métricas, tabla y gráficas; encabezado con datos del negocio y pie con fecha y usuario en cada página; sin datos sensibles de turnos abiertos.

**Independent Test**: aplicar filtros en cada dashboard, exportar y revisar que el PDF coincide con la pantalla, con encabezado/pie correctos; tabla larga continúa con encabezado repetido; inventario incluye todos los productos del filtro.

### Tests for User Story 4

- [X] T048 [P] [US4] Prueba en `tests/Pos.Application.Tests/Reports/ExportReportHandlerTests.cs`: el `ReportDocument` del arqueo con turno abierto NO contiene efectivo esperado, contado ni diferencia de ese turno (FR-017); el de inventario incluye todos los registros del filtro sin paginar (FR-018); período sin datos → error "Sin datos en este período" y no se genera archivo; sin permiso del reporte → rechazado; se registra `REPORT_EXPORTED` sin los datos exportados
- [X] T049 [P] [US4] Prueba de humo en `tests/Pos.Infrastructure.Tests/Reports/PdfReportWriterTests.cs`: el PDF generado empieza con `%PDF`; un documento con muchas filas produce más de una página y con `Business = null` no falla

### Implementation for User Story 4

- [X] T050 [P] [US4] Crear en `src/Pos.Application/Reports/Export/` los tipos `ReportDocument` (`Title`, `PeriodText`, `FilterTexts`, `Metrics` como pares etiqueta/valor, `Tables` con columnas tipadas texto/entero/importe en centavos/fecha UTC/cantidad y filas, `Charts` de `ChartSpec`, `Business` nombre/dirección/teléfono o `null`, `GeneratedAtUtc`, `GeneratedBy`), `ExportRequest` (`ReportKind` Sales/CashCount/Inventory/MyShift, `ExportFormat` Pdf/Xlsx y los parámetros del reporte sin paginar), `ExportedFile` (`FileName`, `ContentType`, `Bytes`) y las interfaces `IPdfReportWriter { byte[] Write(ReportDocument) }` e `IXlsxReportWriter { byte[] Write(ReportDocument) }`
- [X] T051 [US4] Crear `ReportDocumentBuilder` en `src/Pos.Application/Reports/Export/ReportDocumentBuilder.cs`: arma un `ReportDocument` por cada `ReportKind` de Ventas, Arqueo e Inventario a partir de los reportes (consultando lectores sin paginar); **aquí** se omiten efectivo esperado, contado y diferencia de turnos abiertos (FR-017) y se incluyen todos los registros del filtro; obtiene `BusinessProfile` (datos del negocio existentes) y el usuario actual; textos de filtros y rango en español
- [X] T052 [US4] Crear el caso de uso `ExportReport` en `src/Pos.Application/Reports/Export/ExportReportHandler.cs`: verifica el permiso del reporte (`ViewReports` para Ventas/Arqueo, `ViewInventory` para Inventario), devuelve "Sin datos en este período" si no hay datos, genera con `IPdfReportWriter` (formato Pdf), nombre sugerido `ventas_2026-09-01_2026-09-30.pdf` (sin espacios ni acentos; `arqueo_…`, `inventario_<fecha>`), registra `REPORT_EXPORTED` en la bitácora con reporte, formato, período y filtros (nunca los datos exportados). Sin tocar el sistema de archivos. El formato Xlsx se conecta en US5
- [X] T053 [US4] Implementar `PdfReportWriter` en `src/Pos.Infrastructure/Reports/PdfReportWriter.cs` con `SKDocument.CreatePdf`: A4 horizontal 842 × 595 pt; encabezado con nombre, dirección y teléfono del negocio y pie con fecha/hora de generación y usuario en cada página (si `Business` es `null`, sin encabezado roto y con una línea "Faltan los datos del negocio"); título, rango, filtros y métricas; gráficas dibujadas en vectores con el método de dibujo de `ChartRenderer` (T014); tabla paginada con altura de fila fija repitiendo el encabezado de columnas; tipografía por `SKTypeface.FromFamilyName` con respaldo (`Inter`, `Segoe UI`, `DejaVu Sans`, `Liberation Sans`, `Arial`, luego `SKTypeface.Default`) y verificando acentos y eñe. Registrar `IPdfReportWriter` en DI
- [X] T054 [P] [US4] Crear en `src/Pos.Desktop/Reports/` un servicio/clase `ReportExportCoordinator` que ejecute `ExportReport` con `OperationRunner`, abra `DialogService.SaveFilePickerAsync` con el nombre sugerido y la carpeta de documentos, escriba el archivo, muestre "Reporte guardado" con la ruta, no haga nada si se cancela, y si faltan los datos del negocio muestre un aviso con enlace a "Datos del negocio"; errores con mensaje comprensible y registro sin datos exportados
- [X] T055 [US4] Agregar el botón **Exportar PDF** (deshabilitado en estado vacío) a `SalesReportView`, `CashCountReportView` e `InventoryReportView` y su comando en los tres ViewModels de `src/Pos.Desktop/Reports/`, pasando los parámetros visibles (período, filtros, orden) a `ReportExportCoordinator`; cadenas en `Strings.resx`

**Checkpoint**: los tres reportes se exportan a PDF

---

## Phase 7: User Story 5 - Exportación a Excel (Priority: P2)

**Goal**: cada dashboard exporta a XLSX con hojas Resumen, Detalle y Gráficas (imágenes), con importes y fechas como valores.

**Independent Test**: exportar un reporte, abrirlo en una hoja de cálculo y comprobar hojas, cifras numéricas y gráficas; el arqueo omite los datos de turnos abiertos igual que el PDF.

### Tests for User Story 5

- [X] T056 [P] [US5] Prueba de humo en `tests/Pos.Infrastructure.Tests/Reports/XlsxReportWriterTests.cs`: el archivo abre con ClosedXML y contiene las hojas "Resumen", "Detalle" y "Gráficas"; las celdas de importe son numéricas con formato `#,##0.00` y las de fecha son fechas (no texto)

### Implementation for User Story 5

- [X] T057 [US5] Implementar `XlsxReportWriter` en `src/Pos.Infrastructure/Reports/XlsxReportWriter.cs` con ClosedXML: hoja "Resumen" (título, período, filtros, métricas), hoja "Detalle" (tabla con importes convertidos de centavos a `decimal` solo para la celda con formato `#,##0.00`, fechas locales como fecha y hora de Excel; sin fórmulas ni protección) y hoja "Gráficas" (PNG de `IChartRenderer` incrustados). Registrar `IXlsxReportWriter` en DI de Infrastructure
- [X] T058 [US5] Conectar `ExportFormat.Xlsx` en `ExportReportHandler` (`src/Pos.Application/Reports/Export/ExportReportHandler.cs`): usa `IXlsxReportWriter` y `IChartRenderer` (PNG para la hoja Gráficas), `ContentType` de XLSX, extensión `.xlsx`; `MyShift` + Xlsx devuelve error de negocio (solo PDF); misma omisión de datos sensibles porque usa el mismo `ReportDocument`
- [X] T059 [US5] Agregar el botón **Exportar Excel** a `SalesReportView`, `CashCountReportView` e `InventoryReportView` y sus comandos en los ViewModels de `src/Pos.Desktop/Reports/`, reutilizando `ReportExportCoordinator` con filtro de tipo de archivo `.xlsx` en el selector

**Checkpoint**: exportación a PDF y Excel completa

---

## Phase 8: User Story 6 - Acceso del Cajero a su turno (Priority: P2)

**Goal**: el Cajero ve y exporta a PDF el resumen de su propio turno (abierto o uno de sus últimos 10 cerrados), sin acceso a turnos ajenos ni a reportes globales.

**Independent Test**: con dos cajeros con turnos, iniciar sesión con uno y comprobar que solo ve y exporta el suyo; con turno abierto no ve esperado ni contado; con turno cerrado sí.

### Tests for User Story 6

- [X] T060 [P] [US6] Prueba (con SQLite real y permisos reales, en `tests/Pos.Infrastructure.Tests/Reports/MyShiftTests.cs`, porque los repositorios de turnos y ventas son demasiado grandes para simularlos): pedir el `shiftId` de otro usuario → "no encontrado"; turno abierto propio → `ExpectedCashCents`, `CountedCashCents` y `DifferenceCents` = `null`; turno cerrado propio → los tres con valor; `ListMyShifts` devuelve solo turnos propios (máximo 10, más recientes)

### Implementation for User Story 6

- [X] T061 [P] [US6] Crear DTOs en `src/Pos.Application/Reports/GetMyShiftSummary/MyShiftSummaryDtos.cs`: `MyShiftSummary` (`ShiftId`, `FolioText`, `OpenedAtUtc`, `ClosedAtUtc?`, `OpeningFloatCents`, `SalesCount`, `TotalSoldCents`, `DepositsCents`, `WithdrawalsCents`, y solo si está cerrado `ExpectedCashCents?`, `CountedCashCents?`, `DifferenceCents?`; lista de movimientos del turno) y el resumen de lista para `ListMyShifts`
- [X] T062 [US6] Crear los casos de uso `GetMyShiftSummary` (`Guid? shiftId`; sin id devuelve el turno abierto del usuario actual) y `ListMyShifts` (10 más recientes) en `src/Pos.Application/Reports/GetMyShiftSummary/` y `src/Pos.Application/Reports/ListMyShifts/`: permiso `OperateShift`, consulta siempre por `OpenedBy = usuario actual` (nunca acepta ni devuelve turnos ajenos; uno ajeno responde "no encontrado"). Reutilizar `ICashShiftRepository` (008) y `GetShiftTotalsAsync`; si falta una consulta por propietario, agregarla a `ICashShiftRepository` y su implementación en Infrastructure sin cambiar `GetCurrentShift`
- [X] T063 [US6] Agregar el `ReportKind.MyShift` a `ReportDocumentBuilder` y `ExportReportHandler` (`src/Pos.Application/Reports/Export/`): documento con las mismas cifras que la pantalla, permiso `OperateShift`, pie con el nombre del Cajero y omisión de esperado/contado/diferencia si el turno está abierto; solo PDF
- [X] T064 [P] [US6] Crear `MyShiftViewModel` y `MyShiftView.axaml`/`.axaml.cs` en `src/Pos.Desktop/Reports/`: turno abierto (fondo inicial, ventas número y total, ingresos, retiros y lista de movimientos, sin esperado ni contado); sin turno abierto, lista de los últimos 10 cerrados y al elegir uno se muestran además esperado, contado y diferencia; botón Exportar PDF vía `ReportExportCoordinator`. Cadenas `Reports_MyShift_*` en `Strings.resx`
- [X] T065 [US6] Registrar la página **Mi turno** en `ReportsModule` dentro del grupo **Ventas**, junto a "Turnos" (008), con permiso `OperateShift` (`src/Pos.Desktop/Reports/ReportsModule.cs`; contracts/ui.md §Navegación)

**Checkpoint**: el Cajero accede a su turno sin ver información ajena

---

## Phase 9: User Story 7 - Alertas en Inicio (Priority: P3)

**Goal**: el Administrador ve en Inicio la tarjeta "Alertas" con diferencias de arqueo sobre el umbral y productos críticos con existencia baja, y puede marcar productos como críticos.

**Independent Test**: generar un turno con diferencia mayor al umbral y marcar un producto crítico con existencia baja; ambos aparecen en la tarjeta; sin alertas muestra "No hay alertas".

### Tests for User Story 7

- [X] T066 [P] [US7] Prueba con SQLite real en `tests/Pos.Infrastructure.Tests/Reports/ReportAlertsQueryTests.cs` y de permisos en `tests/Pos.Application.Tests/Reports/ReportAlertsHandlerTests.cs`: turnos cerrados de los últimos 7 días con alerta (máximo 10 y total); productos críticos activos con estado baja o agotada según `StockStatusRule` con la existencia actual (máximo 10 y total); `SetProductCritical` sin `ManageProducts` se rechaza y con producto inexistente devuelve error de negocio; Cajero rechazado en `GetReportAlerts`

### Implementation for User Story 7

- [X] T067 [P] [US7] Exponer `IsCritical` en `ProductDto` (`src/Pos.Application/Products/`) y en el mapeo producto→DTO; no cambiar la firma de `IProductRepository` (contracts/application-ports.md)
- [X] T068 [US7] Crear el caso de uso `SetProductCritical` (`ProductId`, `IsCritical`; permiso `ManageProducts`; producto inexistente → error de negocio) en `src/Pos.Application/Reports/SetProductCritical/`, usando `Product.MarkCritical(bool)` y el repositorio de productos existente
- [X] T069 [P] [US7] Crear DTO `ReportAlerts` (`CashAlerts`: hasta 10 turnos cerrados en los últimos 7 días con alerta — cajero, fecha, diferencia — y su total; `CriticalLowStock`: hasta 10 productos críticos con estado baja o agotada y su total) y el caso de uso `GetReportAlerts` (permiso `ViewReports`) en `src/Pos.Application/Reports/GetReportAlerts/`, apoyándose en `ICashCountReportReader`, `IReportSettingsStore` y una consulta de productos críticos (puerto nuevo `IReportAlertsReader` en `src/Pos.Application/Reports/IReportAlertsReader.cs`, que devuelve solo filas crudas de productos críticos con existencia actual; el estado se evalúa en el caso de uso con `StockStatusRule` y las alertas de arqueo con `CashDifferenceRule`) con la existencia actual
- [X] T070 [US7] Implementar `ReportAlertsReader` en `src/Pos.Infrastructure/Reports/ReportAlertsReader.cs` (`AsNoTracking`, existencia actual desde `ProductStocks`, estado con la misma regla de `StockStatusRule`) y registrarlo en DI
- [X] T071 [P] [US7] Crear `AlertsCard.cs` en `src/Pos.Desktop/Reports/AlertsCard.cs` siguiendo `SalesCards.cs` y registrarla con `AddDashboardCard` en `ReportsModule`: visible solo con `ViewReports`; dos secciones (diferencias de arqueo sobre el umbral y productos críticos con existencia baja o agotada), hasta 10 de cada una con el total; sin alertas muestra "No hay alertas"
- [X] T072 [P] [US7] Agregar la casilla "Producto crítico" al formulario de producto en `src/Pos.Desktop/Products/` (ViewModel y vista) y una acción "Marcar/Quitar crítico" en la tabla de Existencias (`src/Pos.Desktop/Inventory/`), ambas solo visibles con `ManageProducts`. **Decisión**: ambas llaman a `SetProductCritical` (T068); en el formulario se invoca tras guardar el producto con éxito y solo si el valor cambió. No se modifica `UpdateProductCommand`/`UpdateProductHandler`. Cadenas `Reports_*` en `Strings.resx`

**Checkpoint**: todas las historias completas

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: rendimiento, arquitectura, documentación y validación final

- [X] T073 [P] Prueba de rendimiento `SalesReportPerformanceTests` en `tests/Pos.Infrastructure.Tests/Reports/SalesReportPerformanceTests.cs`: siembra 10,000 ventas en SQLite y afirma que el reporte de ventas y el de arqueo responden en menos de 2 s y que la generación del PDF tarda menos de 10 s (SC-002, SC-006)
- [X] T074 [P] Verificar con `dotnet test --verbosity quiet tests/Pos.ArchitectureTests` que las reglas de capas siguen pasando (Domain sin dependencias; Application solo Domain; Desktop no toca `DbContext`; ClosedXML y SkiaSharp solo en Infrastructure) y corregir cualquier violación
- [X] T075 [P] Documentar en `docs/reportes.md` (español, al estilo de `docs/turnos-de-caja.md`): qué reporta cada pantalla, permisos, umbral de alerta, cómo se calcula el inventario histórico, exportaciones y bitácora (`REPORT_EXPORTED`, `REPORT_SETTINGS_CHANGED`); actualizar `docs/usuarios-y-permisos.md` con el permiso `ViewReports` y el acceso a "Mi turno"; actualizar `README.md` si lista funcionalidades
- [X] T076 Revisar que todos los textos nuevos estén en `src/Pos.Desktop/Resources/Strings.resx` con prefijo `Reports_`, que ninguna cadena visible esté fija en las vistas y que la compilación termine sin advertencias (`dotnet build -v q`; las advertencias son errores)
- [ ] T077 Ejecutar la validación manual de `specs/009-reports-analytics/quickstart.md` (escenarios 1–9, incluyendo PDF en Windows y Linux con acentos y eñe) y las suites de los proyectos modificados: `dotnet test --verbosity quiet tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`, `tests/Pos.Infrastructure.Tests`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; BLOQUEA todas las historias
- **US1, US2, US3 (Phases 3–5)**: dependen de Foundational; independientes entre sí y pueden ir en paralelo
- **US4 (Phase 6)**: depende de Foundational y de que existan los reportes de US1–US3 (construye documentos a partir de sus lectores y agrega botones a sus vistas)
- **US5 (Phase 7)**: depende de US4 (`ReportDocument`, `ExportReport`, `ReportExportCoordinator`)
- **US6 (Phase 8)**: lógica independiente (solo Foundational); su exportación PDF depende de US4 (T050–T054)
- **US7 (Phase 9)**: depende de US2 (`IReportSettingsStore`, `ICashCountReportReader`) y de T019 (`IsCritical`)
- **Polish (Phase 10)**: depende de las historias deseadas

### User Story Dependencies

- **US1 (P1)**: tras Foundational — sin dependencias de otras historias
- **US2 (P1)**: tras Foundational — sin dependencias de otras historias
- **US3 (P1)**: tras Foundational — sin dependencias de otras historias
- **US4 (P1)**: tras US1–US3 (exporta sus reportes); se puede entregar primero para un solo reporte y extender
- **US5 (P2)**: tras US4
- **US6 (P2)**: tras Foundational; exportación tras US4
- **US7 (P3)**: tras US2 y Foundational

### Within Each User Story

- Pruebas (política mínima) antes de la implementación cuando sea posible
- DTOs y puertos → caso de uso → lector en Infrastructure → ViewModel/Vista → registro en `ReportsModule`
- Historia completa antes de pasar a la siguiente prioridad

### Parallel Opportunities

- Foundational: T003–T010 (dominio y pruebas) en paralelo; T013, T015 y T016 en paralelo. T018 (base de ejemplo) debe ejecutarse antes de T019 (migración) y antes de cualquier cambio de esquema; T019 va después de T018, no en paralelo
- Tras Foundational, US1, US2 y US3 pueden desarrollarse en paralelo (archivos distintos; solo coinciden en `ReportsModule.cs`, `Strings.resx` y DI, por lo que los registros finales de cada historia se hacen en serie)
- Dentro de cada historia: las tareas [P] (DTOs, puertos, pruebas, ViewModel/Vista) en paralelo

---

## Parallel Example: User Story 1

```bash
# Pruebas y contratos de US1 juntos:
Task: "Prueba de permisos GetSalesReport en tests/Pos.Application.Tests/Reports/GetSalesReportHandlerTests.cs"
Task: "Prueba SalesReportReader con SQLite real en tests/Pos.Infrastructure.Tests/Reports/SalesReportReaderTests.cs"
Task: "DTOs en src/Pos.Application/Reports/GetSalesReport/SalesReportDtos.cs"
Task: "ISalesReportReader en src/Pos.Application/Reports/ISalesReportReader.cs"

# Interfaz de US1 en paralelo una vez exista el caso de uso:
Task: "SalesReportViewModel en src/Pos.Desktop/Reports/SalesReportViewModel.cs"
Task: "SalesReportView en src/Pos.Desktop/Reports/SalesReportView.axaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 + exportación mínima)

1. Completar Phase 1: Setup
2. Completar Phase 2: Foundational (crítico)
3. Completar Phase 3: US1 (Reportes > Ventas)
4. **DETENERSE y VALIDAR**: probar US1 de forma independiente (quickstart escenario 1 y permisos)
5. Continuar con US2 y US3 (también P1) y luego US4 para tener el ciclo completo "ver y exportar"

### Incremental Delivery

1. Setup + Foundational → base lista (migración y versión 0.7.0 incluidas)
2. US1 → probar → demo (MVP)
3. US2 → probar → demo
4. US3 → probar → demo
5. US4 (PDF) → probar en Windows y Linux → demo
6. US5 (Excel), US6 (Mi turno), US7 (Alertas) en ese orden de prioridad
7. Cada historia agrega valor sin romper las anteriores

### Notes

- [P] = archivos distintos, sin dependencias pendientes
- La etiqueta [Story] vincula cada tarea con su historia para trazabilidad
- Los reportes son de solo lectura: ninguna tarea debe abrir transacciones de escritura ni guardar resultados pre-calculados (FR-023, FR-024)
- La verificación de permisos va en el caso de uso, no solo en el menú (FR-026)
- Confirmar el compromiso tras cada tarea o grupo lógico; detenerse en cualquier checkpoint para validar la historia
