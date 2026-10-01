# Contrato: casos de uso y puertos de Application

Esta es la interfaz que Desktop consume e Infrastructure implementa. Las firmas son orientativas y los
nombres exactos se fijan al implementar.

- Todos los casos de uso devuelven `Result` o `Result<T>` y reservan las excepciones para fallas
  inesperadas (Principio III).
- Cada escritura es una sola transacción `BEGIN IMMEDIATE` e incluye su entrada de bitácora (FR-019).
- Todos los casos de uso nuevos verifican primero la licencia `CreditAndCustomers` y devuelven
  `ModuleNotLicensed` si falta (FR-020).

## Errores nuevos (`Abstractions/Error.cs`)

| Error | Significado | Mensaje al operador |
|---|---|---|
| `CreditLimitExceeded(long ExcessCents)` | Saldo + venta > límite y sin concesión válida (FR-006) | "La venta excede el límite de crédito del cliente por {excedente}. Se requiere autorización de un Administrador" |
| `CustomerNotEligibleForCredit` | El cliente está inactivo o es "solo efectivo" (Historia 2, escenario 4) | "Este cliente no tiene crédito disponible" |
| `CustomerHasBalance(long BalanceCents)` | Se intenta desactivar con saldo (FR-004) | "El cliente tiene un saldo pendiente de {saldo}; no se puede desactivar" |
| `PaymentExceedsBalance(long MaxCents)` | El abono es ≤ 0 o mayor que el saldo (FR-010) | "El monto debe ser mayor que 0 y no exceder el saldo de {saldo}" |

Se reutilizan:

- `ValidationFailed` (nombre, teléfono, email, límite, motivo) y `Duplicate(TaxId)` (RUC repetido).
- `NotFound`, `Conflict`, `InvalidState` (abono ya anulado, o anulación imposible por una devolución
  posterior, research §7).
- `Forbidden(permiso, CanBeAuthorized)`, `ModuleNotLicensed` y `ShiftRequired`.
- `ShiftOwnedByOther` e `InsufficientCash(null)` (sin revelar montos).
- `AlreadyRegistered` en `ConfirmSale`.

## Puertos nuevos o ampliados

| Puerto | Miembros | Implementación |
|---|---|---|
| `ICustomerRepository` (nuevo, `Application/Customers`) | `GetAsync(id)`; `Add(customer)`; `SearchAsync(CustomerSearch)` → página; `FindForSaleAsync(text)` (solo activos con crédito); `GetBalanceAsync(id)`; `GetBalancesAsync(ids)`; `SaveChangesAsync()` → `SaveOutcome` (RUC duplicado → `DuplicateField = TaxId`) | `Infrastructure/Customers/CustomerRepository` |
| `IReceivableRepository` (nuevo, `Application/Receivables`) | `GetBySaleAsync(saleId)`; `GetPendingAsync(customerId)` en orden FIFO; `GetManyAsync(ids)`; `Add(receivable)`; `GetEntriesByPaymentAsync(paymentId)`; `HasReturnAfterAsync(receivableIds, utc)`; `ListByCustomerAsync(customerId, página)` | `Infrastructure/Receivables/ReceivableRepository` |
| `ICustomerPaymentRepository` (nuevo) | `NextNumberAsync()`; `FindByRequestAsync(requestId)`; `GetAsync(id)`; `Add(payment)`; `ListByCustomerAsync(customerId, página)`; `SaveChangesAsync()` | `Infrastructure/Receivables/CustomerPaymentRepository` |
| `IReceivablesReportReader` (nuevo, `Application/Reports`) | `GetAsync(ReceivablesFilter, todayLocal, termDays)` → filas + totales | `Infrastructure/Reports/ReceivablesReportReader` |
| `IReceivablesSettingsStore` (nuevo) | `Load()` / `Save(ReceivablesSettings)` | `Infrastructure/Receivables/PreferencesReceivablesSettingsStore` |
| `ISaleRepository` (existente) | `GetShiftTotalsAsync` + los 5 acumulados de research §6. `GetDetailAsync` agrega `CreditInfo { CustomerId, CustomerName, BalanceCents, Status }` si la venta es a crédito. `SaleSearch` + filtro opcional `CustomerId` | `SaleRepository` |
| `IAuditLog`, `IAuthorizationGrants`, `ICashShiftRepository` | Sin cambios | — |

## Casos de uso nuevos

### Clientes (`Pos.Application/Customers/…`)

| Caso de uso | Permiso | Entrada | Salida | Reglas |
|---|---|---|---|---|
| `CreateCustomer` | `ManageCustomers` | `Name`, `Phone`, `Email?`, `TaxId?`, `CreditLimitCents?`, `CreditMode?` | `Result<Guid>` | El email se valida con `Customer.IsValidEmail`. Sin `ManageCustomerCredit` se ignoran límite y modalidad y se crea `CASH_ONLY` / 0 (FR-020). Audita `CUSTOMER_CREATED` |
| `UpdateCustomer` | `ManageCustomers` | `Id`, `ExpectedVersion`, datos; `CreditLimitCents?`, `CreditMode?` | `Result` | Cambiar límite o modalidad sin `ManageCustomerCredit` → `Forbidden`. Audita `CUSTOMER_UPDATED`; si cambió el crédito, `CUSTOMER_CREDIT_CHANGED` con valores anterior y nuevo |
| `SetCustomerActive` | `ManageCustomerCredit` | `Id`, `ExpectedVersion`, `Active` | `Result` | Desactivar exige saldo 0 dentro de la transacción (`CustomerHasBalance`). Audita `CUSTOMER_DEACTIVATED` / `CUSTOMER_ACTIVATED` |
| `SearchCustomers` | `ManageCustomers` | `Text?`, `IncludeInactive`, página (100) | `CustomerPage { Id, Name, Phone, TaxId, CreditMode, LimitCents, BalanceCents, IsActive }` | Busca por nombre, teléfono o RUC (FR-003) |
| `GetCustomer` | `ManageCustomers` | `Id` | `CustomerDetailDto` + saldo, disponible, días vencido | `NotFound` |
| `FindCustomersForSale` | `SellOnCredit` | `Text` | ≤ 20 `{ Id, Name, Phone, TaxId }` | Solo activos y `CREDIT` |
| `GetCustomerCreditStatus` | `SellOnCredit` | `CustomerId`, `SaleTotalCents` | `{ BalanceCents, LimitCents, AvailableCents, WouldExceedByCents, HasOverdue }` | Solo lectura; la interfaz muestra el aviso de vencido y de excedente sin calcular (Principio III) |

### Cuentas por cobrar y abonos (`Pos.Application/Receivables/…`)

| Caso de uso | Permiso | Entrada | Salida | Reglas |
|---|---|---|---|---|
| `RegisterCustomerPayment` | `RegisterCustomerPayments` | `RequestId`, `CustomerId`, `AmountCents`, `Method`, `Reference?` | `Result<PaymentReceipt { PaymentId, Folio, BalanceBeforeCents, BalanceAfterCents, PaidReceivables }>` | Orden de research §5. Mismo `RequestId` → devuelve el abono existente. Audita `CUSTOMER_PAYMENT_REGISTERED` |
| `VoidCustomerPayment` | `RegisterCustomerPayments` + concesión `VoidCustomerPayments` (siempre) | `PaymentId`, `Reason`, `AuthorizationGrantId` | `Result` | Orden: licencia → permiso → motivo → abono `ACTIVE` → sin devolución posterior en sus cuentas → turno abierto (sin licencia de Turnos se omiten este paso y el siguiente) → efectivo suficiente si fue en efectivo → **consumir la concesión** → `PAYMENT_VOID` + estados → `Void` → bitácora → guardar. Audita `CUSTOMER_PAYMENT_VOIDED` (quién, autorizador, motivo, folio, monto) |
| `ListCustomerPayments` | `ManageCustomers` | `CustomerId`, página | `{ PaymentId, Folio, CreatedAtUtc, AmountCents, Method, Reference, Status, VoidReason? }` | Historial con anulados visibles |
| `ListCustomerReceivables` | `ManageCustomers` | `CustomerId`, `OnlyPending`, página | `{ SaleId, SaleFolio, SaleDateUtc, OriginalCents, BalanceCents, Status, DaysOverdue }` | |
| `GetReceivablesSettings` / `SaveReceivablesSettings` | `ManageCustomerCredit` | `PaymentTermDays` (1–3650) | `ReceivablesSettings` | Audita `CREDIT_SETTINGS_CHANGED` |

### Reporte (`Pos.Application/Reports/GetReceivablesReport`)

| Caso de uso | Permiso | Entrada | Salida |
|---|---|---|---|
| `GetReceivablesReport` | `ViewReceivables` | `Status?` (`Current`, `Overdue`, `AtLimit`), `Text?` | `ReceivablesReport { Rows[{ CustomerId, Name, BalanceCents, LimitCents, LastPaymentAtUtc?, DaysOverdue, IsOverdue, IsAtLimit }], TotalBalanceCents, CustomerCount }`. Los totales se calculan sobre las filas devueltas (research §13) |

## Casos de uso existentes que cambian

| Caso de uso | Cambio |
|---|---|
| `ConfirmSale` | `ConfirmSaleCommand` gana `CustomerId?` y `OverLimitGrantId?`. Un pago `ACCOUNT` exige: módulo activo, `SellOnCredit`, `CustomerId`, `Customer.CanBuyOnCredit` y ser el único pago. **Dentro de la transacción** lee el saldo y aplica `CreditPolicy`. Si se excede: el Administrador pasa directo; un Cajero necesita la concesión de `ApproveCreditOverLimit` (consumida al final de las validaciones); sin ella → `CreditLimitExceeded`. Crea `Receivable` y audita `CREDIT_SALE_REGISTERED` y, si hubo excedente, `CREDIT_LIMIT_OVERRIDE` (cajero, autorizador, cliente, venta, saldo previo, límite, monto; FR-007) |
| `SaleReturnProcessor` (`CancelSale`, `ReturnSaleItems`) | Si la venta tiene pago `ACCOUNT`: rechaza la compensación `CREDIT_NOTE`, aplica `CreditReturnSettlement` (research §8), pasa al `ReturnCashGate` solo el reintegro en efectivo calculado y crea los renglones `ACCOUNT`/`SETTLED` y `CASH`/`PAID` |
| `PreviewReturn` | `ReturnPreview` gana `CreditSettlement? { ReducesBalanceCents, ReappliedCents, CashRefundCents }` |
| `CancelSale` (ruta sin licencia de Devoluciones) | Una venta a crédito también ajusta su cuenta con `CreditReturnSettlement` |
| `GetSale` / `SearchSales` | Detalle con `CreditInfo`; la lista muestra "Pendiente de pago" / "Pagada" en las ventas a crédito |
| `PrintTicket` | `PrintSource.CustomerPaymentSource(paymentId)` (recibo); el ticket de venta a crédito imprime cliente y "A crédito" (research §12) |
| `GetMyShiftSummary`, `GetShiftDetail`, `CountShiftCash`, `CloseShift` | Leen el `ShiftSalesTotals` ampliado; el esperado incluye abonos y anulaciones en efectivo; la instantánea guarda las 5 columnas; el corte imprime el bloque "Crédito" (FR-012) |
| Reportes 009 | `ACCOUNT` aparece con la etiqueta "A crédito" en el desglose por forma de pago; el total vendido la incluye |

## Catálogo de bitácora (`AuditActions`)

| Código | Texto | Entidad |
|---|---|---|
| `CUSTOMER_CREATED` | "Cliente creado" | `Customer` |
| `CUSTOMER_UPDATED` | "Cliente modificado" | `Customer` |
| `CUSTOMER_CREDIT_CHANGED` | "Límite o modalidad de crédito modificados" | `Customer` |
| `CUSTOMER_DEACTIVATED` / `CUSTOMER_ACTIVATED` | "Cliente desactivado" / "Cliente activado" | `Customer` |
| `CREDIT_SALE_REGISTERED` | "Venta a crédito" | `Sale` |
| `CREDIT_LIMIT_OVERRIDE` | "Venta a crédito sobre el límite autorizada" | `Sale` |
| `CUSTOMER_PAYMENT_REGISTERED` | "Abono registrado" | `CustomerPayment` |
| `CUSTOMER_PAYMENT_VOIDED` | "Abono anulado" | `CustomerPayment` |
| `CREDIT_SETTINGS_CHANGED` | "Plazo de pago modificado" | `ReceivablesSettings` |
| `ADMIN_AUTHORIZATION_DENIED` (existente) | | Intento fallido, sin la contraseña (FR-007, FR-014) |
