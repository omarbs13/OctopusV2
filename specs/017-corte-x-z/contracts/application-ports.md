# Contrato: casos de uso y puertos de Application

Todos los casos de uso devuelven `Result<T>`; las excepciones se reservan para fallas inesperadas.
Errores existentes de `Pos.Application/Abstractions/Error.cs`; no se agrega ningún tipo de error.

## GenerateShiftReadout (Corte X) — nuevo

`Pos.Application/CashShifts/GenerateShiftReadout/`

```csharp
public sealed record GenerateShiftReadoutCommand(Guid? AuthorizationGrantId = null);

public sealed record GeneratedShiftCut(Guid CutId, string Folio, string ShiftFolio);

Task<Result<GeneratedShiftCut>> HandleAsync(GenerateShiftReadoutCommand command, CancellationToken ct);
```

| Orden | Paso | Error |
|---|---|---|
| 1 | `CheckAsync(GenerateShiftReadout, AuthorizationGrantId)` | `ModuleNotLicensed`, `Forbidden(CanBeAuthorized: true)` |
| 2 | `BeginAsync` (escritura) | |
| 3 | `GetOpenAsync(CashRegister.Default)` (no se modifica) | `ShiftRequired` |
| 4 | `GetShiftTotalsAsync(shift.Id)` | |
| 5 | `NextCutNumberAsync(Readout)`; `ShiftCut.Readout(...)`; `AddCut` | |
| 6 | Bitácora `SHIFT_READOUT_GENERATED`, entidad `ShiftCut`, resumen "Corte X X-000001. Turno T-000123. Total vendido $…", `authorizedBy` | |
| 7 | `SaveChangesAsync`; `Commit` | `Conflict` (duplicado de folio) |

- No modifica el turno: su `Version` y sus cifras quedan iguales (FR-003).
- Log: `Corte X generado. CutId Folio ShiftId UserId AuthorizedBy`.

## CloseShift (Corte Z) — cambia

`Pos.Application/CashShifts/CloseShift/CloseShiftHandler.cs`

- Comando sin cambios.
- Después de `shift.Close(...)` y antes de `SaveChangesAsync`:
  `NextCutNumberAsync(Closing)` → `ShiftCut.Closing(number, shift, currentUser)` → `AddCut`.
- Bitácora: la misma entrada `SHIFT_CLOSED` / `SHIFT_CLOSED_BY_ADMIN` con resumen que empieza por
  "Corte Z Z-000001. Turno T-000123. …".
- Resultado: `ClosedShift(Guid ShiftId, string Folio, Guid CutId, string CutFolio)`.
- Cualquier rechazo (`ShiftChanged`, `Conflict`, comentario, `ShiftClosed`) ocurre antes del
  `Commit`: no queda turno cerrado ni folio Z consumido.

## GetShiftCut — nuevo

`Pos.Application/CashShifts/GetShiftCut/`

```csharp
public sealed record GetShiftCutQuery(Guid CutId);
Task<Result<ShiftCutReportDto>> HandleAsync(GetShiftCutQuery query, CancellationToken ct);
```

- Acceso: quien lo generó, o `ManageShifts`. Si no: `Forbidden`. No existe: `NotFound`.

## SearchShiftCuts — nuevo

`Pos.Application/CashShifts/SearchShiftCuts/`

```csharp
public sealed record SearchShiftCutsQuery(
    ShiftCutType? Type,
    DateOnly? From,          // fecha local de generación, inclusive
    DateOnly? To,            // fecha local, inclusive (se convierte a límite UTC exclusivo)
    Guid? UserId,            // quien generó
    int Page = 1);

Task<Result<ShiftCutPage>> HandleAsync(SearchShiftCutsQuery query, CancellationToken ct);
```

- Permiso `ManageShifts`. `From > To` → `ValidationFailed`.
- Orden `GeneratedAt DESC, Id DESC`; `PageSize = ShiftCutPage.DefaultPageSize` (100).

## DTO

```csharp
public sealed record ShiftCutListItemDto(
    Guid Id, ShiftCutType Type, string Folio, DateTime GeneratedAtUtc,
    string GeneratedByName, string ShiftFolio, long TotalSoldCents, long? DifferenceCents);

public sealed record ShiftCutPage(IReadOnlyList<ShiftCutListItemDto> Items, long TotalCount, int Page, int PageSize)
{
    public const int DefaultPageSize = 100;
    public int TotalPages { get; }  // igual que ShiftPage
}

/// <summary>Reporte fijo de un corte: lo que se muestra e imprime (FR-004, FR-014).</summary>
public sealed record ShiftCutReportDto(
    Guid CutId, ShiftCutType Type, string Folio,
    string ShiftFolio, string RegisterName,
    Guid GeneratedById, string GeneratedByName, string? AuthorizedByName,
    Guid ShiftOwnerId, string ShiftOwnerName,
    DateTime ShiftOpenedAtUtc, DateTime GeneratedAtUtc,
    long OpeningFloatCents, int SalesCount, int CancelledCount, long TotalSoldCents,
    long CashSalesCents, long CashCancelledCents, long CardCents, long TransferCents,
    long CashRefundsCents, long NonCashRefundsCents, long CreditNotesIssuedCents,
    ShiftCreditTotals Credit,
    long DepositsCents, long WithdrawalsCents, long ExpectedCashCents,
    long? CountedCashCents, long? DifferenceCents, string? Comment);
```

## Puerto `ICashShiftRepository` — se amplía

Los cortes pertenecen al turno; se amplía el repositorio del agregado en vez de crear otro.

```csharp
/// <summary>MAX(Number) + 1 del tipo; dentro de la transacción de escritura.</summary>
Task<long> NextCutNumberAsync(ShiftCutType type, CancellationToken ct);

void AddCut(ShiftCut cut);

Task<ShiftCutReportDto?> GetCutReportAsync(Guid cutId, CancellationToken ct);

Task<ShiftCutPage> SearchCutsAsync(ShiftCutSearch search, CancellationToken ct);
```

El Corte X usa el `GetOpenAsync` existente: el turno queda en seguimiento pero sin cambios, así que
`SaveChangesAsync` no emite ningún `UPDATE` sobre `CashShifts` (lo verifica la prueba de `Version`).

`SaveChangesAsync` agrega `SaveStatus.Duplicate` con `CashShiftFields.CutNumber` para el índice
`IX_ShiftCuts_Type_Number` o `IX_ShiftCuts_ClosingPerShift`; los casos de uso lo traducen a `Conflict`.

## PrintTicket — cambia

- `PrintSource.ShiftCut(Guid cutId)` → `ShiftCutSource`.
- Permiso base `OperateShift`; acceso al corte: `GeneratedById == currentUser` o `ManageShifts`.
- `ShiftTicketBuilder.BuildCut(profile, ShiftCutReportDto, columns, options, timeZone)`.
- Con `IsReprint = true`: bitácora `SHIFT_CUT_REPRINTED`, entidad `ShiftCut`,
  "Corte X X-000004. Turno T-000123".
- `ShiftReportSource` (corte desde "Turnos") sigue igual; el título muestra "CORTE Z Z-000001" si el
  turno tiene `CutFolio`, o "CORTE DE CAJA" en turnos anteriores.

## Sin cambios

`OpenShift`, `CountShiftCash`, `RegisterCashMovement`, `ConfirmSale`, devoluciones y abonos: ya
rechazan operaciones sobre un turno cerrado (FR-011, research §12).
