# Implementation Plan: Gestión de clientes y crédito

**Branch**: `014-customers-credit` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-customers-credit/spec.md`

## Summary

Vender a crédito a clientes registrados con un límite controlado, cobrar con abonos que generan
recibo, y saber quién debe, cuánto y desde cuándo.

1. **Catálogo de clientes** (research §10): agregado `Customer` con RUC único y búsqueda normalizada.
   Un Cajero solo crea clientes "solo efectivo" con límite 0.
2. **Venta a crédito = forma de pago `ACCOUNT`** (research §1–§2): un único pago por el 100 % del total.
   Usa el flujo normal de `ConfirmSale` (inventario, ticket, idempotencia). Crea una **cuenta por
   cobrar** (`Receivable`) en la misma transacción. `Sales` no cambia.
3. **Límite** (research §4): `CreditPolicy` (`saldo + total ≤ límite`) se evalúa dentro de la
   transacción. Si se excede, un Cajero necesita la autorización de un Administrador
   (`ApproveCreditOverLimit`), y se registra en la bitácora.
4. **Saldo exacto** (research §3): `Receivable.BalanceCents` se guarda junto a un libro inmutable
   `ReceivableEntry`. El saldo del cliente se calcula con una sola consulta.
5. **Abonos** (research §5–§7):
   - Se aplican FIFO (`PaymentAllocator`), siempre dentro de un turno, y son idempotentes por
     `RequestId`.
   - Llevan folio `AB-000001` y recibo imprimible.
   - Solo se anulan con autorización, motivo y turno abierto.
   - El efectivo entra al esperado del turno y el corte muestra un bloque "Crédito".
6. **Devoluciones de ventas a crédito** (research §8): `CreditReturnSettlement` reduce el saldo, reaplica
   el excedente FIFO y reintegra en efectivo lo que sobra, dentro de la transacción de 013.
7. **Reporte "Créditos"** (research §13) con plazo de pago configurable (research §9).
8. **Permisos y licencia** (research §11): 7 permisos nuevos del módulo `CreditAndCustomers`.

Hay una migración nueva, `CustomersAndCredit`: 4 tablas y 5 columnas en `CashShifts`, sin reconstruir
ninguna tabla. No se agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm, Hosting, Serilog,
EF Core 10 Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `CustomersAndCredit`:
  - Tablas `Customers`, `Receivables`, `ReceivableEntries`, `CustomerPayments`.
  - Columnas `CashShifts.OnAccountSalesCents`, `CustomerPaymentsCashCents`,
    `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents`, `CustomerPaymentVoidsNonCashCents`.
- Plazo de pago en preferencias locales (`IPreferencesStore`), sin tabla.
- `Version` 0.8.0 → 0.9.0. Base de ejemplo nueva según [docs/migraciones.md](../../docs/migraciones.md).

**Testing**: xUnit v3, con la política mínima de la constitución v1.2.0 (detalle en research §15):

- **Domain**: `CreditPolicy`, `PaymentAllocator`, `Receivable`, `CustomerPayment.Void`,
  `CreditReturnSettlement`, `CashShiftMath` con abonos, validaciones de `Customer`, `RolePermissions` y
  `ModuleAccess`.
- **Casos de uso sobre SQLite real**:
  - límite con y sin concesión;
  - atomicidad;
  - idempotencia y concurrencia de abonos;
  - anulación;
  - cancelación con excedente;
  - RUC único;
  - total del reporte;
  - saldo igual al libro;
  - consistencia de inventario (obligatoria).
- **Migración**: bases de ejemplo y SQL sin reconstrucciones.
- **Arquitectura**: sin cambios en las reglas.
- Sin pruebas de ViewModels, vistas ni del recibo.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Alta de cliente en menos de 1 min y búsqueda en menos de 5 s de operación (SC-001); la consulta tarda
  menos de 200 ms con 10 000 clientes.
- La venta a crédito agrega al `ConfirmSale` una suma por índice y una inserción: menos de 30 ms
  adicionales (SC-002).
- Un abono con su reparto FIFO guarda en menos de 100 ms con 500 cuentas pendientes del cliente
  (SC-005).
- El reporte "Créditos" tarda menos de 500 ms con 10 000 clientes y 100 000 cuentas, gracias al índice
  (`CustomerId`, `Status`).

**Constraints**:

- Funciona sin conexión (FR-021).
- 0 advertencias.
- Venta a crédito, abono, anulación y devolución de venta a crédito son una transacción cada una
  (FR-019).
- Nunca se revela el efectivo esperado al rechazar una anulación (regla de 008).
- Una falla de impresión no revierte el abono.
- No se reconstruye ninguna tabla en la migración.

**Scale/Scope**:

- Decenas a miles de clientes; decenas de ventas a crédito y abonos por día.
- Casos de uso:
  - **14 nuevos**:
    - clientes: `CreateCustomer`, `UpdateCustomer`, `SetCustomerActive`, `SearchCustomers`,
      `GetCustomer`, `FindCustomersForSale`, `GetCustomerCreditStatus`;
    - abonos y cuentas: `RegisterCustomerPayment`, `VoidCustomerPayment`, `ListCustomerPayments`,
      `ListCustomerReceivables`;
    - configuración y reporte: `Get/SaveReceivablesSettings`, `GetReceivablesReport`.
  - **Que cambian**: `ConfirmSale`, `SaleReturnProcessor`/`PreviewReturn`/`CancelSale`, `GetSale`,
    `SearchSales`, `PrintTicket`, los de turno y corte, y los reportes de 009.
- Pantallas:
  - lista y ficha de clientes, con sus diálogos de abono y anulación;
  - selector de cliente y pago "Venta a crédito" en el cobro;
  - "Reportes > Créditos".

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. La venta a crédito, el abono, la anulación y la devolución a crédito son una sola transacción `BEGIN IMMEDIATE` cada una. El límite se verifica dentro de la transacción. Un error inesperado se registra y se muestra un mensaje sin detalles técnicos. Una falla de impresión no revierte nada. El cliente elegido no se guarda en el borrador; si se pierde, se vuelve a elegir y la venta en curso se conserva. |
| II. Capas | `Customer`, `Receivable`, `ReceivableEntry`, `CustomerPayment`, `CreditPolicy`, `PaymentAllocator` y `CreditReturnSettlement` viven en Domain. Los casos de uso y los puertos (`ICustomerRepository`, `IReceivableRepository`, `ICustomerPaymentRepository`, `IReceivablesReportReader`, `IReceivablesSettingsStore`) viven en Application. Las implementaciones EF Core viven en Infrastructure. Los ViewModels solo invocan casos de uso. Las carpetas nuevas `Customers/` y `Receivables/` quedan cubiertas por las pruebas de arquitectura. |
| III. Lógica en el núcleo | Límite, excedente, reparto FIFO, liquidación de devoluciones, días vencido y esperado de caja viven en Domain. Disponible, aviso de vencido y vista previa de devolución los calculan casos de uso; la interfaz solo los muestra. |
| IV. Integridad de datos | GUID v7, fechas UTC e importes en centavos. `Customer` y `Receivable` tienen `Version`. El libro `ReceivableEntry` y los abonos no se borran, y la anulación es un cambio de estado visible. RUC, folio y `RequestId` son únicos por índice. La migración es de EF Core, sin reconstrucciones, con SQL revisado y base de ejemplo 0.9.0. Hay desviaciones justificadas en Complexity Tracking. |
| V. Multiplataforma | Sin código de plataforma. El recibo usa los adaptadores de impresión de 006. |
| VI. Calidad verificable | Solo se prueban cálculos de dinero (límite, FIFO, liquidación, esperado y saldo contra el libro), validaciones de integridad (RUC, límite, abono > saldo, anulación, idempotencia y concurrencia) y las pruebas obligatorias de migración, inventario y arquitectura. La persistencia se prueba sobre SQLite real. |
| VII. Simplicidad | Sin dependencias nuevas. Se reutiliza `PaymentMethod`, `SaleReturnProcessor`, `ReturnCashGate`, la autorización de 007 y las preferencias. Sin tabla de configuración, sin saldo a favor, sin intereses y sin columna de cliente en `Sales`. Los repositorios son específicos por agregado. |
| VIII. Soporte | Cada venta a crédito, abono, anulación y rechazo se registra en Serilog con cliente, venta, folio, usuario y montos (nunca la contraseña). `docs/clientes-y-credito.md` documenta el saldo, el libro, FIFO, las devoluciones y la anulación. El recibo se reimprime. |
| IX. Seguridad local | El exceso de límite y la anulación exigen la contraseña de un Administrador. La bitácora guarda cajero, autorizador, cliente, venta, saldo previo, límite y monto, además de los intentos fallidos (FR-007, FR-014). Los abonos no se editan ni borran. El Cajero no asigna crédito ni ve el reporte. |

**Resultado**: sin violaciones. Decisiones explícitas, ya incorporadas a la especificación:

- **Anulación de abonos tras una devolución (research §7, spec "Casos límite" y FR-014)**: un abono
  aplicado a una venta que **después** se canceló o devolvió no se puede anular.
- **Abonos sin licencia de Turnos (research §5, FR-012 y FR-014)**: con el módulo Turnos sin licencia,
  los abonos se registran y se anulan sin turno, igual que las ventas (012).
- **Sin licencia de Crédito y clientes (research §11, spec "Casos límite")**: no se registran ni anulan
  abonos; las devoluciones de ventas a crédito siguen ajustando el saldo.
- **Plazo de pago (research §9)**: el vencimiento se calcula con el plazo **actual** y no se guarda por
  venta. Un cambio de plazo afecta a todas las cuentas.
- **Administrador que vende sobre el límite (research §4, FR-006)**: no se le pide su contraseña otra
  vez (como `CancelSales` en 007) y la bitácora lo anota como autorizador. En cambio, la anulación de
  abonos siempre la pide (como `ApproveReturns` en 013).

## Project Structure

### Documentation (this feature)

```text
specs/014-customers-credit/
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
│   ├── Customers/
│   │   ├── Customer.cs  CreditMode.cs  CreditPolicy.cs               # validación, crédito, desactivación
│   ├── Receivables/
│   │   ├── Receivable.cs  ReceivableStatus.cs  ReceivableEntry.cs  ReceivableEntryType.cs
│   │   ├── CustomerPayment.cs  CustomerPaymentStatus.cs  CustomerPaymentFolio.cs
│   │   ├── PaymentAllocator.cs  CreditReturnSettlement.cs  ReceivableAging.cs
│   ├── Sales/PaymentMethod.cs  Sale.cs  Checkout.cs                 # + ACCOUNT exclusivo, SetOnAccount
│   ├── Returns/RefundStatus.cs  SaleReturnRefund.cs                  # + SETTLED
│   ├── CashShifts/ShiftSalesTotals.cs  CashShiftMath.cs  CashShift.cs  # abonos en esperado e instantánea
│   ├── Licensing/ModuleAccess.cs                                     # permisos → CreditAndCustomers
│   └── Users/Permission.cs  RolePermissions.cs                       # 7 permisos nuevos
├── Pos.Application/
│   ├── Abstractions/Error.cs                                         # + CreditLimitExceeded, CustomerHasBalance, …
│   ├── Audit/AuditActions.cs                                         # + CUSTOMER_*, CREDIT_*, CUSTOMER_PAYMENT_*
│   ├── Customers/
│   │   ├── ICustomerRepository.cs  CustomerDtos.cs  CustomerFields.cs  CustomerMessages.cs
│   │   ├── CreateCustomer/  UpdateCustomer/  SetCustomerActive/  SearchCustomers/  GetCustomer/
│   │   └── FindCustomersForSale/  GetCustomerCreditStatus/
│   ├── Receivables/
│   │   ├── IReceivableRepository.cs  ICustomerPaymentRepository.cs  IReceivablesSettingsStore.cs
│   │   ├── ReceivablesSettings.cs  ReceivableDtos.cs  CreditSettlementService.cs   # usado por devoluciones
│   │   ├── RegisterCustomerPayment/  VoidCustomerPayment/  ListCustomerPayments/  ListCustomerReceivables/
│   │   └── GetReceivablesSettings/  SaveReceivablesSettings/
│   ├── Sales/ConfirmSale/                                            # + cliente, límite, Receivable
│   ├── Returns/SaleReturnProcessor.cs  ReturnPlan.cs  PreviewReturn/  # + liquidación a crédito
│   ├── Sales/CancelSale/CancelSaleHandler.cs                         # ruta sin licencia de Devoluciones
│   ├── Reports/IReceivablesReportReader.cs  GetReceivablesReport/
│   ├── Printing/PrintTicket/  Printing/Ticket/CustomerPaymentReceiptBuilder.cs  TicketBuilder.cs
│   ├── CashShifts/…                                                  # esperado, instantánea y corte
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Customers/CustomerRepository.cs
│   ├── Receivables/ReceivableRepository.cs  CustomerPaymentRepository.cs  PreferencesReceivablesSettingsStore.cs
│   ├── Reports/ReceivablesReportReader.cs  SalesReportReader.cs      # etiqueta ACCOUNT
│   ├── Sales/SaleRepository.cs                                       # totales del turno, CreditInfo, filtro por cliente
│   ├── Persistence/Configurations/Customer*  Receivable*  ReceivableEntry*  CustomerPayment*  (+ CashShift)
│   ├── Persistence/PosDbContext.cs                                   # DbSets; RejectImmutableChanges para el libro y los abonos
│   ├── Persistence/Migrations/…_CustomersAndCredit.cs
│   └── DependencyInjection.cs
├── Pos.Desktop/
│   ├── Customers/CustomersModule.cs  CustomerListView*  CustomerListViewModel.cs
│   │   CustomerDetailView*  CustomerDetailViewModel.cs  CustomerFormViewModel.cs
│   │   RegisterPaymentView*  RegisterPaymentViewModel.cs  VoidPaymentView*  VoidPaymentViewModel.cs
│   ├── Sales/PointOfSaleView*  CheckoutView*  CustomerPickerView*  PaymentMethodLabels.cs  # venta a crédito
│   ├── Sales/SaleDetailView*  ReturnSaleView*  SalesHistoryViewModel.cs  # bloque Crédito, vista previa, estado
│   ├── Reports/ReceivablesReportView*  ReceivablesReportViewModel.cs  ReportsModule.cs
│   ├── CashShifts/ShiftDetailView*  Reports/MyShiftView*             # bloque Crédito
│   ├── Resources/Strings.resx                                        # Customer_*, Credit_*, Payment_*, Nav_Customers
│   └── Composition/HostBuilder.cs                                    # + AddCustomersModule
tests/
├── Pos.Domain.Tests/Customers/        CustomerTests, CreditPolicyTests
├── Pos.Domain.Tests/Receivables/      PaymentAllocatorTests, ReceivableTests, CustomerPaymentTests, CreditReturnSettlementTests
├── Pos.Domain.Tests/CashShifts/       CashShiftMathTests (ampliada)
├── Pos.Domain.Tests/Users/            RolePermissionsTests (actualizada)
├── Pos.Infrastructure.Tests/Customers/     CustomerUseCaseTests
├── Pos.Infrastructure.Tests/Receivables/  CreditSaleUseCaseTests, CustomerPaymentUseCaseTests, CreditReturnTests, ReceivablesReportTests
├── Pos.Infrastructure.Tests/Inventory/    InventoryConsistency (ampliada con venta a crédito)
└── Pos.Infrastructure.Tests/SampleDatabases/  v0.9.0.db, CustomersMigrationTests, SampleDatabaseUpgradeTests
docs/
├── clientes-y-credito.md              # guía de soporte: saldo y libro, FIFO, anulación, devoluciones, plazo
├── migraciones.md                     # + sección 0.9.0 (sin reconstrucciones)
├── ventas.md  turnos-de-caja.md  devoluciones.md  usuarios-y-permisos.md   # venta a crédito, corte, permisos
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 013.

- `Customers` y `Receivables` son carpetas nuevas en Domain, Application, Infrastructure y pruebas.
  `Customers` también en Desktop, donde contiene las pantallas de abonos, porque la navegación es
  "Clientes > [cliente] > Abonos".
- El grupo de navegación "Clientes" se registra con orden 6 desde `CustomersModule`.
  "Reportes > Créditos" se agrega en `ReportsModule` con orden 30.
- `CreditSettlementService` (Application/Receivables) es el punto único que ajusta cuentas por una
  devolución. Lo usan `SaleReturnProcessor` y la ruta heredada de `CancelSale`.
- Las pruebas existentes que construyen `ShiftSalesTotals`, `PaymentMethod` o `RolePermissions` se
  ajustan a las firmas nuevas.

## Complexity Tracking

| Desviación | Por qué se necesita | Alternativa más simple descartada |
|---|---|---|
| `ReceivableEntry` y `CustomerPayment` sin `DeletedAt`, `UpdatedAt/By` ni `Version` | Son registros contables inmutables. El abono solo admite la transición `ACTIVE → VOIDED`, protegida por la transacción serializada y la verificación de estado (FR-014). Mismo precedente que `SaleReturn`, `CreditNoteMovement` y `CashMovement` | Agregar los campos estándar: sugiere que se pueden editar o borrar, contra el escenario 6 de la Historia 3 |
| `Receivable` sin `DeletedAt` | Una cuenta por cobrar nunca se borra; se cancela | Borrado lógico: un estado que nunca debe ocurrir |
| `Receivable.BalanceCents` derivado y guardado | El orden FIFO, el saldo del cliente y el reporte necesitan el saldo por venta con un índice. Una prueba compara el saldo contra el libro (SC-004). Mismo precedente que `Sales.ReturnedCents` | Calcular siempre desde el libro: agrupar todo el libro en cada abono, venta y reporte |
| Importes del dominio de crédito como `long` en centavos, sin `Money` en los acumulados (Principio IV) | Son sumas y repartos con enteros, como `ShiftSalesTotals`, `CashShiftMath` y `ReturnMath`. `Money` se usa en las entradas de los casos de uso | `Money` en todo el libro: más conversiones sin protección adicional |
| `CustomerName` copiado en `Receivable` | El ticket reimpreso debe mostrar el cliente de la venta, aunque después se edite su nombre. Mismo criterio que `SaleLine` con el producto | Leer siempre el nombre actual: el ticket reimpreso no coincidiría con el original |
