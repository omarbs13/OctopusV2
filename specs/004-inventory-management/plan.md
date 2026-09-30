# Implementation Plan: Manejo de inventario de productos

**Branch**: `004-inventory-management` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-inventory-management/spec.md`

## Summary

Se agrega el módulo de Inventario sobre el catálogo de Productos:

1. **Configuración en el producto**:
   - `TracksInventory` y `MinimumStock` opcional, guardados en `Products`.
   - La unidad de medida de 003 gana `DecimalPlaces` (0 o 3), que decide si una cantidad es
     entera o admite hasta 3 decimales.
   - Con movimientos registrados, el producto no puede cambiar de unidad ni dejar de controlar
     inventario.
2. **Cantidades exactas**: value object `Quantity` en milésimas (`long`), guardado como
   `INTEGER`. Así la suma, la comparación y el filtrado en SQLite son exactos (research §1).
3. **Existencia y movimientos**:
   - Agregado `ProductStock` (tabla `ProductStocks`), cuyo método `Record(...)` produce un
     `InventoryMovement` inmutable (tabla `InventoryMovements`) con secuencia por producto.
   - Todo en una transacción `BEGIN IMMEDIATE`. Hay dos barreras más en la base: la versión de la
     existencia y el índice único de la secuencia (research §4 a §6).
4. **Pantallas**:
   - Existencias y Movimientos sustituyen a los "disponible más adelante" de 002.
   - Formulario "Registrar movimiento", que se abre desde ambas.
   - Columna "Existencia" y acción "Ver movimientos" en Productos.
5. **Alertas**:
   - Las tarjetas de Inicio cuentan con el mismo predicado SQL que usa el filtro de Existencias.
   - Llevan a la pantalla ya filtrada gracias a la navegación con argumento (research §8 y §9).

Hay una migración nueva (`InventoryManagement`) y ninguna dependencia externa nueva.

**Ajuste a la spec**: la descripción original hablaba de un catálogo de 5 unidades, pero 003 ya
implementó uno de 8, obligatorio en todo producto. Se actualizaron FR-002, FR-003 y H1 de la spec
para reutilizarlo, con gramo y mililitro como cantidades enteras (research §3).

## Technical Context

**Language/Version**: C# 14 / .NET 10

**Primary Dependencies**: las existentes (Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4,
Microsoft.Extensions.Hosting, Serilog, EF Core 10 Sqlite, FluentValidation y SkiaSharp). No se
agrega ninguna.

**Storage**: SQLite existente, en modo WAL.

- Columnas nuevas: `Products.TracksInventory`, `Products.MinimumStock` y
  `UnitsOfMeasure.DecimalPlaces`.
- Tablas nuevas: `ProductStocks` e `InventoryMovements`.
- Sin reconstrucción de tablas.

**Testing**: xUnit v3 con SQLite real para la persistencia (consistencia, atomicidad,
concurrencia, inmutabilidad, filtros, rendimiento y migración); dominio sin dependencias.
Siguiendo la política mínima de la constitución v1.2.0, no se prueban ViewModels.

**Target Platform**: escritorio Windows 10+ y Linux (X11 o Wayland).

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas.

**Performance Goals**: cada página de 100 filas de Existencias y de Movimientos, con filtros,
tarda menos de 2 s con 10,000 productos y 100,000 movimientos (SC-005). Registrar un movimiento
es instantáneo para el operador: una transacción de 3 escrituras.

**Constraints**:

- Funciona sin conexión.
- 0 advertencias.
- Nunca se redondea una cantidad.
- El movimiento y la existencia se guardan en una sola transacción.
- Los movimientos no se modifican ni se borran.
- La migración respalda antes y restaura si falla (el orden de la fundación no cambia).

**Scale/Scope**:

- Alrededor de 10,000 productos y 100,000 movimientos; alrededor de 10 MB adicionales de base.
- 2 pantallas nuevas, 1 formulario nuevo, 2 pantallas modificadas (Productos: listado y editor)
  y 2 tarjetas activadas.

No queda ningún NEEDS CLARIFICATION.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitución v1.2.0.

| Principio o regla | Cómo lo cumple el diseño | Antes | Después |
|---|---|---|---|
| I. Venta sin conexión | Nada depende de la red | ✅ | ✅ |
| I. Transacción única | Movimiento y existencia se guardan en una transacción `BEGIN IMMEDIATE`, con una prueba de atomicidad (research §5) | ✅ | ✅ |
| I. Errores no cierran la app | Registrar un movimiento pasa por `OperationRunner`. Las violaciones de reglas son `ValidationFailed`, no excepciones | ✅ | ✅ |
| I. Respaldos | La migración usa el flujo de respaldo y restauración existente | ✅ | ✅ |
| II. Capas | Hay puertos nuevos en Application: `IWriteTransactions` e `IInventoryRepository`. Infrastructure los implementa y Desktop solo invoca casos de uso. `SystemUser` pasa a Application para no depender de Infrastructure. Las pruebas de arquitectura siguen aplicando | ✅ | ✅ |
| II. Organización por funcionalidad | `Domain/Inventory`, `Application/Inventory/{RegisterMovement,SearchStock,SearchMovements,GetStockAlerts}`, `Infrastructure/Inventory` y `Desktop/Inventory` | ✅ | ✅ |
| III. Lógica en el núcleo | Estas reglas viven en Domain: signo por tipo, existencia no negativa, inventario inicial único, motivo en ajustes, decimales por unidad, estado de existencia y bloqueo de unidad. Application valida y orquesta. La vista previa del formulario solo muestra | ✅ | ✅ |
| IV. GUID v7 y UTC | `InventoryMovement.Id` es GUID v7; `ProductStock` usa el id del producto; `CreatedAt` está en UTC y se muestra en hora local | ✅ | ✅ |
| IV. Auditoría completa (fechas, usuarios, `DeletedAt`, `Version`) | `ProductStock` la tiene completa salvo `DeletedAt`. `InventoryMovement` solo lleva creación | ⚠️ | ⚠️ Justificada (ver Complexity Tracking) |
| IV. No borrar registros históricos | No hay `Remove` de movimientos; un guardián en `SaveChanges` rechaza `Modified` y `Deleted`; hay una prueba | ✅ | ✅ |
| IV. Dinero en centavos | Sin cambios. Las cantidades siguen el mismo criterio de entero escalado | ✅ | ✅ |
| IV. Code First y migraciones | Migración nueva `InventoryManagement`. Se revisa que no reconstruya `Products` | ✅ | ✅ |
| IV. Base de ejemplo por versión | Se genera `v0.3.0.db` (versión 0.3.0) con movimientos; la prueba migra v0.1.0, v0.2.0 y v0.3.0 | ✅ | ✅ |
| IV. Catálogos fijos con `HasData` | `UnitsOfMeasure.DecimalPlaces` se siembra con `HasData` y la migración emite `UpdateData` | ✅ | ✅ |
| V. Multiplataforma | Sin código de plataforma ni rutas nuevas | ✅ | ✅ |
| VI. Build sin advertencias | Sí | ✅ | ✅ |
| VI. Pruebas mínimas | Solo reglas con cálculo (`Quantity`, `Record`, `StockStatusRule`), validaciones de integridad (bloqueo de unidad) y las obligatorias: consistencia de inventario, migración y arquitectura. Sin pruebas de ViewModels (quickstart §2) | ✅ | ✅ |
| VI. Nunca el proveedor InMemory de EF | Todas las pruebas de persistencia usan SQLite real | ✅ | ✅ |
| VII. YAGNI | Un almacén; sin costos, lotes ni usuarios reales; sin cursor de paginación; sin triggers. Sin dependencias nuevas | ✅ | ✅ |
| VII. Sin repositorios genéricos ni MediatR | `IInventoryRepository` es específico; los casos de uso son clases simples | ✅ | ✅ |
| VIII. Diagnóstico | Un movimiento rechazado por conflicto o una falla inesperada se registran con `ProductId`, `Type` y la cantidad. Los tipos se guardan como texto legible (research §7) | ✅ | ✅ |
| IX. Seguridad local | Cada movimiento registra usuario y fecha asignados por el sistema, así que queda como bitácora del ajuste. Aún no hay roles (fuera de alcance según la spec) | ✅ | ✅ |
| Restricciones técnicas | FluentValidation para la forma de la entrada, Serilog, xUnit y NetArchTest. Código en inglés y textos en español | ✅ | ✅ |
| Flujo de desarrollo | Especificación y clarificación hechas; este plan; tareas a continuación | ✅ | ✅ |

**Resultado**: pasa, con una desviación justificada en Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/004-inventory-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── use-cases.md     # RegisterMovement, SearchStock, SearchMovements, GetStockAlerts, puertos
│   └── ui.md            # Existencias, Movimientos, formulario, Productos, Inicio, navegación
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

Archivos nuevos (➕) y modificados (✏️):

```text
src/Pos.Domain/
├── Common/Quantity.cs                        ➕ milésimas, Parse por decimales, aritmética
├── Products/UnitOfMeasure.cs                 ✏️ DecimalPlaces
├── Products/Product.cs                       ✏️ TracksInventory, MinimumStock, bloqueo con movimientos
└── Inventory/                                ➕
    ├── ProductStock.cs                       Start, Record → InventoryMovement
    ├── InventoryMovement.cs                  inmutable
    ├── MovementType.cs                       enum + IsIncrease/RequiresReason
    └── StockStatus.cs                        enum + StockStatusRule

src/Pos.Application/
├── Abstractions/IWriteTransactions.cs        ➕
├── Abstractions/SystemUser.cs                ➕ (antes, constante en Infrastructure)
├── Products/…                                ✏️ comandos, reglas, DTOs, Create/Update con inventario
├── Inventory/                                ➕
│   ├── IInventoryRepository.cs               + StockSearch, StockPage, MovementSearch, MovementPage, DTOs
│   ├── InventoryMessages.cs, InventoryFields.cs
│   ├── RegisterMovement/                     Command, Validator, Handler
│   ├── SearchStock/                          Query, Handler
│   ├── SearchMovements/                      Query, Handler
│   └── GetStockAlerts/                       Handler
└── DependencyInjection.cs                    ✏️

src/Pos.Infrastructure/
├── Persistence/Configurations/               ➕ ProductStockConfiguration, InventoryMovementConfiguration; ✏️ Product, UnitOfMeasure
├── Persistence/PosDbContext.cs               ✏️ DbSets + guardián de inmutabilidad
├── Persistence/WriteTransactions.cs          ➕ BEGIN IMMEDIATE
├── Persistence/Migrations/…_InventoryManagement.cs ➕
├── Inventory/InventoryRepository.cs          ➕ predicado de estado compartido por búsqueda y conteo
├── Products/ProductRepository.cs             ✏️ existencia en el listado y el detalle
├── Platform/SystemCurrentUser.cs             ✏️ usa SystemUser
└── DependencyInjection.cs                    ✏️

src/Pos.Desktop/
├── Navigation/Navigator.cs                   ✏️ NavigateAsync(entryId, argument)
├── Navigation/INavigationArgumentReceiver.cs ➕
├── Home/DashboardCard.cs, HomeViewModel.cs   ✏️ NavigationArgument
├── Home/Cards/ComingSoonCard.cs              ✏️ se retiran LowStockCard y OutOfStockCard
├── Common/QuantityConverter.cs               ➕
├── Inventory/                                ✏️ InventoryModule; ➕ LowStockCard, OutOfStockCard,
│                                                StockView(Model), MovementsView(Model),
│                                                MovementEditorView(Model)
├── Products/…                                ✏️ columna Existencia, "Ver movimientos", sección Inventario
└── Resources/Strings.resx                    ✏️

tests/
├── Pos.Domain.Tests/Common/QuantityTests.cs                  ➕
├── Pos.Domain.Tests/Inventory/ProductStockTests.cs           ➕ reglas de Record
├── Pos.Domain.Tests/Inventory/StockStatusRuleTests.cs        ➕
├── Pos.Application.Tests/Products/UpdateProductInventoryTests.cs ➕ bloqueo con movimientos
├── Pos.Infrastructure.Tests/Inventory/                       ➕ consistencia, atomicidad, concurrencia,
│                                                                inmutabilidad, filtros y conteos, rendimiento
└── Pos.Infrastructure.Tests/SampleDatabases/v0.3.0.db        ➕ (y Directory.Build.props → 0.3.0)
```

**Structure Decision**: se mantienen las cuatro capas y la organización por funcionalidad. Todo
lo nuevo vive en espacios `Inventory` de cada capa. Productos solo se amplía con la
configuración de inventario, que el operador edita en el formulario del producto.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `InventoryMovement` sin `UpdatedAt`, `UpdatedBy`, `DeletedAt` ni `Version` (Principio IV) | FR-010 exige que los movimientos sean inmutables, y el propio Principio IV prohíbe borrarlos. Las correcciones se hacen con un movimiento contrario | Incluir las columnas sugiere que el movimiento se puede editar o anular, y queda código que nunca las escribe. La inmutabilidad se garantiza en el dominio, en el repositorio y con un guardián en `SaveChanges`, más su prueba |
| `ProductStock` sin `DeletedAt` (Principio IV) | Es el estado derivado de un producto (1 a 0..1). Si el producto se borra lógicamente, la existencia se conserva como historia | Un borrado lógico propio podría quedar desincronizado del producto; el filtro del producto (`DeletedAt IS NULL`) ya excluye su existencia de todas las consultas |
