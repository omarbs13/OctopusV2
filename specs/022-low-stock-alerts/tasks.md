---

description: "Lista de tareas para Alertas inteligentes de bajo stock (022)"
---

# Tasks: Alertas inteligentes de bajo stock

**Input**: Documentos de diseño en `specs/022-low-stock-alerts/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: se incluyen solo las pruebas que exige la constitución v1.2.0 (Principio VI) y el plan
(research §14): reglas de Domain (`StockAlertRule`, `StockAlertDedup`, punto de reorden en
`Product`), caso de uso sobre SQLite real (`CheckStockAlertsTests`), consistencia de inventario
(`StockAlertConsistencyTests`, obligatoria) y migración (`v0.16.0.db`, obligatoria). **Sin pruebas
de ViewModels, monitor, centro de notificaciones ni vistas**; la UI se valida con quickstart.md.

**Organization**: tareas agrupadas por historia de usuario.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece (US1, US2, US3)
- Rutas relativas a la raíz del repositorio.

## Convenciones de este repositorio

- Compilar: `dotnet build -v q` (0 advertencias; `TreatWarningsAsErrors`).
- Al implementar se ejecutan solo las pruebas del proyecto modificado, p. ej.
  `dotnet test --project tests/Pos.Domain.Tests --verbosity quiet`.
- Código en inglés; textos de UI, mensajes y documentación en español. Todos los textos de Desktop
  en `src/Pos.Desktop/Resources/Strings.resx`.
- Cantidades en milésimas (`long`) con el value object `Quantity`; ver `MinimumStock` como modelo
  para todo lo del punto de reorden.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: versión de la aplicación.

- [X] T001 Cambiar `<Version>0.15.0</Version>` a `<Version>0.16.0</Version>` en Directory.Build.props

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: dato nuevo en `Products`, regla de nivel, tabla de registros, migración y filtro por
nivel del reporte "Reportes > Inventario" (destino de navegación de US2 y US3, y base de SC-005).

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase.

### Domain

- [X] T002 [P] Crear `src/Pos.Domain/Inventory/StockAlertLevel.cs` con el enum `StockAlertLevel { None = 0, Alert = 1, Urgent = 2 }` (comentario: "Valores persistidos como entero en `StockAlertAcknowledgements.Level`; no se renumeran") y la clase estática `StockAlertRule.Evaluate(StockLevel onHand, Quantity? minimum, Quantity? reorderPoint) → StockAlertLevel`, evaluada en este orden: `reorderPoint` no nulo y `onHand <= reorderPoint` → `Urgent`; `minimum` no nulo y `onHand <= minimum` → `Alert`; otro caso → `None`. Independiente de `StockStatusRule` (src/Pos.Domain/Inventory/StockStatus.cs): un producto en 0 o negativo es `Out` y además puede ser `Urgent`/`Alert`. Usar los mismos tipos y comparaciones que `StockStatusRule`
- [X] T003 [P] Crear la entidad técnica `src/Pos.Domain/Inventory/StockAlertAcknowledgement.cs` con `Id` (`Guid.CreateVersion7()`), `UserId`, `ProductId`, `Level`, `LocalDate` (`DateOnly`), `CreatedAt` (UTC), sin columnas de auditoría, versión ni `DeletedAt` (desviación justificada del Principio IV en plan.md); fábrica `Create(Guid userId, Guid productId, StockAlertLevel level, DateOnly localDate, DateTime utcNow)` que lanza `DomainException` si `level == None`, si algún Guid es `Guid.Empty` o si `utcNow.Kind != DateTimeKind.Utc`; constructor privado para EF Core
- [X] T004 Ampliar `src/Pos.Domain/Products/Product.cs`: propiedad `long? ReorderPointThousandths` y calculada `Quantity? ReorderPoint`; predicado estático `IsValidReorderPoint(Quantity? reorder, Quantity? minimum, UnitOfMeasure unit)` con la misma forma que `IsValidMinimumStock` (nulo es válido; respeta los decimales de la unidad; no supera `MaxCaptureThousandths`; 0 es válido; si hay mínimo, `reorder < minimum` estrictamente — FR-002); parámetro opcional `Quantity? reorderPoint = null` en `Create(...)` y `Update(...)` que se pasa a `ApplyInventory(tracks, minimum, reorder)`; `ApplyInventory` lanza `DomainException("El punto de reorden no es válido para la unidad del producto o no es menor que la existencia mínima.")` si `tracks && !IsValidReorderPoint(...)` y guarda `ReorderPointThousandths = null` cuando `tracks == false` (FR-003)
- [X] T005 [P] Crear `tests/Pos.Domain.Tests/Inventory/StockAlertRuleTests.cs` (xUnit v3): urgente (existencia = reorden), alerta (reorden < existencia ≤ mínimo), sin alerta (existencia > mínimo), sin umbrales → `None`, existencia 0 con solo mínimo → `Alert` (caso límite)
- [X] T006 Agregar a `tests/Pos.Domain.Tests/Products/ProductTests.cs` las pruebas del punto de reorden (depende de T004): menor que el mínimo es válido; igual al mínimo es rechazado (caso límite FR-002); decimales en unidad pieza rechazados; con `tracks = false` queda nulo

### Persistencia (Infrastructure)

- [X] T007 Mapear `ReorderPointThousandths` a la columna `ReorderPoint` (`INTEGER NULL`) en `src/Pos.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`, igual que `MinimumStock`
- [X] T008 [P] Crear `src/Pos.Infrastructure/Persistence/Configurations/StockAlertAcknowledgementConfiguration.cs`: tabla `StockAlertAcknowledgements`, PK `Id`, `UserId` y `ProductId` `NOT NULL` sin clave foránea, `Level` como `INTEGER NOT NULL`, `LocalDate` `TEXT NOT NULL` (`DateOnly`, `yyyy-MM-dd`), `CreatedAt` `TEXT NOT NULL` en UTC (mismo convertidor que el resto de fechas), índice único `IX_StockAlertAcknowledgements_User_Date_Level_Product` sobre `(UserId, LocalDate, Level, ProductId)`
- [X] T009 Agregar `DbSet<StockAlertAcknowledgement> StockAlertAcknowledgements` en `src/Pos.Infrastructure/Persistence/PosDbContext.cs` (depende de T003, T008)
- [X] T010 Generar la migración `LowStockAlerts` con `dotnet ef migrations add LowStockAlerts` en `src/Pos.Infrastructure/Persistence/Migrations/` (actualiza `PosDbContextModelSnapshot.cs`); revisar el SQL con `GenerateScript`: debe ser exactamente `ALTER TABLE "Products" ADD "ReorderPoint" INTEGER NULL;`, `CREATE TABLE "StockAlertAcknowledgements"` y `CREATE UNIQUE INDEX`, **sin reconstrucción de `Products`** (depende de T007, T009)
- [X] T011 Crear `tests/Pos.Infrastructure.Tests/SampleDatabases/LowStockAlertsMigrationTests.cs` siguiendo `ScannerBarcodeFormatsMigrationTests.cs`: el script entre `_ScannerBarcodeFormats` y `_LowStockAlerts` contiene el `ALTER TABLE ... ADD "ReorderPoint"`, el `CREATE TABLE` y el `CREATE UNIQUE INDEX` y ninguna sentencia `"ef_temp_Products"`/`DROP TABLE "Products"`; prueba `LowStockAlerts_EsLaUltimaMigracion`. Quitar la prueba `ScannerBarcodeFormats_EsLaUltimaMigracion` de `tests/Pos.Infrastructure.Tests/SampleDatabases/ScannerBarcodeFormatsMigrationTests.cs` (ya no es la última)
- [X] T012 Ampliar `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs` con datos "Desde 0.16.0": un producto activo que controla inventario con mínimo y punto de reorden y una fila de `StockAlertAcknowledgements`; generar `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.16.0.db` con `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"` y agregar en `SampleDatabaseUpgradeTests.cs` las verificaciones: en bases anteriores `ReorderPoint` queda nulo; en `v0.16.0.db` se conservan el punto de reorden y el registro (depende de T010)

### Inventario y reporte compartidos (Application / Infrastructure / Desktop)

- [X] T013 En `src/Pos.Application/Inventory/IInventoryRepository.cs`: agregar `Alert` y `Urgent` **al final** de `StockFilter` (los valores existentes no cambian) y cambiar a `public sealed record StockAlertCounts(long Low, long Out, long Alert, long Urgent);`. Ajustar los constructores posicionales en `tests/Pos.Application.Tests/TestSupport/InMemoryInventoryRepository.cs`, `tests/Pos.Desktop.Tests/Home/HomeViewModelTests.cs` y demás usos hasta que compile
- [X] T014 En `src/Pos.Infrastructure/Inventory/InventoryRepository.cs`: crear el predicado SQL privado `FilterByAlertLevel(IQueryable<StockRow>, StockAlertLevel)` junto a `FilterByStatus`, equivalente a `StockAlertRule` (urgente: `ReorderPoint != null && OnHand <= ReorderPoint`; alerta: no urgente y `MinimumStock != null && OnHand <= MinimumStock`; existencia 0 si no hay fila de existencia), solo productos activos, no borrados y que controlan inventario; que `FilterByStatus` delegue `StockFilter.Alert`/`Urgent` a él; y que `CountAlertsAsync` llene `Alert` y `Urgent` (depende de T013)
- [X] T015 [P] En `src/Pos.Application/Reports/GetInventoryReport/InventoryReportDtos.cs`: agregar `long? ReorderPointThousandths` (después del mínimo) y `StockAlertLevel Level` a `InventoryReportRow`, e `int Alert`, `int Urgent` a `InventoryCounts` (solo informativos); `InventoryReportQuery.Filter` acepta `StockFilter.Alert`/`Urgent`
- [X] T016 En `src/Pos.Infrastructure/Reports/InventoryReportReader.cs`: leer `ReorderPoint`, calcular `Level` por fila con `StockAlertRule.Evaluate` (existencia a la fecha del reporte y umbrales actuales; productos inactivos o que no controlan inventario → `None`), filtrar `Urgent` = `Level == Urgent` y `Alert` = `Level == Alert` (los agotados se incluyen si les corresponde, FR-016), y contar `Alert`/`Urgent` en `InventoryCounts` (depende de T015)
- [X] T017 Agregar la columna "Punto de reorden" después de "Existencia mínima" (con los decimales de la unidad; vacío si no tiene) a la exportación PDF/XLSX del reporte de inventario en `src/Pos.Application/Reports/Export/ReportDocumentBuilder.cs`; en el mismo archivo, agregar a `StatusFilterText` los casos `StockFilter.Alert` → "En alerta" y `StockFilter.Urgent` → "Urgente" (textos nuevos en `src/Pos.Application/Reports/Export/ReportTexts.cs`), porque hoy el caso por omisión imprimiría "Sin existencia" en el encabezado del filtro (depende de T015)
- [X] T018 Ajustar a las firmas nuevas de `InventoryReportRow`/`InventoryCounts` las pruebas existentes en `tests/Pos.Application.Tests/Reports/GetInventoryReportHandlerTests.cs`, `tests/Pos.Application.Tests/Reports/ExportReportHandlerTests.cs`, `tests/Pos.Infrastructure.Tests/Reports/InventoryReportReaderTests.cs` e `InventoryByCategoryReportTests.cs` (depende de T015–T017)
- [X] T019 En `src/Pos.Desktop/Reports/InventoryReportViewModel.cs`: agregar a la lista de filtros `new(StockFilter.Alert, Strings.Stock_FilterAlert)` ("En alerta") y `new(StockFilter.Urgent, Strings.Stock_FilterUrgent)` ("Urgente"); implementar `INavigationArgumentReceiver` (src/Pos.Desktop/Navigation/INavigationArgumentReceiver.cs): al recibir un `StockFilter`, poner el período en "Hoy", aplicar el filtro, limpiar la búsqueda, volver a la página 1 y recargar. Agregar `Stock_FilterAlert` y `Stock_FilterUrgent` en `src/Pos.Desktop/Resources/Strings.resx`
- [X] T020 En `src/Pos.Desktop/Reports/InventoryReportView.axaml`: columna "Punto de reorden" después de "Existencia mínima", con "—" cuando es nulo y el mismo formato de cantidad que el mínimo (texto en `Strings.resx`)
- [X] T021 Crear la prueba obligatoria de consistencia de inventario `tests/Pos.Infrastructure.Tests/Inventory/StockAlertConsistencyTests.cs` sobre SQLite real (`TestDb`): con productos urgente, alerta, agotado sin reorden, normal, inactivo y sin control de inventario, comprobar que `CountAlertsAsync` (`Alert`, `Urgent`) = conteo con `StockAlertRule` en memoria = filas de `InventoryReportReader` con fecha de hoy y filtro `Alert`/`Urgent` (SC-005) (depende de T014, T016)

**Checkpoint**: `dotnet build -v q` sin advertencias; pruebas de Pos.Domain.Tests, Pos.Application.Tests e Pos.Infrastructure.Tests en verde.

---

## Phase 3: User Story 1 - Configuración de umbrales por producto (Priority: P3, primera) 🎯 MVP

**Goal**: capturar, validar, guardar, mostrar y auditar el punto de reorden en el editor de producto.

**Independent Test**: editar un producto que controla inventario con mínimo 20 y reorden 5, guardar y
reabrir; capturar reorden 20 con mínimo 20 → "El punto de reorden debe ser menor que la existencia
mínima."; reorden 2.5 en piezas → error de decimales; sin "Controla inventario" el campo se oculta
y se guarda vacío (quickstart escenario 1).

- [X] T022 [P] [US1] Agregar `ReorderPoint = "ReorderPoint"` en `src/Pos.Application/Products/ProductFields.cs`, `ReorderPointNotBelowMinimum = "El punto de reorden debe ser menor que la existencia mínima."` en `src/Pos.Application/Products/ProductMessages.cs` y el sujeto "El punto de reorden" para los mensajes de formato/decimales en `src/Pos.Application/Inventory/InventoryMessages.cs` (igual que el de la existencia mínima)
- [X] T023 [US1] En `src/Pos.Application/Products/ProductRules.cs`: `ParseReorderPoint(bool tracks, string? text, string unitCode)` con `Quantity.Parse(..., allowZero: true)` y los decimales de la unidad (vacío o `tracks == false` → nulo), y la regla FluentValidation sobre el campo `ProductFields.ReorderPoint`: con mínimo capturado y válido, si `reorden >= mínimo` → `ProductMessages.ReorderPointNotBelowMinimum`; ignorada si `TracksInventory = false` (depende de T022)
- [X] T024 [P] [US1] Agregar `string? ReorderPointText` a `src/Pos.Application/Products/CreateProduct/CreateProductCommand.cs` y `src/Pos.Application/Products/UpdateProduct/UpdateProductCommand.cs` (parámetro opcional al final, `= null`, para no romper llamadas existentes)
- [X] T025 [US1] Aplicar la regla de T023 en `src/Pos.Application/Products/CreateProduct/CreateProductValidator.cs` y `src/Pos.Application/Products/UpdateProduct/UpdateProductValidator.cs` (depende de T023, T024)
- [X] T026 [US1] Pasar `reorderPoint: ProductRules.ParseReorderPoint(...)` a `Product.Create`/`Product.Update` en `src/Pos.Application/Products/CreateProduct/CreateProductHandler.cs` y `src/Pos.Application/Products/UpdateProduct/UpdateProductHandler.cs` (depende de T004, T025)
- [X] T027 [P] [US1] Agregar `long? ReorderPointThousandths` a `src/Pos.Application/Products/ProductDto.cs` y mapearlo en `src/Pos.Application/Products/ProductMapping.cs` (lo devuelve `GetProduct`)
- [X] T028 [P] [US1] Agregar el campo auditado "Punto de reorden" con los decimales de la unidad en `src/Pos.Application/Products/ProductAuditFields.cs`, igual que "Existencia mínima" (FR-004)
- [X] T029 [US1] En `src/Pos.Desktop/Products/ProductEditorViewModel.cs`: propiedad `ReorderPointText` cargada desde `ProductDto.ReorderPointThousandths` con los decimales de la unidad, enviada en los comandos de crear/actualizar, con los errores del campo `ReorderPoint` mapeados junto al control y limpiada/oculta igual que la existencia mínima cuando se desmarca "Controla inventario" (depende de T024, T027)
- [X] T030 [US1] En `src/Pos.Desktop/Products/ProductEditorView.axaml`, sección "Inventario", debajo de "Existencia mínima": campo "Punto de reorden" visible y habilitado solo con "Controla inventario", con la ayuda "Debe ser menor que la existencia mínima. Al llegar a este nivel la alerta es urgente." y los errores junto al control; textos `Editor_ReorderPoint` y `Editor_ReorderPointHelp` en `src/Pos.Desktop/Resources/Strings.resx` (depende de T029)

**Checkpoint**: US1 funcional y validable con quickstart escenario 1.

---

## Phase 4: User Story 2 - Notificaciones automáticas (Priority: P3, segunda)

**Goal**: al iniciar sesión y cada hora, notificar sin bloquear los niveles urgente y alerta, como
máximo una vez al día por producto, nivel y usuario; pulsar abre el reporte filtrado y "×" descarta.

**Independent Test**: con A-URG (3/20/5), B-ALE (12/20/5), C-OUT (0/10/—), D-OK (50/20/5), entrar
como Administrador → "Existencia urgente" (1) y "Existencia en alerta" (2); pulsar la urgente → reporte
filtrado con 1 fila; descartar; volver a entrar → nada; entrar como Cajero → ambas (quickstart
escenarios 2–7, 9, 10).

### Domain

- [X] T031 [P] [US2] Crear `src/Pos.Domain/Inventory/StockAlertDedup.cs` con `IsPending(StockAlertLevel level, IReadOnlySet<StockAlertLevel> acknowledgedToday) → bool`: `Urgent` pendiente si `acknowledgedToday` no contiene `Urgent`; `Alert` pendiente si `acknowledgedToday` está vacío (urgente → alerta no notifica); `None` nunca
- [X] T032 [US2] Crear `tests/Pos.Domain.Tests/Inventory/StockAlertDedupTests.cs`: escalamiento alerta → urgente sí; descenso urgente → alerta no; repetición del mismo nivel no; sin registros sí (depende de T031)

### Application

- [X] T033 [P] [US2] Crear el puerto `src/Pos.Application/Inventory/IStockAlertStore.cs` exactamente como en contracts/application-ports.md: `StockAlertCandidate(Guid ProductId, long OnHandThousandths, long? MinimumThousandths, long? ReorderPointThousandths)`, `GetCandidatesAsync`, `GetAcknowledgedAsync(Guid userId, DateOnly localDate, ...)` → `IReadOnlyList<(Guid ProductId, StockAlertLevel Level)>`, `Add`, `PurgeBeforeAsync(DateOnly localDate, ...)`, `SaveChangesAsync` → `SaveOutcome`
- [X] T034 [US2] Crear `src/Pos.Application/Inventory/CheckStockAlerts/StockAlertCheck.cs` (`public sealed record StockAlertCheck(int UrgentCount, int AlertCount, bool NotifyUrgent, bool NotifyAlert);`) y `src/Pos.Application/Inventory/CheckStockAlerts/CheckStockAlertsHandler.cs` con `Task<Result<StockAlertCheck>> HandleAsync(CancellationToken)`: exige `Permission.ViewInventory` con la licencia del módulo Inventario (`Forbidden`/`ModuleNotLicensed`, como `GetStockAlertsHandler`); `ICurrentUser.UserId == Guid.Empty` → `Forbidden`; `today = ReportPeriodResolver.ToLocalDate(IClock.UtcNow)`; dentro de `IWriteTransactions.BeginAsync`: evaluar candidatos con `StockAlertRule`, agrupar registros de hoy por producto, `NotifyUrgent`/`NotifyAlert` con `StockAlertDedup.IsPending`; si `NotifyUrgent` registrar todos los `Urgent` sin registro `Urgent`, si `NotifyAlert` todos los `Alert` sin registro `Alert` (`StockAlertAcknowledgement.Create(..., IClock.UtcNow)`); `PurgeBeforeAsync(today.AddDays(-7))`; `SaveChangesAsync`; confirmar. Con `SaveOutcome.Conflict` revertir la transacción (no se confirma ni la purga) y devolver éxito con `Notify* = false`. Los conteos son los totales actuales de cada nivel (depende de T002, T003, T031, T033)
- [X] T035 [US2] Registrar `CheckStockAlertsHandler` en `src/Pos.Application/DependencyInjection.cs` (depende de T034)

### Infrastructure

- [X] T036 [US2] Crear `src/Pos.Infrastructure/Inventory/StockAlertStore.cs` (implementa `IStockAlertStore` sobre `PosDbContext`): `GetCandidatesAsync` solo productos activos, no borrados, que controlan inventario y con al menos un umbral, con existencia actual (0 sin fila en `ProductStocks`), en una sola consulta `AsNoTracking`; `GetAcknowledgedAsync` por el índice `(UserId, LocalDate)`; `PurgeBeforeAsync` borra filas con `LocalDate < localDate`; `SaveChangesAsync` traduce la violación del índice único a `SaveOutcome.Conflict` como los demás repositorios. Registrarlo en `src/Pos.Infrastructure/DependencyInjection.cs` (depende de T009, T033)
- [X] T037 [US2] Crear `tests/Pos.Infrastructure.Tests/Inventory/CheckStockAlertsTests.cs` sobre SQLite real con el handler real y reloj fijo: primera revisión notifica urgente y alerta y persiste los registros; segunda revisión del mismo día no notifica; otro usuario sí; día siguiente sí; alerta notificada hoy que baja a urgente → `NotifyUrgent` (escalamiento); filas de hace más de 7 días se purgan (depende de T034–T036)

### Desktop

- [X] T038 [P] [US2] Crear `src/Pos.Desktop/Shell/NotificationItem.cs` (`Severity` Warning/Danger, `Title`, `Message`, `NavigateTo`, `Argument`, `Key`) y `src/Pos.Desktop/Shell/NotificationCenter.cs` (`ObservableCollection<NotificationItem>`, `Show(item)` reemplaza la existente con la misma `Key` y deja la de `Danger` arriba, `ActivateCommand` navega con `Navigator.NavigateAsync(item.NavigateTo, item.Argument)` y la cierra, `DismissCommand` la cierra sin navegar) y `src/Pos.Desktop/Shell/NotificationSeverityBrushConverter.cs` (`Warning` → advertencia, `Danger` → error, con los mismos colores que `StockStatusBrushConverter` para que el shell no dependa de inventario); registrarlo con `AddScoped` en `src/Pos.Desktop/Auth/AuthModule.cs` junto a `ModalHost`
- [X] T039 [US2] En `src/Pos.Desktop/Shell/MainView.axaml` (y `MainViewModel.cs` si hay que exponer el centro): pila de notificaciones abajo a la derecha, ancho 360, encima del contenido y debajo del modal/bloqueo de sesión; fondo con `NotificationSeverityBrushConverter` (T038) según `Severity`; cuerpo pulsable y botón "×" con nombre accesible "Descartar"; **todos los botones `Focusable="False"`** y la pila no intercepta el teclado (FR-012) (depende de T038)
- [X] T040 [US2] Crear `src/Pos.Desktop/Inventory/StockAlertMonitor.cs` (ámbito de sesión, misma forma que `IdleMonitor`/`LicenseClockScheduler`): `Start()` no hace nada sin `Permission.ViewInventory`; revisión inmediata y luego `DispatcherTimer` de 1 hora con `DispatcherPriority.Background`; sin solapar revisiones; cada revisión con `OperationRunner.RunQuietlyResultAsync("RevisarAlertasDeExistencia", ...)` registrando en el log conteos, si notificó, el usuario y la duración en ms (FR-015, SC-004); con `NotifyUrgent` muestra "Existencia urgente" / `Notify_UrgentMessage` con `UrgentCount` (`Key = "stock.urgent"`, destino `ReportsModule.InventoryPageId`, argumento `StockFilter.Urgent`), con `NotifyAlert` "Existencia en alerta" / `Notify_AlertMessage` con `AlertCount` (`Key = "stock.alert"`, `StockFilter.Alert`); `IDisposable` detiene el timer (depende de T034, T038)
- [X] T041 [US2] Registrar `StockAlertMonitor` con `AddScoped` en `src/Pos.Desktop/Inventory/InventoryModule.cs` y llamar a `monitor.Start()` en `src/Pos.Desktop/Shell/RootViewModel.cs` `OpenSessionAsync` después de mostrar la ventana principal del nuevo `SessionScope` (se detiene al desechar el ámbito) (depende de T040)
- [X] T042 [P] [US2] Agregar en `src/Pos.Desktop/Resources/Strings.resx`: `Notify_UrgentTitle` = "Existencia urgente", `Notify_UrgentMessage` = "{0} producto(s) llegaron a su punto de reorden.", `Notify_AlertTitle` = "Existencia en alerta", `Notify_AlertMessage` = "{0} producto(s) están en su existencia mínima o por debajo.", `Notify_Dismiss` = "Descartar"

**Checkpoint**: US2 funcional y validable con quickstart escenarios 2–7, 9, 10 y 11.

---

## Phase 5: User Story 3 - Tarjeta de alertas en Inicio (Priority: P3, tercera)

**Goal**: tarjeta "Alertas de existencia" con "Urgentes" y "En alerta", con colores y navegación
al reporte filtrado; reemplaza a "Existencia baja".

**Independent Test**: con 3 urgentes y 5 en alerta, Inicio muestra "Urgentes: 3" (peligro) y
"En alerta: 5" (advertencia); en 0, color neutro; pulsar cada cifra abre el reporte filtrado;
"Existencia baja" ya no aparece y "Sin existencia" sí (quickstart escenario 8).

- [X] T043 [P] [US3] Crear `src/Pos.Desktop/Home/DashboardCardSegment.cs` (`Label`, `Value`, `Tone` = `Neutral`/`Warning`/`Danger`, `NavigateTo`, `Argument`) y agregar a `src/Pos.Desktop/Home/DashboardCard.cs` la propiedad `IReadOnlyList<DashboardCardSegment> Segments` (vacía por omisión); las tarjetas existentes no cambian
- [X] T044 [US3] Agregar `ActivateSegmentCommand(DashboardCardSegment)` en `src/Pos.Desktop/Home/HomeViewModel.cs` que navega a `segment.NavigateTo` con `segment.Argument` (depende de T043)
- [X] T045 [US3] En la plantilla de indicadores de `src/Pos.Desktop/Home/HomeView.axaml`: si la tarjeta tiene segmentos, dibujar una fila de botones (número grande + etiqueta, color por `Tone`) enlazados a `ActivateSegmentCommand` en lugar de `Value`, con la tarjeta exterior no navegable (depende de T044)
- [X] T046 [US3] Crear `src/Pos.Desktop/Inventory/StockAlertLevelsCard.cs`: `Order = 20`, permiso `ViewInventory`, ícono `Icon.Warning`, `NavigateTo = null`; carga con `GetStockAlertsHandler` (vía `InventoryCardCounts`) cada vez que se muestra Inicio (FR-019) y expone dos segmentos: "Urgentes" (`Danger` si > 0, `Neutral` si 0, `reports.inventory` + `StockFilter.Urgent`) y "En alerta" (`Warning` si > 0, `Neutral` si 0, `StockFilter.Alert`). Quitar `LowStockCard` de `src/Pos.Desktop/Inventory/InventoryCards.cs` y en `src/Pos.Desktop/Inventory/InventoryModule.cs` reemplazar `AddDashboardCard<LowStockCard>()` por `AddDashboardCard<StockAlertLevelsCard>()`; `OutOfStockCard` sin cambios (FR-018a) (depende de T013, T014, T043)
- [X] T047 [P] [US3] En `src/Pos.Desktop/Resources/Strings.resx`: agregar `Card_StockAlertLevels` = "Alertas de existencia", `Card_StockAlertLevelsUrgent` = "Urgentes", `Card_StockAlertLevelsAlert` = "En alerta"; quitar los textos de `LowStockCard` que dejen de usarse
- [X] T048 [US3] Actualizar `tests/Pos.Desktop.Tests/Home/HomeViewModelTests.cs` para que no referencie `LowStockCard` (sustituir por `StockAlertLevelsCard` o retirar el caso; no se agregan pruebas nuevas de ViewModels) (depende de T046)

**Checkpoint**: las tres historias funcionan de forma independiente.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T049 [P] Crear `docs/alertas-de-existencia.md`: umbrales (mínimo = alerta, punto de reorden = urgente, reorden < mínimo), niveles e independencia del estado "sin existencia", cuándo se revisa (inicio de sesión y cada hora), una vez al día por producto, nivel y usuario con escalamiento, descartar, tarjeta de Inicio, permiso y licencia, log `RevisarAlertasDeExistencia`
- [X] T050 [P] Actualizar `docs/reportes.md` con los filtros "En alerta" y "Urgente", la columna "Punto de reorden" y la navegación desde notificaciones y tarjeta
- [X] T051 [P] Agregar la sección 0.16.0 a `docs/migraciones.md`: migración `LowStockAlerts` (`ADD COLUMN`, tabla e índice), purga a 7 días y base de ejemplo `v0.16.0.db`
- [X] T052 Ejecutar `dotnet build -v q` (0 advertencias) y `dotnet test --verbosity quiet` (incluye pruebas de arquitectura sobre las carpetas nuevas) y corregir lo que falle
- [ ] T053 Validar manualmente los 11 escenarios de [quickstart.md](quickstart.md), incluido el 11 (rendimiento con 10 000 productos, SC-004)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias.
- **Foundational (Phase 2)**: depende de Setup; **bloquea** todas las historias.
- **US1 (Phase 3)**, **US2 (Phase 4)**, **US3 (Phase 5)**: dependen solo de Foundational; pueden
  hacerse en paralelo o en orden US1 → US2 → US3.
- **Polish (Phase 6)**: después de las historias deseadas.

### User Story Dependencies

- **US1**: solo Foundational (T004 para `Product.Create/Update`).
- **US2**: solo Foundational (T002, T003, T009 y el filtro del reporte T019 para la navegación).
  Para probarla de punta a punta hacen falta productos con punto de reorden: sin US1 se capturan con
  la base de ejemplo o directamente en SQLite.
- **US3**: solo Foundational (T013, T014 para los conteos y T019 para la navegación).
- `Strings.resx` lo tocan T019, T030, T042 y T047: si se trabajan en paralelo, combinar con cuidado.

### Within Each Phase

- Domain → persistencia/puertos → casos de uso → Infrastructure → Desktop.
- Las pruebas de una regla se escriben junto con ella y deben pasar antes de seguir.

### Parallel Opportunities

- Phase 2: T002, T003 y T005 en paralelo; luego T008 en paralelo con T004/T007; T015 en
  paralelo con T014, y después T016 y T017 (ambas dependen de T015).
- US1: T022, T024, T027 y T028 en paralelo.
- US2: T031 (y luego T032), T033, T038 y T042 en paralelo.
- US3: T043 y T047 en paralelo.
- Polish: T049, T050 y T051 en paralelo.

---

## Parallel Example: User Story 2

```bash
# Primero, en paralelo:
Task: "StockAlertDedup en src/Pos.Domain/Inventory/StockAlertDedup.cs"
Task: "Puerto IStockAlertStore en src/Pos.Application/Inventory/IStockAlertStore.cs"
Task: "NotificationItem/NotificationCenter en src/Pos.Desktop/Shell/"
Task: "Textos Notify_* en src/Pos.Desktop/Resources/Strings.resx"

# Después: CheckStockAlertsHandler → StockAlertStore → CheckStockAlertsTests;
# en paralelo con eso: MainView.axaml → StockAlertMonitor → registro en RootViewModel.
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 + Phase 2 (incluye migración, base de ejemplo y consistencia de inventario).
2. Phase 3 (US1): el punto de reorden se captura, valida, guarda y audita; el reporte ya filtra por
   nivel.
3. **Detenerse y validar** con quickstart escenario 1.

### Incremental Delivery

1. Setup + Foundational → base lista.
2. US1 → validar → entregable (configuración de umbrales).
3. US2 → validar → notificaciones automáticas (objetivo principal de la funcionalidad).
4. US3 → validar → tarjeta de Inicio.
5. Polish → documentación y validación completa.

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes.
- La migración publicada no se modifica después; cualquier corrección va en una migración nueva.
- Confirmar con `GenerateScript` que `Products` no se reconstruye antes de integrar (Principio IV).
- Hacer commit por tarea o por grupo lógico.
