# Implementation Plan: Turnos de caja

**Branch**: `008-cash-shifts` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-cash-shifts/spec.md`

## Summary

Cada venta queda ligada a un turno de caja y a su cajero. Al cerrar el turno, el efectivo contado se
compara con el esperado sin mostrarlo antes (arqueo ciego).

1. **Turno**: un agregado `CashShift` nuevo con sus `CashMovement` (ingresos y retiros inmutables)
   (research §1).
   - Un índice único filtrado garantiza un solo turno abierto por caja (research §2).
   - `RegisterCode = "CAJA-1"` es la única preparación para varias cajas.
2. **Efectivo esperado**: se calcula dentro de la transacción de escritura, nunca se acumula
   (research §3). La fórmula está en `CashShiftMath` (Domain):
   `fondo + efectivo de ventas (neto de cambio) − efectivo cancelado + ingresos − retiros`.
   - La diferencia del arqueo es un entero con signo; `Money` sigue sin negativos (research §4).
3. **Ventas**:
   - `Sales.CashShiftId` es una columna nula nueva, sin llave foránea y sin reconstruir `Sales`.
   - `ConfirmSale` exige un turno abierto del propio usuario (research §5).
   - `CancelSale` solo cancela ventas del turno abierto y rechaza, sin revelar montos, la devolución
     que dejaría el esperado en negativo (research §6).
4. **Permisos**: `OperateShift` (los dos roles), `WithdrawCash` (administrador; el cajero lo obtiene
   por autorización con el mecanismo de 007) y `ManageShifts` (administrador) (research §7).
5. **Cierre**: `CountShiftCash` revela las cifras solo después del conteo y audita cada conteo;
   luego `CloseShift` recalcula, exige comentario si hay diferencia y guarda una instantánea
   inmutable (research §8, §10).
   - La venta en curso del dueño bloquea su propio cierre.
   - Si un administrador cierra un turno ajeno, la venta conservada se descarta con confirmación y
     queda en la bitácora (research §9).
6. **Impresión**: `PrintTicket` agrega el corte y el comprobante de movimiento con la tubería de 006
   (research §12).
7. **Interfaz** (research §14, [contracts/ui.md](contracts/ui.md)):
   - El Punto de venta tiene tres estados: sin turno, turno ajeno y turno propio.
   - Diálogos de apertura, movimiento y cierre.
   - Pantalla "Turnos" para administradores.
   - Tarjeta de turno en Inicio.

Hay una migración nueva, `CashShifts`. Crea dos tablas y agrega una columna nula a `Sales`, sin
reconstruir ninguna tabla. No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `CashShifts`:
  - Tablas `CashShifts` (con `IX_CashShifts_OpenPerRegister` único filtrado por `Status = 'OPEN'`) y
    `CashMovements`.
  - `Sales.CashShiftId` nula con `IX_Sales_CashShiftId_Status`.
- `Version` 0.5.0 → 0.6.0. Base de ejemplo `v0.6.0.db`, generada antes de la migración.

**Testing**: xUnit v3, según la política mínima de la constitución v1.2.0 (detalle en research §16):

- **Domain**:
  - `CashShiftMath` (esperado con cambio, pagos mixtos, cancelaciones, ingresos y retiros; SC-003).
  - Reglas de `CashShift`: retiro excedente, comentario obligatorio, turno cerrado.
  - `RolePermissions` actualizada.
- **Application**:
  - Venta sin turno propio.
  - Cancelación fuera del turno o sin efectivo.
  - Retiro del cajero sin autorización y mensajes con o sin monto.
  - Cierre con venta en curso y descarte por administrador.
  - `RestrictedOperationsTests` ampliada.
- **Infrastructure** (SQLite real):
  - Totales del turno por consulta.
  - Aperturas simultáneas contra el índice único (SC-002).
  - Inmutabilidad del turno cerrado.
  - Migración de las bases de ejemplo más `v0.6.0.db`, y revisión del SQL sin reconstrucciones.
- **Arquitectura**: sin cambios.
- Sin pruebas de ViewModels, vistas ni del constructor del corte.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Abrir un turno en menos de 30 s y cerrarlo con arqueo en menos de 2 min (SC-004). Es tiempo del
  operador; cada operación en la base tarda menos de 50 ms.
- El cálculo del esperado es una suma indexada por `CashShiftId` sobre cientos de ventas: menos de
  20 ms.
- La consulta de "Turnos", 100 filas con subconsulta de totales para los abiertos, tarda menos de
  200 ms con 10 000 turnos.
- Confirmar una venta agrega una lectura por índice filtrado, menos de 5 ms.

**Constraints**:

- Funciona sin conexión.
- 0 advertencias.
- Apertura, movimiento y cierre van cada uno en una sola transacción, con su bitácora (FR-026,
  FR-027).
- El cajero nunca ve el esperado antes de contar (SC-005).
- Una falla de impresión no revierte ni bloquea el cierre.
- No se reconstruye `Sales` en la migración.

**Scale/Scope**:

- 1 caja por instalación, 1 a 3 turnos por día y cientos de ventas por turno.
- Casos de uso: 7 nuevos (`GetCurrentShift`, `OpenShift`, `RegisterCashMovement`, `CountShiftCash`,
  `CloseShift`, `SearchShifts` y `GetShiftDetail`) y 3 que cambian (`ConfirmSale`, `CancelSale` y
  `PrintTicket`).
- Pantallas y diálogos nuevos: estados del Punto de venta, apertura, movimiento, cierre en tres
  pasos, "Turnos" con detalle y tarjeta de Inicio.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó tras el diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. La venta en curso nunca se pierde: el borrador por usuario sobrevive al cierre de sesión y al reinicio. Solo se descarta cuando un administrador lo confirma explícitamente al cerrar un turno ajeno, y queda auditado. Apertura, movimiento y cierre son una transacción cada uno. La impresión del corte ocurre después de confirmar el cierre, y una falla del dispositivo solo muestra "Reintentar". |
| II. Capas | `CashShift`, `CashMovement`, `CashShiftMath` y los permisos están en Domain. Los casos de uso y el puerto `ICashShiftRepository` están en Application. El repositorio EF Core y la configuración están en Infrastructure. Los ViewModels solo invocan casos de uso. Las pruebas de arquitectura existentes cubren las carpetas nuevas `CashShifts/`. |
| III. Lógica en el núcleo | La fórmula del esperado, el retiro excedente, el comentario obligatorio y la inmutabilidad están en Domain. La orquestación (turno propio, venta en curso, verificación en la transacción) está en Application. La interfaz no calcula la diferencia ni el esperado: los recibe de `CountShiftCash`. |
| IV. Integridad de datos | GUID v7 y fechas UTC. `CashShift` tiene auditoría, `Version` y `DeletedAt`. `CashMovement` es un registro contable inmutable, sin modificación ni borrado, como `InventoryMovement`. Los turnos y movimientos nunca se borran. Los importes son `Money`, guardados en centavos; la diferencia es un entero con signo porque `Money` no admite negativos (research §4). La migración es EF Core y su SQL se revisa con una prueba. `v0.6.0.db` se agrega a la prueba de bases de ejemplo. El índice único filtrado hace imposible un segundo turno abierto. |
| V. Multiplataforma | Sin código de plataforma. La impresión usa los adaptadores existentes de 006. |
| VI. Calidad verificable | Solo se prueban cálculos de dinero (esperado, diferencia, excedente), validaciones de integridad (turno propio, turno cerrado, único abierto) y las pruebas obligatorias de migración y arquitectura. La persistencia se prueba con SQLite real. |
| VII. Simplicidad | Sin dependencias nuevas, sin tabla de cajas, sin denominaciones ni corte X. El repositorio es específico del agregado. El único elemento pensado para el futuro es `RegisterCode`: una columna de costo mínimo que evita migrar los turnos si llega a haber varias cajas (preparación permitida por el Principio VII). |
| VIII. Soporte | Apertura, movimientos, conteos, cierres y rechazos se registran en Serilog con el turno, el usuario y los importes (no son datos sensibles). La bitácora permite reconstruir quién contó, cuánto y cuántas veces. El corte se puede reimprimir. |
| IX. Seguridad local | Los cortes, los retiros (con autorizador) y el cierre de un turno ajeno quedan en la bitácora inmutable (FR-027). Los retiros del cajero requieren autorización de administrador de un solo uso. El cajero nunca recibe el esperado ni datos para deducirlo antes de contar (FR-022). |

**Resultado**: sin violaciones. Hay tres decisiones que conviene hacer explícitas (no son
violaciones):

- `Sales.CashShiftId` no tiene llave foránea, para no reconstruir `Sales` (research §5). Es el
  mismo criterio que 007 aplicó a los campos de auditoría.
- La diferencia del arqueo y los totales calculados son `long` en centavos, no `Money`, porque
  pueden ser negativos o exceder su máximo (research §4). Los importes que captura el operador sí
  son `Money` (FR-025).
- Las ventas anteriores a 0.6.0 (sin turno) ya no se pueden cancelar. La especificación deja fuera
  la cancelación de ventas de turnos cerrados, y esas ventas no pertenecen a ningún arqueo
  (research §6). Se documenta en `docs/turnos-de-caja.md`.

## Project Structure

### Documentation (this feature)

```text
specs/008-cash-shifts/
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
│   ├── CashShifts/
│   │   ├── CashShift.cs                          # agregado: Open, RecordDeposit/Withdrawal, Close, instantánea
│   │   ├── CashMovement.cs                       # inmutable, Sequence por turno
│   │   ├── CashShiftStatus.cs  CashMovementType.cs  CashRegister.cs  ShiftFolio.cs
│   │   ├── ShiftSalesTotals.cs  CashDifference.cs
│   │   └── CashShiftMath.cs                      # ExpectedCash, CanRefund (FR-015, FR-008)
│   ├── Sales/Sale.cs                             # + CashShiftId; Register(…, cashShiftId)
│   └── Users/Permission.cs  RolePermissions.cs   # + OperateShift, WithdrawCash, ManageShifts
├── Pos.Application/
│   ├── Abstractions/Error.cs                     # + ShiftRequired, ShiftOwnedByOther, ShiftAlreadyOpen, ShiftClosed,
│   │                                             #   InsufficientCash, SaleInProgress, HeldSaleWillBeDiscarded, ShiftChanged
│   ├── Audit/AuditActions.cs                     # + SHIFT_OPENED, CASH_DEPOSIT, CASH_WITHDRAWAL, SHIFT_CASH_COUNTED,
│   │                                             #   SHIFT_CLOSED, SHIFT_CLOSED_BY_ADMIN
│   ├── CashShifts/
│   │   ├── ICashShiftRepository.cs  CashShiftDtos.cs  CashShiftMessages.cs  CashShiftFields.cs
│   │   ├── ShiftGuard.cs                         # turno abierto, propiedad y venta en curso (compartido)
│   │   ├── GetCurrentShift/  OpenShift/  RegisterCashMovement/
│   │   ├── CountShiftCash/  CloseShift/
│   │   └── SearchShifts/  GetShiftDetail/
│   ├── Sales/ConfirmSale/ConfirmSaleHandler.cs   # + turno propio en la transacción
│   ├── Sales/CancelSale/CancelSaleHandler.cs     # + turno abierto y efectivo suficiente
│   ├── Sales/ISaleRepository.cs                  # + GetShiftTotalsAsync, ListByShiftAsync
│   ├── Printing/PrintTicket/                     # + PrintSource.ShiftReport, PrintSource.CashMovement
│   ├── Printing/Ticket/ShiftTicketBuilder.cs     # corte y comprobante
│   ├── Users/Access/AccessControl.cs             # + HasAsync
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── CashShifts/CashShiftRepository.cs
│   ├── Sales/SaleRepository.cs                   # + totales por turno y ventas del turno
│   ├── Persistence/Configurations/CashShiftConfiguration.cs  CashMovementConfiguration.cs  (+ SaleConfiguration)
│   ├── Persistence/PosDbContext.cs               # + DbSets; RejectImmutableChanges (turno cerrado, movimientos)
│   ├── Persistence/Migrations/…_CashShifts.cs
│   └── DependencyInjection.cs
├── Pos.Desktop/
│   ├── CashShifts/
│   │   ├── CashShiftsModule.cs                   # página "Turnos", diálogos y tarjeta
│   │   ├── OpenShift*  CashMovement*  CloseShift*          # diálogos en ModalHost
│   │   ├── Shifts*  ShiftDetail*                           # pantalla de administrador
│   │   └── CurrentShiftCard.cs
│   ├── Sales/PointOfSaleView*  PointOfSaleViewModel.cs     # tres estados y barra de turno
│   ├── Sales/SaleDetailViewModel.cs                        # mensaje de turno cerrado o sin efectivo al cancelar
│   ├── Resources/Strings.resx                              # textos Shift_*, CashMovement_*, Nav_Shifts
│   └── Composition/HostBuilder.cs                          # + AddCashShiftsModule
tests/
├── Pos.Domain.Tests/CashShifts/          CashShiftMathTests, CashShiftTests
├── Pos.Domain.Tests/Users/               RolePermissionsTests (actualizada)
├── Pos.Domain.Tests/Sales/               SaleTests (firma de Register)
├── Pos.Application.Tests/CashShifts/     RegisterCashMovementTests, CloseShiftTests
├── Pos.Application.Tests/Sales/          ConfirmSaleShiftTests, CancelSaleShiftTests
├── Pos.Application.Tests/Security/       RestrictedOperationsTests (+ casos nuevos)
├── Pos.Infrastructure.Tests/CashShifts/  CashShiftPersistenceTests
└── Pos.Infrastructure.Tests/SampleDatabases/  v0.6.0.db, CashShiftsMigrationTests, SampleDatabaseUpgradeTests
docs/
├── turnos-de-caja.md                     # guía de soporte: fórmula, arqueo, cierre ajeno, reimpresión, ventas sin turno
└── migraciones.md                        # + sección 0.6.0 (CashShifts sin reconstrucciones)
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 007.

- `CashShifts` es una carpeta nueva en Domain, Application, Infrastructure, Desktop y en las
  pruebas.
- La página "Turnos" se registra desde `CashShiftsModule` dentro del grupo Ventas existente
  (`SalesModule.GroupId`, orden 20). No se crea un grupo de menú nuevo.
- Las pruebas existentes que registran ventas (`SaleTests`, las de persistencia de ventas y la
  generación de bases de ejemplo) se ajustan: abren un turno antes de confirmar o pasan un
  `cashShiftId`.

## Complexity Tracking

Sin violaciones de la constitución; no aplica.
