# Implementation Plan: Corte X y Corte Z

**Branch**: `017-corte-x-z` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/017-corte-x-z/spec.md`

## Summary

Agregar a la caja el Corte X (lectura parcial del turno abierto) y presentar el cierre de turno de
la spec 008 como Corte Z con folio propio, más un histórico de ambos.

1. **Registro de cortes** (research §1–§3, §9): agregado inmutable `ShiftCut` (tabla `ShiftCuts`)
   con tipo X o Z, folio consecutivo por tipo (`MAX + 1` dentro de `BEGIN IMMEDIATE` e índice único
   `(Type, Number)`) y copia fija de todas las cifras del turno.
2. **Corte X** (research §5, §6): caso de uso `GenerateShiftReadout`. Calcula los totales con la
   misma regla del cierre y guarda solo el corte, sin tocar el turno. Permiso nuevo
   `GenerateShiftReadout`: Administrador, autorizable para el Cajero.
3. **Corte Z** (research §4): `CloseShift` crea el corte Z en la misma transacción del cierre. No hay
   flujo nuevo; el diálogo de cierre se rotula "Corte Z".
4. **Histórico** (research §7): `SearchShiftCuts` y `GetShiftCut` con filtros por tipo, fechas y
   usuario, páginas de 100, permiso `ManageShifts`.
5. **Impresión** (research §10): `PrintSource.ShiftCut` y `ShiftTicketBuilder.BuildCut`; las
   reimpresiones quedan en la bitácora.
6. **Menú** (research §8): grupo nuevo "Caja" con Corte X, Corte Z e Histórico de cortes.

Hay una migración nueva, `ShiftCuts`, que solo crea una tabla, sin reconstrucciones. No se agrega
ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `ShiftCuts`: tabla nueva con FK a `CashShifts` (`Restrict`) y cinco índices
  ([data-model.md](data-model.md)).
- `Version` 0.11.0 → 0.12.0. Base de ejemplo `v0.12.0.db` según [docs/migraciones.md](../../docs/migraciones.md).

**Testing**: xUnit v3 con la política mínima de la constitución v1.2.0 (research §14):

- **Domain**: `ShiftCutTests`. El esperado de la instantánea coincide con `CashShiftMath`, y el Z
  exige un turno cerrado.
- **Casos de uso sobre SQLite real** (`ShiftCutUseCaseTests`):
  - El Corte X no altera el turno y su instantánea queda fija.
  - Folios consecutivos sin huecos; un Z rechazado no consume folio.
  - De dos Corte Z simultáneos, solo uno se completa.
  - Rechazos: Cajero sin autorización y caja sin turno.
  - Un turno nuevo empieza en cero.
  - Histórico filtrado y paginado.
- **Migración**: `ShiftCutsMigrationTests` y bases de ejemplo.
- **Arquitectura e inventario**: las obligatorias existentes.
- Sin pruebas de ViewModels, vistas, ticket ni mapeos.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Generar e imprimir un Corte X en menos de 30 s de operación (SC-005). La consulta es la misma
  `GetShiftTotalsAsync` del cierre, ya acotada por `ShiftId`.
- Histórico: menos de 200 ms por página de 100 con 10,000 cortes (índice `IX_ShiftCuts_GeneratedAt`).
  Localizar y reimprimir en menos de 1 minuto (SC-006).

**Constraints**:

- Funciona sin conexión. El Corte X no bloquea la venta más que lo que dura su transacción de
  escritura (milisegundos).
- 0 advertencias.
- Cada Corte X y cada Corte Z es una transacción (`BEGIN IMMEDIATE`).
- Sin reconstrucción de tablas en la migración.

**Scale/Scope**:

- Una caja; decenas de Corte X y un Corte Z por día.
- Casos de uso: **3 nuevos** (`GenerateShiftReadout`, `GetShiftCut`, `SearchShiftCuts`); **cambian**
  `CloseShift`, `PrintTicket`, `GetShiftDetail` y el reporte de turno de 008 (folio Z).
- Pantallas: nuevo grupo "Caja" con Corte X, Corte Z, Histórico de cortes y el diálogo de vista del
  corte. Cambian el diálogo de cierre, el botón del Punto de venta y el detalle de turno.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. El Corte X no modifica el turno ni la venta en curso (los borradores no están en `Sales`). Cada corte es una transacción y una falla no deja datos parciales ni consume folio (FR-019). Una impresora no disponible no impide registrar el corte. |
| II. Capas | `ShiftCut`, `ShiftCutType` y `ShiftCutFolio` en Domain; casos de uso y DTO en Application; repositorio, configuración EF y migración en Infrastructure; ViewModels solo invocan casos de uso. No se crea ninguna carpeta fuera de las cubiertas por las pruebas de arquitectura (`CashShifts/`). |
| III. Lógica en el núcleo | El esperado se calcula con `CashShift.ExpectedCash`/`CashShiftMath`, las instantáneas y su inmutabilidad viven en `ShiftCut`, y el folio, en el caso de uso. La interfaz no calcula importes. |
| IV. Integridad de datos | GUID v7, UTC, `Version`, `DeletedAt` y auditoría en `ShiftCut`. Los cortes no se borran ni se modifican (FR-013). Importes en centavos (FR-018). Folios únicos por índice. Migración Code First sin reconstrucciones, con SQL revisado y base de ejemplo 0.12.0. |
| V. Multiplataforma | Sin código de plataforma; la impresión usa los adaptadores existentes. |
| VI. Calidad verificable | Solo se prueban cálculos de dinero (instantánea y esperado), validaciones de integridad (folios, atomicidad, concurrencia, inmutabilidad del turno) y las pruebas obligatorias de migración, inventario y arquitectura. SQLite real. |
| VII. Simplicidad | Sin dependencias nuevas. Un permiso nuevo, necesario para que el Corte X sea autorizable sin abrir otras operaciones. Se amplía `ICashShiftRepository` en lugar de crear otro repositorio. Sin gran total acumulado (FR-012a) ni CFDI. |
| VIII. Soporte | Serilog registra cada corte con `CutId`, folio, turno, usuario y autorizador, y los rechazos. `docs/turnos-de-caja.md` documenta Corte X, Corte Z, folios, histórico y la verificación de huecos. `docs/migraciones.md` agrega la sección 0.12.0. |
| IX. Seguridad local | Corte X, Corte Z y reimpresiones quedan en la bitácora con usuario, tipo, folio y turno, y el Corte X con quién autorizó (FR-017). El histórico es solo del Administrador. |

**Resultado**: sin violaciones. Decisiones explícitas:

- **Copia completa en el Z (research §2)**: las cifras del cierre se guardan en `CashShifts` (008) y
  también en `ShiftCuts`, en la misma transacción. Así el histórico y la reimpresión tienen una sola
  fuente.
- **Efectivo esperado en el Corte X (research §6)**: el Corte X lo muestra (FR-004). Un Cajero solo
  lo ve con autorización de un Administrador presente, así que el arqueo ciego del cierre se mantiene.
- **"Turnos" sigue en Ventas (research §8)**: el grupo "Caja" se agrega sin mover pantallas que los
  clientes ya usan.
- **Bitácora del Z (research §11)**: se conserva una sola entrada de cierre (`SHIFT_CLOSED*`) y se
  le agrega el folio Z. No se crea un evento duplicado.

## Project Structure

### Documentation (this feature)

```text
specs/017-corte-x-z/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── application-ports.md
│   └── ui.md
├── checklists/
└── tasks.md             # Lo genera /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── Pos.Domain/
│   ├── CashShifts/ShiftCut.cs  ShiftCutType.cs  ShiftCutFolio.cs        # nuevo agregado
│   ├── Users/Permission.cs  RolePermissions.cs                         # + GenerateShiftReadout (autorizable)
│   └── Licensing/ModuleAccess.cs                                       # GenerateShiftReadout → CashShifts
├── Pos.Application/
│   ├── Audit/AuditActions.cs                                           # + SHIFT_READOUT_GENERATED, SHIFT_CUT_REPRINTED, ShiftCutEntity
│   ├── CashShifts/
│   │   ├── ICashShiftRepository.cs  CashShiftDtos.cs  CashShiftFields.cs  CashShiftMessages.cs
│   │   ├── ShiftCutDtos.cs                                             # ShiftCutReportDto, ShiftCutPage, ShiftCutSearch
│   │   ├── GenerateShiftReadout/  GetShiftCut/  SearchShiftCuts/       # nuevos
│   │   ├── CloseShift/CloseShiftHandler.cs                             # + Corte Z
│   │   └── GetShiftDetail/                                             # + CutFolio
│   ├── Printing/PrintTicket/PrintTicketCommand.cs  PrintTicketHandler.cs   # + ShiftCutSource, reimpresión
│   ├── Printing/Ticket/ShiftTicketBuilder.cs                           # + BuildCut, título Z
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── CashShifts/CashShiftRepository.cs                               # cortes: número, alta, reporte, búsqueda
│   ├── Persistence/Configurations/ShiftCutConfiguration.cs             # nuevo
│   ├── Persistence/PosDbContext.cs                                     # DbSet<ShiftCut>
│   └── Persistence/Migrations/…_ShiftCuts.cs
├── Pos.Desktop/
│   ├── CashShifts/CashModule.cs                                        # grupo "Caja" y sus tres páginas
│   ├── CashShifts/ShiftReadoutView*  ShiftReadoutViewModel.cs          # Caja > Corte X
│   ├── CashShifts/ShiftClosingView*  ShiftClosingViewModel.cs          # Caja > Corte Z
│   ├── CashShifts/ShiftCutsView*  ShiftCutsViewModel.cs                # Caja > Histórico de cortes
│   ├── CashShifts/ShiftCutDetailView*  ShiftCutDetailViewModel.cs      # vista e (re)impresión
│   ├── CashShifts/CloseShiftView*  CloseShiftViewModel.cs  ShiftDetailView*   # rótulos "Corte Z" y folio
│   ├── Sales/PointOfSaleView.axaml                                     # botón "Corte Z"
│   ├── Resources/Strings.resx                                          # Cut_*, Nav_Cash*
│   └── Composition/HostBuilder.cs                                      # + AddCashModule
tests/
├── Pos.Domain.Tests/CashShifts/              ShiftCutTests
├── Pos.Infrastructure.Tests/CashShifts/      ShiftCutUseCaseTests
└── Pos.Infrastructure.Tests/SampleDatabases/ v0.12.0.db, ShiftCutsMigrationTests
docs/
├── turnos-de-caja.md                  # Corte X, Corte Z, folios, histórico, autorización, verificación de huecos
├── impresion.md                       # tickets de Corte X y Corte Z
├── usuarios-y-permisos.md             # permiso GenerateShiftReadout (autorizable)
└── migraciones.md                     # + sección 0.12.0 (sin reconstrucciones)
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 016.

- Los cortes son parte del agregado de caja: todo vive en las carpetas `CashShifts/` existentes de
  cada capa.
- `CashModule` registra el grupo `cash` ("Caja", orden 6) y las páginas `cash.readout`,
  `cash.close` y `cash.cuts`. `CashShiftsModule` conserva "Turnos" en Ventas y los diálogos.
- `ClosedShift`, `ShiftReportDto` y `ShiftDetailDto` cambian de firma (parámetros opcionales donde
  sea posible). Hoy solo se construyen en `src/`; si alguna prueba deja de compilar, se ajusta.

## Complexity Tracking

Sin violaciones que justificar.
