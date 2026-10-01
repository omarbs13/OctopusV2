# Implementation Plan: Devoluciones y cancelaciones

**Branch**: `013-returns-cancellations` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/013-returns-cancellations/spec.md`

## Summary

Cancelar o devolver ventas, completas o en parte, con autorización de un Administrador, inventario
restaurado, y el dinero devuelto como reintegro o conservado como nota de crédito. Todo queda en un
registro inmutable y en la bitácora.

1. **Registro inmutable `SaleReturn`** (research §1): cada cancelación o devolución parcial crea uno,
   con líneas, motivo, autorizador y compensación. `Sale` conserva su detalle y gana `ReturnedCents` y
   `SaleLine.ReturnedQuantity` (research §2).
2. **Importes exactos** (research §3): importe por línea con acumulado, reparto entre formas de pago
   por resto mayor ponderado por lo aún devolvible. Es la función pura `ReturnMath` (Domain).
3. **Compensación** (research §4–§6):
   - Reintegro en efectivo: sale del efectivo esperado del **turno abierto actual**, aunque la venta
     sea de un turno cerrado; el turno cerrado no se modifica.
   - Tarjeta y transferencia: se anota como pendiente de reversa manual; el Administrador la marca.
   - Nota de crédito: nueva, con saldo **calculado** a partir de sus movimientos.
4. **Notas de crédito como pago** (research §5): forma de pago `CREDIT` en el cobro, a lo más una por
   venta, con saldo validado dentro de la transacción.
5. **Autorización** (research §7): siempre una concesión de `ApproveReturns`, también para el
   Administrador, con el mecanismo de 007. Permisos nuevos y módulo Devoluciones (012).
6. **Inventario** (research §8): `SALE_CANCEL` ("Devolución por venta cancelada") y `SALE_RETURN`
   nuevo para parciales.
7. **Plazo configurable** (research §9), **ticket de la nota** (research §13), totales netos y corte
   con reintegros (research §6, §11), **cancelación básica conservada sin licencia** (research §12).
8. **Interfaz** ([contracts/ui.md](contracts/ui.md)): formulario unificado en el detalle de venta,
   pago con nota en el cobro y página "Devoluciones y vales" para el Administrador.

Hay una migración nueva, `ReturnsAndCreditNotes`: 5 tablas y 6 columnas agregadas, sin reconstruir
ninguna tabla existente. No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `ReturnsAndCreditNotes`:
  - Tablas `SaleReturns`, `SaleReturnLines`, `SaleReturnRefunds`, `CreditNotes`,
    `CreditNoteMovements`.
  - Columnas: `Sales.ReturnedCents`, `SaleLines.ReturnedQuantity`, `SalePayments.CreditNoteId`,
    `CashShifts.CashRefundsCents`, `NonCashRefundsCents`, `CreditNotesIssuedCents`.
- Plazo de devoluciones en preferencias locales (`IPreferencesStore`), sin tabla.
- `Version` 0.7.0 → 0.8.0. Base de ejemplo nueva siguiendo [docs/migraciones.md](../../docs/migraciones.md).

**Testing**: xUnit v3, política mínima de la constitución v1.2.0 (detalle en research §15):

- **Domain**: `ReturnMath`, `SaleReturn`, `Sale.ApplyReturn`, saldo de `CreditNote`,
  `CashShiftMath` con reintegros, `RolePermissions`/`ModuleAccess`.
- **Casos de uso sobre SQLite real**: cancelación (efectivo, tarjeta, mixto, nota), devolución parcial
  acumulada, concurrencia, nota con saldo insuficiente, autorización obligatoria para todos, plazo,
  atomicidad y consistencia de inventario (obligatoria).
- **Migración**: bases de ejemplo y SQL sin reconstrucciones. **Arquitectura**: sin cambios.
- Sin pruebas de ViewModels, vistas ni de los constructores de tickets.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Cancelación o devolución confirmada en menos de 1 min de operación (SC-001); cada operación en
  la base tarda menos de 100 ms.
- La vista previa se calcula en memoria con la venta cargada: menos de 50 ms.
- Listas de "Devoluciones y vales": 100 filas en menos de 200 ms con 10 000 devoluciones (índice
  filtrado de pendientes).
- Confirmar una venta sin nota de crédito no agrega consultas; con nota agrega una suma por índice.

**Constraints**:

- Funciona sin conexión (FR-017).
- 0 advertencias.
- Una devolución es una sola transacción: venta, inventario, reintegro o nota y bitácora (FR-011).
- Nunca se revela el efectivo esperado al rechazar un reintegro (regla de 008).
- Una falla de impresión no revierte la devolución.
- No se reconstruye ninguna tabla en la migración.

**Scale/Scope**:

- Cientos de ventas por turno; de unas pocas a decenas de devoluciones por día.
- Casos de uso: 9 nuevos (`PreviewReturn`, `ReturnSaleItems`, `GetCreditNoteBalance`,
  `SearchCreditNotes`, `GetCreditNoteDetail`, `SearchPendingReversals`, `MarkReversalDone`,
  `GetReturnsSettings`, `SaveReturnsSettings`) y 6 que cambian (`CancelSale`, `ConfirmSale`, `GetSale`/`ReviewSale`,
  `PrintTicket`, cierre y detalle de turno, reportes).
- Pantallas: formulario "Devolver o cancelar", pago con nota, página "Devoluciones y vales".

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó tras el diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. Devolución, inventario, compensación y bitácora son una sola transacción `BEGIN IMMEDIATE`: se guarda completa o no se guarda (FR-011). Un error inesperado se registra y se muestra un mensaje sin detalles técnicos. Una falla de impresión no revierte nada. Las devoluciones no tocan la venta en curso. |
| II. Capas | `SaleReturn`, `CreditNote`, `ReturnMath` y las reglas están en Domain. Los casos de uso y los puertos `IReturnRepository`, `ICreditNoteRepository` e `IReturnsSettingsStore` están en Application. Los repositorios EF Core y las configuraciones están en Infrastructure. Los ViewModels solo invocan casos de uso. Las pruebas de arquitectura cubren las carpetas nuevas `Returns/` y `CreditNotes/`. |
| III. Lógica en el núcleo | Reparto proporcional, importe acumulado, límites de cantidad y saldo de la nota viven en Domain. La vista previa la calcula `PreviewReturn`, no la interfaz. La orquestación (plazo, turno, efectivo, concesión) está en `SaleReturnProcessor`. |
| IV. Integridad de datos | GUID v7, fechas UTC, importes en centavos con `Money` donde el dominio lo permite (los acumulados son `long` como en 008). Registros contables inmutables sin borrado (ver Complexity Tracking). Concurrencia con `Sale.Version` y transacción serializada. Migración EF Core, sin reconstrucciones, SQL revisado con prueba y base de ejemplo agregada. Folios únicos por índice. |
| V. Multiplataforma | Sin código de plataforma; la impresión usa los adaptadores de 006. |
| VI. Calidad verificable | Solo se prueban cálculos de dinero (reparto, acumulado, efectivo esperado, saldo), validaciones de integridad (límites, autorización, plazo, concurrencia) y las pruebas obligatorias de migración e inventario. Persistencia sobre SQLite real. |
| VII. Simplicidad | Sin dependencias nuevas. Saldo calculado en lugar de guardado. Plazo en preferencias, sin tabla. Repositorios específicos por agregado. Sin reportes dedicados, sin clientes, sin conversión del vale a efectivo (fuera de alcance). |
| VIII. Soporte | Cada devolución, rechazo y fallo se registra en Serilog con venta, folio, usuario y montos (no sensibles; nunca el PIN). `docs/devoluciones.md` documenta reparto, turnos y reversas. El ticket de la nota se reimprime. |
| IX. Seguridad local | Toda cancelación o devolución exige la contraseña de un Administrador, incluida la de quien la opera si es Administrador. La bitácora registra usuario, fecha, motivo, autorizador, venta, monto y compensación, y también los intentos fallidos. Los registros no se editan ni borran. El cajero no ve el efectivo esperado ni el origen o los usos de una nota. |

**Resultado**: sin violaciones. Decisiones que conviene hacer explícitas (no son violaciones):

- El reintegro en efectivo de una venta de turno cerrado se ancla al turno abierto actual (research §6),
  lo que cambia la regla de 008 que rechazaba esa cancelación. Está respaldado por la clarificación
  del responsable del proyecto.
- `Sales.ReturnedCents` y `SaleLines.ReturnedQuantity` son valores derivados guardados por rendimiento
  y para que los reportes los lean sin unir tablas; se actualizan solo en `Sale.ApplyReturn` y una
  prueba compara ambos contra la suma de `SaleReturnLines`.
- `PaymentMethod.CreditNote` usa el código `CREDIT` para no alterar el tamaño de columna (research §10).
- La cancelación sin licencia de Devoluciones conserva el comportamiento de 005/008 (research §12).

## Project Structure

### Documentation (this feature)

```text
specs/013-returns-cancellations/
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
│   ├── Returns/
│   │   ├── SaleReturn.cs  SaleReturnLine.cs  SaleReturnRefund.cs   # inmutables; MarkReversed
│   │   ├── ReturnKind.cs  ReturnCompensation.cs  RefundStatus.cs  ReturnFolio.cs
│   │   └── ReturnMath.cs                                           # acumulado y reparto (research §3)
│   ├── CreditNotes/
│   │   ├── CreditNote.cs  CreditNoteMovement.cs  CreditNoteFolio.cs  # saldo calculado
│   ├── Sales/Sale.cs  SaleLine.cs  SalePayment.cs  PaymentMethod.cs  Checkout.cs
│   │                                         # + ReturnedCents/ApplyReturn, ReturnedQuantity, CreditNoteId, CREDIT
│   ├── Inventory/MovementType.cs  ProductStock.cs                  # + SALE_RETURN, RecordSaleReturn
│   ├── CashShifts/CashShiftMath.cs  ShiftSalesTotals.cs  CashShift.cs  # reintegros en el esperado y en la instantánea
│   ├── Licensing/ModuleAccess.cs                                   # permisos nuevos → Devoluciones
│   └── Users/Permission.cs  RolePermissions.cs                     # + ProcessReturns, ApproveReturns, ManageCreditNotes
├── Pos.Application/
│   ├── Abstractions/Error.cs                                       # + ReturnWindowExpired, NothingToReturn, CreditNote*
│   ├── Audit/AuditActions.cs                                       # + SALE_RETURNED, CARD_REVERSAL_DONE, …
│   ├── Returns/
│   │   ├── IReturnRepository.cs  IReturnsSettingsStore.cs  ReturnsSettings.cs  ReturnDtos.cs  ReturnMessages.cs
│   │   ├── SaleReturnProcessor.cs                                  # núcleo compartido por cancelar y devolver
│   │   ├── PreviewReturn/  ReturnSaleItems/
│   │   ├── SearchPendingReversals/  MarkReversalDone/
│   │   └── GetReturnsSettings/  SaveReturnsSettings/
│   ├── CreditNotes/
│   │   ├── ICreditNoteRepository.cs  CreditNoteDtos.cs
│   │   └── GetCreditNoteBalance/  SearchCreditNotes/  GetCreditNoteDetail/
│   ├── Sales/CancelSale/CancelSaleHandler.cs  CancelSaleCommand.cs  # delega en el procesador
│   ├── Sales/ConfirmSale/ConfirmSaleHandler.cs                      # + pago con nota de crédito
│   ├── Sales/SaleDtos.cs  ISaleRepository.cs                        # + devuelto, historial, totales netos
│   ├── Printing/PrintTicket/  Printing/Ticket/CreditNoteTicketBuilder.cs
│   ├── CashShifts/…                                                 # esperado, instantánea y corte
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Returns/ReturnRepository.cs  PreferencesReturnsSettingsStore.cs
│   ├── CreditNotes/CreditNoteRepository.cs
│   ├── Sales/SaleRepository.cs                                      # totales netos y reintegros por turno
│   ├── CashShifts/CashShiftRepository.cs  Reports/SalesReportReader.cs  CashCountReportReader.cs
│   ├── Persistence/Configurations/SaleReturn*Configuration.cs  CreditNote*Configuration.cs  (+ Sale*, CashShift*)
│   ├── Persistence/PosDbContext.cs                                  # + DbSets; RejectImmutableChanges para devoluciones y notas
│   ├── Persistence/Migrations/…_ReturnsAndCreditNotes.cs
│   └── DependencyInjection.cs
├── Pos.Desktop/
│   ├── Sales/SaleDetailView*  SaleDetailViewModel.cs               # botones e historial
│   ├── Sales/ReturnSaleView*  ReturnSaleViewModel.cs                # reemplaza CancelSaleView*
│   ├── Sales/CheckoutView*  CheckoutViewModel.cs  PaymentMethodLabels.cs   # nota de crédito
│   ├── Returns/ReturnsAdminView*  ReturnsAdminViewModel.cs  ReturnsModule.cs   # página "Devoluciones y vales"
│   ├── CashShifts/ShiftDetailView*                                  # reintegros en el detalle
│   ├── Resources/Strings.resx                                       # textos Return_*, CreditNote_*, Nav_Returns
│   └── Composition/HostBuilder.cs                                   # + AddReturnsModule
tests/
├── Pos.Domain.Tests/Returns/             ReturnMathTests, SaleReturnTests
├── Pos.Domain.Tests/CreditNotes/         CreditNoteTests
├── Pos.Domain.Tests/CashShifts/          CashShiftMathTests (ampliada)
├── Pos.Domain.Tests/Users/               RolePermissionsTests (actualizada)
├── Pos.Infrastructure.Tests/Returns/     ReturnsUseCaseTests, InventoryConsistencyAfterReturnsTests
└── Pos.Infrastructure.Tests/SampleDatabases/  v0.8.0.db, ReturnsMigrationTests, SampleDatabaseUpgradeTests
docs/
├── devoluciones.md                       # guía de soporte: reparto, turnos, reversas de tarjeta, notas, plazo
├── migraciones.md                        # + sección 0.8.0 (sin reconstrucciones)
├── ventas.md  turnos-de-caja.md          # actualizar cancelación y totales
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 012.

- `Returns` y `CreditNotes` son carpetas nuevas en Domain, Application y pruebas; `Returns` también en
  Infrastructure y Desktop.
- `CancelSale` conserva su carpeta y su comando para no romper a sus llamadores, pero delega en
  `SaleReturnProcessor`.
- La página "Devoluciones y vales" se registra desde `ReturnsModule` dentro del grupo Ventas
  (`SalesModule.GroupId`, orden 30).
- Las pruebas existentes que construyen `ShiftSalesTotals`, `Sale` o `PaymentMethod` se ajustan a las
  firmas nuevas.

## Complexity Tracking

| Desviación | Por qué se necesita | Alternativa más simple descartada |
|---|---|---|
| `SaleReturn*`, `CreditNote` y `CreditNoteMovement` sin `DeletedAt`, `UpdatedAt/By` ni `Version` | Son registros contables inmutables: se corrigen con otro registro, nunca se editan ni se borran (FR-013). Mismo precedente que `InventoryMovement` (004) y `CashMovement` (008) | Agregar los campos estándar: sugeriría que se pueden editar o borrar, contra el criterio de aceptación 4 |
| `SaleReturnRefund` admite una sola transición (`PENDING_REVERSAL → REVERSED`) | El Administrador debe poder marcar que la reversa de tarjeta ya se hizo (FR-018) | Una tabla aparte de "reversas": agrega un registro por cada reintegro por una sola columna de estado |
| Importes de `SaleReturn*`, `CreditNote*` y `ReturnMath` como `long` en centavos, sin value object `Money` (Principio IV) | Son acumulados y repartos con enteros, igual que `ShiftSalesTotals` y `CashShiftMath` de 008; envolver cada paso en `Money` agrega conversiones sin proteger más | Usar `Money` en todo el dominio de devoluciones: más código y cambios en tipos existentes de ventas y turnos |
| `Sales.ReturnedCents` y `SaleLines.ReturnedQuantity` derivados y guardados | Los reportes de 005, 008 y 009 suman ventas en consultas agregadas; sin el total guardado todas tendrían que unirse con las devoluciones | Calcular siempre desde `SaleReturnLines`: más lento y más cambios en consultas que hoy son simples |
