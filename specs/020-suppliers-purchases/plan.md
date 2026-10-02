# Implementation Plan: Proveedores y compras básicas

**Branch**: `020-suppliers-purchases` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/020-suppliers-purchases/spec.md`

## Summary

Registrar de dónde viene la mercancía y a qué costo, con un histórico de compras para auditoría e
inventario.

1. **Catálogo de proveedores** (research §9): agregado `Supplier` con el molde de `Customer` (014):
   RUC opcional y único normalizado, búsqueda por nombre o RUC, condiciones de pago contado o crédito
   con días, activo/inactivo y sin borrado.
2. **Compra como agregado inmutable** (research §1, §4, §8): `Purchase` + `PurchaseLine` guardan
   subtotal, impuestos y total en centavos, el costo por línea y los nombres congelados de proveedor y
   producto. El importe de línea usa la misma regla de redondeo que la venta (`SaleMath.LineAmount`).
   Una línea a $0.00 es bonificación; subtotal $0.00 se rechaza.
3. **Inventario** (research §2, §3): tipos nuevos `PURCHASE` ("Entrada de compra") y `PURCH_VOID`
   ("Anulación de compra"), generados solo por `ProductStock.RecordPurchase` / `RecordPurchaseVoid`.
   Cada línea enlaza su movimiento (`MovementId`, `VoidMovementId`); `InventoryMovements` no cambia.
4. **Registro y anulación atómicos** (research §5–§7): `RegisterPurchase` y `VoidPurchase` en una
   transacción `BEGIN IMMEDIATE`. Factura única por proveedor entre compras vigentes (índice filtrado
   por estado). La anulación se rechaza si alguna existencia no alcanza o el producto está inactivo.
5. **Reporte "Compras"** (research §13): filtros por proveedor, fechas de factura y total; 100 por
   página; acumulados de todo el filtro solo con compras vigentes, en una consulta agregada.
6. **Permisos y licencia** (research §11): `ManageSuppliers`, `RegisterPurchases`, `VoidPurchases` y
   `ViewPurchaseReport`, solo Administrador, módulo `Inventory`.
7. **Bitácora** (research §14): 6 eventos nuevos y el grupo "Proveedores y compras".

Hay una migración nueva, `SuppliersAndPurchases`: 3 tablas nuevas, sin tocar tablas existentes. No se
agrega ninguna dependencia externa.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia, CommunityToolkit.Mvvm, Hosting, Serilog, EF Core 10
Sqlite, FluentValidation). **No se agrega ninguna.**

**Storage**:

- SQLite, migración `SuppliersAndPurchases`: tablas `Suppliers`, `Purchases`, `PurchaseLines`.
- Valores nuevos `PURCHASE` y `PURCH_VOID` en la columna existente `InventoryMovements.Type` (TEXT(12)).
- `Version` 0.13.0 → 0.14.0. Bases de ejemplo `v0.13.0.db` (falta en el repositorio, research §12) y
  `v0.14.0.db` según [docs/migraciones.md](../../docs/migraciones.md).

**Testing**: xUnit v3, con la política mínima de la constitución v1.2.0 (detalle en research §16):

- **Domain**: `PurchaseMath`, `Purchase` (registro y anulación), `ProductStock` con los tipos de compra,
  `Supplier`, `RolePermissions` y `ModuleAccess`.
- **Casos de uso sobre SQLite real**:
  - existencia exacta y producto sin cambios;
  - factura duplicada (mayúsculas, espacios, reutilización tras anular, concurrencia);
  - atomicidad con fallo inyectado y compra de 200 líneas;
  - anulación con existencia insuficiente y producto inactivo;
  - inmutabilidad;
  - acumulados del reporte;
  - RUC único;
  - consistencia de inventario (obligatoria).
- **Migración**: bases de ejemplo y SQL sin reconstrucciones.
- **Arquitectura**: sin reglas nuevas.
- Sin pruebas de ViewModels ni vistas.

**Target Platform**: Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Captura de 10 líneas en menos de 3 min (SC-003): buscador con teclado y totales en vivo sin base de
  datos (`CalculatePurchaseTotals`).
- Guardar una compra de 200 líneas en menos de 1 s: un solo `SaveChanges` en una transacción.
- Reporte con 10,000 compras en menos de 2 s (SC-005), objetivo < 300 ms por los índices de
  research §13; total de un proveedor en un mes en menos de 30 s de operación (SC-004).

**Constraints**:

- Funciona sin conexión.
- 0 advertencias.
- Registro y anulación son una transacción cada uno (FR-012, FR-017a, SC-006).
- La anulación nunca deja la existencia bajo cero (FR-017b).
- Registrar una compra no modifica el producto (FR-015, SC-002).
- No se reconstruye ninguna tabla en la migración.

**Scale/Scope**:

- Decenas a cientos de proveedores; decenas de compras por semana; hasta 10,000 compras en el reporte.
- Casos de uso:
  - **12 nuevos**:
    - proveedores: `CreateSupplier`, `UpdateSupplier`, `SetSupplierActive`, `SearchSuppliers`,
      `GetSupplier`, `ListSuppliersForPurchase`, `ListSuppliersForReport`;
    - compras: `CalculatePurchaseTotals`, `RegisterPurchase`, `VoidPurchase`, `GetPurchase`;
    - reporte: `GetPurchaseReport`;
    - (más el validador de `RegisterPurchase` y el de `CreateSupplier`/`UpdateSupplier`).
  - **Que cambian**: `SearchMovements` (proveedor y compra en el kárdex).
- Pantallas: lista y formulario de proveedores, "Entrada de mercancía", detalle de compra con diálogo
  de anulación, "Reportes > Compras" y etiquetas nuevas en el kárdex.

## Constitution Check

*GATE: debe pasar antes de la Fase 0. Se reevaluó después del diseño de la Fase 1.*

| Principio | Cumplimiento |
|---|---|
| I. La venta nunca se detiene | Todo es local. Registro y anulación son una sola transacción `BEGIN IMMEDIATE` cada uno, serializados con las ventas; la anulación lee la existencia ya afectada por cualquier venta. Un error inesperado se registra en Serilog y se muestra un mensaje sin detalles técnicos; la captura en pantalla se conserva (escenario 12). Las compras no tocan el flujo de venta. |
| II. Capas | `Supplier`, `Purchase`, `PurchaseLine`, `PurchaseMath` y los métodos nuevos de `ProductStock` viven en Domain. Casos de uso y puertos (`ISupplierRepository`, `IPurchaseRepository`, `IPurchaseReportReader`) en Application. Implementaciones EF Core en Infrastructure. Los ViewModels solo invocan casos de uso. Las carpetas nuevas `Suppliers/` y `Purchases/` quedan cubiertas por las pruebas de arquitectura. |
| III. Lógica en el núcleo | Importes, redondeo, subtotal, total, bonificación, factura duplicada y reglas de anulación viven en Domain y en los casos de uso. Los totales en vivo vienen de `CalculatePurchaseTotals`; la interfaz no suma. |
| IV. Integridad de datos | GUID v7, fechas en UTC (la fecha de factura es `DateOnly`, como los cupones de 015), importes en centavos y cantidades en milésimas. `Supplier` y `Purchase` tienen `Version`. Compras y movimientos no se borran; la anulación es un cambio de estado visible con movimientos inversos. Unicidad de RUC y de factura vigente por índice. Migración EF Core sin reconstrucciones, con SQL revisado y bases de ejemplo 0.13.0 y 0.14.0. Desviaciones justificadas en Complexity Tracking. |
| V. Multiplataforma | Sin código de plataforma. |
| VI. Calidad verificable | Solo se prueban cálculos de dinero e inventario, validaciones de integridad (RUC, factura, anulación, inmutabilidad, atomicidad, concurrencia) y las pruebas obligatorias de migración, consistencia de inventario y arquitectura, sobre SQLite real. |
| VII. Simplicidad | Sin dependencias nuevas. Sin órdenes de compra, cuentas por pagar, costo promedio ni exportación del reporte. Se reutilizan `ProductStock`, `SaleMath.LineAmount`, `ProductPickerView`, `SearchStock`, el patrón de `Customer` y la bitácora de 018. Repositorios específicos por agregado. |
| VIII. Soporte | Registro y anulación se escriben en Serilog con proveedor, factura, número de líneas, total y usuario; los rechazos de anulación con los productos afectados. `docs/compras.md` documenta el flujo, los tipos de movimiento y cómo corregir una compra. |
| IX. Seguridad local | Registrar y anular requieren permisos propios del Administrador (no autorizables). La bitácora guarda registro y anulación (con motivo) y los cambios de proveedores (FR-018). Las compras no se editan ni borran. |

**Resultado**: sin violaciones. Decisiones explícitas:

- **Códigos de movimiento de 12 caracteres (research §2)**: `PURCH_VOID` en lugar de `PURCHASE_VOID`
  para no reconstruir `InventoryMovements`.
- **Enlace en la línea de compra (research §3)**: `PurchaseLines.MovementId`/`VoidMovementId` en lugar
  de una columna en `InventoryMovements`, por la misma razón.
- **Límite de importes (research §4)**: línea, subtotal, impuestos y total ≤ $999,999.99
  (`Money.MaxCents`), igual que una venta.
- **Anulación no autorizable (research §7)**: un Cajero no puede pedir la contraseña del Administrador
  para anular; la anulación la hace el Administrador con su sesión (FR-026).
- **Sin borrado de proveedores (research §9)**: solo desactivación, que cumple FR-006 sin una regla de
  "tiene compras".
- **Base de ejemplo 0.13.0 faltante (research §12)**: se genera antes de la migración nueva.

## Project Structure

### Documentation (this feature)

```text
specs/020-suppliers-purchases/
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
│   ├── Suppliers/Supplier.cs  PaymentTerms.cs
│   ├── Purchases/Purchase.cs  PurchaseLine.cs  PurchaseStatus.cs  PurchaseMath.cs
│   ├── Inventory/MovementType.cs  ProductStock.cs                  # PURCHASE, PURCH_VOID, RecordPurchase(Void)
│   ├── Common/EmailAddress.cs                                     # regla compartida con Customer
│   ├── Customers/Customer.cs                                      # IsValidEmail delega en EmailAddress
│   ├── Licensing/ModuleAccess.cs                                  # permisos nuevos → Inventory
│   └── Users/Permission.cs                                        # 4 permisos nuevos
├── Pos.Application/
│   ├── Abstractions/Error.cs                                      # + SupplierTaxIdInUse, DuplicateInvoice, PurchaseVoidBlocked
│   ├── Audit/AuditActions.cs  AuditEntityGroup.cs                 # + SUPPLIER_*, PURCHASE_*; grupo Purchases
│   ├── Suppliers/
│   │   ├── ISupplierRepository.cs  SupplierDtos.cs  SupplierFields.cs  SupplierMessages.cs  SupplierAuditFields.cs
│   │   ├── CreateSupplier/  UpdateSupplier/  SetSupplierActive/  SearchSuppliers/  GetSupplier/
│   │   └── ListSuppliersForPurchase/  ListSuppliersForReport/
│   ├── Purchases/
│   │   ├── IPurchaseRepository.cs  PurchaseDtos.cs  PurchaseFields.cs  PurchaseMessages.cs
│   │   └── CalculatePurchaseTotals/  RegisterPurchase/  VoidPurchase/  GetPurchase/
│   ├── Reports/IPurchaseReportReader.cs  GetPurchaseReport/
│   ├── Inventory/IInventoryRepository.cs                          # MovementDto + PurchaseId, SupplierName
│   └── DependencyInjection.cs
├── Pos.Infrastructure/
│   ├── Suppliers/SupplierRepository.cs
│   ├── Purchases/PurchaseRepository.cs
│   ├── Reports/PurchaseReportReader.cs
│   ├── Inventory/InventoryRepository.cs                           # left joins a PurchaseLines/Purchases
│   ├── Persistence/Configurations/SupplierConfiguration.cs  PurchaseConfiguration.cs  PurchaseLineConfiguration.cs
│   ├── Persistence/PosDbContext.cs                                # DbSets; RejectImmutableChanges para compras
│   ├── Persistence/Migrations/…_SuppliersAndPurchases.cs
│   └── DependencyInjection.cs
├── Pos.Desktop/
│   ├── Purchases/PurchasesModule.cs
│   │   SupplierListView*  SupplierListViewModel.cs  SupplierFormViewModel.cs
│   │   PurchaseEntryView*  PurchaseEntryViewModel.cs  PurchaseLineViewModel.cs
│   │   PurchaseDetailView*  PurchaseDetailViewModel.cs  VoidPurchaseView*  VoidPurchaseViewModel.cs
│   ├── Reports/PurchaseReportView*  PurchaseReportViewModel.cs  ReportsModule.cs
│   ├── Inventory/MovementTypeLabels.cs  MovementsView*  MovementsViewModel.cs   # tipos nuevos, referencia y enlace
│   ├── Resources/Strings.resx                                     # Supplier_*, Purchase_*, Nav_*
│   └── Composition/HostBuilder.cs                                 # + AddPurchasesModule
tests/
├── Pos.Domain.Tests/Purchases/        PurchaseMathTests, PurchaseTests
├── Pos.Domain.Tests/Suppliers/        SupplierTests
├── Pos.Domain.Tests/Inventory/        ProductStockTests (ampliada)
├── Pos.Domain.Tests/Users/            RolePermissionsTests (ampliada)
├── Pos.Infrastructure.Tests/Purchases/  RegisterPurchaseTests, PurchaseAtomicityTests, PurchaseConcurrencyTests,
│                                        VoidPurchaseTests, PurchaseImmutabilityTests, PurchaseReportTests,
│                                        PurchaseReportPerformanceTests (explícita)
├── Pos.Infrastructure.Tests/Suppliers/  SupplierUseCaseTests
├── Pos.Infrastructure.Tests/Inventory/  InventoryConsistencyTests (ampliada con compras y anulaciones)
└── Pos.Infrastructure.Tests/SampleDatabases/  v0.13.0.db, v0.14.0.db, SuppliersAndPurchasesMigrationTests, SampleDatabaseUpgradeTests
docs/
├── compras.md                         # guía de soporte: proveedores, captura, bonificaciones, anulación, kárdex, reporte
├── migraciones.md                     # + sección 0.14.0 (sin reconstrucciones)
├── usuarios-y-permisos.md  auditoria.md   # permisos y eventos nuevos
```

**Structure Decision**: se mantiene la estructura por capas y por funcionalidad de 001 a 018.

- `Suppliers` y `Purchases` son carpetas nuevas en Domain, Application, Infrastructure y pruebas. En
  Desktop una sola carpeta `Purchases` contiene proveedores y compras, porque ambas pantallas cuelgan
  del grupo "Inventario".
- `PurchasesModule` registra "Entrada de mercancía" (orden 20) y "Proveedores" (orden 30) en el grupo
  `InventoryModule.GroupId`. "Reportes > Compras" se agrega en `ReportsModule` con orden 25.
- Las pruebas existentes que enumeran `MovementType` (`InventoryConsistencyTests`) o permisos
  (`RolePermissionsTests`) se ajustan a los valores nuevos.

## Complexity Tracking

| Desviación | Por qué se necesita | Alternativa más simple descartada |
|---|---|---|
| `PurchaseLine` sin `UpdatedAt/By`, `DeletedAt` ni `Version` | Registro contable inmutable; solo `VoidMovementId` pasa de nulo a un valor dentro de la transacción de anulación, protegida por `RejectImmutableChanges`. Mismo precedente que `SaleLine`, `InventoryMovement` y `ReceivableEntry` | Agregar los campos estándar: sugiere que la línea se puede editar o borrar, contra FR-017 |
| `Purchase` sin `DeletedAt` | Una compra nunca se borra; se anula (FR-017) | Borrado lógico: un estado que nunca debe ocurrir |
| `SubtotalCents`, `TotalCents` y `LineCount` derivados y guardados | El reporte filtra por total y acumula sobre 10,000 compras con índice (SC-005); la compra es inmutable, así que no pueden desincronizarse. Una prueba compara contra la suma de líneas. Mismo precedente que `Sales.TotalCents` | Calcular desde las líneas en cada consulta: agrupar todas las líneas en cada página y en los acumulados |
| `SupplierName`, `ProductName`, `ProductSku` y `UnitCode` copiados | FR-016 exige mostrar los datos tal como estaban al registrar. Mismo criterio que `SaleLine` y `Receivable.CustomerName` | Leer siempre los datos actuales: el detalle cambiaría al renombrar |
| Importes de la compra como `long` en centavos, sin `Money` en sumas internas (Principio IV) | Sumas enteras como en `SaleMath` y `ShiftSalesTotals`; `Money` se usa al interpretar la captura y en los límites | `Money` en todos los acumulados: más conversiones sin protección adicional |
