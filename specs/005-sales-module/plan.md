# Implementation Plan: Módulo de ventas

**Branch**: `005-sales-module` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-sales-module/spec.md`

## Summary

Se agrega el flujo de venta completo: captura, cobro, registro atómico con descuento de
inventario, consulta, cancelación y datos reales en Inicio.

1. **Venta en curso en el dominio**:
   - `Cart` calcula líneas, importes y total en memoria. `SaleMath.LineAmount` redondea "mitad
     hacia arriba" una vez por línea.
   - `Checkout` aplica las reglas de pago: un pago en efectivo que da cambio, y tarjeta o
     transferencia que nunca exceden el pendiente.
   - El ViewModel solo presenta (research §1, §2 y §6).
2. **Borrador durable**:
   - Una fila en la tabla `SaleDrafts` de SQLite, que se guarda en segundo plano después de cada
     cambio.
   - Se borra en la misma transacción que registra la venta. Así, un cierre inesperado nunca deja
     la venta a medias ni duplicada (research §7).
3. **Registro atómico**:
   - `ConfirmSale` corre en una transacción `BEGIN IMMEDIATE`: folio `MAX + 1` con índice único,
     venta, líneas con copia de datos, pagos, movimientos `SALE` y borrado del borrador.
   - La idempotencia viene del `DraftId` con índice único (research §4).
   - Antes de cobrar, `ReviewSale` actualiza precios, señala las líneas no vendibles y advierte la
     falta de existencia (research §5).
4. **Existencia negativa**: nuevo `StockLevel` con signo. Solo las ventas pueden dejarla bajo cero,
   y "sin existencia" pasa a ser `<= 0` (research §3).
5. **Consulta y cancelación**:
   - "Ventas realizadas" con filtros y paginación.
   - La cancelación regresa exactamente los movimientos que generó la venta (`SALE_CANCEL`) y
     escribe en la nueva bitácora `AuditEntries`, todo en una transacción (research §9 y §10).
6. **Inicio**: las tres tarjetas de ventas usan `GetSalesDashboard` con ventanas de día local en
   UTC. Las gráficas se dibujan con controles de Avalonia (research §11).

Hay una migración nueva (`SalesModule`) que solo crea tablas, y ninguna dependencia externa nueva.

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4,
Microsoft.Extensions.Hosting, Serilog, EF Core 10 Sqlite, FluentValidation y SkiaSharp). No se
agrega ninguna; las gráficas se dibujan con controles propios (research §11).

**Storage**: SQLite existente, en modo WAL.

- Tablas nuevas: `Sales`, `SaleLines`, `SalePayments`, `SaleDrafts` y `AuditEntries`.
- Códigos nuevos en `InventoryMovements.Type`: `SALE` y `SALE_CANCEL`, sin cambio de esquema.
- `ProductStocks.OnHand` puede ser negativo; ya es `INTEGER` con signo.
- Sin reconstrucción de tablas (research §13).

**Testing**: xUnit v3.

- Dominio sin dependencias: `SaleMath`, `Cart`, `Checkout`, `Sale` y `StockLevel`/`ProductStock`.
- SQLite real para consistencia de inventario, atomicidad, folio, idempotencia, borrador, Inicio,
  rendimiento y migración.
- Sin pruebas de ViewModels (constitución v1.2.0).

**Target Platform**: escritorio Windows 10+ y Linux (X11 o Wayland), con teclado, lector de
códigos tipo teclado y pantalla táctil.

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**:

- Agregar un producto por código actualiza la pantalla en menos de 0.5 s.
- Confirmar una venta de 50 líneas y ver el folio tarda menos de 2 s (SC-002).
- Una página de 100 ventas con 50,000 ventas tarda menos de 2 s.

**Constraints**:

- Funciona sin conexión.
- 0 advertencias.
- Dinero exacto al centavo.
- La venta, sus líneas, sus pagos, los movimientos y el borrado del borrador van en una sola
  transacción.
- Una falla no consume folio.
- Nunca se pierde la venta en curso.
- Movimientos y bitácora inmutables.
- Las ventas nunca se borran.

**Scale/Scope**:

- Cientos de ventas por día, alrededor de 50,000 ventas por año y hasta 50 líneas por venta.
- Una terminal y una venta en curso por instalación.
- 2 pantallas nuevas (Punto de venta y Ventas realizadas), 1 diálogo de cobro, 1 detalle con
  cancelación y 3 tarjetas de Inicio activadas.
- Cambios menores en Existencias (estado con negativos) y Movimientos (tipos nuevos).

No queda ningún NEEDS CLARIFICATION.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitución v1.2.0.

| Principio o regla | Cómo lo cumple el diseño | Antes | Después |
|---|---|---|---|
| I. Venta sin conexión | Nada depende de la red; tarjeta y transferencia solo se registran | ✅ | ✅ |
| I. Transacción única | `ConfirmSale` y `CancelSale` corren en una transacción `BEGIN IMMEDIATE`, con prueba de atomicidad (research §4 y §9) | ✅ | ✅ |
| I. Errores no pierden la venta | El borrador está en SQLite y el carrito sigue en memoria después de un error. Todo pasa por `OperationRunner` con mensaje comprensible. Un error al guardar el borrador se registra y no interrumpe (research §7) | ✅ | ✅ |
| I. Respaldos | La migración usa el flujo de respaldo y restauración existente | ✅ | ✅ |
| II. Capas | Hay puertos nuevos en Application (`ISaleRepository`, `ISaleDraftStore` e `IAuditLog`) que implementa Infrastructure. Desktop usa casos de uso y objetos de dominio puros (`Cart` y `Checkout`), igual que en 004. Las pruebas de arquitectura siguen aplicando | ✅ | ✅ |
| II. Organización por funcionalidad | `Domain/Sales`, `Application/Sales/{FindProductsForSale,SaveSaleDraft,GetSaleDraft,DiscardSaleDraft,ReviewSale,ConfirmSale,SearchSales,GetSale,CancelSale,GetSalesDashboard}`, `Infrastructure/Sales` y `Desktop/Sales` | ✅ | ✅ |
| III. Lógica en el núcleo | Importes, total, cambio, faltante, reglas de pago, existencia negativa, estados de la venta y folio viven en Domain y Application. El ViewModel no calcula | ✅ | ✅ |
| IV. GUID v7 y UTC | Todos los ids son v7. Las fechas están en UTC y los filtros de día local se convierten en Desktop | ✅ | ✅ |
| IV. Auditoría completa | `Sale` la tiene completa salvo `DeletedAt`. `SaleLine`, `SalePayment`, `AuditEntry` y `SaleDraft` llevan un subconjunto | ⚠️ | ⚠️ Justificada (Complexity Tracking) |
| IV. No borrar registros históricos | Las ventas se cancelan, no se borran. Los movimientos y la bitácora tienen un guardián de inmutabilidad y su prueba | ✅ | ✅ |
| IV. Dinero en centavos | Se usa `Money` en todo. Las columnas `*Cents` son `INTEGER`. El redondeo está documentado (research §2) | ✅ | ✅ |
| IV. Code First y migraciones | Migración nueva `SalesModule` que solo crea tablas. Se revisa el SQL (research §13) | ✅ | ✅ |
| IV. Base de ejemplo por versión | Se genera `v0.4.0.db` (versión 0.4.0) y la prueba migra v0.1.0 a v0.4.0 | ✅ | ✅ |
| IV. Catálogos fijos con `HasData` | No hay catálogos nuevos: formas de pago y tipos son enums con código de texto | ✅ | ✅ |
| V. Multiplataforma | El lector se trata como teclado. No hay código de plataforma ni rutas nuevas | ✅ | ✅ |
| VI. Build sin advertencias | Sí | ✅ | ✅ |
| VI. Pruebas mínimas | Solo reglas con cálculo (`SaleMath`, `Cart`, `Checkout`, `StockLevel`), validaciones de integridad (cancelación, idempotencia, folio) y las obligatorias: consistencia de inventario, migración y arquitectura (quickstart §2) | ✅ | ✅ |
| VI. Nunca el proveedor InMemory de EF | Todas las pruebas de persistencia usan SQLite real | ✅ | ✅ |
| VII. YAGNI | Una venta en curso, sin ventas en espera, descuentos, impuestos, roles ni pantalla de bitácora. Sin librería de gráficas. Folio con `MAX + 1` en lugar de una tabla contador | ✅ | ✅ |
| VII. Sin repositorios genéricos ni MediatR | `ISaleRepository` e `ISaleDraftStore` son específicos; los casos de uso son clases simples | ✅ | ✅ |
| VIII. Diagnóstico | Se registran en el log la venta (`SaleId`, `Folio`, líneas, total), los errores de confirmación (`DraftId`, líneas), la cancelación y los fallos del borrador. Sin datos sensibles (las referencias de pago no se registran en el log) | ✅ | ✅ |
| IX. Seguridad local | La cancelación de ventas queda en `AuditEntries` con usuario, fecha y motivo. Usuario de sistema hasta que exista autenticación (FR-030) | ✅ | ✅ |
| Restricciones técnicas | FluentValidation para la forma de los comandos, Serilog, xUnit y NetArchTest. Código en inglés y textos en español | ✅ | ✅ |
| Flujo de desarrollo | Especificación y clarificación hechas; este plan; tareas a continuación | ✅ | ✅ |

**Resultado**: pasa, con desviaciones justificadas en Complexity Tracking.

**Revisión posterior al diseño (Phase 1)**: data-model y contratos no introducen violaciones
nuevas. La liga entre venta y movimiento vive en `SaleLines` para no reconstruir
`InventoryMovements`.

## Project Structure

### Documentation (this feature)

```text
specs/005-sales-module/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── use-cases.md     # casos de uso, errores y puertos de Sales
│   └── ui.md            # Punto de venta, cobro, Ventas realizadas, detalle, Inicio, atajos
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

Archivos nuevos (➕) y modificados (✏️):

```text
src/Pos.Domain/
├── Inventory/StockLevel.cs                   ➕ existencia con signo
├── Inventory/ProductStock.cs                 ✏️ OnHand: StockLevel; RecordSale, RecordSaleCancellation, IsShort
├── Inventory/InventoryMovement.cs            ✏️ ResultingStock: StockLevel
├── Inventory/MovementType.cs                 ✏️ Sale (SALE), SaleCancellation (SALE_CANCEL)
├── Inventory/StockStatus.cs                  ✏️ <= 0 → Out
├── Audit/AuditEntry.cs                       ➕ bitácora inmutable
└── Sales/                                    ➕
    ├── SaleDraft.cs                          fila única del borrador
    ├── SaleMath.cs                           LineAmount (mitad hacia arriba)
    ├── Cart.cs, CartLine.cs                  venta en curso
    ├── Checkout.cs, PaymentMethod.cs         reglas de pago
    ├── Sale.cs, SaleLine.cs, SalePayment.cs  agregado; Register, Cancel
    ├── SaleStatus.cs                         COMPLETED / CANCELLED
    └── Folio.cs                              formato y lectura de V-000123

src/Pos.Application/
├── Abstractions/Error.cs                     ✏️ SaleChanged, AlreadyRegistered, InvalidState
├── Abstractions/IAuditLog.cs                 ➕
├── Inventory/…                               ✏️ StockLevel en DTOs y mensajes; el validador excluye tipos de venta
├── Products/IProductRepository.cs            ✏️ FindForSale, SearchForSale, GetMany
├── Sales/                                    ➕
│   ├── ISaleRepository.cs, ISaleDraftStore.cs, SaleDtos.cs, SaleMessages.cs, SaleFields.cs
│   ├── FindProductsForSale/  SaveSaleDraft/  GetSaleDraft/  DiscardSaleDraft/
│   ├── ReviewSale/  ConfirmSale/  SearchSales/  GetSale/  CancelSale/
│   └── GetSalesDashboard/
└── DependencyInjection.cs                    ✏️

src/Pos.Infrastructure/
├── Persistence/Configurations/               ➕ Sale, SaleLine, SalePayment, SaleDraft, AuditEntry
├── Persistence/PosDbContext.cs               ✏️ DbSets + guardián de inmutabilidad de AuditEntries
├── Persistence/Migrations/…_SalesModule.cs   ➕ solo CREATE TABLE / INDEX
├── Sales/SaleRepository.cs                   ➕ búsqueda, detalle, dashboard, MAX+1
├── Sales/SqliteSaleDraftStore.cs             ➕ upsert de la fila única
├── Audit/AuditLog.cs                         ➕
├── Inventory/InventoryRepository.cs          ✏️ predicado <= 0; GetStocksAsync
├── Products/ProductRepository.cs             ✏️ búsquedas para venta
└── DependencyInjection.cs                    ✏️

src/Pos.Desktop/
├── Sales/                                    ➕ SalesModule, PointOfSaleView(Model), CheckoutView(Model),
│                                                ProductChooserView(Model), SalesHistoryView(Model),
│                                                SaleDetailView(Model), CancelSaleView(Model),
│                                                DraftAutosaver, ScanQueue, SalesCards, PaymentMethodLabels
├── Home/ChartCard.cs, HomeView.axaml         ➕/✏️ barras
├── Home/HomeModule.cs, Home/Cards/ComingSoonCard.cs ✏️ se retiran los placeholders de ventas
├── Shell/MainWindow.axaml                    ✏️ KeyBinding F9
├── Inventory/MovementTypeLabels.cs           ✏️ "Salida por venta", "Cancelación de venta"
├── Composition/HostBuilder.cs                ✏️ AddSalesModule
└── Resources/Strings.resx, Icons.axaml       ✏️

tests/
├── Pos.Domain.Tests/Sales/                   ➕ SaleMathTests, CartTests, CheckoutTests, SaleTests
├── Pos.Domain.Tests/Inventory/               ✏️ ProductStockTests (ventas y negativos), StockStatusRuleTests
├── Pos.Infrastructure.Tests/Sales/           ➕ consistencia, atomicidad, folio, idempotencia, borrador,
│                                                copia, dashboard, rendimiento
├── Pos.Infrastructure.Tests/Inventory/StockStatusQueryTests.cs ✏️ negativos
└── Pos.Infrastructure.Tests/SampleDatabases/v0.4.0.db ➕ (y Directory.Build.props → 0.4.0)
```

**Structure Decision**: se mantienen las cuatro capas y la organización por funcionalidad. Todo
lo nuevo vive en espacios `Sales` de cada capa. Inventario solo se amplía con la existencia con
signo y los dos tipos de movimiento de venta. La bitácora de auditoría es transversal
(`Application/Abstractions`, `Infrastructure/Audit`).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `Sale` sin `DeletedAt` (Principio IV) | La spec prohíbe borrar ventas; su anulación es la cancelación, con estado, motivo, fecha y usuario | Una columna `DeletedAt` que nunca se escribe sugiere que una venta se puede borrar y abre un segundo camino de anulación que no regresaría inventario |
| `SaleLine` y `SalePayment` sin `Version` ni `DeletedAt`, y sin fecha y usuario propios (Principio IV) | Son partes del agregado `Sale`: se crean con ella, comparten su auditoría y su versión, y no se editan por separado. La única escritura posterior (`SaleLine.CancellationMovementId`) ocurre dentro de `Sale.Cancel`, que incrementa la versión de la venta | Duplicar la auditoría en cada hijo agrega columnas que siempre serían iguales a las de la venta |
| `AuditEntry` solo con creación (Principio IV) | Es una bitácora inmutable, igual que los movimientos de 004 | Las columnas de modificación y borrado contradicen su propósito; la inmutabilidad se garantiza con un guardián en `SaveChanges` y su prueba |
| `SaleDraft` sin auditoría ni GUID como PK (Principio IV) | Es un dato temporal de la terminal, no un registro de negocio: una fila (`Slot = 1`) que se sobrescribe y se borra | Guardarlo en un archivo impediría borrarlo en la misma transacción que la venta (research §7); tratarlo como entidad de negocio agrega historia que nadie consulta |
