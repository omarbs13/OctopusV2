# Data Model: Devoluciones y cancelaciones

**Funcionalidad**: `013-returns-cancellations` | **Fecha**: 2026-09-30 | **Plan**: [plan.md](plan.md)

Todos los importes se guardan como enteros en centavos y las cantidades en milésimas. Las fechas se
guardan en UTC y los identificadores son GUID v7 generados en la aplicación (Principio IV). Los
campos `CreatedAt`/`CreatedBy` los asigna el `AuditingInterceptor` existente.

## Entidades nuevas (`Pos.Domain/Returns` y `Pos.Domain/CreditNotes`)

### `SaleReturn`: tabla `SaleReturns` (inmutable)

| Campo | Tipo (C# / SQLite) | Reglas |
|---|---|---|
| `Id` | `Guid` / TEXT PK | `Guid.CreateVersion7()` |
| `Number` | `long` / INTEGER | `MAX + 1` en la transacción; índice único. Se muestra `D-000001` |
| `SaleId` | `Guid` / TEXT | Venta de origen. FK (Restrict) de la tabla nueva hacia `Sales`; no modifica `Sales` |
| `Kind` | enum / TEXT(12) | `CANCELLATION` \| `PARTIAL` |
| `Reason` | `string` / TEXT(250) | Obligatorio, recortado, ≤ 250 (FR-001) |
| `AuthorizedBy` | `Guid` / TEXT | Administrador que autorizó (puede ser quien opera, §7) |
| `TotalCents` | `long` / INTEGER | > 0; suma de las líneas |
| `Compensation` | enum / TEXT(12) | `REFUND` \| `CREDIT_NOTE` |
| `CashShiftId` | `Guid?` / TEXT | Turno abierto en que se hizo; nulo si el módulo Turnos no tiene licencia (§6) |
| `CreditNoteId` | `Guid?` / TEXT | Nota emitida, si la compensación es `CREDIT_NOTE` |
| `CreatedAt`, `CreatedBy` | auditoría | Interceptor. `CreatedBy` = quien operó |
| `Lines` | `IReadOnlyList<SaleReturnLine>` | ≥ 1 |
| `Refunds` | `IReadOnlyList<SaleReturnRefund>` | Solo con `REFUND`; su suma = `TotalCents` |

Índices: `IX_SaleReturns_Number` (único), `IX_SaleReturns_SaleId`, `IX_SaleReturns_CashShiftId`.

Desviación del Principio IV (igual que `InventoryMovement` y `CashMovement`): sin `DeletedAt`,
`UpdatedAt/By` ni `Version`, porque el registro es inmutable y no se borra (Complexity Tracking).

### `SaleReturnLine`: tabla `SaleReturnLines`

| Campo | Tipo | Reglas |
|---|---|---|
| `Id`, `SaleReturnId` | `Guid` | PK y FK (Restrict) |
| `SaleLineId` | `Guid` | Línea original; única por devolución |
| `QuantityThousandths` | `long` | > 0 y ≤ disponible de la línea |
| `AmountCents` | `long` | Acumulado nuevo − acumulado previo (research §3) |
| `ReturnMovementId` | `Guid?` | Movimiento `SALE_CANCEL`/`SALE_RETURN`; nulo si el producto no controlaba inventario o el módulo Inventario no tiene licencia |

### `SaleReturnRefund`: tabla `SaleReturnRefunds`

| Campo | Tipo | Reglas |
|---|---|---|
| `Id`, `SaleReturnId` | `Guid` | PK y FK |
| `SalePaymentId` | `Guid` | Pago de la venta al que corresponde |
| `Method` | `PaymentMethod` / TEXT(10) | `CASH`, `CARD`, `TRANSFER`, `CREDIT` |
| `AmountCents` | `long` | > 0 |
| `Status` | enum / TEXT(16) | `PAID` (efectivo), `PENDING_REVERSAL` (tarjeta, transferencia), `REVERSED`, `RESTORED` (nota) |
| `ReversedAt`, `ReversedBy` | `DateTime?`, `Guid?` | Solo al pasar a `REVERSED` |

Única mutación permitida de todo el módulo: `PENDING_REVERSAL → REVERSED`, una sola vez
(`SaleReturnRefund.MarkReversed`). `RejectImmutableChanges` en `PosDbContext` rechaza cualquier otro
cambio. Índice `IX_SaleReturnRefunds_Pending` sobre (`Status`, `Id`) filtrado por
`Status = 'PENDING_REVERSAL'`, para la lista del Administrador.

### `CreditNote`: tabla `CreditNotes` (inmutable)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | PK |
| `Number` | `long` | `MAX + 1`; índice único. Folio `NC-000001` |
| `InitialCents` | `long` | > 0; igual al total devuelto |
| `SaleReturnId` | `Guid` | Devolución que la originó |
| `CreatedAt`, `CreatedBy` | auditoría | |

### `CreditNoteMovement`: tabla `CreditNoteMovements` (inmutable)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id`, `CreditNoteId` | `Guid` | PK y FK (Restrict) |
| `Sequence` | `int` | Consecutivo por nota; índice único (`CreditNoteId`, `Sequence`) |
| `Type` | enum / TEXT(8) | `ISSUE` \| `REDEEM` \| `RESTORE` |
| `AmountCents` | `long` | > 0 |
| `SaleId` | `Guid?` | Venta pagada (`REDEEM`) o devuelta (`RESTORE`) |
| `SaleReturnId` | `Guid?` | Devolución que emite o restaura |
| `CreatedAt`, `CreatedBy` | auditoría | |

**Saldo** = Σ`ISSUE` + Σ`RESTORE` − Σ`REDEEM`. Nunca es negativo: `CreditNote.Redeem` lo valida con
el saldo recién calculado en la misma transacción (research §5).

## Cambios a entidades existentes

| Entidad / tabla | Cambio | Notas |
|---|---|---|
| `Sale` / `Sales` | + `ReturnedCents` (`long`, `NOT NULL DEFAULT 0`) | Lo actualiza `Sale.ApplyReturn`. Estado derivado: ninguna / parcial / total (research §2) |
| `SaleLine` / `SaleLines` | + `ReturnedQuantity` (`long`, `NOT NULL DEFAULT 0`) | Milésimas devueltas acumuladas. Disponible = `QuantityThousandths − ReturnedQuantity` |
| `SalePayment` / `SalePayments` | + `CreditNoteId` (`Guid?`) | Solo con `Method = CREDIT`; sin llave foránea |
| `PaymentMethod` | + `CreditNote` (código `CREDIT`) | Una venta admite a lo más un pago de este tipo |
| `MovementType` | + `SaleReturn` (código `SALE_RETURN`) | Aumenta existencia |
| `CashShift` / `CashShifts` | + `CashRefundsCents`, `NonCashRefundsCents`, `CreditNotesIssuedCents` (`long?`) | Instantánea al cerrar (FR-016). Nulos en turnos ya cerrados |
| `ShiftSalesTotals` | + `CashRefundsCents`, `NonCashRefundsCents`, `CreditNotesIssuedCents`; `TotalSoldCents` neto de devoluciones | Research §6 |
| `Permission` | + `ProcessReturns`, `ApproveReturns`, `ManageCreditNotes` | `ModuleAccess` → Devoluciones; `ApproveReturns` autorizable |
| `ReturnsSettings` | Nuevo, en preferencias (no es tabla) | `ReturnWindowDays` = 30 |

## Reglas de dominio

- `Sale.ApplyReturn(lines)`: la venta debe estar `COMPLETED`; por línea, cantidad > 0 y ≤
  disponible; actualiza `ReturnedQuantity` y `ReturnedCents`. Una **cancelación completa** exige
  `ReturnedCents = 0`; marca `CANCELLED` con el motivo (`Sale.Cancel` existente) y vincula los
  movimientos.
- `ReturnMath.LineRefund(importeLínea, cantidadVendida, devueltoPrevio, cantidadAhora)`: acumulado
  exacto (research §3). `ReturnMath.Allocate(total, pagosRestantes)`: resto mayor con topes.
- `SaleReturn.Create(...)`: motivo obligatorio; ≥ 1 línea; suma de líneas = total; con `REFUND` la
  suma de reintegros = total; con `CREDIT_NOTE` no hay reintegros y sí nota.
- `CreditNote.Issue(...)` y `CreditNote.Redeem(monto, saldoActual)`: monto > 0 y ≤ saldo.
- `CashShiftMath.ExpectedCash(...)`: resta `cashRefundsCents` (research §6).

## Transiciones

```text
Venta:  COMPLETED ──cancelación (ReturnedCents = 0)──▶ CANCELLED          (terminal)
        COMPLETED ──devolución parcial──▶ COMPLETED (ReturnedCents ↑) ──todo devuelto──▶ "totalmente devuelta" (derivado)
Reintegro tarjeta/transferencia:  PENDING_REVERSAL ──Administrador──▶ REVERSED   (terminal)
```

## Migración `ReturnsAndCreditNotes` (0.8.0)

- Crea `SaleReturns`, `SaleReturnLines`, `SaleReturnRefunds`, `CreditNotes`, `CreditNoteMovements`
  con sus índices.
- Agrega columnas (`AddColumn`, sin reconstruir tablas): `Sales.ReturnedCents`,
  `SaleLines.ReturnedQuantity` (ambas `DEFAULT 0`), `SalePayments.CreditNoteId`,
  `CashShifts.CashRefundsCents`, `CashShifts.NonCashRefundsCents`,
  `CashShifts.CreditNotesIssuedCents` (nulas).
- No hay relleno de datos: las ventas ya canceladas siguen sin `SaleReturn` y conservan la regla
  heredada del efectivo (research §6).
