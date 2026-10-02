# Data Model: Corte X y Corte Z

Importes en centavos (`long`), fechas en UTC. Referencias a [research.md](research.md).

## ShiftCut (nuevo agregado, `Pos.Domain/CashShifts/ShiftCut.cs`)

Registro inmutable de un Corte X o Z con la copia fija de sus cifras.

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | `Guid.CreateVersion7()` |
| `Type` | `ShiftCutType` (`Readout` = `X`, `Closing` = `Z`) | Se guarda como texto `X` / `Z` |
| `Number` | `long` | ≥ 1; consecutivo por tipo (research §3) |
| `ShiftId` | `Guid` | Turno del corte; FK a `CashShifts.Id`, `Restrict` |
| `ShiftNumber` | `long` | Número del turno al generar (folio `T-000123` sin unir tablas) |
| `RegisterCode` | `string(20)` | `CashRegister.Default` |
| `ShiftOpenedBy` | `Guid` | Dueño del turno |
| `ShiftOpenedAt` | `DateTime` | Apertura del turno |
| `GeneratedAt` | `DateTime` | Momento del corte (UTC) |
| `GeneratedBy` | `Guid` | Quien lo generó (en Z, quien cerró) |
| `AuthorizedBy` | `Guid?` | Administrador que autorizó a un Cajero (solo X) |
| `OpeningFloatCents` | `long` | Fondo inicial |
| `SalesCount`, `CancelledCount` | `int` | De `ShiftSalesTotals` |
| `TotalSoldCents`, `CashSalesCents`, `CashCancelledCents`, `CardCents`, `TransferCents` | `long` | De `ShiftSalesTotals` |
| `CashRefundsCents`, `NonCashRefundsCents`, `CreditNotesIssuedCents` | `long` | 013 |
| `OnAccountSalesCents`, `CustomerPaymentsCashCents`, `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents`, `CustomerPaymentVoidsNonCashCents` | `long` | 014 |
| `DepositsCents`, `WithdrawalsCents` | `long` | Suma de movimientos del turno al generar |
| `ExpectedCashCents` | `long` | `CashShiftMath.ExpectedCash` |
| `CountedCashCents` | `long?` | Solo Z; nulo en X (FR-004a) |
| `DifferenceCents` | `long?` | Solo Z; contado − esperado, con signo |
| `Comment` | `string(250)?` | Solo Z; comentario del arqueo |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | | Los asigna la persistencia |
| `DeletedAt` | `DateTime?` | Siempre nulo (Principio IV; research §9) |
| `Version` | `int` | Token de concurrencia; siempre 1 |

Derivados (no se guardan): `Folio` (`ShiftCutFolio.Format(Type, Number)` → `X-000001`),
`ShiftFolio` (`ShiftFolio.Format(ShiftNumber)`), `Difference` (`CashDifference?`).

### Fábricas y validaciones

- `ShiftCut.Readout(long number, CashShift shift, ShiftSalesTotals totals, Guid generatedBy, Guid? authorizedBy, DateTime utcNow)`
  - El turno debe estar `Open` (`DomainException` "El turno ya está cerrado.").
  - `number ≥ 1`, `generatedBy ≠ Guid.Empty`.
  - Esperado = `shift.ExpectedCash(totals)`; ingresos y retiros desde `shift`. **No modifica `shift`.**
- `ShiftCut.Closing(long number, CashShift shift, Guid closedBy)`
  - El turno debe estar `Closed` con su instantánea (`ClosedAt`, `CountedCashCents` no nulos): copia
    las cifras del turno ya cerrado, así ambas fuentes son idénticas (research §2).
  - `GeneratedAt = shift.ClosedAt`.
- Sin métodos de modificación (FR-013).

### Índices

| Nombre | Columnas | Tipo |
|---|---|---|
| `IX_ShiftCuts_Type_Number` | `Type, Number` | Único (FR-010) |
| `IX_ShiftCuts_ClosingPerShift` | `ShiftId` filtro `"Type" = 'Z'` | Único (un Z por turno) |
| `IX_ShiftCuts_ShiftId` | `ShiftId` | Normal (FK, Cortes X del turno) |
| `IX_ShiftCuts_GeneratedAt` | `GeneratedAt, Id` | Histórico ordenado |
| `IX_ShiftCuts_GeneratedBy_GeneratedAt` | `GeneratedBy, GeneratedAt` | Filtro por usuario |

## ShiftCutType (nuevo enum)

`Readout` (código `X`), `Closing` (código `Z`). Extensiones `ToCode()` / `FromCode()` como
`CashShiftStatus`.

## ShiftCutFolio (nuevo, `Pos.Domain/CashShifts/ShiftCutFolio.cs`)

`Format(ShiftCutType type, long number)` → `X-000001` / `Z-000001` (cultura invariante, 6 dígitos;
crece sin truncar).

## Relaciones

```text
CashShift 1 ──── 0..* ShiftCut (Type = X)
CashShift 1 ──── 0..1 ShiftCut (Type = Z)   # 1 si se cerró desde 0.12.0; 0 si antes (FR-010a)
```

## Cambios a entidades y DTO existentes

| Elemento | Cambio |
|---|---|
| `CashShift` | Sin cambios de esquema ni de comportamiento |
| `Permission` | + `GenerateShiftReadout` (Administrador; autorizable; módulo `CashShifts`) |
| `RolePermissions.Authorizable` | + `GenerateShiftReadout` |
| `ModuleAccess.Required` | `GenerateShiftReadout` → `LicensedModule.CashShifts` |
| `ClosedShift` | + `CutId`, `CutFolio` |
| `ShiftReportDto`, `ShiftDetailDto` | + `CutFolio` (`string?`, nulo en turnos anteriores) |
| `AuditActions` | + `ShiftReadoutGenerated`, `ShiftCutReprinted`, `ShiftCutEntity`; textos de `ShiftClosed*` con "(Corte Z)" |
| `PrintSource` | + `ShiftCut(Guid cutId)` |

## Ciclo de vida

```text
Turno Open ──(Corte X)*──► Turno Open (sin cambios; nueva fila ShiftCut X)
Turno Open ──(Corte Z = CloseShift)──► Turno Closed + fila ShiftCut Z   [misma transacción]
ShiftCut: creado → inmutable (no hay transiciones)
```

## Migración `ShiftCuts` (0.12.0)

- `CREATE TABLE "ShiftCuts"` con FK a `CashShifts` y los índices de arriba.
- Sin reconstrucción de tablas; no inserta filas (sin Z retroactivos, FR-010a).
- Base de ejemplo `v0.12.0.db` con al menos un turno cerrado, uno abierto, un Corte X y un Corte Z.
