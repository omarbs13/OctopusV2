---

description: "Lista de tareas para Turnos de caja"
---

# Tasks: Turnos de caja

**Input**: documentos de diseño en `/specs/008-cash-shifts/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/application-ports.md, contracts/ui.md, quickstart.md

**Tests**: se incluyen solo las pruebas que fija la política mínima de la constitución v1.2.0 y research §16 (cálculos de dinero, integridad, migración). No hay pruebas de ViewModels, vistas ni del constructor del corte.

**Organization**: tareas agrupadas por historia de usuario. La base (Domain, puertos, persistencia y migración) es común y va en la Fase 2.

## Format: `[ID] [P?] [Story] Descripción con ruta`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencia de tareas incompletas)
- **[Story]**: historia a la que pertenece (US1 a US6)
- Rutas relativas a la raíz del repositorio. Compilar: `dotnet build -v q`. Probar: `dotnet test --verbosity quiet` (al implementar, solo el proyecto modificado)

---

## Phase 1: Setup

**Purpose**: versión y base de ejemplo previas a la migración (research §15)

- [X] T001 Subir `Version` de 0.5.0 a 0.6.0 en `Directory.Build.props`
- [X] T002 Generar `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.6.0.db` con el esquema actual (migración `UsersAndRoles`, **antes** de crear `CashShifts`): un administrador, un cajero y ventas de ambos. Seguir el procedimiento de `docs/migraciones.md` y el generador existente de bases de ejemplo

---

## Phase 2: Foundational (bloquea todas las historias)

**Purpose**: dominio, puertos, persistencia y migración que usan todas las historias

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

### Domain (`src/Pos.Domain`)

- [X] T003 [P] Crear `CashShifts/CashShiftStatus.cs` (`OPEN` | `CLOSED`) y `CashShifts/CashMovementType.cs` (`IN` | `OUT`), ambos con `ToCode()` y `FromCode()` como `SaleStatus`
- [X] T004 [P] Crear `CashShifts/CashRegister.cs` (`Default = "CAJA-1"`, `DisplayName = "Caja 1"`) y `CashShifts/ShiftFolio.cs` (`Format(long)` → `T-000123`; `FormatMovement(long, int)` → `T-000123-02`)
- [X] T005 [P] Crear `CashShifts/ShiftSalesTotals.cs` (record: `SalesCount`, `CancelledCount`, `TotalSoldCents`, `CashSalesCents`, `CashCancelledCents`, `CardCents`, `TransferCents`, todo `long` en centavos) y `CashShifts/CashDifference.cs` (`From(long countedCents, long expectedCents)` → `Money Amount` + `DifferenceKind Kind` con `Balanced` | `Over` | `Short`). La diferencia es un entero con signo porque `Money` no admite negativos (research §4)
- [X] T006 Crear `CashShifts/CashShiftMath.cs`: `ExpectedCash(openingFloat, totals, deposits, withdrawals)` = `float + CashSalesCents − CashCancelledCents + deposits − withdrawals` (FR-015) y `CanRefund(expected, saleCashCents)` = `expected − saleCashCents ≥ 0` (FR-008). Todo `long`, nunca lanza
- [X] T007 [P] Crear `CashShifts/CashMovement.cs`: inmutable, sin `UpdatedAt`, `Version` ni `DeletedAt`. Campos: `Id` (GUID v7), `CashShiftId`, `Sequence` (1, 2, 3… por turno), `Type`, `AmountCents` ("> 0, ≤ `Money.MaxCents`"), `Reason` ("Obligatorio y recortado", TEXT(250)), `AuthorizedBy` (`Guid?`), `CreatedAt`, `CreatedBy`. Solo se crea desde `CashShift`
- [X] T008 Crear `CashShifts/CashShift.cs` (agregado) con los campos de data-model.md: `Number`, `RegisterCode` (TEXT(20)), `Status`, `OpenedBy`, `OpenedAt` (UTC), `OpeningFloatCents` ("0 ≤ valor ≤ `Money.MaxCents`"), `ClosedAt?`, `ClosedBy?`, instantánea nula al abrir (`SalesCount`, `CancelledCount`, `TotalSoldCents`, `CashSalesCents`, `CashCancelledCents`, `CardCents`, `TransferCents`, `DepositsCents`, `WithdrawalsCents`, `ExpectedCashCents`, `CountedCashCents`, `DifferenceCents`), `ClosingComment` (TEXT(250), "Obligatorio si `DifferenceCents ≠ 0`"), `DeletedAt` (siempre nulo), `Version`, `Movements`. Métodos: `static Open(long number, Money openingFloat, Guid openedBy, DateTime utcNow)` ("`number ≥ 1`"), `RecordDeposit(Money, string reason, Guid userId)` ("Monto > 0 y motivo obligatorio de 1 a 250 caracteres", solo `OPEN`), `RecordWithdrawal(Money, string reason, long expectedCashCents, Guid userId, Guid? authorizedBy)` ("`amount ≤ expectedCashCents`", si no `InsufficientCashException(expectedCashCents)`), `Close(ShiftSalesTotals, Money counted, string? comment, Guid closedBy, DateTime utcNow)` (solo `OPEN`, calcula con `CashShiftMath`, comentario obligatorio si diferencia ≠ 0, guarda la instantánea), `DepositsCents`, `WithdrawalsCents`, `Difference`. Crear también `InsufficientCashException` en la misma carpeta (depende de T003 a T007)
- [X] T009 [P] Agregar al final de `Users/Permission.cs` los permisos `OperateShift`, `WithdrawCash`, `ManageShifts` y actualizar `Users/RolePermissions.cs`: Administrador los tiene todos; Cajero solo `OperateShift`; `Authorizable` = `{ CancelSales, OpenDrawerWithoutSale, WithdrawCash }` (research §7)
- [X] T010 [P] Agregar `Guid? CashShiftId` a `Sales/Sale.cs` y cambiar `Sale.Register(…)` para recibir `Guid cashShiftId` (obligatorio en ventas nuevas). Ajustar los llamadores de `Register` en las pruebas existentes (`tests/Pos.Domain.Tests/Sales/SaleTests`, pruebas de persistencia de ventas y generación de bases de ejemplo) para que compilen

### Application (`src/Pos.Application`)

- [X] T011 [P] Agregar en `Abstractions/Error.cs`: `ShiftRequired`, `ShiftOwnedByOther(string OpenedByName)`, `ShiftAlreadyOpen`, `ShiftClosed`, `InsufficientCash(long? AvailableCents)`, `SaleInProgress`, `HeldSaleWillBeDiscarded(string OwnerName)`, `ShiftChanged`, con los mensajes de contracts/application-ports.md
- [X] T012 [P] Agregar en `Audit/AuditActions.cs`: `SHIFT_OPENED`, `CASH_DEPOSIT`, `CASH_WITHDRAWAL`, `SHIFT_CASH_COUNTED`, `SHIFT_CLOSED`, `SHIFT_CLOSED_BY_ADMIN`, con el texto y los detalles de data-model.md (`HELD_SALE_DISCARDED` ya existe)
- [X] T013 [P] Crear `CashShifts/ICashShiftRepository.cs` (`GetOpenAsync(registerCode)`, `GetAsync(id)`, `FindMovementAsync(movementId)`, `NextNumberAsync()`, `Add`, `AddMovement`, `SearchAsync(ShiftSearch)`, `GetDetailAsync(id)`, `SaveChangesAsync()` → `SaveOutcome` con `Duplicate` para `OpenPerRegister` o `Number`), `CashShifts/CashShiftDtos.cs` (`CurrentShiftSummary`, `ShiftListItemDto`, `ShiftPage`, `ShiftDetailDto`, `ShiftCountResult`, `RegisteredMovement`, `ClosedShift`, `ShiftReportDto`, `CashMovementReceiptDto`), `CashShifts/CashShiftMessages.cs` y `CashShifts/CashShiftFields.cs` según contracts/application-ports.md
- [X] T014 [P] Agregar a `Sales/ISaleRepository.cs` `GetShiftTotalsAsync(shiftId)` → `ShiftSalesTotals` y `ListByShiftAsync(shiftId)` → filas de venta del detalle
- [X] T015 [P] Agregar `HasAsync(Permission)` a `Users/Access/AccessControl.cs` y su interfaz `IAccessControl`: como `CheckAsync`, pero sin registrar rechazos ni consumir concesiones (research §7)
- [X] T016 Crear `CashShifts/ShiftGuard.cs`: servicio compartido que, dentro de la transacción, resuelve el turno abierto, verifica propiedad (`ShiftRequired` / `ShiftOwnedByOther`) y detecta venta en curso con `ISaleDraftStore.HasForAsync` (depende de T011 y T013)

### Infrastructure (`src/Pos.Infrastructure`)

- [X] T017 [P] Crear `Persistence/Configurations/CashShiftConfiguration.cs` y `CashMovementConfiguration.cs` con tipos y longitudes de data-model.md. Índices: `IX_CashShifts_Number` (único), `IX_CashShifts_OpenPerRegister` único sobre `RegisterCode` filtrado con `WHERE "Status" = 'OPEN'`, `IX_CashShifts_OpenedAt` (`OpenedAt`, `Id`), `IX_CashShifts_OpenedBy_OpenedAt`, `IX_CashMovements_Shift_Sequence` único (`CashShiftId`, `Sequence`). FK `CashMovements.CashShiftId` con `DeleteBehavior.Restrict`. Token de concurrencia `Version`
- [X] T018 [P] Actualizar `Persistence/Configurations/SaleConfiguration.cs`: `CashShiftId` nula, **sin llave foránea**, índice `IX_Sales_CashShiftId_Status` sobre (`CashShiftId`, `Status`)
- [X] T019 Actualizar `Persistence/PosDbContext.cs`: `DbSet<CashShift>` y `DbSet<CashMovement>`, y renombrar `RejectMovementChanges` a `RejectImmutableChanges` para que también rechace modificar o borrar un `CashMovement` y modificar un `CashShift` cuyo `Status` original es `CLOSED` (research §10)
- [X] T020 Generar la migración `CashShifts` con `dotnet ef migrations add CashShifts --project src/Pos.Infrastructure` en `Persistence/Migrations/`. Verificar que el SQL solo tenga `CREATE TABLE` (2), `CREATE UNIQUE INDEX … WHERE "Status" = 'OPEN'`, `ALTER TABLE "Sales" ADD "CashShiftId" TEXT NULL` y `CREATE INDEX`, sin `DROP TABLE` ni `ef_temp_`
- [X] T021 Implementar `CashShifts/CashShiftRepository.cs` (`ICashShiftRepository`): `GetOpenAsync` con movimientos y seguimiento, `NextNumberAsync` como `MAX + 1`, `SaveChangesAsync` que traduce el índice único a `SaveOutcome.Duplicate`, `SearchAsync` (100 por página, apertura más reciente primero, total vendido de los abiertos por subconsulta agrupada sobre la página, del snapshot para los cerrados), `GetDetailAsync`
- [X] T022 Implementar en `Sales/SaleRepository.cs` `GetShiftTotalsAsync` (una consulta agregada por `CashShiftId`: conteos de completadas y canceladas, total vendido de `COMPLETED`, importes por forma de pago de completadas, efectivo aplicado a las ventas (ya neto de cambio, `CashApplied`; nunca sumar `ChangeCents`) y efectivo aplicado de las canceladas, sobre `SalePayments.AmountCents`) y `ListByShiftAsync`
- [X] T023 Registrar `ICashShiftRepository` (en `src/Pos.Infrastructure/DependencyInjection.cs`) y `ShiftGuard` (en `src/Pos.Application/DependencyInjection.cs`). Cada handler nuevo se registra en `src/Pos.Application/DependencyInjection.cs` dentro de la tarea que lo crea (T028, T029, T039, T040, T047, T054, T055), para que la compilación no se rompa al cerrar esta fase

### Pruebas de la base

- [X] T024 [P] Crear `tests/Pos.Domain.Tests/CashShifts/CashShiftMathTests.cs` (fondo + venta en efectivo con cambio + pago mixto − cancelación + ingreso − retiro coincide con el cálculo manual, SC-003; `CanRefund` detecta esperado negativo) y `CashShiftTests.cs` (retiro igual al esperado se acepta; retiro de 1 centavo más se rechaza; cierre con diferencia y comentario; conteo 0 con esperado > 0 exige comentario; turno cerrado rechaza movimientos y segundo cierre)
- [X] T025 [P] Actualizar `tests/Pos.Domain.Tests/Users/RolePermissionsTests.cs`: matriz del cajero con `OperateShift`; autorizables = cancelar, cajón y retiro
- [X] T026 [P] Crear `tests/Pos.Infrastructure.Tests/CashShifts/CashShiftPersistenceTests.cs` con SQLite real: totales del turno por consulta = cálculo manual; dos aperturas simultáneas, una falla por el índice único (SC-002); modificar un turno cerrado o un movimiento lanza
- [X] T027 [P] Crear `tests/Pos.Infrastructure.Tests/SampleDatabases/CashShiftsMigrationTests.cs` (falla si el SQL contiene `DROP TABLE` o `ef_temp_`) y agregar `v0.6.0.db` a `SampleDatabaseUpgradeTests.cs`: tras migrar, las ventas conservan sus datos con `CashShiftId` nulo, `CashShifts` está vacía y el índice único filtrado existe con `WHERE "Status" = 'OPEN'`

**Checkpoint**: `dotnet build -v q` sin errores ni advertencias y pruebas de Domain e Infrastructure en verde.

---

## Phase 3: User Story 1 - Abrir turno con fondo inicial (Priority: P1) 🎯 MVP

**Goal**: sin turno abierto, el Punto de venta pide abrirlo con fondo inicial; solo un turno abierto por caja y solo su dueño vende.

**Independent Test**: iniciar sesión sin turno, entrar al Punto de venta, abrir el turno (también con 0 confirmado) y ver el estado de turno propio; con otro usuario ver el estado de turno ajeno (quickstart 1 a 3 y 14).

- [X] T028 [P] [US1] Crear `src/Pos.Application/CashShifts/GetCurrentShift/` (handler, permiso `OperateShift`) que devuelve `CurrentShiftSummary?` = `{ ShiftId, Version, Folio, OpenedAtUtc, OpenedById, OpenedByName, IsMine, SalesCount, TotalSoldCents }` usando `GetShiftTotalsAsync`. Nunca devuelve fondo, esperado, desglose ni movimientos (FR-022); `null` sin turno. Registrar `GetCurrentShiftHandler` en `Pos.Application/DependencyInjection.cs`
- [X] T029 [US1] Crear `src/Pos.Application/CashShifts/OpenShift/` (comando `OpeningFloatCents`, `ConfirmZeroFloat`, validador y handler, permiso `OperateShift`): `ValidationFailed` si es negativo o mayor que el máximo, o 0 sin `ConfirmZeroFloat` (FR-003); `ShiftAlreadyOpen` por verificación y por `Duplicate`; en una sola transacción crea el turno con `NextNumberAsync` y audita `SHIFT_OPENED` (FR-026, FR-027). Registra en Serilog la apertura y los rechazos con turno y usuario. Registrar `OpenShiftHandler` en `Pos.Application/DependencyInjection.cs`
- [X] T030 [P] [US1] Agregar a `src/Pos.Desktop/Resources/Strings.resx` los textos `Shift_*` de apertura y estados del Punto de venta ("Abrir turno", "¿Abrir el turno sin fondo inicial?", "Hay un turno abierto de {nombre} desde {hora}. Debe cerrarse antes de vender", barra de turno)
- [X] T031 [US1] Crear en `src/Pos.Desktop/CashShifts/` el diálogo de apertura en `ModalHost` (`OpenShiftViewModel` y vista) con captura por `MoneyConverter` y confirmación "Sí/No" cuando el fondo es 0, y `CashShiftsModule.cs` con `AddCashShiftsModule()`
- [X] T032 [US1] Llamar a `AddCashShiftsModule` desde `src/Pos.Desktop/Composition/HostBuilder.cs`, junto a `AddSalesModule`
- [X] T033 [US1] Actualizar `src/Pos.Desktop/Sales/PointOfSaleView*` y `PointOfSaleViewModel.cs`: al activarse consulta `GetCurrentShift` y muestra tres estados (sin turno con panel "Abrir turno" y captura deshabilitada; turno de otro usuario con mensaje bloqueado; turno propio con carrito y barra "Turno T-000123 · desde 08:15 · 23 ventas · $4,560.00" sin fondo, esperado ni movimientos). El cajero con turno ajeno solo puede cerrar sesión (contracts/ui.md)

**Checkpoint**: se puede abrir turno y el Punto de venta refleja los tres estados. Aún no se puede vender (US2) ni cerrar (US3).

---

## Phase 4: User Story 2 - Ventas ligadas al turno (Priority: P1)

**Goal**: toda venta nueva queda ligada al turno abierto propio y a su cajero; solo se cancelan ventas del turno abierto actual.

**Independent Test**: vender y verificar turno y cajero; cancelar y verificar que el efectivo esperado se descuenta; intentar cancelar una venta de un turno cerrado (quickstart 4, 5, 9, 9b, 13).

- [X] T034 [US2] Modificar `src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs`: dentro de la transacción y **después** de la idempotencia por `DraftId`, usar `ShiftGuard` para devolver `ShiftRequired` / `ShiftOwnedByOther(OpenedByName)` y registrar la venta con `Sale.CashShiftId` (FR-001, FR-005, FR-007)
- [X] T035 [US2] Modificar `src/Pos.Application/Sales/CancelSale/CancelSaleHandler.cs`: antes de `sale.Cancel`, si `CashShiftId` es nulo o distinto del turno abierto devolver `InvalidState("La venta pertenece a un turno cerrado")`; si la venta tiene pago en efectivo, calcular el esperado con `CashShiftMath` y devolver `InsufficientCash(null)` cuando `CanRefund` sea falso, sin revelar montos a ningún rol (FR-008, research §6)
- [X] T036 [P] [US2] Crear `tests/Pos.Infrastructure.Tests/CashShifts/CashShiftUseCaseTests.cs` (antes `tests/Pos.Application.Tests/Sales/ConfirmSaleShiftTests.cs`; ubicación cambiada: usa los casos de uso reales sobre SQLite): venta ligada al turno propio; rechazo sin turno y con turno de otro usuario (SC-001)
- [X] T037 [P] [US2] Crear `tests/Pos.Application.Tests/Sales/CancelSaleShiftTests.cs`: cancelación en el turno abierto; rechazo de venta de turno cerrado o sin turno; efectivo insuficiente sin monto
- [X] T038 [US2] Actualizar `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs` para refrescar el estado de turno tras cada venta o cancelación y ante `ShiftRequired` / `ShiftOwnedByOther` de `ConfirmSale` (el carrito sigue en el borrador), y `SaleDetailViewModel.cs` para mostrar los mensajes de turno cerrado y de efectivo insuficiente al cancelar

**Checkpoint**: US1 + US2 funcionan: abrir turno, vender y cancelar dentro del turno.

---

## Phase 5: User Story 3 - Cerrar turno con arqueo (Priority: P1)

**Goal**: cierre con arqueo ciego, cifras tras el conteo, comentario obligatorio con diferencia, turno inmutable y corte impreso. Incluye el cierre de un turno ajeno por un Administrador (US1 escenario 5).

**Independent Test**: con un turno con ventas, cerrar capturando un conteo y verificar cifras, diferencia, inmutabilidad y corte impreso (quickstart 10 a 12, 15 y 16).

- [X] T039 [US3] Crear `src/Pos.Application/CashShifts/CountShiftCash/` (`ShiftId`, `CountedCents`, `DiscardHeldSale`; permiso `OperateShift` en turno propio, `ManageShifts` en ajeno): devuelve `ShiftCountResult { ExpectedCents, CountedCents, DifferenceCents, DifferenceKind, CardCents, TransferCents, Version }`; devuelve `SaleInProgress` o `HeldSaleWillBeDiscarded` **antes** de revelar cifras; `ShiftClosed`; audita `SHIFT_CASH_COUNTED` con el monto contado, permitiendo recontar pero dejando cada conteo en la bitácora y en Serilog (FR-014, research §8). Registrar `CountShiftCashHandler` en `Pos.Application/DependencyInjection.cs`
- [X] T040 [US3] Crear `src/Pos.Application/CashShifts/CloseShift/` (`ShiftId`, `ExpectedVersion`, `CountedCents`, `ShownExpectedCents`, `Comment?`, `DiscardHeldSale`): recalcula en la transacción; `ShiftChanged` si el esperado ≠ `ShownExpectedCents`; `Conflict` por versión; `ValidationFailed` si hay diferencia sin comentario (FR-017); con `DiscardHeldSale` borra el borrador del dueño con `ISaleDraftStore.RemoveForAsync` y audita `HELD_SALE_DISCARDED`; llama a `CashShift.Close` y audita `SHIFT_CLOSED` o, si `ClosedBy ≠ OpenedBy`, `SHIFT_CLOSED_BY_ADMIN` (una sola entrada de cierre, con `Dueño: {nombre}`). Registra el cierre y los rechazos en Serilog con turno, usuario e importes. Registrar `CloseShiftHandler` en `Pos.Application/DependencyInjection.cs`
- [X] T041 [P] [US3] Crear `tests/Pos.Application.Tests/CashShifts/CloseShiftTests.cs`: cierre propio cuadrado; borrador propio → `SaleInProgress`; administrador con borrador ajeno exige confirmación, descarta y audita
- [X] T042 [P] [US3] Crear `src/Pos.Application/Printing/Ticket/ShiftTicketBuilder.cs` con `BuildReport(BusinessProfileDto?, ShiftReportDto, int columns, TicketOptions)` → `TicketDocument` ("CORTE DE CAJA" o "REIMPRESIÓN"; caja, turno, usuario, fechas locales, "Cerrado por", fondo inicial, ventas y canceladas, totales por forma de pago, efectivo cancelado, ingresos, retiros, esperado, contado, diferencia y comentario; solo los campos de FR-019, leídos de la instantánea), reutilizando `TextWrap` y los anchos de 32 y 48 columnas de `TicketBuilder`
- [X] T043 [US3] Ampliar `src/Pos.Application/Printing/PrintTicket/` con `PrintSource.ShiftReport(Guid ShiftId)`: permiso `ManageShifts` o `ClosedBy` = usuario actual, solo turnos cerrados; la falla de impresión no revierte el cierre (research §12)
- [X] T044 [P] [US3] Agregar a `Strings.resx` los textos de cierre (paso de conteo, cifras, "Sobrante", "Faltante", "Cuadrado", "Comentario", "Volver a contar", "Turno T-000123 cerrado", avisos de venta en curso y de venta conservada)
- [X] T045 [US3] Crear en `src/Pos.Desktop/CashShifts/` el diálogo de cierre en tres pasos (`CloseShiftViewModel` y vista): 1 conteo sin ninguna cifra esperada; 2 cifras con comentario y "Confirmar cierre" deshabilitado si hay diferencia sin comentario, y "Volver a contar"; 3 `CloseShift` (ante `ShiftChanged` vuelve al paso 2) e impresión automática del corte con "Reintentar" si falla. Maneja `SaleInProgress` y la confirmación de `HeldSaleWillBeDiscarded` reintentando con `DiscardHeldSale = true`
- [X] T046 [US3] Actualizar `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`: botón "Cerrar turno" en la barra (espera `FlushDraftAsync` antes de contar), botón "Cerrar ese turno" para Administrador en el estado de turno ajeno, y regreso al estado "Sin turno" al cerrar

**Checkpoint**: flujo P1 completo (abrir, vender, cancelar, cerrar con corte). Es el MVP.

---

## Phase 6: User Story 4 - Ingresos y retiros de efectivo (Priority: P2)

**Goal**: registrar ingresos y retiros con monto y motivo; retiro del cajero con autorización; comprobante imprimible.

**Independent Test**: registrar un ingreso y un retiro y verificar el efecto en el esperado y la bitácora (quickstart 6 a 9b).

- [X] T047 [US4] Crear `src/Pos.Application/CashShifts/RegisterCashMovement/` (`ShiftId`, `Type`, `AmountCents`, `Reason`, `AuthorizationGrantId?`; ingreso con `OperateShift`, retiro con `WithdrawCash` autorizable): `ValidationFailed` (monto ≤ 0, motivo vacío o de más de 250); `ShiftClosed` / `NotFound`; `Forbidden` si el turno no es del usuario y no tiene `ManageShifts`; `InsufficientCash` en retiros, con `AvailableCents` solo si `HasAsync(ManageShifts)` (FR-010); devuelve `RegisteredMovement { MovementId, Folio }`; audita `CASH_DEPOSIT` o `CASH_WITHDRAWAL` con `AuthorizedBy`; registra movimientos y rechazos en Serilog con turno, usuario e importe. Registrar `RegisterCashMovementHandler` en `Pos.Application/DependencyInjection.cs`
- [X] T048 [P] [US4] Crear `tests/Pos.Application.Tests/CashShifts/RegisterCashMovementTests.cs`: ingreso del cajero; retiro del administrador; retiro del cajero sin concesión → `Forbidden`; excedente con mensaje sin monto para el cajero y con monto para el administrador
- [X] T049 [P] [US4] Agregar a `ShiftTicketBuilder.cs` `BuildMovementReceipt(BusinessProfileDto?, CashMovementReceiptDto, int columns, TicketOptions)` ("INGRESO DE EFECTIVO" o "RETIRO DE EFECTIVO", folio `T-000123-02`, fecha, usuario, monto, motivo, "Autorizó" y línea de firma) y `PrintSource.CashMovement(Guid MovementId)` en `PrintTicket`: `ManageShifts`, o movimiento propio en el turno propio
- [X] T050 [P] [US4] Ampliar `tests/Pos.Application.Tests/Security/RestrictedOperationsTests.cs` con el caso de uso restringido de esta historia para el cajero (retiro sin autorización). Los casos de `SearchShifts`, `GetShiftDetail` y cierre de turno ajeno se agregan en T059b
- [X] T051 [P] [US4] Agregar a `Strings.resx` los textos `CashMovement_*` (campos, "Movimiento T-000123-02 registrado", "Imprimir comprobante", rechazos de excedente con y sin "Disponible: $X")
- [X] T052 [US4] Crear en `src/Pos.Desktop/CashShifts/` el diálogo de movimiento (`CashMovementViewModel` y vista): tipo fijado por el botón, monto, motivo de 1 a 250 caracteres con "Guardar" deshabilitado si falta; ante `Forbidden(CanBeAuthorized)` usa `AdminAuthorizationService.RequestAsync(WithdrawCash)` y reintenta con la concesión; muestra éxito con "Imprimir comprobante" y "Cerrar"
- [X] T053 [US4] Agregar los botones "Ingreso" y "Retiro" a la barra de turno en `src/Pos.Desktop/Sales/PointOfSaleView*` y `PointOfSaleViewModel.cs`

**Checkpoint**: US1 a US4 funcionan y el esperado refleja movimientos.

---

## Phase 7: User Story 5 - Consulta de turnos (Priority: P2)

**Goal**: el Administrador consulta todos los turnos con filtros, detalle y reimpresión del corte; el Cajero solo ve el resumen de su turno (ya cubierto por la barra de US1).

**Independent Test**: con varios turnos de distintos usuarios, filtrar el listado, abrir un detalle y reimprimir un corte (quickstart 17 y 18).

- [X] T054 [P] [US5] Registrar `SearchShiftsHandler` en `Pos.Application/DependencyInjection.cs`. Crear `src/Pos.Application/CashShifts/SearchShifts/` (`ManageShifts`; `FromUtc?`, `ToUtcExclusive?`, `UserId?`, `Status?`, página): `ShiftPage` de 100 por página de `ShiftListItemDto { Id, Folio, OpenedByName, OpenedAtUtc, ClosedAtUtc?, Status, TotalSoldCents, DifferenceCents? }`; `ValidationFailed` si el rango es inválido; diferencia nula en los abiertos
- [X] T055 [P] [US5] Registrar `GetShiftDetailHandler` en `Pos.Application/DependencyInjection.cs`. Crear `src/Pos.Application/CashShifts/GetShiftDetail/` (`ManageShifts`): `ShiftDetailDto` con datos del turno, `Sales[]`, `Movements[]` y `Reconciliation` (la instantánea si está cerrado; el esperado actual si está abierto); `NotFound`
- [X] T056 [P] [US5] Agregar a `Strings.resx` `Nav_Shifts` y los textos de la pantalla "Turnos" (columnas, filtros, pestañas Ventas, Movimientos y Arqueo, "Reimprimir corte", "Efectivo esperado al momento")
- [X] T057 [US5] Crear `src/Pos.Desktop/CashShifts/ShiftsView*` y `ShiftsViewModel`: filtros (desde y hasta con fecha local de apertura, hoy por omisión; usuario con "Todos" y `ListCashiers`; estado), columnas Turno, Usuario, Apertura, Cierre, Total vendido y Diferencia (faltante en rojo, sobrante en ámbar, vacía en abiertos) y paginación de 100 como "Ventas realizadas"
- [X] T058 [US5] Crear `src/Pos.Desktop/CashShifts/ShiftDetail*` (panel lateral como `SaleDetailView`, abre con doble clic o Enter): encabezado, pestañas Ventas (abrir una venta lleva al detalle existente), Movimientos (con "Imprimir") y Arqueo, y botones "Reimprimir corte" (cerrado) y "Cerrar turno" (abierto, abre el diálogo de T045)
- [X] T059 [US5] Registrar la página "Turnos" en `CashShiftsModule.cs` dentro del grupo Ventas (`SalesModule.GroupId`, orden 20) con permiso `ManageShifts`; el cajero no la ve y la navegación directa devuelve `Forbidden`
- [X] T059b [P] [US5] Ampliar `tests/Pos.Application.Tests/Security/RestrictedOperationsTests.cs` con `SearchShifts`, `GetShiftDetail` y cierre de turno ajeno, que el cajero no puede ejecutar

**Checkpoint**: el Administrador localiza un turno y reimprime su corte.

---

## Phase 8: User Story 6 - Tarjeta de turno en Inicio (Priority: P2)

**Goal**: Inicio muestra el turno actual (usuario, hora de apertura, total vendido) o "No hay turno abierto".

**Independent Test**: abrir turno, vender y ver la tarjeta en Inicio (quickstart 18).

- [X] T060 [P] [US6] Agregar a `Strings.resx` los textos de la tarjeta ("Turno de {nombre}", "Desde {hora}", "Total vendido $X", "No hay turno abierto")
- [X] T061 [US6] Crear `src/Pos.Desktop/CashShifts/CurrentShiftCard.cs` (vista y ViewModel con `GetCurrentShift`) y registrarla en `CashShiftsModule.cs` con `AddDashboardCard`: métrica de orden 100, permiso `OperateShift`; con turno, clic lleva al Punto de venta; sin turno, estado vacío

**Checkpoint**: las seis historias funcionan de forma independiente.

---

## Phase 9: Polish & Cross-Cutting

- [X] T062 [P] Crear `docs/turnos-de-caja.md`: fórmula del esperado, arqueo ciego, cierre de turno ajeno, reimpresión del corte y que las ventas anteriores a 0.6.0 (sin turno) ya no se pueden cancelar
- [X] T063 [P] Agregar a `docs/migraciones.md` la sección 0.6.0 (`CashShifts` sin reconstrucciones)
- [X] T064 Verificar que las pruebas de arquitectura existentes cubren las carpetas nuevas `CashShifts/` sin cambios y que `dotnet build -v q` termina con 0 errores y 0 advertencias
- [X] T065 Ejecutar `dotnet test --verbosity quiet` en los proyectos modificados (Domain, Application, Infrastructure, Architecture)
- [ ] T066 Recorrer los escenarios manuales 1 a 18 de `specs/008-cash-shifts/quickstart.md` y la verificación de integridad de la sección 4

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: T002 debe ejecutarse **antes** de T020 (la base de ejemplo se genera con el esquema previo)
- **Foundational (Fase 2)**: depende de Setup; bloquea todas las historias
  - Orden interno: T003 a T005 y T007 en paralelo → T006 y T008 → T009 a T015 en paralelo → T016 → T017 a T019 → T020 → T021 y T022 → T023
  - Pruebas T024 a T027 al terminar su código
- **US1, US2, US3**: todas P1, dependen de la Fase 2; US2 y US3 usan el estado del Punto de venta de US1 (T033)
- **US4, US5, US6**: P2, dependen de la Fase 2; US4 reutiliza la barra de turno de US1
- **Polish**: al final

### User Story Dependencies

- **US1**: sin dependencias de otras historias
- **US2**: necesita un turno abierto para probarse (US1)
- **US3**: necesita turno (US1) y ventas (US2) para un arqueo con cifras; el cierre de turno ajeno cubre el escenario 5 de US1
- **US4**: sin dependencias de datos; se integra con la barra de US1 y con el cierre de US3 (el esperado incluye movimientos)
- **US5** y **US6**: lectura; solo requieren la Fase 2 y, para tener datos, US1 a US3

### Within Each Story

- Casos de uso antes que ViewModels; textos de `Strings.resx` antes que las vistas
- `Pos.Desktop/CashShifts/CashShiftsModule.cs` (T031) lo tocan T059 y T061, así que no editarlo en paralelo
- `PointOfSaleViewModel.cs` lo tocan T033, T038, T046 y T053: secuenciales

---

## Parallel Examples

```text
# Fase 2, dominio:
T003 enums | T004 CashRegister y ShiftFolio | T005 totales y diferencia | T007 CashMovement

# Fase 2, Application, tras el dominio:
T011 errores | T012 auditoría | T013 puerto y DTOs | T014 ISaleRepository | T015 HasAsync

# US5, casos de uso:
T054 SearchShifts | T055 GetShiftDetail | T056 textos

# Historias P2 en paralelo, con equipo:
US4 (T047 a T053) | US5 (T054 a T059) | US6 (T060 a T061)
```

---

## Implementation Strategy

### MVP First (US1 a US3, todas P1)

1. Fase 1 y Fase 2 completas (migración y pruebas de base en verde)
2. US1 → probar apertura y estados del Punto de venta
3. US2 → probar ventas ligadas y cancelación
4. US3 → probar arqueo ciego y corte
5. **DETENER y validar** con quickstart 1 a 5, 10 a 16

### Incremental Delivery

1. Base + US1 + US2 + US3 → MVP (la caja ya se controla por turno)
2. US4 → ingresos y retiros
3. US5 → consulta y reimpresión para el Administrador
4. US6 → tarjeta de Inicio
5. Polish → documentación y validación final

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- Importes siempre como `Money` en lo que captura el operador (FR-025); esperado, totales y diferencia son `long` en centavos (research §4)
- Cada escritura (apertura, movimiento, cierre) es una sola transacción `BEGIN IMMEDIATE` con su bitácora (FR-026, FR-027)
- El cajero nunca recibe el esperado antes de contar (SC-005, FR-022)
- Commit tras cada tarea o grupo lógico
