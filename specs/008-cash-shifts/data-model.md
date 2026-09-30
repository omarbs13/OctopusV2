# Data Model: Turnos de caja

**Funcionalidad**: `008-cash-shifts` | **Fecha**: 2026-09-30 | **Plan**: [plan.md](plan.md)

Todos los importes se guardan como enteros en centavos. Las fechas se guardan en UTC y los
identificadores son GUID v7 generados en la aplicación (Principio IV). Los campos
`CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` los asigna el `AuditingInterceptor` existente.

## Entidades nuevas (`Pos.Domain/CashShifts`)

### `CashShift` (agregado): tabla `CashShifts`

| Campo | Tipo (C# / SQLite) | Reglas |
|---|---|---|
| `Id` | `Guid` / TEXT PK | `Guid.CreateVersion7()` |
| `Number` | `long` / INTEGER | Consecutivo `MAX + 1` en la transacción; índice único `IX_CashShifts_Number`. Se muestra como `T-000123` (research §11) |
| `RegisterCode` | `string` / TEXT(20) | `CashRegister.Default = "CAJA-1"` (research §2) |
| `Status` | `CashShiftStatus` / TEXT(10) | `OPEN` \| `CLOSED` |
| `OpenedBy` | `Guid` / TEXT | Dueño del turno: el único que vende en él. Es igual a `CreatedBy`, pero explícito para las consultas |
| `OpenedAt` | `DateTime` / TEXT | UTC |
| `OpeningFloatCents` | `long` / INTEGER | 0 ≤ valor ≤ `Money.MaxCents`. El 0 requiere confirmación en el caso de uso (FR-003) |
| `ClosedAt` | `DateTime?` | Solo cuando está cerrado |
| `ClosedBy` | `Guid?` | Quien cerró: el dueño o un administrador (FR-024) |
| `SalesCount` | `int?` | Instantánea al cerrar: ventas completadas |
| `CancelledCount` | `int?` | Instantánea: ventas canceladas |
| `TotalSoldCents` | `long?` | Instantánea: suma de `TotalCents` de las completadas (FR-019a) |
| `CashSalesCents` | `long?` | Instantánea: efectivo aplicado de **todas** las ventas del turno |
| `CashCancelledCents` | `long?` | Instantánea: efectivo aplicado de las canceladas |
| `CardCents` | `long?` | Instantánea: tarjeta de las completadas |
| `TransferCents` | `long?` | Instantánea: transferencia de las completadas |
| `DepositsCents` | `long?` | Instantánea: suma de ingresos |
| `WithdrawalsCents` | `long?` | Instantánea: suma de retiros |
| `ExpectedCashCents` | `long?` | Instantánea: esperado (research §3) |
| `CountedCashCents` | `long?` | Conteo capturado, `Money` en el dominio |
| `DifferenceCents` | `long?` | Contado − esperado, **con signo** (research §4) |
| `ClosingComment` | `string?` / TEXT(250) | Obligatorio si `DifferenceCents ≠ 0` (FR-017) |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | auditoría | Interceptor |
| `DeletedAt` | `DateTime?` | Siempre nulo: los turnos no se borran. Existe por el Principio IV |
| `Version` | `int` | Token de concurrencia |
| `Movements` | `IReadOnlyList<CashMovement>` | Navegación, `DeleteBehavior.Restrict` |

Índices:

- `IX_CashShifts_Number` (único).
- `IX_CashShifts_OpenPerRegister` único sobre `RegisterCode`, **filtrado** con
  `WHERE "Status" = 'OPEN'` (SC-002).
- `IX_CashShifts_OpenedAt` sobre (`OpenedAt`, `Id`), para el listado.
- `IX_CashShifts_OpenedBy_OpenedAt`, para el filtro por usuario.

Métodos del dominio:

| Método | Reglas |
|---|---|
| `static Open(long number, Money openingFloat, Guid openedBy, DateTime utcNow)` | `number ≥ 1`. Queda `OPEN` |
| `RecordDeposit(Money amount, string reason, Guid userId)` → `CashMovement` | Solo `OPEN`. Monto > 0 y motivo obligatorio de 1 a 250 caracteres |
| `RecordWithdrawal(Money amount, string reason, long expectedCashCents, Guid userId, Guid? authorizedBy)` → `CashMovement` | Además de lo anterior, `amount ≤ expectedCashCents`. Si no, `InsufficientCashException(expectedCashCents)`: la decisión de mostrar el monto la toma Application |
| `Close(ShiftSalesTotals totals, Money counted, string? comment, Guid closedBy, DateTime utcNow)` | Solo `OPEN`. Calcula el esperado con `CashShiftMath`. Si la diferencia ≠ 0 y el comentario está vacío, lanza. Guarda la instantánea y queda `CLOSED` |
| `DepositsCents` / `WithdrawalsCents` | Sumas de `Movements` |
| `Difference` → `CashDifference?` | `Money Amount` y `DifferenceKind Kind` (`Balanced` \| `Over` \| `Short`) |

Transiciones de estado:

```text
          Open(...)                       Close(...)
(nada) ───────────────▶ OPEN ──────────────────────────▶ CLOSED (inmutable)
                         │  ▲
   RecordDeposit /       │  │
   RecordWithdrawal ─────┘  │
   (ventas y cancelaciones ─┘ se ligan por Sales.CashShiftId)
```

Un turno `CLOSED` no admite movimientos, ventas, cancelaciones ni un segundo cierre. Lo rechazan el
dominio y `PosDbContext` (research §10).

### `CashMovement` (parte de `CashShift`): tabla `CashMovements`

Es inmutable y no tiene `UpdatedAt`, `Version` ni `DeletedAt`, igual que `InventoryMovement`: es un
registro contable que nunca se modifica.

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` / TEXT PK | GUID v7 |
| `CashShiftId` | `Guid` / TEXT FK → `CashShifts.Id` | `Restrict` |
| `Sequence` | `int` | 1, 2, 3… dentro del turno. Único con `CashShiftId`. Folio `T-000123-02` |
| `Type` | `CashMovementType` / TEXT(10) | `IN` (ingreso) \| `OUT` (retiro) |
| `AmountCents` | `long` | > 0, ≤ `Money.MaxCents` |
| `Reason` | `string` / TEXT(250) | Obligatorio y recortado |
| `AuthorizedBy` | `Guid?` | Administrador que autorizó el retiro de un cajero (FR-011) |
| `CreatedAt`, `CreatedBy` | auditoría | `CreatedBy` es quien registró el movimiento |

Índice: `IX_CashMovements_Shift_Sequence`, único sobre (`CashShiftId`, `Sequence`).

### Tipos de valor y reglas puras

| Tipo | Contenido |
|---|---|
| `CashShiftStatus`, `CashMovementType` | Enums con `ToCode()` y `FromCode()`, como `SaleStatus` |
| `CashRegister` | `Default = "CAJA-1"` y `DisplayName = "Caja 1"` |
| `ShiftFolio` | `Format(long)` → `T-000123`; `FormatMovement(long, int)` → `T-000123-02` |
| `ShiftSalesTotals` (record) | `SalesCount`, `CancelledCount`, `TotalSoldCents`, `CashSalesCents`, `CashCancelledCents`, `CardCents`, `TransferCents`. Todo con ventas del turno; los importes, de pagos de ventas completadas, salvo el efectivo bruto y el cancelado |
| `CashShiftMath` | `ExpectedCash(openingFloat, totals, deposits, withdrawals)` = `float + CashSalesCents − CashCancelledCents + deposits − withdrawals` (FR-015). `CanRefund(expected, saleCashCents)` = `expected − saleCashCents ≥ 0` (FR-008) |
| `CashDifference` | `From(long countedCents, long expectedCents)` |

## Entidades existentes que cambian

### `Sale`: tabla `Sales`

| Cambio | Detalle |
|---|---|
| `CashShiftId` | `Guid?` / TEXT, **nula**, sin llave foránea (research §5). Nula en las ventas anteriores a 0.6.0 |
| `Sale.Register(...)` | Recibe `Guid cashShiftId` (obligatorio para ventas nuevas) |
| Índice | `IX_Sales_CashShiftId_Status` sobre (`CashShiftId`, `Status`) |
| Cajero | Sin cambio: es `CreatedBy` (007) |

### `Permission` y `RolePermissions` (`Pos.Domain/Users`)

- Se agregan al final `OperateShift`, `WithdrawCash` y `ManageShifts`.
- El cajero recibe `OperateShift`.
- `Authorizable` agrega `WithdrawCash` (research §7).

### Bitácora (`Pos.Application/Audit/AuditActions`)

| Código | Texto | Entidad | Detalles | `AuthorizedBy` |
|---|---|---|---|---|
| `SHIFT_OPENED` | Turno abierto | `CashShift` | `Turno T-000123. Fondo $500.00` | — |
| `CASH_DEPOSIT` | Ingreso de efectivo | `CashShift` | `Turno T-000123-02. $200.00. Motivo: …` | — |
| `CASH_WITHDRAWAL` | Retiro de efectivo | `CashShift` | `Turno T-000123-03. $1,000.00. Motivo: …` | Administrador, si lo hubo |
| `SHIFT_CASH_COUNTED` | Conteo de caja | `CashShift` | `Turno T-000123. Contado $4,480.00` | — |
| `SHIFT_CLOSED` | Turno cerrado | `CashShift` | `Turno T-000123. Esperado …, contado …, diferencia …` | — |
| `SHIFT_CLOSED_BY_ADMIN` | Turno cerrado por administrador | `CashShift` | Igual, más `Dueño: {nombre}` | — |
| `HELD_SALE_DISCARDED` (existente) | Venta conservada descartada | `User` | `Al cerrar el turno T-000123` | — |

`SHIFT_CLOSED_BY_ADMIN` **sustituye** a `SHIFT_CLOSED` cuando `ClosedBy ≠ OpenedBy`: un cierre
produce una sola entrada de cierre.

## Relaciones

```text
Users 1 ──── * CashShifts (OpenedBy, ClosedBy; sin FK, como toda la auditoría de 007)
CashShifts 1 ──── * CashMovements (FK Restrict)
CashShifts 1 ──── * Sales (Sales.CashShiftId nula, sin FK)
Users 1 ──── 0..1 SaleDrafts (existente; la "venta en curso" del dueño del turno)
```

## Migración `CashShifts`

| Paso | SQL esperado | Reconstruye |
|---|---|---|
| Crear `CashShifts` e índices (incluye el filtrado) | `CREATE TABLE`, `CREATE UNIQUE INDEX … WHERE "Status" = 'OPEN'` | No |
| Crear `CashMovements` e índice | `CREATE TABLE` con FK | No |
| `Sales.CashShiftId` | `ALTER TABLE "Sales" ADD "CashShiftId" TEXT NULL` | **No** |
| `IX_Sales_CashShiftId_Status` | `CREATE INDEX` | No |

No actualiza datos. Antes de crearla se sube `Version` a 0.6.0 y se genera `v0.6.0.db`
(research §15). `CashShiftsMigrationTests` falla si el SQL contiene `DROP TABLE` o `ef_temp_`.
