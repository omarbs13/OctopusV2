# Contrato: casos de uso y puertos de Application

Interfaz que Desktop consume e Infrastructure implementa. Las firmas son orientativas; los nombres
exactos se fijan al implementar. Todos los casos de uso devuelven `Result` o `Result<T>` y
reservan las excepciones para fallas inesperadas (Principio III). Cada escritura es una sola
transacción `BEGIN IMMEDIATE` (FR-011) e incluye su entrada de bitácora (FR-012).

## Errores nuevos (`Abstractions/Error.cs`)

| Error | Significado | Mensaje al operador |
|---|---|---|
| `ReturnWindowExpired(int Days)` | La venta excede el plazo (FR-006a) | "La venta tiene más de {N} días y ya no admite devoluciones" |
| `NothingToReturn` | Sin líneas, o la cantidad excede lo disponible (FR-009, FR-014) | "Seleccione al menos un artículo y no exceda la cantidad disponible" |
| `CreditNoteNotFound` | Folio inexistente o sin saldo (Historia 4, escenario 4) | "La nota de crédito no existe o no tiene saldo" |
| `InsufficientCreditNote(long AvailableCents)` | El monto excede el saldo | "El saldo de la nota es {saldo}" |

Se reutilizan `Forbidden(ApproveReturns, CanBeAuthorized: true)` (falta la autorización),
`ModuleNotLicensed`, `ShiftRequired`, `ShiftOwnedByOther`, `InsufficientCash(null)` (sin revelar
montos), `InvalidState` (venta ya cancelada), `Conflict`, `NotFound` y `ValidationFailed`.

## Puertos nuevos o ampliados

| Puerto | Miembros | Implementación |
|---|---|---|
| `IReturnRepository` (nuevo, `Application/Returns`) | `NextNumberAsync()`; `Add(saleReturn)`; `GetRefundAsync(id)`; `GetReturnedByPaymentAsync(saleId)` (reintegrado por pago); `SearchPendingReversalsAsync(ReversalSearch)` → página; `SaveChangesAsync()` → `SaveOutcome` | `Infrastructure/Returns/ReturnRepository` |
| `ICreditNoteRepository` (nuevo, `Application/CreditNotes`) | `NextNumberAsync()`; `FindByFolioAsync(number)` → nota con saldo; `GetAsync(id)`; `Add(note)`; `AddMovement(movement)`; `GetBalanceAsync(id)`; `SearchAsync(CreditNoteSearch)` → página; `GetDetailAsync(id)`; `SaveChangesAsync()` | `Infrastructure/CreditNotes/CreditNoteRepository` |
| `ISaleRepository` (existente) | + `GetShiftTotalsAsync` con reintegros y neto de devoluciones; `GetDetailAsync` incluye historial de devoluciones y disponible por línea; `SaleSearch` sin cambios | `SaleRepository` |
| `IReturnsSettingsStore` (nuevo) | `Get()` / `Save(settings)` con `ReturnsSettings { ReturnWindowDays }` | `Infrastructure/Returns/PreferencesReturnsSettingsStore` |
| `IInventoryRepository`, `IAuditLog`, `IAuthorizationGrants` | Sin cambios | — |

## Casos de uso nuevos (`Pos.Application/Returns/…` y `CreditNotes/…`)

| Caso de uso | Permiso | Entrada | Salida | Reglas |
|---|---|---|---|---|
| `PreviewReturn` | `ProcessReturns` | `SaleId`, `Lines[{SaleLineId, QuantityThousandths}]` o `All` | `ReturnPreview { TotalCents, Lines[], RefundBreakdown[{Method, AmountCents}], CashRefundCents, WithinWindow, CanRefundCash }` | Solo lectura. Aplica el plazo y calcula con `ReturnMath` (la interfaz no calcula, Principio III). `CanRefundCash` es falso sin turno abierto utilizable o si el efectivo esperado no alcanza, **sin** revelar montos |
| `ReturnSaleItems` | `ProcessReturns` + concesión `ApproveReturns` | `SaleId`, `ExpectedVersion`, `Lines[]`, `Reason`, `Compensation`, `AuthorizationGrantId` | `Result<ReturnResult { ReturnId, Folio, TotalCents, CreditNoteId?, CreditNoteFolio? }>` | Devolución parcial (H2). Mismas reglas que `CancelSale` salvo que es por líneas |
| `GetCreditNoteBalance` | `Sell` | `Folio` | `Result<CreditNoteBalance { CreditNoteId, Folio, BalanceCents }>` | `CreditNoteNotFound` si no existe o saldo 0. No muestra origen ni usos |
| `SearchCreditNotes` | `ManageCreditNotes` | `Folio?`, `OnlyWithBalance`, página | `CreditNotePage` (100 por página): `{ Id, Folio, IssuedAtUtc, InitialCents, BalanceCents, SaleFolio }` | |
| `GetCreditNoteDetail` | `ManageCreditNotes` | `CreditNoteId` | Cabecera + movimientos con venta/devolución relacionada | `NotFound` |
| `SearchPendingReversals` | `ManageCreditNotes` | `Status` (pendientes, reversados, todos), página | `{ RefundId, ReturnFolio, SaleFolio, Method, AmountCents, CreatedAtUtc, Status, ReversedBy? }` | |
| `MarkReversalDone` | `ManageCreditNotes` | `RefundId` | `Result` | `InvalidState` si ya está reversado; audita `CARD_REVERSAL_DONE` |
| `GetReturnsSettings` / `SaveReturnsSettings` | `ManageCreditNotes` | `ReturnWindowDays` (1–3650) | `ReturnsSettings` | Audita `RETURN_SETTINGS_CHANGED` |

Núcleo compartido (no es un caso de uso): `SaleReturnProcessor` en `Application/Returns`, usado por
`CancelSale` y `ReturnSaleItems`. Orden dentro de la transacción: licencia → permiso → validar entrada
→ cargar venta → versión y estado → plazo → cantidades → turno y efectivo (si hay reintegro en
efectivo) → **consumir la concesión** → construir `SaleReturn`, movimientos de inventario, reintegros o
nota → bitácora → guardar → confirmar.

## Casos de uso existentes que cambian

| Caso de uso | Cambio |
|---|---|
| `CancelSale` | `CancelSaleCommand` gana `Compensation` (`REFUND` por defecto). Con Devoluciones activo: usa `SaleReturnProcessor`, exige la concesión de `ApproveReturns` (también del Administrador) y deja de restringir a ventas del turno abierto. Con el módulo inactivo: conserva el comportamiento de 005/008 con `CancelSales` (research §12). Rechaza ventas con devoluciones parciales previas (`InvalidState`) |
| `ConfirmSale` | Acepta un pago `CREDIT` con `Reference` = folio de la nota: resuelve la nota, valida saldo y módulo, agrega el movimiento `REDEEM` y fija `SalePayment.CreditNoteId`. A lo más un pago con nota; solo si el módulo Devoluciones está activo. Audita `CREDIT_NOTE_REDEEMED` |
| `GetSale` / `ReviewSale` | `SaleDetailDto` agrega `ReturnedCents`, disponible por línea e historial de devoluciones (`ReturnSummaryDto`) |
| `PrintTicket` | `PrintSource.CreditNote(creditNoteId)` (research §13) |
| `GetMyShiftSummary`, `GetShiftDetail`, `CountShiftCash`, `CloseShift` | Leen `ShiftSalesTotals` ampliado; el esperado resta `CashRefundsCents`; la instantánea guarda las tres columnas nuevas; el corte las imprime (FR-016) |
| Reportes 009 (`SalesReportReader`, `CashCountReportReader`) | Total neto de devoluciones (research §11) |

## Catálogo de bitácora (`AuditActions`)

| Código | Texto | Cuándo |
|---|---|---|
| `SALE_CANCELLED` (existente) | "Venta cancelada" | Cancelación completa; el detalle agrega compensación, autorizador, folios de devolución y nota |
| `SALE_RETURNED` | "Devolución parcial de venta" | Devolución parcial; mismo detalle |
| `CARD_REVERSAL_DONE` | "Reversa de tarjeta realizada" | `MarkReversalDone` |
| `CREDIT_NOTE_REDEEMED` | "Nota de crédito usada" | Pago con nota en `ConfirmSale` |
| `RETURN_SETTINGS_CHANGED` | "Plazo de devoluciones modificado" | `SaveReturnsSettings` |
| `ADMIN_AUTHORIZATION_DENIED` (existente) | | Intento de autorización fallido, sin el PIN (FR-012) |

Entidad de la bitácora: `Sale` con el `Id` de la venta (como hoy) para que aparezca junto a ella.
