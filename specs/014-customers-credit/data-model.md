# Data Model: Gestión de clientes y crédito

**Funcionalidad**: `014-customers-credit` | **Fecha**: 2026-10-01 | **Plan**: [plan.md](plan.md)

Todos los importes se guardan como enteros en centavos. Las fechas se guardan en UTC y los
identificadores son GUID v7 generados en la aplicación (Principio IV). Los campos
`CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` los asigna el `AuditingInterceptor` existente.

## Entidades nuevas

### `Customer`: tabla `Customers` (`Pos.Domain/Customers`)

| Campo | Tipo (C# / SQLite) | Reglas |
|---|---|---|
| `Id` | `Guid` / TEXT PK | `Guid.CreateVersion7()` |
| `Name` | `string` / TEXT(120) | Obligatorio, recortado (FR-001, FR-002) |
| `Phone` | `string` / TEXT(30) | Obligatorio, recortado |
| `Email` | `string?` / TEXT(254) | Opcional; formato válido si se captura |
| `TaxId` | `string?` / TEXT(20) | RUC opcional; recortado y en mayúsculas; único si no es nulo |
| `CreditLimitCents` | `long` / INTEGER | ≥ 0 y ≤ `Money.MaxCents`. Un Cajero solo crea con 0 (FR-020) |
| `CreditMode` | enum / TEXT(10) | `CASH_ONLY` \| `CREDIT`. Un Cajero solo crea con `CASH_ONLY` |
| `IsActive` | `bool` / INTEGER | `true` al crear. Solo se desactiva con saldo 0 (FR-004) |
| `SearchText` | `string` / TEXT(200) | `TextNormalizer` de nombre + teléfono + RUC (research §10) |
| `CreatedAt/By`, `UpdatedAt/By` | auditoría | Interceptor |
| `DeletedAt` | `DateTime?` | Estándar del Principio IV; no se usa para desactivar |
| `Version` | `int` | Concurrencia optimista |

**Índices**:

- `IX_Customers_TaxId`: único, filtrado `TaxId IS NOT NULL`.
- `IX_Customers_Active_Search` sobre (`IsActive`, `SearchText`).

El saldo pendiente **no se guarda**: es Σ `Receivables.BalanceCents` con `Status = PENDING` del cliente
(FR-015).

### `Receivable`: tabla `Receivables` (`Pos.Domain/Receivables`)

Hay una por cada venta a crédito (research §3).

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | PK |
| `SaleId` | `Guid` | FK (Restrict) → `Sales`; **único** |
| `CustomerId` | `Guid` | FK (Restrict) → `Customers` |
| `CustomerName` | `string` / TEXT(120) | Copia del nombre al vender; es la que imprime el ticket (research §2) |
| `OriginalCents` | `long` | = total de la venta, > 0 |
| `BalanceCents` | `long` | 0 ≤ saldo ≤ `OriginalCents`. Invariante: `OriginalCents + Σ Entries.AmountCents` |
| `Status` | enum / TEXT(10) | `PENDING` \| `PAID` \| `CANCELLED` |
| `OverLimitAuthorizedBy` | `Guid?` | Administrador que autorizó el excedente (FR-007); nulo si no hubo |
| `CreatedAt/By`, `UpdatedAt/By`, `Version` | estándar | `CreatedAt` = fecha de la venta (origen del plazo) |

**Índices**:

- `IX_Receivables_SaleId`: único.
- `IX_Receivables_Customer_Status` sobre (`CustomerId`, `Status`, `CreatedAt`, `Id`), para el orden
  FIFO, el saldo y el reporte.

No tiene `DeletedAt`, porque nunca se borra (ver Complexity Tracking del plan).

### `ReceivableEntry`: tabla `ReceivableEntries` (inmutable)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id`, `ReceivableId` | `Guid` | PK y FK (Restrict) |
| `Type` | enum / TEXT(14) | Ver tabla de tipos |
| `AmountCents` | `long` | Con signo: negativo baja el saldo y positivo lo sube; ≠ 0 |
| `CustomerPaymentId` | `Guid?` | En `PAYMENT` y `PAYMENT_VOID` |
| `SaleReturnId` | `Guid?` | En `RETURN`, `EXCESS_OUT` y `EXCESS_IN` (devolución que lo originó) |
| `CreatedAt`, `CreatedBy` | auditoría | |

**Tipos de entrada**:

| Tipo | Signo | Origen |
|---|---|---|
| `PAYMENT` | − | Parte de un abono aplicada a esta cuenta (FR-011) |
| `PAYMENT_VOID` | + | Reverso exacto de un `PAYMENT` al anular el abono (FR-014) |
| `RETURN` | − | Monto devuelto o cancelado de esta venta (FR-016) |
| `EXCESS_OUT` | + | Abonado de más sobre lo devuelto; se libera de esta cuenta |
| `EXCESS_IN` | − | Ese excedente aplicado a otra cuenta pendiente del cliente (FIFO) |

**Índices**: `IX_ReceivableEntries_Receivable` sobre (`ReceivableId`, `CreatedAt`) e
`IX_ReceivableEntries_Payment` sobre (`CustomerPaymentId`).

### `CustomerPayment`: tabla `CustomerPayments` (abono; inmutable salvo la anulación)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | PK |
| `Number` | `long` | `MAX + 1` en la transacción; índice único. Folio `AB-000001` |
| `RequestId` | `Guid` | Clave de idempotencia de la interfaz; índice único (research §5) |
| `CustomerId` | `Guid` | FK (Restrict) |
| `AmountCents` | `long` | > 0 y ≤ saldo del cliente (FR-010) |
| `Method` | `PaymentMethod` / TEXT(10) | `CASH`, `CARD` o `TRANSFER` (FR-009) |
| `Reference` | `string?` / TEXT(50) | Opcional |
| `CashShiftId` | `Guid?` | FK (Restrict) → `CashShifts`. Obligatorio si Turnos tiene licencia |
| `BalanceBeforeCents`, `BalanceAfterCents` | `long` | Para el recibo (FR-013) |
| `Status` | enum / TEXT(8) | `ACTIVE` \| `VOIDED` |
| `VoidedAt`, `VoidedBy`, `VoidAuthorizedBy` | `DateTime?`, `Guid?`, `Guid?` | Solo al anular |
| `VoidReason` | `string?` / TEXT(250) | Obligatorio al anular |
| `VoidCashShiftId` | `Guid?` | Turno en que se anuló (corte y efectivo, research §6) |
| `CreatedAt`, `CreatedBy` | auditoría | |

**Índices**:

- `IX_CustomerPayments_Number`: único.
- `IX_CustomerPayments_RequestId`: único.
- `IX_CustomerPayments_Customer` sobre (`CustomerId`, `CreatedAt`).
- `IX_CustomerPayments_CashShiftId`.
- `IX_CustomerPayments_VoidCashShiftId`.

Única mutación permitida: `ACTIVE → VOIDED`, una sola vez (`CustomerPayment.Void`).
`RejectImmutableChanges` en `PosDbContext` rechaza cualquier otro cambio, como en 013.

## Configuración (no es tabla)

| Tipo | Campos | Almacén |
|---|---|---|
| `ReceivablesSettings` | `PaymentTermDays` = 30 (1–3650) | `IReceivablesSettingsStore` sobre `IPreferencesStore` (research §9) |

## Cambios a entidades existentes

| Entidad / tabla | Cambio | Notas |
|---|---|---|
| `PaymentMethod` | + `OnAccount` (código `ACCOUNT`) | Debe ser el único pago de su venta (research §1). `SalePayments` no cambia |
| `Sale.Register` | Regla: `ACCOUNT` es exclusivo | Sin cambios de columnas en `Sales` |
| `Checkout` | + `SetOnAccount()` | Reemplaza los demás pagos por un único `ACCOUNT` por el total |
| `RefundStatus` | + `Settled` (código `SETTLED`) | Parte de una devolución a crédito que redujo deuda (research §8) |
| `SaleReturnRefund.Create` | `OnAccount → Settled` | El efectivo excedente se crea como renglón `CASH` / `PAID` |
| `ShiftSalesTotals` | + `OnAccountSalesCents`, `CustomerPaymentsCashCents`, `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents`, `CustomerPaymentVoidsNonCashCents` (por defecto 0) | Research §6 |
| `CashShiftMath.ExpectedCash` | + abonos en efectivo − anulaciones en efectivo | SC-007 |
| `CashShift` / `CashShifts` | + las 5 columnas anteriores como `long?` | Instantánea al cerrar; nulas en turnos ya cerrados |
| `Permission` | + `ManageCustomers`, `SellOnCredit`, `RegisterCustomerPayments`, `ManageCustomerCredit`, `ApproveCreditOverLimit`, `VoidCustomerPayments`, `ViewReceivables` | Research §11 |
| `RolePermissions` | Cajero + `ManageCustomers`, `SellOnCredit`, `RegisterCustomerPayments`; autorizables + `ApproveCreditOverLimit`, `VoidCustomerPayments` | |
| `ModuleAccess` | Los 7 permisos → `CreditAndCustomers` | |

## Reglas de dominio

- **`Customer.Create(...)` / `Update(...)`**:
  - nombre y teléfono obligatorios;
  - límite ≥ 0;
  - email con formato básico;
  - RUC normalizado.
  - La unicidad del RUC la garantiza el índice y se traduce a `Duplicate(TaxId)`.
- **`Customer.ChangeCredit(limit, mode)`**: solo lo invoca un caso de uso con `ManageCustomerCredit`.
  Se permite un límite menor que el saldo.
- **`Customer.Deactivate(balanceCents)`**: rechaza si el saldo es > 0 (FR-004).
- **`Customer.CanBuyOnCredit`**: `IsActive && CreditMode == Credit` (Historia 2, escenario 4).
- **`CreditPolicy.Check(balance, limit, saleTotal)`**: devuelve `Within` si `balance + saleTotal ≤
  limit`; si no, `Exceeded(balance + saleTotal − limit)` (FR-006).
- **`PaymentAllocator.Allocate(amount, pending[])`**:
  - reparte de la cuenta más antigua a la más reciente;
  - cubre por completo una cuenta antes de pasar a la siguiente;
  - lanza si el monto es ≤ 0 o mayor que Σ saldos.
- **`Receivable`**:
  - `ApplyPayment(paymentId, amount)` agrega `PAYMENT`;
  - `RevertPayment(paymentId)` agrega `PAYMENT_VOID` por lo aplicado de ese abono;
  - `ApplyReturn(returnId, amount, out excess)` agrega `RETURN` y, si hace falta, `EXCESS_OUT`;
  - `ApplyExcess(returnId, amount)` agrega `EXCESS_IN`.
  - Cada método recalcula `BalanceCents` y `Status`, y rechaza saldos < 0 o > `OriginalCents`.
- **`CreditReturnSettlement.Settle(returned, own, otherPending[])`**: devuelve
  `(Excess, Reapplied[], CashRefundCents)` (research §8).
- **`CustomerPayment.Register(...)`**: monto > 0, método permitido y saldos anterior y nuevo
  coherentes (`after = before − amount`).
- **`CustomerPayment.Void(reason, userId, authorizedBy, shiftId, utcNow)`**: solo desde `ACTIVE` y con
  motivo obligatorio.
- **`ReceivableAging.DaysOverdue(saleLocalDate, todayLocal, termDays)`**: `max(0, hoy − (venta + plazo))`
  (FR-018).

## Transiciones

```text
Cuenta por cobrar:
  PENDING ──abonos o devolución dejan saldo 0──▶ PAID
  PAID ──anulación de abono──▶ PENDING
  PENDING | PAID ──venta cancelada o totalmente devuelta──▶ CANCELLED   (terminal)

Abono:
  ACTIVE ──Administrador + motivo + turno abierto──▶ VOIDED   (terminal)

Cliente:
  activo ⇄ inactivo   (desactivar solo con saldo 0; reactivar sin condición)
```

## Migración `CustomersAndCredit` (0.9.0)

- Crea `Customers`, `Receivables`, `ReceivableEntries` y `CustomerPayments` con sus índices. Las llaves
  foráneas solo van de las tablas nuevas hacia `Sales`, `Customers` y `CashShifts`.
- Agrega a `CashShifts` cinco columnas `long?` con `AddColumn`, sin reconstruir la tabla.
- No rellena datos: ninguna venta existente es a crédito.
