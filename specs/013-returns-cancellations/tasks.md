# Tasks: Devoluciones y cancelaciones

**Input**: documentos de diseño en `/specs/013-returns-cancellations/` (plan.md, spec.md, research.md, data-model.md, contracts/application-ports.md, contracts/ui.md, quickstart.md)

**Prerequisites**: plan.md, spec.md. Parte de 005 (ventas), 007 (autorización), 008 (turnos), 004 (inventario), 006 (impresión), 009 (reportes) y 012 (licencias) ya implementada.

**Tests**: Política mínima del plan (research §15, Principio VI): pruebas de Domain, casos de uso sobre SQLite real, consistencia de inventario y migración (obligatorias). Sin pruebas de ViewModels, vistas, mapeos ni constructores de tickets.

**Organization**: Tareas agrupadas por historia de usuario. Comandos: compilar `dotnet build -v q`; pruebas `dotnet test <proyecto> --verbosity quiet` (solo los proyectos modificados).

## Format: `[ID] [P?] [Story] Descripción con ruta`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia de usuario (US1–US4)

## Path Conventions

Aplicación de escritorio por capas: `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`; pruebas en `tests/Pos.*.Tests`. Importes en centavos (`long`), cantidades en milésimas, GUID v7, fechas UTC.

---

## Phase 1: Setup

**Purpose**: línea base antes de tocar ventas, turnos e inventario

- [X] T001 Ejecutar `dotnet build -v q` en la raíz y anotar el estado base (0 errores, 0 advertencias) y qué pruebas de `tests/Pos.Domain.Tests/`, `tests/Pos.Application.Tests/Sales/` y `tests/Pos.Infrastructure.Tests/` pasan hoy, para distinguir regresiones de cambios esperados
- [X] T002 Crear las carpetas nuevas `src/Pos.Domain/Returns/`, `src/Pos.Domain/CreditNotes/`, `src/Pos.Application/Returns/`, `src/Pos.Application/CreditNotes/`, `src/Pos.Infrastructure/Returns/`, `src/Pos.Infrastructure/CreditNotes/`, `src/Pos.Desktop/Returns/`, `tests/Pos.Domain.Tests/Returns/`, `tests/Pos.Domain.Tests/CreditNotes/`, `tests/Pos.Infrastructure.Tests/Returns/` y confirmar que `tests/Pos.ArchitectureTests/` las cubre sin cambios

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: dominio, puertos, persistencia, migración, permisos y totales netos que todas las historias necesitan

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

### Dominio

- [X] T003 [P] Crear enums y folios en `src/Pos.Domain/Returns/`: `ReturnKind.cs` (`CANCELLATION` | `PARTIAL`, TEXT(12)), `ReturnCompensation.cs` (`REFUND` | `CREDIT_NOTE`, TEXT(12)), `RefundStatus.cs` (`PAID`, `PENDING_REVERSAL`, `REVERSED`, `RESTORED`, TEXT(16)) y `ReturnFolio.cs` (formato `D-000001`, igual que `Folio` de ventas)
- [X] T004 [P] Crear `ReturnMath` estático en `src/Pos.Domain/Returns/ReturnMath.cs` (research §3): `LineRefund(importeLínea, cantidadVendida, devueltoPrevio, cantidadAhora)` = acumulado `round(importeLínea × q / cantidadVendida)` mitad hacia arriba en enteros, menos el acumulado previo; `Allocate(total, pagosRestantes)` por resto mayor ponderado por lo aún devolvible de cada pago, con topes por pago, empates por orden de captura y suma exacta al total
- [X] T005 [P] Crear `CreditNoteFolio.cs` (formato `NC-000001`) y `CreditNoteMovementType.cs` (`ISSUE` | `REDEEM` | `RESTORE`, TEXT(8)) en `src/Pos.Domain/CreditNotes/`
- [X] T006 [P] Agregar `PaymentMethod.CreditNote` con código `CREDIT` en `src/Pos.Domain/Sales/PaymentMethod.cs`, agregar `SalePayment.CreditNoteId` (`Guid?`, solo con `Method = CREDIT`) en `src/Pos.Domain/Sales/SalePayment.cs`, y ajustar `src/Pos.Domain/Sales/Checkout.cs` para que trate la nota como pago no en efectivo (a lo más uno por venta, sin cambio)
- [X] T007 [P] Agregar `MovementType.SaleReturn` (código `SALE_RETURN`, aumenta existencia) en `src/Pos.Domain/Inventory/MovementType.cs` y `RecordSaleReturn` (análogo a `RecordSaleCancellation`, sin revisar el estado actual del producto) en `src/Pos.Domain/Inventory/ProductStock.cs`
- [X] T008 [P] Agregar `Permission.ProcessReturns`, `ApproveReturns` (autorizable) y `ManageCreditNotes` en `src/Pos.Domain/Users/Permission.cs`; asignar `ProcessReturns` a Cajero y Administrador y `ApproveReturns`/`ManageCreditNotes` solo a Administrador en `src/Pos.Domain/Users/RolePermissions.cs`; mapear los tres a `LicensedModule.Returns` en `src/Pos.Domain/Licensing/ModuleAccess.cs` (conservar `CancelSales` sin módulo, research §12)
- [X] T009 Agregar a `src/Pos.Domain/Sales/SaleLine.cs` la propiedad `ReturnedQuantity` (`long`, milésimas devueltas acumuladas, `DEFAULT 0`) y a `src/Pos.Domain/Sales/Sale.cs` `ReturnedCents` (`long`, `DEFAULT 0`) con `ApplyReturn(lines)`: la venta debe estar `COMPLETED`; por línea cantidad > 0 y ≤ `QuantityThousandths − ReturnedQuantity`; actualiza `ReturnedQuantity` y `ReturnedCents`; una cancelación completa exige `ReturnedCents = 0` y usa `Sale.Cancel` existente; propiedades derivadas parcialmente/totalmente devuelta (`0 < ReturnedCents < TotalCents` / `= TotalCents`) (depende de T004)
- [X] T010 Crear `SaleReturn`, `SaleReturnLine` y `SaleReturnRefund` inmutables en `src/Pos.Domain/Returns/`: `SaleReturn.Create(...)` valida motivo obligatorio recortado ≤ 250, ≥ 1 línea, suma de líneas = `TotalCents` (> 0), con `REFUND` suma de reintegros = total y con `CREDIT_NOTE` sin reintegros y con `CreditNoteId`; `Number` `long` con índice único; `SaleReturnLine.QuantityThousandths` > 0, `AmountCents`, `ReturnMovementId` nulo si no controlaba inventario o sin licencia; `SaleReturnRefund.Method` TEXT(10), `AmountCents` > 0 y `MarkReversed(userId, utcNow)` como única mutación (`PENDING_REVERSAL → REVERSED`, una sola vez, si no `InvalidOperationException`) (depende de T003, T004)
- [X] T011 [P] Crear `CreditNote` y `CreditNoteMovement` inmutables en `src/Pos.Domain/CreditNotes/`: `CreditNote.Issue(...)` (`InitialCents` > 0, `SaleReturnId`), `CreditNote.Redeem(monto, saldoActual)` valida monto > 0 y ≤ saldo (nunca negativo), `CreditNoteMovement` con `Sequence` consecutivo por nota y `Amount` > 0; saldo = Σ`ISSUE` + Σ`RESTORE` − Σ`REDEEM` calculado, no guardado (depende de T005)
- [X] T012 [P] Ampliar `src/Pos.Domain/CashShifts/ShiftSalesTotals.cs` con `CashRefundsCents`, `NonCashRefundsCents` y `CreditNotesIssuedCents` (`TotalSoldCents` pasa a ser neto de devoluciones); agregar `CashRefundsCents`, `NonCashRefundsCents`, `CreditNotesIssuedCents` (`long?`, nulos en turnos cerrados antes) a `src/Pos.Domain/CashShifts/CashShift.cs`; `CashShiftMath.ExpectedCash(...)` en `src/Pos.Domain/CashShifts/CashShiftMath.cs` resta `cashRefundsCents` y conserva el "efectivo cancelado heredado" solo para canceladas sin `SaleReturn` (research §6)

### Pruebas de dominio

- [X] T013 [P] Pruebas de `ReturnMath` en `tests/Pos.Domain.Tests/Returns/ReturnMathTests.cs`: acumulado exacto (devolver todo suma el importe de la línea sin centavos perdidos), cantidades fraccionarias, reparto proporcional con resto mayor, topes por pago tras devoluciones previas, suma exacta, un solo pago, reintegro nunca mayor a lo pagado (SC-004)
- [X] T014 [P] Pruebas de `SaleReturn` y `Sale.ApplyReturn` en `tests/Pos.Domain.Tests/Returns/SaleReturnTests.cs`: motivo vacío o > 250, sin líneas, sumas que no cuadran, no exceder lo vendido, no cancelar con devoluciones previas, no devolver sobre venta cancelada, `MarkReversed` una sola vez
- [X] T015 [P] Pruebas de saldo en `tests/Pos.Domain.Tests/CreditNotes/CreditNoteTests.cs`: emisión, `Redeem` válido, saldo insuficiente, restauración, saldo nunca negativo (SC-005)
- [X] T016 [P] Ampliar `tests/Pos.Domain.Tests/CashShifts/CashShiftMathTests.cs` con reintegros en efectivo, efectivo cancelado heredado y nota emitida sin efecto en efectivo; actualizar `tests/Pos.Domain.Tests/Users/RolePermissionsTests.cs` y las pruebas de `ModuleAccess` con los permisos nuevos; ajustar las pruebas existentes que construyen `ShiftSalesTotals`, `Sale` o `PaymentMethod` a las firmas nuevas

### Application

- [X] T017 [P] Agregar a `src/Pos.Application/Abstractions/Error.cs` los errores `ReturnWindowExpired(int Days)`, `NothingToReturn`, `CreditNoteNotFound` e `InsufficientCreditNote(long AvailableCents)` con los mensajes de contracts/application-ports.md; agregar a `src/Pos.Application/Audit/AuditActions.cs` `SALE_RETURNED` ("Devolución parcial de venta"), `CARD_REVERSAL_DONE` ("Reversa de tarjeta realizada"), `CREDIT_NOTE_REDEEMED` ("Nota de crédito usada") y `RETURN_SETTINGS_CHANGED` ("Plazo de devoluciones modificado")
- [X] T018 [P] Crear los puertos en `src/Pos.Application/Returns/` y `src/Pos.Application/CreditNotes/`: `IReturnRepository.cs` (`NextNumberAsync`, `Add`, `GetRefundAsync`, `GetReturnedByPaymentAsync(saleId)`, `SearchPendingReversalsAsync(ReversalSearch)` → página, `SaveChangesAsync` → `SaveOutcome`), `ICreditNoteRepository.cs` (`NextNumberAsync`, `FindByFolioAsync`, `GetAsync`, `Add`, `AddMovement`, `GetBalanceAsync`, `SearchAsync(CreditNoteSearch)`, `GetDetailAsync`, `SaveChangesAsync`), `IReturnsSettingsStore.cs` y `ReturnsSettings.cs` (`ReturnWindowDays` = 30, rango 1–3650), más `ReturnDtos.cs`, `ReturnMessages.cs` y `CreditNoteDtos.cs`
- [X] T019 Ampliar `src/Pos.Application/Sales/SaleDtos.cs` y `src/Pos.Application/Sales/ISaleRepository.cs` (solo contratos; la carga la hace T043): `SaleDetailDto` con `ReturnedCents`, disponible por línea (`Devuelto: X de Y`) e historial `ReturnSummaryDto` (folio D-…, fecha, usuario, motivo, autorizó, monto, compensación, folio de nota); `GetShiftTotalsAsync` con reintegros y neto de devoluciones (depende de T012, T018)
- [X] T020 Ampliar casos de uso de turnos en `src/Pos.Application/CashShifts/` (`GetMyShiftSummary`, `GetShiftDetail`, `CountShiftCash`, `CloseShift` y `CashShiftDtos.cs`): leen `ShiftSalesTotals` ampliado, el esperado resta `CashRefundsCents`, el cierre guarda la instantánea de las tres columnas nuevas (FR-016); revelar el esperado a Cajero sigue prohibido (depende de T012, T019)

### Infrastructure y migración

- [X] T021 [P] Crear configuraciones EF en `src/Pos.Infrastructure/Persistence/Configurations/`: `SaleReturnConfiguration.cs` (índices `IX_SaleReturns_Number` único, `IX_SaleReturns_SaleId`, `IX_SaleReturns_CashShiftId`; FK Restrict a `Sales`), `SaleReturnLineConfiguration.cs`, `SaleReturnRefundConfiguration.cs` (índice filtrado `IX_SaleReturnRefunds_Pending` sobre (`Status`, `Id`) con `Status = 'PENDING_REVERSAL'`), `CreditNoteConfiguration.cs` (índice único de `Number`) y `CreditNoteMovementConfiguration.cs` (índice único (`CreditNoteId`, `Sequence`)); sin `DeletedAt`, `UpdatedAt/By` ni `Version` (Complexity Tracking)
- [X] T022 [P] Ampliar `SaleConfiguration.cs` (`ReturnedCents` `NOT NULL DEFAULT 0`), `SaleLineConfiguration.cs` (`ReturnedQuantity` `NOT NULL DEFAULT 0`), `SalePaymentConfiguration.cs` (`CreditNoteId` nulo, sin FK; `Method` sigue TEXT(10)) y `CashShiftConfiguration.cs` (tres columnas `long?`) en `src/Pos.Infrastructure/Persistence/Configurations/`
- [X] T023 Agregar a `src/Pos.Infrastructure/Persistence/PosDbContext.cs` los `DbSet` de las 5 entidades nuevas y `RejectImmutableChanges` para `SaleReturn`, `SaleReturnLine`, `CreditNote` y `CreditNoteMovement` (rechazar cualquier modificación o borrado) y para `SaleReturnRefund` (solo permitir `PENDING_REVERSAL → REVERSED` una vez) (depende de T021)
- [X] T024 Generar la migración `ReturnsAndCreditNotes` con `dotnet ef migrations add ReturnsAndCreditNotes --project src/Pos.Infrastructure --startup-project src/Pos.Desktop` y revisar el SQL: 5 tablas nuevas y 6 `AddColumn` (`Sales.ReturnedCents`, `SaleLines.ReturnedQuantity` con `DEFAULT 0`; `SalePayments.CreditNoteId`, `CashShifts.CashRefundsCents`, `NonCashRefundsCents`, `CreditNotesIssuedCents` nulas); sin reconstruir ninguna tabla existente (sin `ef_temp_`); subir `Version` 0.7.0 → 0.8.0 donde esté declarada (depende de T022, T023)
- [X] T025 [P] Implementar `ReturnRepository` en `src/Pos.Infrastructure/Returns/ReturnRepository.cs` (`MAX + 1` del folio dentro de la transacción, reintegrado por pago, búsqueda de pendientes de 100 filas por página con el índice filtrado) y `PreferencesReturnsSettingsStore` en `src/Pos.Infrastructure/Returns/PreferencesReturnsSettingsStore.cs` con `IPreferencesStore` (30 días por defecto) (depende de T018, T023)
- [X] T026 [P] Implementar `CreditNoteRepository` en `src/Pos.Infrastructure/CreditNotes/CreditNoteRepository.cs`: folio `MAX + 1`, `FindByFolioAsync`, saldo calculado desde movimientos en la misma transacción, búsqueda y detalle con movimientos (depende de T018, T023)
- [X] T027 Actualizar `src/Pos.Infrastructure/Sales/SaleRepository.cs`, `src/Pos.Infrastructure/CashShifts/CashShiftRepository.cs`, `src/Pos.Infrastructure/Reports/SalesReportReader.cs` y `src/Pos.Infrastructure/Reports/CashCountReportReader.cs` para usar `TotalCents − ReturnedCents` en las ventas completadas (Inicio, turnos, reportes; research §11), sumar reintegros en efectivo, reintegros no efectivo y notas emitidas por `CashShiftId`, y calcular el efectivo cancelado heredado con `NOT EXISTS (SaleReturns)`; el importe de la fila de una venta sigue siendo el original (depende de T019, T023)
- [X] T028 Registrar en `src/Pos.Application/DependencyInjection.cs` y `src/Pos.Infrastructure/DependencyInjection.cs` los repositorios y el store de ajustes (depende de T025, T026). Cada tarea posterior que cree un caso de uso o `SaleReturnProcessor` lo registra en `src/Pos.Application/DependencyInjection.cs` como parte de la misma tarea

### Pruebas de migración

- [X] T029 Crear `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.8.0.db` siguiendo `docs/migraciones.md`, agregar `ReturnsMigrationTests` (SQL sin `CREATE TABLE "ef_temp_…"`, `dotnet ef migrations list` termina en `ReturnsAndCreditNotes`) y ampliar `SampleDatabaseUpgradeTests` para migrar `v0.1.0.db` … `v0.7.0.db` a la versión actual verificando que las ventas ya canceladas conserven su efectivo heredado (depende de T024)

**Checkpoint**: base lista; `dotnet build -v q` sin advertencias y pruebas de dominio y migración en verde

---

## Phase 3: User Story 1 - Cancelar una venta completa con autorización y reintegro o nota de crédito (Priority: P1) 🎯 MVP

**Goal**: cancelar una venta completada con motivo, autorización de un Administrador, inventario restaurado y compensación por reintegro (proporcional por forma de pago) o nota de crédito, en una sola transacción.

**Independent Test**: cancelar una venta en efectivo con reintegro, otra con tarjeta con reintegro, una mixta y otra con nota de crédito; verificar estado, inventario, efectivo de caja, anotación de reversa y saldo del vale (quickstart §2.1, 2.2, 2.5–2.9).

### Tests for User Story 1

- [X] T030 [P] [US1] Pruebas de casos de uso sobre SQLite real en `tests/Pos.Infrastructure.Tests/Returns/ReturnsUseCaseTests.cs` para `CancelSale`: motivo vacío rechazado; sin concesión `ApproveReturns` → `Forbidden(CanBeAuthorized: true)` y sin cambios, también para Administrador; efectivo descuenta del esperado; tarjeta anota `PENDING_REVERSAL` sin tocar efectivo; mixto reparte proporcionalmente con suma exacta; nota de crédito crea vale por el total sin mover efectivo; venta ya cancelada rechazada (`InvalidState`); doble cancelación concurrente (solo una tiene éxito, sin duplicar inventario ni dinero); plazo vencido (`ReturnWindowExpired`); venta de turno cerrado con reintegro en el turno abierto actual y el cerrado intacto; sin turno abierto no hay reintegro en efectivo; efectivo insuficiente → `InsufficientCash(null)` sin revelar montos; atomicidad (si falla el guardado no queda nada); con módulo Devoluciones inactivo conserva el comportamiento de 005/008
- [X] T031 [P] [US1] Prueba obligatoria de consistencia en `tests/Pos.Infrastructure.Tests/Returns/InventoryConsistencyAfterReturnsTests.cs`: tras cancelaciones, las existencias coinciden con el cálculo manual (`SALE_CANCEL`), producto inactivo o borrado regresa la cantidad, producto sin control de inventario no genera movimiento, y sin licencia de Inventario no hay movimientos

### Implementation for User Story 1

- [X] T032 [US1] Implementar `SaleReturnProcessor` en `src/Pos.Application/Returns/SaleReturnProcessor.cs` con el orden de contracts/application-ports.md dentro de una transacción `BEGIN IMMEDIATE`: licencia → permiso → validar entrada → cargar venta → versión y estado → plazo (`IReturnsSettingsStore`) → cantidades → turno y efectivo (solo si hay reintegro en efectivo: turno abierto propio para Cajero, cualquiera para `ManageShifts`; omitir con Turnos sin licencia) → consumir la concesión `ApproveReturns` → construir `SaleReturn` + movimientos `SALE_CANCEL`/`SALE_RETURN` (solo productos con inventario y módulo con licencia) + reintegros (`ReturnMath.Allocate`) o nota (`ISSUE` por el total) + restauración de nota si el pago original fue `CREDIT` → entrada de bitácora (`SALE_CANCELLED`/`SALE_RETURNED` con compensación, autorizador, folios) → guardar → confirmar; reintento ante folio duplicado; Serilog con venta, folio, usuario y montos sin PIN; registrar `SaleReturnProcessor` en DI (depende de T028)
- [X] T033 [US1] Refactorizar `src/Pos.Application/Sales/CancelSale/CancelSaleHandler.cs`, `CancelSaleCommand.cs` (+ `Compensation`, `REFUND` por defecto, + `AuthorizationGrantId`) y `CancelSaleValidator.cs` (motivo no vacío ≤ 250): con Devoluciones activo delega en `SaleReturnProcessor`, exige la concesión y deja de restringir a ventas del turno abierto; con el módulo inactivo conserva el flujo de 005/008 con `CancelSales` (research §12); rechaza ventas con devoluciones parciales previas con `InvalidState`; actualizar las pruebas existentes de cancelación de 005/008 (`tests/Pos.Application.Tests/Sales/`, `tests/Pos.Infrastructure.Tests/`) que afirman el rechazo de ventas de turnos cerrados, que FR-006 reemplaza (depende de T032)
- [X] T034 [P] [US1] Crear `PreviewReturn` en `src/Pos.Application/Returns/PreviewReturn/` (comando, validador y handler): solo lectura, permiso `ProcessReturns`, entrada `SaleId` + líneas o `All`, salida `ReturnPreview { TotalCents, Lines[], RefundBreakdown[], CashRefundCents, WithinWindow, CanRefundCash }` calculada con `ReturnMath`; `CanRefundCash` falso sin turno utilizable o efectivo insuficiente sin revelar montos; registrar en DI (depende de T032)
- [X] T035 [P] [US1] Implementar `CreditNoteTicketBuilder` en `src/Pos.Application/Printing/Ticket/CreditNoteTicketBuilder.cs` (folio, saldo, fecha, folio de la venta de origen) y agregar `PrintSource.CreditNote(creditNoteId)` en `src/Pos.Application/Printing/PrintTicket/`; se imprime al emitir sin pedir confirmación y una falla de impresión no revierte nada (research §13)
- [X] T036 [US1] Crear `ReturnSaleView.axaml`, `ReturnSaleView.axaml.cs` y `ReturnSaleViewModel.cs` en `src/Pos.Desktop/Sales/` (reemplaza a `CancelSaleView*`; se elimina `CancelSaleView.axaml`, `.axaml.cs` y `CancelSaleViewModel.cs`): líneas con "Seleccionar todo", motivo obligatorio (≤ 250), compensación *Reintegro* | *Nota de crédito*, resumen vía `PreviewReturn` con reparto por forma de pago y mensaje genérico si el efectivo no alcanza, confirmar abre siempre `AdminAuthorizationView` de 007 (también para Administrador) y luego invoca `CancelSale`; resultado con folio D-…, impresión del ticket de la nota con "Reintentar" si falla, y `Conflict` recarga el detalle (depende de T033, T034, T035)
- [X] T037 [US1] Ajustar `src/Pos.Desktop/Sales/SaleDetailView.axaml` y `SaleDetailViewModel.cs` para abrir `ReturnSaleViewModel` desde "Cancelar venta" (visible con `ProcessReturns` y módulo; oculto si la venta está cancelada, totalmente devuelta, con devoluciones previas o fuera del plazo con la leyenda "Fuera del plazo de devoluciones"); sin el módulo solo queda el botón básico de siempre (contracts/ui.md §1) (depende de T036)
- [X] T038 [P] [US1] Agregar textos `Return_*` y `CreditNote_*` de este flujo (motivo, compensación, resumen, errores de `ReturnWindowExpired`/`NothingToReturn`/`InsufficientCash`, resultado) a `src/Pos.Desktop/Resources/Strings.resx`, y registrar `SALE_CANCEL` → "Devolución por venta cancelada" y `SALE_RETURN` → "Devolución de venta" en las etiquetas de movimientos y del reporte de inventario (contracts/ui.md §6)
- [X] T039 [P] [US1] Mostrar en el detalle de turno y en el corte impreso "Reintegros en efectivo", "Reintegros de tarjeta y transferencia pendientes de reversa" y "Notas de crédito emitidas" (0 en turnos cerrados antes de 0.8.0) en `src/Pos.Desktop/CashShifts/ShiftDetailView.axaml` y el constructor del corte en `src/Pos.Application/Printing/Ticket/` (FR-016, contracts/ui.md §5) (depende de T020)

**Checkpoint**: la cancelación completa con autorización, reintegro o nota de crédito funciona y se prueba por sí sola; es el MVP

---

## Phase 4: User Story 3 - Auditoría e historial inmutable (Priority: P1)

**Goal**: rastro confiable: bitácora con todos los datos, historial de eventos en el detalle de la venta, intentos de autorización fallidos registrados, reversas de tarjeta administrables e inmutabilidad real.

**Independent Test**: realizar una cancelación y una devolución parcial y verificar bitácora e historial de la venta; confirmar que no existe forma de modificarlos (quickstart §2.2, 2.6).

### Tests for User Story 3

- [X] T040 [P] [US3] Ampliar `tests/Pos.Infrastructure.Tests/Returns/ReturnsUseCaseTests.cs` con: la bitácora contiene usuario, fecha, motivo, autorizador, venta, monto y compensación; un intento fallido de `AuthorizeAdmin` registra `ADMIN_AUTHORIZATION_DENIED` sin el PIN y, tras repetidos intentos fallidos, la autorización se bloquea temporalmente según `LoginThrottle` de 007; `MarkReversalDone` cambia el estado una sola vez (`InvalidState` la segunda) y audita `CARD_REVERSAL_DONE`; `PosDbContext` rechaza editar o borrar `SaleReturn`, `SaleReturnLine`, `CreditNote` y `CreditNoteMovement`; `SaleReturn` y los acumulados `ReturnedCents`/`ReturnedQuantity` coinciden con la suma de `SaleReturnLines`; solo `ManageCreditNotes` puede listar y marcar

### Implementation for User Story 3

- [X] T041 [P] [US3] Crear `SearchPendingReversals` en `src/Pos.Application/Returns/SearchPendingReversals/` (permiso `ManageCreditNotes`, filtro Pendientes/Reversados/Todos, 100 por página, salida `{ RefundId, ReturnFolio, SaleFolio, Method, AmountCents, CreatedAtUtc, Status, ReversedBy? }`) y `MarkReversalDone` en `src/Pos.Application/Returns/MarkReversalDone/` (`InvalidState` si ya está reversado, `SaleReturnRefund.MarkReversed`, auditoría `CARD_REVERSAL_DONE`, una sola transacción); registrar ambos en DI (depende de T028)
- [X] T042 [P] [US3] Crear `GetReturnsSettings` y `SaveReturnsSettings` en `src/Pos.Application/Returns/GetReturnsSettings/` y `SaveReturnsSettings/` (permiso `ManageCreditNotes`, `ReturnWindowDays` 1–3650 con validador FluentValidation, audita `RETURN_SETTINGS_CHANGED`); registrar en DI (depende de T028)
- [X] T043 [US3] Hacer que `GetSale`/`ReviewSale` en `src/Pos.Application/Sales/` devuelva el historial de devoluciones con la venta original sin cambios, y que `SaleRepository.GetDetailAsync` en `src/Pos.Infrastructure/Sales/SaleRepository.cs` lo cargue junto con el disponible por línea (FR-013; depende de T019)
- [X] T044 [US3] Mostrar en `src/Pos.Desktop/Sales/SaleDetailView.axaml` y `SaleDetailViewModel.cs` la sección "Historial" (folio D-…, fecha, usuario, motivo, autorizó, monto, compensación y folio de nota) debajo de las líneas, con "Devuelto: X de Y" por línea y sin ninguna opción de editar o eliminar (depende de T043)
- [X] T045 [US3] Crear la página "Devoluciones y vales" en `src/Pos.Desktop/Returns/`: `ReturnsModule.cs` (grupo Ventas `SalesModule.GroupId`, orden 30, permiso `ManageCreditNotes`, módulo Devoluciones, desaparece sin licencia), `ReturnsAdminView.axaml`, `ReturnsAdminView.axaml.cs` y `ReturnsAdminViewModel.cs` con las pestañas **Reintegros pendientes** (tabla, filtro Pendientes/Reversados/Todos, "Marcar como reversado" con confirmación, paginación de 100 como "Turnos") y **Configuración** (plazo en días, 30 por defecto, guardar); registrar `AddReturnsModule` en `src/Pos.Desktop/Composition/HostBuilder.cs` (depende de T041, T042)
- [X] T046 [P] [US3] Agregar `Nav_Returns` y los textos de la página, el historial y las pestañas a `src/Pos.Desktop/Resources/Strings.resx`

**Checkpoint**: US1 y US3 funcionan; el rastro es completo e inmutable

---

## Phase 5: User Story 2 - Devolución parcial de líneas y cantidades (Priority: P2)

**Goal**: devolver líneas y cantidades parciales con monto recalculado y acumulable, con el mismo flujo de motivo, autorización y compensación.

**Independent Test**: devolver parte de una línea de una venta de varias líneas y verificar monto, inventario, compensación y que la venta sigue vigente por el resto (quickstart §2.3, 2.4).

### Tests for User Story 2

- [X] T047 [P] [US2] Ampliar `tests/Pos.Infrastructure.Tests/Returns/ReturnsUseCaseTests.cs` con `ReturnSaleItems`: cantidad mayor a la disponible → `NothingToReturn`; sin líneas rechazada; devolución acumulada hasta agotar lo vendido y la venta pasa a "totalmente devuelta"; devolución parcial en venta de pagos mixtos reparte proporcionalmente con topes por pago restante; `CancelSale` tras una devolución parcial → `InvalidState`; concurrencia de dos devoluciones sobre la misma línea sin exceder lo vendido; venta sigue `COMPLETED` y conserva su detalle; totales de turno e Inicio netos de la devolución (FR-015); ampliar `InventoryConsistencyAfterReturnsTests.cs` con `SALE_RETURN`

### Implementation for User Story 2

- [X] T048 [US2] Crear `ReturnSaleItems` en `src/Pos.Application/Returns/ReturnSaleItems/` (`ReturnSaleItemsCommand` con `SaleId`, `ExpectedVersion`, `Lines[{SaleLineId, QuantityThousandths}]`, `Reason`, `Compensation`, `AuthorizationGrantId`; validador; handler que delega en `SaleReturnProcessor` como `PARTIAL`; devuelve `ReturnResult { ReturnId, Folio, TotalCents, CreditNoteId?, CreditNoteFolio? }`); registrar en DI (depende de T032)
- [X] T049 [US2] Ampliar `src/Pos.Desktop/Sales/ReturnSaleViewModel.cs` y `ReturnSaleView.axaml` con modo "Devolver artículos": casilla y cantidad por línea respetando los decimales de la unidad con máximo = disponible, parte con todo sin marcar, monto recalculado vía `PreviewReturn` antes de confirmar, e invoca `ReturnSaleItems` tras la autorización (depende de T048, T036)
- [X] T050 [US2] Agregar a `src/Pos.Desktop/Sales/SaleDetailView.axaml` y `SaleDetailViewModel.cs` el botón **"Devolver artículos"** (visible con `ProcessReturns` y módulo; oculto si la venta está cancelada, totalmente devuelta o fuera del plazo) y mostrar el estado parcialmente/totalmente devuelta (depende de T049)
- [X] T051 [P] [US2] Textos de devolución parcial en `src/Pos.Desktop/Resources/Strings.resx` (etiquetas de estado, "Devuelto: X de Y", errores de cantidad)

**Checkpoint**: devoluciones parciales y acumuladas funcionan junto con US1 y US3

---

## Phase 6: User Story 4 - Usar una nota de crédito como forma de pago (Priority: P2)

**Goal**: pagar una venta, total o parcialmente, con una nota de crédito, combinada con otras formas de pago, descontando el saldo; el Administrador consulta y reimprime notas.

**Independent Test**: emitir una nota, pagar una venta menor con ella y verificar el saldo restante; pagar una venta mayor combinándola con efectivo (quickstart §2.5).

### Tests for User Story 4

- [X] T052 [P] [US4] Ampliar `tests/Pos.Infrastructure.Tests/Returns/ReturnsUseCaseTests.cs` con: pago con nota menor al saldo descuenta el total de la venta; mayor al saldo con efectivo deja saldo 0; folio inexistente o sin saldo → `CreditNoteNotFound`; saldo insuficiente → `InsufficientCreditNote`; a lo más un pago con nota por venta; dos cobros simultáneos con la misma nota no gastan el saldo dos veces; módulo sin licencia → `ModuleNotLicensed`; cancelar una venta pagada con nota restaura el saldo a esa nota (`RESTORE`) nunca como efectivo; suma de saldos más usos coincide con lo emitido (SC-005); `GetCreditNoteBalance` no revela origen ni usos

### Implementation for User Story 4

- [X] T053 [US4] Ampliar `src/Pos.Application/Sales/ConfirmSale/ConfirmSaleHandler.cs` para aceptar un pago `CREDIT` con `Reference` = folio: resuelve la nota, valida módulo y saldo dentro de la transacción, agrega el movimiento `REDEEM`, fija `SalePayment.CreditNoteId`, a lo más un pago con nota, audita `CREDIT_NOTE_REDEEMED` (depende de T028)
- [X] T054 [P] [US4] Crear `GetCreditNoteBalance` en `src/Pos.Application/CreditNotes/GetCreditNoteBalance/` (permiso `Sell`, entrada `Folio`, salida `CreditNoteBalance { CreditNoteId, Folio, BalanceCents }`, `CreditNoteNotFound` si no existe o saldo 0, sin origen ni usos); registrar en DI (depende de T028)
- [X] T055 [P] [US4] Crear `SearchCreditNotes` en `src/Pos.Application/CreditNotes/SearchCreditNotes/` (permiso `ManageCreditNotes`, filtro `Folio?` y `OnlyWithBalance`, 100 por página, `{ Id, Folio, IssuedAtUtc, InitialCents, BalanceCents, SaleFolio }`) y `GetCreditNoteDetail` en `src/Pos.Application/CreditNotes/GetCreditNoteDetail/` (cabecera + movimientos con venta o devolución relacionada, `NotFound`); registrar ambos en DI (depende de T028)
- [X] T056 [US4] Agregar la forma de pago **"Nota de crédito"** a `src/Pos.Desktop/Sales/CheckoutView.axaml` y `CheckoutViewModel.cs` (solo con módulo Devoluciones): campo de folio y botón "Aplicar" → `GetCreditNoteBalance` → muestra el saldo y agrega el pago por `min(saldo, pendiente)`; folio inválido muestra el mensaje del error; a lo más una nota por venta; registrar la etiqueta `CREDIT` → "Nota de crédito" en `src/Pos.Desktop/Sales/PaymentMethodLabels.cs` (depende de T053, T054)
- [X] T057 [US4] Agregar la pestaña **Notas de crédito** a `src/Pos.Desktop/Returns/ReturnsAdminView.axaml` y `ReturnsAdminViewModel.cs`: tabla (folio, fecha, importe, saldo, venta de origen), filtro por folio y "solo con saldo", detalle con movimientos y botón "Reimprimir" (`PrintTicket` con `PrintSource.CreditNote`, `ManageCreditNotes`) (depende de T055, T045, T035)
- [X] T058 [P] [US4] Textos del cobro con nota y de la pestaña de notas en `src/Pos.Desktop/Resources/Strings.resx`

**Checkpoint**: las cuatro historias funcionan de forma independiente y combinada

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: documentación, validación final y regresiones

- [X] T059 [P] Crear `docs/devoluciones.md` (guía de soporte: reparto proporcional, turnos y reintegro en el turno abierto actual, reversas de tarjeta, notas de crédito y su riesgo de folio consecutivo, plazo configurable, licencia y cancelación básica); agregar la sección 0.8.0 (sin reconstrucciones) a `docs/migraciones.md`; actualizar `docs/ventas.md` y `docs/turnos-de-caja.md` (cancelación y totales netos)
- [X] T060 Revisar con `grep` que ninguna otra pantalla o reporte sume `Sales.TotalCents` sin restar `ReturnedCents` (Inicio, turnos, `SalesReportReader`, `CashCountReportReader`) y que las cifras de Inicio, "Turnos" y corte coincidan (FR-015)
- [X] T061 Verificar en Serilog que cada devolución, rechazo y fallo se registra con venta, folio, usuario y montos y nunca el PIN, y que un error inesperado muestra un mensaje sin detalles técnicos (Principio VIII)
- [X] T062 Ejecutar `dotnet build -v q` (0 errores, 0 advertencias) y `dotnet test --verbosity quiet` completo; corregir regresiones en pruebas existentes que construyen `ShiftSalesTotals`, `Sale` o `PaymentMethod`
- [ ] T063 Ejecutar los 10 escenarios manuales de `specs/013-returns-cancellations/quickstart.md` §2 y la verificación de migración de §3, y confirmar SC-001 a SC-006

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; BLOQUEA todas las historias
- **US1 (P1)**: depende de Foundational; es el MVP
- **US3 (P1)**: depende de Foundational; usa `SaleReturnProcessor` (T032) para probar bitácora, así que conviene después de US1
- **US2 (P2)**: depende de Foundational y de `SaleReturnProcessor` (T032) y `ReturnSaleView` (T036) de US1
- **US4 (P2)**: depende de Foundational; la pestaña de notas (T057) depende de la página de US3 (T045) y del ticket de US1 (T035)
- **Polish (Phase 7)**: depende de las historias deseadas

### Within Foundational

- Dominio: T003, T004, T005, T006, T007, T008 en paralelo → T009, T010, T011, T012 → pruebas T013–T016 en paralelo
- T017, T018 en paralelo → T019, T020
- T021, T022 en paralelo → T023 → T024 → T029
- T025, T026 en paralelo tras T023 → T027, T028

### Within Each User Story

- Pruebas primero (deben fallar antes de implementar)
- Dominio → casos de uso → repositorios → ViewModels y vistas → textos
- Historia completa antes de pasar a la siguiente prioridad

### Parallel Opportunities

- Todas las tareas [P] de Foundational dentro de su bloque
- US2 y US4 pueden avanzar en paralelo una vez completada US1 (archivos distintos salvo `ReturnsUseCaseTests.cs`, `Strings.resx` y `SaleDetailViewModel.cs`)
- Dentro de US1: T034 y T035 en paralelo; T038 y T039 en paralelo

---

## Parallel Example: User Story 1

```bash
# Pruebas de US1 en paralelo:
Task: "Pruebas de CancelSale en tests/Pos.Infrastructure.Tests/Returns/ReturnsUseCaseTests.cs"
Task: "Prueba de consistencia en tests/Pos.Infrastructure.Tests/Returns/InventoryConsistencyAfterReturnsTests.cs"

# Tras SaleReturnProcessor:
Task: "PreviewReturn en src/Pos.Application/Returns/PreviewReturn/"
Task: "CreditNoteTicketBuilder en src/Pos.Application/Printing/Ticket/CreditNoteTicketBuilder.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 y Phase 2 (dominio, migración, repositorios, totales netos)
2. Phase 3: cancelación completa con autorización, reintegro y nota de crédito
3. **DETENER y VALIDAR**: quickstart §2.1, 2.2, 2.5–2.9
4. Phase 4 (US3) para completar el rastro obligatorio del dueño del negocio

### Incremental Delivery

1. Foundational → US1 (MVP) → US3 (auditoría y reversas) → US2 (parciales) → US4 (nota como pago y administración de notas) → Polish
2. Cada historia agrega valor sin romper las anteriores

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- [Story] vincula la tarea con su historia para trazabilidad
- Las ventas no tienen descuentos ni impuestos por línea hoy: el importe de línea es `cantidad × precio` (research, hallazgos); `ReturnMath` debe seguir siendo exacto si existieran
- La "contraseña de Administrador" de 007 es lo que la especificación llama "PIN"; no se crea un PIN nuevo
- No se agrega ninguna dependencia externa
- Confirmar tras cada tarea o grupo lógico
