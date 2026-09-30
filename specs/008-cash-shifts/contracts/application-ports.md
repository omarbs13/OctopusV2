# Contrato: casos de uso y puertos de Application

Interfaz que Desktop consume e Infrastructure implementa. Las firmas son orientativas; los nombres
exactos se fijan al implementar. Todos los casos de uso devuelven `Result` o `Result<T>` y
reservan las excepciones para fallas inesperadas (Principio III). Cada escritura es una sola
transacción `BEGIN IMMEDIATE` (FR-026) e incluye su entrada de bitácora (FR-027).

## Errores nuevos (`Abstractions/Error.cs`)

| Error | Significado | Mensaje al operador |
|---|---|---|
| `ShiftRequired` | No hay turno abierto (FR-001) | "Abra un turno para vender" |
| `ShiftOwnedByOther(string OpenedByName)` | El turno abierto es de otro usuario (FR-005) | "Hay un turno abierto de {nombre}. Debe cerrarse antes de vender" |
| `ShiftAlreadyOpen` | Ya existe un turno abierto (FR-004), también por el índice único | "Ya hay un turno abierto" |
| `ShiftClosed` | El turno ya está cerrado (FR-018) | "El turno ya está cerrado" |
| `InsufficientCash(long? AvailableCents)` | El retiro o la devolución excede el efectivo esperado. `AvailableCents` solo se llena para quien tiene `ManageShifts` y **solo** en retiros (FR-008, FR-010) | Cajero: "El retiro excede el efectivo disponible en caja". Administrador: "… Disponible: $X". Cancelación: "No hay efectivo suficiente en caja para devolver esta venta. Registre un ingreso e intente de nuevo" |
| `SaleInProgress` | El dueño tiene una venta en curso (FR-013) | "Termine o cancele la venta en curso antes de cerrar el turno" |
| `HeldSaleWillBeDiscarded(string OwnerName)` | Un administrador cierra un turno ajeno con venta conservada; falta confirmar | "{nombre} tiene una venta en curso guardada. Si continúa, se descartará" |
| `ShiftChanged` | El esperado cambió entre el conteo y el cierre | "El turno cambió. Revise las cifras de nuevo" |

Se reutilizan `ValidationFailed`, `NotFound`, `Conflict`, `Forbidden` e `InvalidState`.

## Puertos nuevos o ampliados

| Puerto | Miembros | Implementación |
|---|---|---|
| `ICashShiftRepository` (nuevo, `Application/CashShifts`) | `GetOpenAsync(registerCode)` con movimientos y seguimiento; `GetAsync(id)`; `FindMovementAsync(movementId)`; `NextNumberAsync()`; `Add(shift)`; `AddMovement(movement)`; `SearchAsync(ShiftSearch)` → `ShiftPage`; `GetDetailAsync(id)` → `ShiftDetailDto?`; `SaveChangesAsync()` → `SaveOutcome` (`Duplicate` con `OpenPerRegister` o `Number`) | `Infrastructure/CashShifts/CashShiftRepository` |
| `ISaleRepository` (existente) | + `GetShiftTotalsAsync(shiftId)` → `ShiftSalesTotals`; + `ListByShiftAsync(shiftId)` → filas de venta del detalle; `SaleSearch` sin cambios | `SaleRepository` |
| `IAccessControl` (existente) | + `HasAsync(Permission)`: igual que `CheckAsync`, pero sin registrar rechazos ni consumir concesiones (research §7) | `AccessControl` |
| `ISaleDraftStore` (existente) | Sin cambios: se usan `HasForAsync` y `RemoveForAsync` | — |

## Casos de uso nuevos (`Pos.Application/CashShifts/…`)

| Caso de uso | Permiso | Entrada | Salida | Errores y reglas |
|---|---|---|---|---|
| `GetCurrentShift` | `OperateShift` | — | `CurrentShiftSummary?` = `{ ShiftId, Version, Folio, OpenedAtUtc, OpenedById, OpenedByName, IsMine, SalesCount, TotalSoldCents }` | Nunca devuelve fondo, esperado, desglose ni movimientos (FR-022). `null` si no hay turno |
| `OpenShift` | `OperateShift` | `OpeningFloatCents`, `ConfirmZeroFloat` | `Result<CurrentShiftSummary>` | `ValidationFailed` si es negativo o mayor que el máximo, o si es 0 sin `ConfirmZeroFloat` (FR-003); `ShiftAlreadyOpen`. Audita `SHIFT_OPENED` |
| `RegisterCashMovement` | Ingreso: `OperateShift`. Retiro: `WithdrawCash` (autorizable) | `ShiftId`, `Type`, `AmountCents`, `Reason`, `AuthorizationGrantId?` | `Result<RegisteredMovement { MovementId, Folio }>` | `ValidationFailed` (monto ≤ 0, motivo vacío o de más de 250); `ShiftClosed`/`NotFound`; `Forbidden` si el turno no es del usuario y no tiene `ManageShifts`; `InsufficientCash` en retiros (FR-010). Audita `CASH_DEPOSIT` o `CASH_WITHDRAWAL` con `AuthorizedBy` |
| `CountShiftCash` | Turno propio: `OperateShift`. Turno ajeno: `ManageShifts` | `ShiftId`, `CountedCents`, `DiscardHeldSale` | `Result<ShiftCountResult { ExpectedCents, CountedCents, DifferenceCents, DifferenceKind, CardCents, TransferCents, Version }>` | `SaleInProgress` o `HeldSaleWillBeDiscarded` **antes** de revelar cifras; `ShiftClosed`; `ValidationFailed`. Audita `SHIFT_CASH_COUNTED` (research §8) |
| `CloseShift` | Igual que `CountShiftCash` | `ShiftId`, `ExpectedVersion`, `CountedCents`, `ShownExpectedCents`, `Comment?`, `DiscardHeldSale` | `Result<ClosedShift { ShiftId, Folio }>` | `ShiftChanged` si el esperado recalculado ≠ `ShownExpectedCents`; `Conflict` por versión; `ValidationFailed` si hay diferencia y no hay comentario (FR-017); `SaleInProgress` / `HeldSaleWillBeDiscarded`. Con `DiscardHeldSale` borra el borrador del dueño y audita `HELD_SALE_DISCARDED`. Audita `SHIFT_CLOSED` o `SHIFT_CLOSED_BY_ADMIN` |
| `SearchShifts` | `ManageShifts` | `FromUtc?`, `ToUtcExclusive?` (apertura), `UserId?`, `Status?`, página | `ShiftPage` (100 por página) de `ShiftListItemDto { Id, Folio, OpenedByName, OpenedAtUtc, ClosedAtUtc?, Status, TotalSoldCents, DifferenceCents? }` | `ValidationFailed` si el rango es inválido. Diferencia nula en los abiertos |
| `GetShiftDetail` | `ManageShifts` | `ShiftId` | `ShiftDetailDto`: datos del turno, `Sales[]`, `Movements[]`, `Reconciliation` (la instantánea si está cerrado; el esperado actual si está abierto) | `NotFound` |

## Casos de uso existentes que cambian

| Caso de uso | Cambio |
|---|---|
| `ConfirmSale` | Dentro de la transacción, **después** de la idempotencia por `DraftId`: `ShiftRequired` / `ShiftOwnedByOther`. Registra `Sale.CashShiftId` (research §5) |
| `CancelSale` | Dentro de la transacción: `InvalidState("La venta pertenece a un turno cerrado")` si `CashShiftId` es nulo o no es el turno abierto. Con pago en efectivo: `InsufficientCash(null)` si el esperado quedaría negativo (research §6) |
| `PrintTicket` | `PrintSource.ShiftReport(ShiftId)`: `ManageShifts`, o `ClosedBy` = usuario actual; solo turnos cerrados. `PrintSource.CashMovement(MovementId)`: `ManageShifts`, o movimiento propio en el turno propio (research §12) |
| `ReviewSale`, `FindProductsForSale`, `SaveSaleDraft` | Sin cambios. La barrera es `ConfirmSale`; la interfaz deshabilita la captura sin turno propio |

## DTO de impresión

`ShiftTicketBuilder.BuildReport(BusinessProfileDto?, ShiftReportDto, int columns, TicketOptions)` y
`BuildMovementReceipt(BusinessProfileDto?, CashMovementReceiptDto, int columns, TicketOptions)`.
Ambos devuelven `TicketDocument`. `ShiftReportDto` contiene **solo** los campos de FR-019, leídos de
la instantánea del turno cerrado.
