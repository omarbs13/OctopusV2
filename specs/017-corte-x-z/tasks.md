---

description: "Lista de tareas de la funcionalidad 017: Corte X y Corte Z"
---

# Tasks: Corte X y Corte Z

**Input**: Documentos de diseño en `/specs/017-corte-x-z/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/application-ports.md](contracts/application-ports.md), [contracts/ui.md](contracts/ui.md), [quickstart.md](quickstart.md)

**Tests**: se incluyen solo las pruebas que pide el plan bajo la política mínima de la constitución v1.2.0 (research §14): `ShiftCutTests` (Domain), `ShiftCutUseCaseTests` (SQLite real), `ShiftCutsMigrationTests` y `SampleDatabaseUpgradeTests`. Sin pruebas de ViewModels, vistas, ticket ni mapeos.

**Organization**: las tres historias tienen prioridad P2; se ordenan como en spec.md (Corte X → Corte Z → Histórico). Todas dependen de la Fase 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece (US1, US2, US3)

## Path Conventions

Capas en `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`; pruebas en `tests/`. Todo el código de la funcionalidad vive en las carpetas `CashShifts/` existentes de cada capa.

Comandos: `dotnet build -v q` y `dotnet test --verbosity quiet` (al implementar, solo el proyecto de pruebas modificado).

---

## Phase 1: Setup

**Purpose**: versión de la aplicación

- [X] T001 Subir `<Version>` de `0.11.0` a `0.12.0` en Directory.Build.props

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: agregado `ShiftCut`, permiso, persistencia, migración, lectura del reporte e impresión del corte, que usan las tres historias.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase.

### Domain

- [X] T002 [P] Crear el enum `ShiftCutType` (`Readout` = código `"X"`, `Closing` = código `"Z"`) con extensiones `ToCode()` / `FromCode()` siguiendo el patrón de `CashShiftStatus` en src/Pos.Domain/CashShifts/ShiftCutType.cs
- [X] T003 [P] Crear `ShiftCutFolio.Format(ShiftCutType type, long number)` que devuelve `X-000001` / `Z-000001` (cultura invariante, 6 dígitos, "crece sin truncar"), igual que `ShiftFolio`, en src/Pos.Domain/CashShifts/ShiftCutFolio.cs
- [X] T004 Crear el agregado inmutable `ShiftCut` en src/Pos.Domain/CashShifts/ShiftCut.cs (depende de T002, T003) con:
  - Campos de data-model.md: `Id` (`Guid.CreateVersion7()`), `Type`, `Number` ("≥ 1; consecutivo por tipo"), `ShiftId`, `ShiftNumber`, `RegisterCode` (`string(20)`, `CashRegister.Default`), `ShiftOpenedBy`, `ShiftOpenedAt`, `GeneratedAt` (UTC), `GeneratedBy`, `AuthorizedBy` (`Guid?`, "solo X"), `OpeningFloatCents`, `SalesCount`, `CancelledCount`, `TotalSoldCents`, `CashSalesCents`, `CashCancelledCents`, `CardCents`, `TransferCents`, `CashRefundsCents`, `NonCashRefundsCents`, `CreditNotesIssuedCents`, `OnAccountSalesCents`, `CustomerPaymentsCashCents`, `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents`, `CustomerPaymentVoidsNonCashCents`, `DepositsCents`, `WithdrawalsCents`, `ExpectedCashCents`, `CountedCashCents` (`long?`, "Solo Z; nulo en X"), `DifferenceCents` (`long?`, "Solo Z; contado − esperado, con signo"), `Comment` (`string(250)?`, "Solo Z"), auditoría (`CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`), `DeletedAt` ("Siempre nulo") y `Version` ("Token de concurrencia; siempre 1"), todos con setters privados
  - Derivados no persistidos: `Folio` (`ShiftCutFolio.Format`), `ShiftFolio` (`ShiftFolio.Format(ShiftNumber)`), `Difference` (`CashDifference?`)
  - Fábrica `Readout(long number, CashShift shift, ShiftSalesTotals totals, Guid generatedBy, Guid? authorizedBy, DateTime utcNow)`: exige turno `Open` (`DomainException` "El turno ya está cerrado."), `number ≥ 1` y `generatedBy ≠ Guid.Empty`; esperado = `shift.ExpectedCash(totals)`; ingresos y retiros desde `shift`; **no modifica `shift`**
  - Fábrica `Closing(long number, CashShift shift, Guid closedBy)`: exige turno `Closed` con `ClosedAt` y `CountedCashCents` no nulos; copia las cifras ya guardadas en el turno; `GeneratedAt = shift.ClosedAt`
  - Sin ningún método de modificación (FR-013)
- [X] T005 [P] Agregar `GenerateShiftReadout` al enum en src/Pos.Domain/Users/Permission.cs y concederlo solo al Administrador e incluirlo en `Authorizable` en src/Pos.Domain/Users/RolePermissions.cs
- [X] T006 [P] Mapear `Permission.GenerateShiftReadout` → `LicensedModule.CashShifts` en src/Pos.Domain/Licensing/ModuleAccess.cs

### Application

- [X] T007 [P] Agregar a src/Pos.Application/Audit/AuditActions.cs: `ShiftReadoutGenerated` = `SHIFT_READOUT_GENERATED` ("Corte X generado"), `ShiftCutReprinted` = `SHIFT_CUT_REPRINTED` ("Corte reimpreso"), entidad `ShiftCutEntity` = `ShiftCut`; cambiar los textos de `SHIFT_CLOSED` / `SHIFT_CLOSED_BY_ADMIN` a "Turno cerrado (Corte Z)" / "Turno cerrado por administrador (Corte Z)"
- [X] T008 [P] Crear src/Pos.Application/CashShifts/ShiftCutDtos.cs con `ShiftCutListItemDto`, `ShiftCutPage` (`DefaultPageSize = 100`, `TotalPages` igual que `ShiftPage`), `ShiftCutReportDto` (firma exacta de contracts/application-ports.md) y `ShiftCutSearch` (tipo, `FromUtc` inclusivo, `ToUtc` exclusivo, usuario, página, tamaño)
- [X] T009 [P] Agregar `CutNumber` a src/Pos.Application/CashShifts/CashShiftFields.cs y los mensajes de corte (sin turno abierto para Corte X, folio duplicado, `From > To`) a src/Pos.Application/CashShifts/CashShiftMessages.cs
- [X] T010 Ampliar el puerto src/Pos.Application/CashShifts/ICashShiftRepository.cs con `Task<long> NextCutNumberAsync(ShiftCutType type, CancellationToken ct)`, `void AddCut(ShiftCut cut)`, `Task<ShiftCutReportDto?> GetCutReportAsync(Guid cutId, CancellationToken ct)` y `Task<ShiftCutPage> SearchCutsAsync(ShiftCutSearch search, CancellationToken ct)`, con comentarios XML como en el contrato (depende de T004, T008)

### Infrastructure

- [X] T011 [P] Crear src/Pos.Infrastructure/Persistence/Configurations/ShiftCutConfiguration.cs (depende de T004): tabla `ShiftCuts`; `Type` como texto `X`/`Z` vía `ToCode`/`FromCode`; `RegisterCode` máx. 20; `Comment` máx. 250; `Version` como token de concurrencia; ignorar `Folio`, `ShiftFolio`, `Difference`; FK `ShiftId → CashShifts.Id` con `DeleteBehavior.Restrict`; sin FK para `GeneratedBy`/`AuthorizedBy`; índices `IX_ShiftCuts_Type_Number` (`Type, Number`, único), `IX_ShiftCuts_ClosingPerShift` (`ShiftId` único con filtro `"Type" = 'Z'`), `IX_ShiftCuts_ShiftId`, `IX_ShiftCuts_GeneratedAt` (`GeneratedAt, Id`), `IX_ShiftCuts_GeneratedBy_GeneratedAt`
- [X] T012 Agregar `DbSet<ShiftCut> ShiftCuts` a src/Pos.Infrastructure/Persistence/PosDbContext.cs (depende de T011)
- [X] T013 Implementar en src/Pos.Infrastructure/CashShifts/CashShiftRepository.cs (depende de T010, T012):
  - `NextCutNumberAsync`: `MAX(Number) + 1` del tipo dentro de la transacción de escritura, como `NextNumberAsync`
  - `AddCut`
  - `GetCutReportAsync`: proyección a `ShiftCutReportDto` resolviendo nombres de `GeneratedBy`, `AuthorizedBy` y dueño del turno, nombre de caja y `ShiftCreditTotals`
  - `SearchCutsAsync`: filtros tipo, rango UTC y `GeneratedBy`; orden `GeneratedAt DESC, Id DESC`; conteo total y página
  - `SaveChangesAsync`: traducir la violación de `IX_ShiftCuts_Type_Number` o `IX_ShiftCuts_ClosingPerShift` a `SaveStatus.Duplicate` con `CashShiftFields.CutNumber`
- [X] T014 Generar la migración con `dotnet ef migrations add ShiftCuts` en src/Pos.Infrastructure/Persistence/Migrations/ y revisar el SQL: solo `CREATE TABLE "ShiftCuts"` y sus cinco índices, **sin reconstruir ninguna tabla** ni insertar filas (FR-010a) (depende de T012)

### Lectura e impresión del corte (compartidas por US1, US2 y US3)

- [X] T015 [P] Crear `GetShiftCutQuery(Guid CutId)` y su handler en src/Pos.Application/CashShifts/GetShiftCut/GetShiftCutQuery.cs y src/Pos.Application/CashShifts/GetShiftCut/GetShiftCutHandler.cs: permiso base `OperateShift`; acceso si `GeneratedById == currentUser` o tiene `ManageShifts`, si no `Forbidden`; inexistente → `NotFound` (depende de T013)
- [X] T016 [P] Agregar `BuildCut(profile, ShiftCutReportDto, columns, options, timeZone)` a src/Pos.Application/Printing/Ticket/ShiftTicketBuilder.cs: extraer a un método común las filas de cifras del corte de 008; título "CORTE X" / "CORTE Z"; en X la leyenda "LECTURA PARCIAL - NO ES CIERRE DE CAJA" y sin contado/diferencia/comentario; "REIMPRESIÓN" si aplica; filas Corte, Turno, Caja, Usuario, Generado por, Autorizó (solo si aplica), Apertura y Fecha corte según el ticket de contracts/ui.md (depende de T008)
- [X] T017 Agregar `PrintSource.ShiftCut(Guid cutId)` / `ShiftCutSource` en src/Pos.Application/Printing/PrintTicket/PrintTicketCommand.cs y atenderlo en src/Pos.Application/Printing/PrintTicket/PrintTicketHandler.cs: permiso base `OperateShift`, acceso `GeneratedById == currentUser` o `ManageShifts`, usar `GetCutReportAsync` + `BuildCut`; con `IsReprint = true` registrar `SHIFT_CUT_REPRINTED`, entidad `ShiftCut`, resumen "Corte X X-000004. Turno T-000123" (depende de T007, T013, T016)
- [X] T018 Registrar `GetShiftCutHandler` en src/Pos.Application/DependencyInjection.cs (depende de T015)

### Desktop compartido

- [X] T019 [P] Agregar a src/Pos.Desktop/Resources/Strings.resx los textos `Nav_Cash*` (grupo "Caja", "Corte X", "Corte Z", "Histórico de cortes") y `Cut_*` de contracts/ui.md (estados sin turno, "Generar Corte X", "Hacer Corte Z", "Confirmar Corte Z", "Lectura parcial: no es un cierre de caja", "Se requiere autorización de un administrador", "No hay cortes con estos filtros.", encabezados y columnas, "Imprimir", "Reimprimir", "Cerrar")
- [X] T020 Crear el diálogo de vista del corte src/Pos.Desktop/CashShifts/ShiftCutDetailView.axaml, src/Pos.Desktop/CashShifts/ShiftCutDetailView.axaml.cs y src/Pos.Desktop/CashShifts/ShiftCutDetailViewModel.cs: carga `GetShiftCut`; encabezado "Corte X X-000004"/"Corte Z Z-000001", turno, caja, dueño, generado por, "Autorizó: {nombre}" si aplica, apertura y fecha; bloques de cifras de contracts/ui.md; solo Z: contado, diferencia con "Sobrante"/"Faltante"/"Cuadrado" y comentario; solo X: leyenda; botón "Imprimir" (recién generado) o "Reimprimir" (`IsReprint = true`) vía `TicketPrintingService` con `PrintSource.ShiftCut`, y "Cerrar" (depende de T015, T017, T019)
- [X] T021 Crear src/Pos.Desktop/CashShifts/CashModule.cs con `AddCashModule()` que registra el grupo de navegación `cash` ("Caja", orden 6) y el ViewModel/vista de `ShiftCutDetail`; llamarlo desde src/Pos.Desktop/Composition/HostBuilder.cs junto a `AddCashShiftsModule()` (depende de T020)

### Pruebas obligatorias de la fase

- [X] T022 [P] Crear tests/Pos.Domain.Tests/CashShifts/ShiftCutTests.cs: (a) la instantánea de `Readout` y de `Closing` da el mismo esperado que `CashShiftMath` y no altera el turno; (b) `Readout` sobre turno cerrado y `Closing` sobre turno abierto/sin conteo lanzan `DomainException` (depende de T004)
- [X] T023 Generar la base de ejemplo tests/Pos.Infrastructure.Tests/SampleDatabases/v0.12.0.db ampliando tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs ("Desde 0.12.0": al menos un turno cerrado sin Z anterior a la actualización, un turno abierto, un Corte X y un Corte Z), según docs/migraciones.md (depende de T014)
- [X] T024 [P] Crear tests/Pos.Infrastructure.Tests/SampleDatabases/ShiftCutsMigrationTests.cs (tabla y cinco índices creados; bases anteriores sin filas en `ShiftCuts`) y agregar a tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs la verificación de `v0.12.0.db` (cortes conservados, folios intactos) (depende de T023)
- [X] T025 Ejecutar `dotnet build -v q` y las pruebas de tests/Pos.Domain.Tests, tests/Pos.Infrastructure.Tests y tests/Pos.ArchitectureTests; 0 errores y 0 advertencias

**Checkpoint**: el corte existe, se persiste, se lee e imprime; las historias pueden empezar.

---

## Phase 3: User Story 1 - Corte X: lectura parcial del turno (Priority: P2) 🎯 MVP

**Goal**: desde "Caja > Corte X" generar, mostrar e imprimir la lectura del turno abierto, con folio X propio, sin modificar el turno; el Cajero solo con autorización de Administrador.

**Independent Test**: con un turno abierto con ventas, generar un Corte X, imprimirlo, comprobar que el turno sigue abierto con las mismas cifras, vender otra vez y generar un segundo Corte X que incluya la venta (quickstart Escenarios 1 y 2).

### Tests for User Story 1

- [X] T026 [US1] Crear tests/Pos.Infrastructure.Tests/CashShifts/ShiftCutUseCaseTests.cs con las pruebas del Corte X sobre SQLite real: (a) el Corte X no cambia `Version`, cifras ni esperado del turno, y su instantánea no cambia tras otra venta (SC-001, escenarios 1.2 y 1.4); (b) folios X consecutivos `X-000001`, `X-000002`; (c) Cajero sin concesión → `Forbidden(CanBeAuthorized: true)` y con concesión genera el corte con `AuthorizedBy`; (d) sin turno abierto → `ShiftRequired` y no se consume folio

### Implementation for User Story 1

- [X] T027 [US1] Crear src/Pos.Application/CashShifts/GenerateShiftReadout/GenerateShiftReadoutCommand.cs (`GenerateShiftReadoutCommand(Guid? AuthorizationGrantId = null)`, `GeneratedShiftCut(Guid CutId, string Folio, string ShiftFolio)`) y src/Pos.Application/CashShifts/GenerateShiftReadout/GenerateShiftReadoutHandler.cs con el orden del contrato: `CheckAsync(GenerateShiftReadout, AuthorizationGrantId)` → `BeginAsync` (escritura) → `GetOpenAsync(CashRegister.Default)` sin modificarlo (`ShiftRequired`) → `GetShiftTotalsAsync` → `NextCutNumberAsync(Readout)` + `ShiftCut.Readout` + `AddCut` → bitácora `SHIFT_READOUT_GENERATED` con entidad `ShiftCut`, resumen "Corte X X-000001. Turno T-000123. Total vendido $…" y `authorizedBy` → `SaveChangesAsync` (`Duplicate` → `Conflict`) → `Commit`; log Serilog "Corte X generado" con `CutId`, `Folio`, `ShiftId`, `UserId`, `AuthorizedBy`
- [X] T028 [US1] Registrar `GenerateShiftReadoutHandler` en src/Pos.Application/DependencyInjection.cs
- [X] T029 [US1] Crear src/Pos.Desktop/CashShifts/ShiftReadoutView.axaml, src/Pos.Desktop/CashShifts/ShiftReadoutView.axaml.cs y src/Pos.Desktop/CashShifts/ShiftReadoutViewModel.cs: cargar el estado con `GetCurrentShiftHandler` (`CurrentShiftSummary`: `Folio`, `OpenedByName`, `OpenedAtUtc`; **no** mostrar sus `SalesCount` ni `TotalSoldCents`); sin turno, mensaje y botón deshabilitado; con turno, "Turno T-000123 de {nombre} · desde {hora}" y "Generar Corte X" sin mostrar cifras; ante `Forbidden(CanBeAuthorized: true)` abrir `AdminAuthorizationService.RequestAsync(GenerateShiftReadout, "Corte X del turno T-000123")` y reintentar con la concesión (cancelado → "Se requiere autorización de un administrador"); `ShiftRequired` → estado sin turno; éxito → abrir `ShiftCutDetailView` en modo "Imprimir"
- [X] T030 [US1] Registrar la página `cash.readout` (orden 0, permiso de menú `OperateShift`) con `ShiftReadoutViewModel` en src/Pos.Desktop/CashShifts/CashModule.cs
- [ ] T031 [US1] Ejecutar las pruebas `ShiftCutUseCaseTests` de tests/Pos.Infrastructure.Tests y validar el quickstart Escenarios 1 y 2

**Checkpoint**: el Corte X funciona y se imprime de forma independiente.

---

## Phase 4: User Story 2 - Corte Z: cierre definitivo del turno (Priority: P2)

**Goal**: el cierre de turno de 008 se presenta como "Corte Z" y crea, en la misma transacción, el corte Z con folio consecutivo; cualquier cierre (incluido el de turno ajeno) es un Corte Z.

**Independent Test**: cerrar un turno con "Caja > Corte Z", verificar el folio Z, intentar vender (rechazo), abrir turno nuevo y comprobar que su Corte X empieza en cero (quickstart Escenario 3).

### Tests for User Story 2

- [X] T032 [US2] Agregar a tests/Pos.Infrastructure.Tests/CashShifts/ShiftCutUseCaseTests.cs: (a) folios Z consecutivos y un cierre rechazado por `ShiftChanged` no consume folio Z (FR-010); (b) dos `CloseShift` simultáneos sobre el mismo turno: uno éxito y otro `ShiftClosed`, sin huecos; (c) Corte X después de Corte Z y turno nuevo: cifras solo del turno nuevo (escenario 2.4); si alguna prueba existente de tests/Pos.Infrastructure.Tests/CashShifts/CashShiftUseCaseTests.cs deja de compilar por la nueva firma de `ClosedShift`, ajustarla (hoy ninguna prueba construye `ClosedShift`, `ShiftReportDto` ni `ShiftDetailDto` directamente)

### Implementation for User Story 2

- [X] T033 [US2] Cambiar `ClosedShift` a `ClosedShift(Guid ShiftId, string Folio, Guid CutId, string CutFolio)` y agregar `string? CutFolio` ("nulo en turnos anteriores") a `ShiftReportDto` y `ShiftDetailDto` en src/Pos.Application/CashShifts/CashShiftDtos.cs (parámetros opcionales donde sea posible)
- [X] T034 [US2] En src/Pos.Application/CashShifts/CloseShift/CloseShiftHandler.cs, después de `shift.Close(...)` y antes del único `SaveChangesAsync`: `NextCutNumberAsync(Closing)` → `ShiftCut.Closing(number, shift, currentUser)` → `AddCut`; el resumen de bitácora `SHIFT_CLOSED` / `SHIFT_CLOSED_BY_ADMIN` empieza por "Corte Z Z-000001. Turno T-000123. …"; `Duplicate` → `Conflict`; devolver `CutId` y `CutFolio`; incluir el folio Z en el log (depende de T033)
- [X] T035 [US2] Proyectar `CutFolio` (folio del corte Z del turno, si existe) en las consultas de reporte y detalle de turno de src/Pos.Infrastructure/CashShifts/CashShiftRepository.cs y exponerlo en src/Pos.Application/CashShifts/GetShiftDetail/GetShiftDetailHandler.cs (depende de T033)
- [X] T036 [US2] En src/Pos.Application/Printing/Ticket/ShiftTicketBuilder.cs, el corte desde "Turnos" (`ShiftReportSource`) titula "CORTE Z Z-000001" si hay `CutFolio` y conserva "CORTE DE CAJA" si no (depende de T033)
- [X] T037 [US2] Rotular el diálogo de cierre como "Corte Z" (título y botón final "Confirmar Corte Z") en src/Pos.Desktop/CashShifts/CloseShiftView.axaml; en src/Pos.Desktop/CashShifts/CloseShiftViewModel.cs mostrar al terminar "Corte Z Z-000001 · Turno T-000123 cerrado" e imprimir automáticamente con `PrintSource.ShiftCut(cutId)` (con "Reintentar" si falla) (depende de T034)
- [X] T038 [P] [US2] Cambiar el botón "Cerrar turno" por "Corte Z" en src/Pos.Desktop/Sales/PointOfSaleView.axaml y en src/Pos.Desktop/CashShifts/ShiftDetailView.axaml; mostrar "Corte Z: Z-000001" en el detalle de un turno cerrado cuando `CutFolio` existe (src/Pos.Desktop/CashShifts/ShiftDetailViewModel.cs)
- [X] T039 [US2] Crear src/Pos.Desktop/CashShifts/ShiftClosingView.axaml, src/Pos.Desktop/CashShifts/ShiftClosingView.axaml.cs y src/Pos.Desktop/CashShifts/ShiftClosingViewModel.cs: cargar el estado con `GetCurrentShiftHandler`; sin turno, "No hay un turno abierto."; con turno, "Turno T-000123 de {nombre} · desde {hora}" y "Hacer Corte Z" (si `IsMine` es falso, solo lo ve el Administrador) que abre `CashShiftDialogs.CloseShiftAsync`; refrescar el estado al terminar
- [X] T040 [US2] Registrar la página `cash.close` (orden 10, permiso de menú `OperateShift`) con `ShiftClosingViewModel` en src/Pos.Desktop/CashShifts/CashModule.cs
- [ ] T041 [US2] Ejecutar `ShiftCutUseCaseTests` y `CashShiftUseCaseTests` de tests/Pos.Infrastructure.Tests y validar el quickstart Escenario 3

**Checkpoint**: Corte X y Corte Z funcionan; todo cierre tiene folio Z.

---

## Phase 5: User Story 3 - Histórico de cortes (Priority: P2)

**Goal**: el Administrador lista los Cortes X y Z con filtros y paginación, abre cualquiera y lo reimprime con sus cifras originales.

**Independent Test**: con varios turnos con Cortes X y Z, filtrar por tipo y fechas, abrir un corte, reimprimirlo y comprobar que coincide con el original; el Cajero no ve el histórico (quickstart Escenario 4).

### Tests for User Story 3

- [X] T042 [US3] Agregar a tests/Pos.Infrastructure.Tests/CashShifts/ShiftCutUseCaseTests.cs: `SearchShiftCuts` filtra por tipo y pagina en 100 con orden `GeneratedAt DESC, Id DESC`; `From > To` → `ValidationFailed`

### Implementation for User Story 3

- [X] T043 [US3] Crear src/Pos.Application/CashShifts/SearchShiftCuts/SearchShiftCutsQuery.cs (`SearchShiftCutsQuery(ShiftCutType? Type, DateOnly? From, DateOnly? To, Guid? UserId, int Page = 1)`) y src/Pos.Application/CashShifts/SearchShiftCuts/SearchShiftCutsHandler.cs: permiso `ManageShifts`; `From > To` → `ValidationFailed`; convertir fechas locales a UTC (`From` inclusivo, `To` + 1 día exclusivo) igual que `SearchShifts`; `PageSize = ShiftCutPage.DefaultPageSize`
- [X] T044 [US3] Registrar `SearchShiftCutsHandler` en src/Pos.Application/DependencyInjection.cs
- [X] T045 [US3] Crear src/Pos.Desktop/CashShifts/ShiftCutsView.axaml, src/Pos.Desktop/CashShifts/ShiftCutsView.axaml.cs y src/Pos.Desktop/CashShifts/ShiftCutsViewModel.cs: filtros Tipo (Todos, Corte X, Corte Z), Desde/Hasta (hoy por omisión), Usuario (Todos o uno, opciones cargadas con `ListCashiersHandler` como `ShiftsViewModel.LoadUsersAsync`) y "Buscar"; columnas Tipo, Folio, Fecha y hora, Usuario, Turno, Total vendido, Diferencia ("Sobrante"/"Faltante"/"Cuadrado" en Z, "—" en X); paginación "Anterior"/"Siguiente" y "Página N de M" como `ShiftsView`; doble clic o "Ver" abre `ShiftCutDetailView` en modo "Reimprimir"; sin resultados "No hay cortes con estos filtros."
- [X] T046 [US3] Registrar la página `cash.cuts` (orden 20, permiso de menú `ManageShifts`) con `ShiftCutsViewModel` en src/Pos.Desktop/CashShifts/CashModule.cs
- [ ] T047 [US3] Ejecutar `ShiftCutUseCaseTests` de tests/Pos.Infrastructure.Tests y validar el quickstart Escenario 4

**Checkpoint**: las tres historias funcionan de forma independiente.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T048 [P] Documentar en docs/turnos-de-caja.md el Corte X, el Corte Z, los folios por tipo, el histórico, la autorización del Cajero y la verificación de huecos con el SQL de quickstart.md
- [X] T049 [P] Documentar en docs/impresion.md los tickets de Corte X y Corte Z y la reimpresión
- [X] T050 [P] Documentar en docs/usuarios-y-permisos.md el permiso `GenerateShiftReadout` (Administrador, autorizable, módulo "Turnos y arqueo")
- [X] T051 [P] Agregar a docs/migraciones.md la sección 0.12.0 (migración `ShiftCuts`, sin reconstrucciones, sin Z retroactivos, base `v0.12.0.db`)
- [ ] T052 Ejecutar la validación de licencia y fallas del quickstart Escenario 5 (grupo "Caja" desaparece sin el módulo; corte registrado con impresora desconectada)
- [X] T053 Ejecutar `dotnet build -v q` y `dotnet test --verbosity quiet` desde la raíz: 0 errores y 0 advertencias

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias.
- **Foundational (Fase 2)**: depende de la Fase 1; bloquea las tres historias.
- **US1, US2, US3 (Fases 3–5)**: dependen solo de la Fase 2; son independientes entre sí.
- **Polish (Fase 6)**: después de las historias que se entreguen.

### User Story Dependencies

- **US1 (Corte X)**: solo Fase 2.
- **US2 (Corte Z)**: solo Fase 2. Su prueba (c) usa `GenerateShiftReadout` de US1; si US2 se hace antes, posponer esa prueba hasta tener T027.
- **US3 (Histórico)**: solo Fase 2. Para probarlo a mano conviene tener cortes de US1 o US2.

### Archivos compartidos (no paralelizar)

- `ShiftCutUseCaseTests.cs`: T026 → T032 → T042.
- `CashModule.cs`: T021 → T030 / T040 / T046.
- `src/Pos.Application/DependencyInjection.cs`: T018, T028, T044.
- `ShiftTicketBuilder.cs`: T016 → T036.
- `CashShiftRepository.cs`: T013 → T035.

### Within Each User Story

- Pruebas de casos de uso primero (deben fallar), luego caso de uso, registro DI, vista y página del menú.

### Parallel Opportunities

- Fase 2: T002, T003, T005, T006, T007, T008, T009 en paralelo; luego T011, T015, T016, T019, T022 en paralelo cuando sus dependencias estén listas.
- Fase 6: T048–T051 en paralelo.
- Con varias personas, US1, US2 y US3 en paralelo tras la Fase 2 (cuidando los archivos compartidos de arriba).

---

## Parallel Example: Fase 2

```bash
# Domain y Application sin dependencias entre sí:
Task: "Crear ShiftCutType en src/Pos.Domain/CashShifts/ShiftCutType.cs"
Task: "Crear ShiftCutFolio en src/Pos.Domain/CashShifts/ShiftCutFolio.cs"
Task: "Agregar GenerateShiftReadout en src/Pos.Domain/Users/Permission.cs y RolePermissions.cs"
Task: "Mapear GenerateShiftReadout en src/Pos.Domain/Licensing/ModuleAccess.cs"
Task: "Agregar acciones de bitácora en src/Pos.Application/Audit/AuditActions.cs"
Task: "Crear DTO en src/Pos.Application/CashShifts/ShiftCutDtos.cs"
```

## Parallel Example: User Story 2

```bash
# Tras T033 (DTO):
Task: "Folio Z en CloseShiftHandler.cs"
Task: "Botón 'Corte Z' en PointOfSaleView.axaml y ShiftDetailView.axaml"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Fase 1 y Fase 2 (el agregado, la migración y la impresión).
2. Fase 3: Corte X.
3. **Validar**: quickstart Escenarios 1 y 2.

### Incremental Delivery

1. Setup + Foundational → base lista.
2. US1 Corte X → validar.
3. US2 Corte Z → validar (todo cierre recibe folio Z desde aquí).
4. US3 Histórico → validar.
5. Polish: documentación y suite completa.

Nota: la versión 0.12.0 solo debe publicarse con US2 incluida, para que todos los turnos cerrados desde esa versión tengan Corte Z (SC-002).

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes.
- No se agrega ninguna dependencia externa.
- La migración publicada nunca se modifica; revisar su SQL antes de integrarla.
- Commit después de cada tarea o grupo lógico.
