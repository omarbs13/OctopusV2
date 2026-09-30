---

description: "Lista de tareas: Manejo de inventario de productos"
---

# Tasks: Manejo de inventario de productos

**Input**: Documentos de diseño en `specs/004-inventory-management/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/use-cases.md, contracts/ui.md, quickstart.md

**Tests**: Solo los que exige la constitución v1.2.0 (Principio VI, pruebas mínimas): reglas con cálculo
(`Quantity`, `Record`, `StockStatusRule`), bloqueo de unidad, consistencia de inventario, atomicidad,
escritura serializada, inmutabilidad, estado SQL vs dominio, rendimiento, migración y arquitectura.
**No** se escriben pruebas de ViewModels ni de vistas. Al implementar, ejecutar solo el proyecto de
pruebas modificado (`dotnet test tests/<Proyecto> --verbosity quiet`); compilar con `dotnet build -v q`
(0 advertencias).

**Organization**: Tareas agrupadas por historia de usuario. Código en inglés, textos de interfaz en español
(`src/Pos.Desktop/Resources/Strings.resx`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: Historia a la que pertenece (US1..US5)

## Path Conventions

Solución por capas: `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`;
pruebas en `tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`, `tests/Pos.Infrastructure.Tests`,
`tests/Pos.ArchitectureTests`. Antes de editar un archivo existente, leerlo y seguir su estilo (predicados
estáticos, `Result`, `FieldError`, patrones de `Products`).

---

## Phase 1: Setup

**Purpose**: Verificar el punto de partida

- [X] T001 Verificar que `dotnet build -v q` y `dotnet test --verbosity quiet` pasan en la rama `004-inventory-management` antes de cambiar código, y revisar cómo `src/Pos.Domain/Products/`, `src/Pos.Application/Products/` y `src/Pos.Desktop/Products/` estructuran entidad, reglas, casos de uso, repositorio, listado y editor (patrón a replicar)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tipos de dominio, esquema, migración, puertos y navegación con argumento que necesitan todas las historias

**⚠️ CRITICAL**: Ninguna historia puede empezar hasta terminar esta fase

### Dominio

- [X] T002 [P] Crear `Quantity` en `src/Pos.Domain/Common/Quantity.cs`: value object `readonly record struct` con `long Thousandths`, `Zero`, `MaxCaptureThousandths = 9_999_999_999`, `MaxStockThousandths = 999_999_999_999`, `Parse(string? text, int decimalPlaces)` → `QuantityParseResult` (recorta espacios, dígitos con punto decimal opcional y coma como separador de miles igual que `Money`, nunca redondea), `FitsDecimals(int decimalPlaces)` (`Thousandths % 10^(3-dp) == 0`), `ToEditableString(int decimalPlaces)` ("12" o "1.250"), operadores `+`, `-` y comparación. `QuantityParseError`: `Empty`, `Format`, `TooManyDecimals`, `TooLarge`, `NotPositive` (0 en un movimiento; para existencia mínima 0 sí es válido mediante el parámetro `allowZero = false` de la firma `Parse(text, decimalPlaces, allowZero)`)
- [X] T003 [P] Agregar `DecimalPlaces` (int, 0 o 3) a `UnitOfMeasure` en `src/Pos.Domain/Products/UnitOfMeasure.cs` y a su siembra `HasData`: 3 para KGM, LTR y MTR; 0 para H87, GRM, MLT, XBX y XPK
- [X] T004 [P] Crear `MovementType` en `src/Pos.Domain/Inventory/MovementType.cs`: enum `Initial`, `Receipt`, `AdjustIn`, `AdjustOut` con extensiones `IsIncrease()` (falso solo en `AdjustOut`) y `RequiresReason()` (verdadero en `AdjustIn` y `AdjustOut`), y códigos de texto estables `INITIAL`, `RECEIPT`, `ADJUST_IN`, `ADJUST_OUT` (métodos `ToCode`/`FromCode`)
- [X] T005 [P] Crear `StockStatus` (`Normal`, `Low`, `Out`) y `StockStatusRule.Evaluate(Quantity onHand, Quantity? minimum)` en `src/Pos.Domain/Inventory/StockStatus.cs`: `Out` si `onHand == 0`; `Low` si `onHand > 0` y `minimum` no nulo y `onHand <= minimum`; `Normal` en otro caso
- [X] T006 Crear `InventoryMovement` en `src/Pos.Domain/Inventory/InventoryMovement.cs` (depende de T002, T004): inmutable, sin setters públicos ni métodos de modificación; campos `Id` (GUID v7 con `Guid.CreateVersion7`), `ProductId`, `Sequence`, `Type`, `Quantity`, `ResultingStock`, `Reason` (TEXT 250, recortado, vacío = nulo), `Reference` (TEXT 50, recortado), `CreatedAt` (UTC, lo asigna la persistencia con `IClock`) y `CreatedBy`. Solo lo construye `ProductStock.Record`
- [X] T007 Crear `ProductStock` en `src/Pos.Domain/Inventory/ProductStock.cs` (depende de T002, T003, T004, T006): `ProductId` (PK), `OnHand` (≥ 0 y ≤ `MaxStockThousandths`), `MovementCount`, auditoría (`CreatedAt/By`, `UpdatedAt/By`) y `Version` (concurrencia); `Start(Guid productId)` crea existencia 0 sin movimientos; `Record(MovementType type, Quantity quantity, UnitOfMeasure unit, bool productActive, bool tracksInventory, string? reason, string? reference)` → `InventoryMovement`, que lanza `DomainException` si: el producto no controla inventario o está inactivo (FR-014); cantidad ≤ 0 o con más decimales que la unidad (FR-003, FR-008); motivo vacío en `AdjustIn`/`AdjustOut` (FR-009); tipo `Initial` con `MovementCount > 0` (FR-015); `AdjustOut` dejaría `OnHand < 0` (FR-011); el resultado supera `MaxStockThousandths`. Si es válido actualiza `OnHand` y `MovementCount`, asigna `Sequence = MovementCount` nuevo y `ResultingStock = OnHand`. Predicados estáticos `CanRecordInitial(int movementCount)` y `WouldGoNegative(Quantity onHand, Quantity quantity)`
- [X] T008 Modificar `Product` en `src/Pos.Domain/Products/Product.cs`: agregar `TracksInventory` (bool, por omisión `false`) y `MinimumStock` (`Quantity?`, ≥ 0, respeta los decimales de `UnitCode`, se guarda nulo si `TracksInventory = false`); `Create(...)` y `Update(...)` reciben `tracksInventory` y `minimumStock`; `Update(...)` recibe `hasMovements` y con `true` lanza `DomainException` si cambia `UnitCode` o `TracksInventory` pasa de `true` a `false` (FR-006); predicados estáticos `IsValidMinimumStock(Quantity? minimum, UnitOfMeasure unit)` y `CanChangeInventorySettings(bool hasMovements, string currentUnit, string newUnit, bool currentTracks, bool newTracks)`. Actualizar los llamadores existentes para que compile

### Pruebas de dominio (obligatorias por la constitución)

- [X] T008a Adaptar los llamadores y pruebas existentes a las nuevas firmas de `Product.Create/Update` (siembras, handlers, tests en `tests/Pos.Domain.Tests/Products` y `tests/Pos.Application.Tests/Products`) pasando `tracksInventory: false`, `minimumStock: null` y `hasMovements: false`; ejecutar `dotnet build -v q` hasta 0 advertencias y `dotnet test tests/Pos.Domain.Tests --verbosity quiet` (depende de T008)
- [X] T009 [P] Pruebas de `Quantity` (depende de T002) en `tests/Pos.Domain.Tests/Common/QuantityTests.cs`: `Parse("1.5", 0)` → `TooManyDecimals`; `Parse("1.250", 3)` válido = 1250; `Parse("1.2505", 3)` → `TooManyDecimals`; `Parse("10000000", 3)` → `TooLarge`; `Parse("0", 3)` → `NotPositive` y `Parse("0", 3, allowZero: true)` válido; `Parse("", 0)` → `Empty`; `Parse("abc", 0)` → `Format`
- [X] T010 [P] Pruebas de `Record` (depende de T004, T007) en `tests/Pos.Domain.Tests/Inventory/ProductStockTests.cs`: con existencia 3, `AdjustOut` de 3 deja 0 y de 3.001 (kg) lanza; `AdjustOut` sin motivo lanza; `Initial` con `MovementCount > 0` lanza; producto sin inventario o inactivo lanza; cantidad con decimales en unidad entera lanza; secuencia y `ResultingStock` correctos tras Initial 10, Receipt 5, AdjustOut 3 (10, 15, 12)
- [X] T011 [P] Pruebas de `StockStatusRule` (depende de T005) en `tests/Pos.Domain.Tests/Inventory/StockStatusRuleTests.cs`: igual al mínimo → `Low`; 0 con mínimo → `Out` (no `Low`); sin mínimo con existencia > 0 → `Normal`; sin mínimo con 0 → `Out`; mayor al mínimo → `Normal`

### Application (puertos)

- [X] T012 [P] Crear `SystemUser` (`Id` y `DisplayName = "Sistema"`) en `src/Pos.Application/Abstractions/SystemUser.cs` y hacer que `src/Pos.Infrastructure/Platform/SystemCurrentUser.cs` lo use en lugar de su constante propia
- [X] T013 [P] Crear `IWriteTransactions` e `IWriteTransaction : IAsyncDisposable` (`Task CommitAsync(CancellationToken)`) en `src/Pos.Application/Abstractions/IWriteTransactions.cs` con `Task<IWriteTransaction> BeginAsync(CancellationToken)`; al descartar sin `CommitAsync` revierte
- [X] T014 Crear en `src/Pos.Application/Inventory/`: `IInventoryRepository.cs` (`GetStockAsync`, `HasMovementsAsync`, `AddStock`, `AddMovement`, `SaveChangesAsync` → `SaveOutcome` `Saved`/`Conflict`, `SearchStockAsync`, `CountAlertsAsync`, `SearchMovementsAsync`; sin `Update` ni `Remove` de movimientos) más los tipos `StockFilter` (`All`, `Normal`, `Low`, `Out`), `StockSearch`, `StockItemDto`, `StockPage`, `StockAlertCounts(long Low, long Out)`, `MovementSearch`, `MovementDto`, `MovementPage` (ambas páginas con `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`, `DefaultPageSize = 100`) exactamente como en `contracts/use-cases.md`; y `InventoryMessages.cs` / `InventoryFields.cs` con los mensajes y nombres de campo en español de ese contrato. Verificar si `SaveOutcome` ya existe de 003; si no, crearlo en `src/Pos.Application/Inventory/`. ~~Ampliar `IProductRepository` con `GetForMovementAsync`~~ (no hizo falta: se reutiliza `IProductRepository.GetAsync` y la unidad sale del catálogo fijo con `UnitOfMeasure.Find`) (depende de T004, T005, T007)

### Infrastructure (persistencia)

- [X] T015 [P] Crear `ProductStockConfiguration` en `src/Pos.Infrastructure/Persistence/Configurations/ProductStockConfiguration.cs`: tabla `ProductStocks`, PK y FK `ProductId` → `Products.Id` (Restrict), `OnHand` y `MovementCount` INTEGER NOT NULL, `Version` como token de concurrencia, conversión de `Quantity` a `long`
- [X] T016 [P] Crear `InventoryMovementConfiguration` en `src/Pos.Infrastructure/Persistence/Configurations/InventoryMovementConfiguration.cs`: tabla `InventoryMovements`, FK `ProductId` → `Products.Id` (Restrict), `Type` TEXT(12) con conversión de códigos (`MovementType.ToCode/FromCode`), `Quantity` y `ResultingStock` INTEGER, `Reason` TEXT(250), `Reference` TEXT(50), `CreatedAt` UTC, `CreatedBy`; sin `UpdatedAt`, `UpdatedBy`, `DeletedAt` ni `Version`; índices `IX_InventoryMovements_Product_Sequence (ProductId, Sequence)` único, `IX_InventoryMovements_CreatedAt (CreatedAt, Id)` e `IX_InventoryMovements_Type_CreatedAt (Type, CreatedAt)`
- [X] T017 [P] Modificar `ProductConfiguration` y `UnitOfMeasureConfiguration` en `src/Pos.Infrastructure/Persistence/Configurations/`: `Products.TracksInventory` INTEGER NOT NULL DEFAULT 0, `Products.MinimumStock` INTEGER NULL (conversión de `Quantity?`), índice `IX_Products_TracksInventory (TracksInventory, IsActive)` con filtro `DeletedAt IS NULL`, `UnitsOfMeasure.DecimalPlaces` INTEGER NOT NULL DEFAULT 0 con `HasData` actualizado
- [X] T018 Modificar `src/Pos.Infrastructure/Persistence/PosDbContext.cs` (depende de T015, T016, T017): agregar `DbSet<ProductStock>` y `DbSet<InventoryMovement>`; en `SaveChanges`/`SaveChangesAsync` (o el interceptor existente) un guardián que lance excepción si hay entradas `Modified` o `Deleted` de `InventoryMovement`; asegurar que `AuditingInterceptor` asigna `CreatedAt`/`CreatedBy` a `InventoryMovement` (con `IClock` y `ICurrentUser`) y auditoría completa a `ProductStock`; ~~definir una interfaz `ICreationAudited`~~ (no hizo falta: `AuditingInterceptor` ya asigna `CreatedAt`/`CreatedBy` por nombre de propiedad solo si existe, así que `InventoryMovement` queda cubierto sin cambios)
- [X] T019 Generar la migración `InventoryManagement` con `dotnet ef migrations add InventoryManagement -p src/Pos.Infrastructure -s src/Pos.Desktop` (depende de T018) y revisar el SQL con `dotnet ef migrations script --idempotent -p src/Pos.Infrastructure -s src/Pos.Desktop`: solo `ALTER TABLE ADD COLUMN`, `CREATE TABLE`, `CREATE INDEX` y `UPDATE` (`UpdateData` de KGM/LTR/MTR a 3); si reconstruye `Products`, ajustar la configuración hasta que no lo haga
- [X] T020 [P] Crear `WriteTransactions` en `src/Pos.Infrastructure/Persistence/WriteTransactions.cs` implementando `IWriteTransactions` con `Database.BeginTransactionAsync()` sobre el `DbContext` del ámbito (`BEGIN IMMEDIATE`); registrar en `src/Pos.Infrastructure/DependencyInjection.cs`
- [X] T021 Crear `InventoryRepository` en `src/Pos.Infrastructure/Inventory/InventoryRepository.cs` implementando `IInventoryRepository` (depende de T014, T018): `GetStockAsync` con seguimiento, `HasMovementsAsync` (existe fila en `ProductStocks`), `AddStock`/`AddMovement`, `SaveChangesAsync` que mapea `DbUpdateConcurrencyException` y la violación del índice único de secuencia a `SaveOutcome.Conflict`; una **función de filtro de estado compartida** (`Out`: `OnHand = 0` o sin fila; `Low`: `OnHand > 0` y `MinimumStock` no nulo y `OnHand <= MinimumStock`) usada por `SearchStockAsync` y `CountAlertsAsync`; `SearchStockAsync` (solo `TracksInventory = true` y no borrados, activos por omisión, texto normalizado como `SearchProducts`, orden por `NameSearch` y `Sku`, paginado con `TotalCount`); `CountAlertsAsync` (solo activos, mismo predicado); `SearchMovementsAsync` (filtros por producto, tipo y rango `[FromUtc, ToUtcExclusive)`, orden `CreatedAt` desc y `Id` desc, uniendo producto y unidad). Registrar en `src/Pos.Infrastructure/DependencyInjection.cs`

### Navegación y formato (Desktop)

- [X] T022 [P] Crear `INavigationArgumentReceiver` (`void Receive(object argument)`) en `src/Pos.Desktop/Navigation/INavigationArgumentReceiver.cs` y modificar `src/Pos.Desktop/Navigation/Navigator.cs` a `NavigateAsync(string entryId, object? argument = null)`: si el destino lo implementa, entrega el argumento antes de `OnActivatedAsync`, incluso si ya es la pantalla actual
- [X] T023 [P] Agregar `virtual object? NavigationArgument => null` a `DashboardCard` en `src/Pos.Desktop/Home/DashboardCard.cs` y hacer que `HomeViewModel` (`src/Pos.Desktop/Home/HomeViewModel.cs`) lo pase a `NavigateAsync` (depende de T022)
- [X] T024 [P] Crear `QuantityConverter` en `src/Pos.Desktop/Common/QuantityConverter.cs`: solo formatea milésimas con los decimales de la unidad y separador de miles `es-MX` ("12", "1,250", "1.250"); sin cálculo

### Pruebas obligatorias de esquema (infra)

- [X] T025 Generar la base de ejemplo `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.3.0.db` con un producto que controla inventario y movimientos de los cuatro tipos (con el método usado para `v0.1.0.db` y `v0.2.0.db`), subir la versión a 0.3.0 en `Directory.Build.props`, y extender la prueba de migración existente en `tests/Pos.Infrastructure.Tests/SampleDatabases/` para que migre `v0.1.0`, `v0.2.0` y `v0.3.0` a la versión actual y verifique que los productos existentes quedan con `TracksInventory = 0` y que las unidades KGM/LTR/MTR tienen `DecimalPlaces = 3` (FR-023) (depende de T019)
- [X] T026 Ejecutar `dotnet test tests/Pos.ArchitectureTests --verbosity quiet` y corregir cualquier violación de capas por los espacios `Inventory` y `SystemUser` nuevos

**Checkpoint**: Dominio, esquema, migración, puertos, repositorio y navegación listos; las historias pueden empezar

---

## Phase 3: User Story 1 - Configuración de inventario en el producto (Priority: P1) 🎯 MVP

**Goal**: El operador indica si el producto controla inventario y su existencia mínima; la existencia actual se ve (solo lectura) en el editor y el listado; con movimientos no se puede cambiar de unidad ni dejar de controlar inventario.

**Independent Test**: Crear un producto en Kilogramo con inventario y mínimo 5: se guarda, la existencia aparece en 0 y no es editable. Un mínimo `1.5` en Pieza se rechaza. Un servicio no muestra datos de inventario.

### Implementation for User Story 1

- [X] T027 [US1] Ampliar `ProductDto` (`TracksInventory`, `MinimumStockThousandths`, `OnHandThousandths` solo lectura, `HasMovements`, `DecimalPlaces`) y `ProductListItemDto` (`TracksInventory`, `OnHandThousandths` nulo si no controla inventario, `DecimalPlaces`) en `src/Pos.Application/Products/`, y actualizar `ProductRepository` en `src/Pos.Infrastructure/Products/ProductRepository.cs` para poblarlos uniendo `ProductStocks` y `UnitsOfMeasure` (sin fila de existencia = 0; `HasMovements` = existe fila)
- [X] T028 [US1] Agregar `bool TracksInventory` y `string? MinimumStockText` a `CreateProductCommand` y `UpdateProductCommand` en `src/Pos.Application/Products/`, y a `ProductRules` la validación de `MinimumStock`: `Quantity.Parse` con los decimales de la unidad elegida, 0 permitido, ignorado si `TracksInventory = false`, con mensajes de `InventoryMessages` con sujeto "La existencia mínima" (campo `MinimumStock`)
- [X] T029 [US1] Modificar los manejadores `CreateProduct` y `UpdateProduct` en `src/Pos.Application/Products/` (depende de T027, T028): `Create` pasa `tracksInventory`/`minimumStock` al dominio; `UpdateProduct` abre `IWriteTransactions.BeginAsync`, consulta `IInventoryRepository.HasMovementsAsync` y con movimientos devuelve `ValidationFailed` en `UnitCode` ("No se puede cambiar la unidad de un producto con movimientos de inventario.") o en `TracksInventory` ("No se puede dejar de controlar el inventario de un producto con movimientos."), luego guarda con la concurrencia optimista existente y confirma; pasa `hasMovements` al dominio
- [X] T030 [P] [US1] Prueba de bloqueo de unidad en `tests/Pos.Application.Tests/Products/UpdateProductInventoryTests.cs`: `UpdateProduct` rechaza cambiar la unidad y rechaza desactivar el inventario si hay movimientos, y los permite si no los hay (FR-006)
- [X] T031 [US1] Agregar textos de la sección "Inventario" a `src/Pos.Desktop/Resources/Strings.resx` (casilla "Controla inventario", "Existencia mínima", "Existencia actual", nota "Tiene movimientos de inventario; no se puede cambiar la unidad ni dejar de controlar el inventario.", columna "Existencia", acción "Ver movimientos")
- [X] T032 [US1] Modificar el editor de producto (ViewModel y vista en `src/Pos.Desktop/Products/`, depende de T027–T029, T031, T024): sección "Inventario" con casilla "Controla inventario", "Existencia mínima" (texto opcional visible y habilitado solo con la casilla marcada), "Existencia actual" solo lectura con cantidad y unidad ("—" si no controla), y con `HasMovements` deshabilitar unidad y casilla mostrando la nota; enviar `TracksInventory` y `MinimumStockText` en los comandos; mostrar errores de campo `MinimumStock`, `UnitCode` y `TracksInventory` junto a su control; sin opción de registrar movimientos (FR-015a)
- [X] T033 [US1] Modificar el listado de productos (ViewModel y vista en `src/Pos.Desktop/Products/`, depende de T027, T024): columna "Existencia" con la cantidad formateada si controla inventario y "—" si no; la acción de fila "Ver movimientos" se agrega completa en US4 (T050)

**Checkpoint**: US1 funcional y comprobable de forma independiente (quickstart §3 pasos 1, 2 y 6)

---

## Phase 4: User Story 2 - Movimientos de inventario (Priority: P1)

**Goal**: El operador registra inventario inicial, entradas y ajustes positivos/negativos desde un formulario; la existencia y el movimiento se guardan juntos de forma atómica, con reglas de negocio claras.

**Independent Test**: Inventario inicial 10, entrada 5 y ajuste negativo 3 sobre un producto: existencia 12 y tres movimientos con existencias resultantes 10, 15 y 12; un ajuste negativo mayor a la existencia se rechaza sin cambios.

### Tests for User Story 2 (obligatorias por la constitución)

- [X] T034 [P] [US2] Prueba de consistencia de inventario en `tests/Pos.Infrastructure.Tests/Inventory/InventoryConsistencyTests.cs`: SQLite real, secuencia aleatoria reproducible (semilla fija) de movimientos válidos e inválidos sobre varios productos y unidades vía el caso `RegisterMovement`/repositorio; al final verificar los 6 invariantes de `data-model.md` (`OnHand` = suma algebraica; `MovementCount` = COUNT y secuencias exactamente 1..N; cada `ResultingStock` = anterior ± cantidad y el último = `OnHand`; ninguno negativo; solo `Sequence = 1` puede ser `INITIAL`; `Quantity` y `ResultingStock` respetan los decimales de la unidad)
- [X] T035 [P] [US2] Prueba de atomicidad en `tests/Pos.Infrastructure.Tests/Inventory/InventoryAtomicityTests.cs`: forzar una falla después de `AddMovement` y antes de `CommitAsync` y comprobar que `ProductStocks` e `InventoryMovements` quedan sin cambios
- [X] T036 [P] [US2] Prueba de escritura serializada en `tests/Pos.Infrastructure.Tests/Inventory/InventoryConcurrencyTests.cs`: dos contextos concurrentes intentan un ajuste negativo sobre una existencia que solo alcanza para uno; uno se guarda, el otro se rechaza y la existencia nunca queda negativa
- [X] T037 [P] [US2] Prueba de inmutabilidad en `tests/Pos.Infrastructure.Tests/Inventory/InventoryImmutabilityTests.cs`: modificar o borrar un `InventoryMovement` por el contexto y llamar `SaveChanges` lanza excepción

### Implementation for User Story 2

- [X] T038 [P] [US2] Crear `RegisterMovementCommand(Guid ProductId, MovementType Type, string QuantityText, string? Reason, string? Reference)` y su `RegisterMovementValidator` (FluentValidation: tipo válido, cantidad capturada, motivo ≤ 250, referencia ≤ 50) en `src/Pos.Application/Inventory/RegisterMovement/`
- [X] T039 [US2] Crear `RegisterMovementHandler` en `src/Pos.Application/Inventory/RegisterMovement/` (depende de T013, T014, T038): abrir `IWriteTransactions.BeginAsync`; cargar el producto y su unidad con `GetForMovementAsync` (si no existe, `NotFound`) y el `ProductStock` (o `ProductStock.Start`); validar reglas de negocio devolviendo `ValidationFailed` por campo con los mensajes de `contracts/use-cases.md` (`ProductId`: no controla inventario / inactivo; `Type`: ya tiene movimientos, use ajuste; `Quantity`: vacía, ≤ 0, sin decimales para la unidad, máximo 3 decimales, máximo 9,999,999.999, ajuste dejaría bajo cero con existencia actual, excedería el máximo; `Reason`: obligatorio en ajustes) usando `Quantity.Parse`, `ProductStock.CanRecordInitial` y `WouldGoNegative`; llamar `stock.Record(...)`, `AddStock` si es nueva, `AddMovement`, guardar y confirmar; ante `SaveOutcome.Conflict` devolver `Conflict`; resolver `CreatedByName` ("Sistema" para `SystemUser`, si no los primeros 8 caracteres del id) y devolver `Result<MovementDto>`. Registrar en `src/Pos.Application/DependencyInjection.cs`. Registrar en el log (Serilog) conflictos y fallas inesperadas con `ProductId`, `Type` y cantidad
- [X] T040 [US2] Agregar a `src/Pos.Desktop/Resources/Strings.resx` los textos del formulario "Registrar movimiento" (etiquetas Producto, Existencia actual, Tipo con las 4 etiquetas, Cantidad con ayudas "Enteros"/"Hasta 3 decimales", Existencia resultante, Motivo, Referencia con ayuda "Folio de factura o remisión", aviso de conflicto "La existencia cambió mientras capturaba; revise y vuelva a intentar.", notificación "Movimiento registrado. Existencia: {n} {unidad}.")
- [X] T041 [US2] Crear `MovementEditorViewModel` y `MovementEditorView` en `src/Pos.Desktop/Inventory/` (depende de T039, T040, T024) usando `FormHost` y el patrón de `FormViewModel` de 002 (guardar, cancelar, aviso de cambios sin guardar): Producto fijo (desde Existencias) o selector con búsqueda que invoca `SearchStockQuery(text, StockFilter.All, IncludeInactive: false)` (desde Movimientos); la existencia actual y si el producto tiene movimientos se toman del `StockItemDto` elegido y, tras un `Conflict`, se recargan con `SearchStock`, Existencia actual de solo lectura con unidad, Tipo (lista de 4; "Inventario inicial" solo si el producto no tiene movimientos, que es el valor por omisión en ese caso y "Entrada" en los demás), Cantidad con ayuda por unidad, Existencia resultante como vista previa con `Quantity` (roja si sería negativa; solo informativa, la regla autoritativa es el caso de uso), Motivo multilínea ≤ 250 marcado obligatorio en ajustes, Referencia ≤ 50; mostrar errores de campo junto a su control; ante `Conflict` mostrar el aviso y recargar la existencia; tras guardar cerrar, notificar y devolver el control a la pantalla de origen para que recargue
- [X] T042 [US2] Registrar el formulario y sus servicios en el módulo `InventoryModule` (`src/Pos.Desktop/Inventory/InventoryModule.cs`) y en la inyección de dependencias de Desktop, dejando listo el punto de apertura que usarán Existencias (US3) y Movimientos (US4)

**Checkpoint**: US2 funcional: el caso de uso y el formulario funcionan; pruebas de consistencia, atomicidad, concurrencia e inmutabilidad en verde (`dotnet test tests/Pos.Infrastructure.Tests --verbosity quiet`, `dotnet test tests/Pos.Domain.Tests --verbosity quiet`)

---

## Phase 5: User Story 3 - Consulta de existencias (Priority: P1)

**Goal**: La pantalla Existencias lista los productos con inventario con existencia, unidad, mínimo y estado; filtro por estado, búsqueda, inactivos opcionales y paginación de 100; abre el formulario de movimiento.

**Independent Test**: Con productos en distintos estados, filtrar por "baja" muestra solo los correspondientes; buscar por SKU y por código de barras encuentra el producto; los inactivos solo aparecen con "Incluir inactivos".

### Tests for User Story 3 (obligatorias por la constitución)

- [X] T043 [P] [US3] Prueba de estado SQL vs dominio en `tests/Pos.Infrastructure.Tests/Inventory/StockStatusQueryTests.cs`: sobre los mismos datos, el filtro `SearchStockAsync` y el conteo `CountAlertsAsync` coinciden con `StockStatusRule.Evaluate` en los casos frontera (igual al mínimo, 0 con mínimo, sin mínimo, sin fila en `ProductStocks`), y los productos inactivos no cuentan en `CountAlertsAsync` pero sí salen con `IncludeInactive`
- [X] T044 [P] [US3] Prueba de rendimiento en `tests/Pos.Infrastructure.Tests/Inventory/InventoryPerformanceTests.cs` (siguiendo el patrón de `ProductPerformanceTests`): con 10,000 productos y 100,000 movimientos, cada página de 100 de Existencias y de Movimientos, con y sin filtros, tarda menos de 2 s (SC-005)

### Implementation for User Story 3

- [X] T045 [P] [US3] Crear `SearchStockQuery(string? Text, StockFilter Filter, bool IncludeInactive, int Page = 1)` y `SearchStockHandler` en `src/Pos.Application/Inventory/SearchStock/`: normalizar el texto igual que `SearchProducts` (nombre sin acentos, SKU en mayúsculas, código de barras exacto si el texto es un código completo), llamar `SearchStockAsync` y devolver `Result<StockPage>`; registrar en `src/Pos.Application/DependencyInjection.cs`
- [X] T046 [US3] Crear `StockViewModel` y `StockView` en `src/Pos.Desktop/Inventory/` (depende de T045, T041, T024, T022): barra con búsqueda (misma espera y comportamiento que Productos), filtro de estado (Todos, Normal, Existencia baja, Sin existencia), casilla "Incluir inactivos" y botón "Registrar movimiento" habilitado con una fila seleccionada; columnas Nombre, SKU, Existencia, Unidad, Mínimo ("—" si no hay) y Estado como etiqueta de color (Normal neutro, Baja advertencia, Sin existencia error); filas inactivas atenuadas con etiqueta "Inactivo" (`InactiveOpacityConverter` existente); paginación de Productos (100 por página, primera/anterior/siguiente/última); estados vacíos "Ningún producto controla inventario. Actívelo en Catálogos > Productos." y "No hay productos que coincidan."; doble clic o Enter abre `MovementEditorViewModel` con el producto elegido; tras guardar recarga la página conservando la selección; implementar `INavigationArgumentReceiver` para `StockFilter` (fija el filtro, limpia búsqueda, desmarca inactivos, página 1)
- [X] T047 [US3] En `src/Pos.Desktop/Inventory/InventoryModule.cs` sustituir la página `AddComingSoonPage` de `inventory.stock` por `StockViewModel`/`StockView`, y agregar los textos de Existencias a `src/Pos.Desktop/Resources/Strings.resx`

**Checkpoint**: US1–US3 funcionan de forma independiente (quickstart §3 pasos 3 y 4)

---

## Phase 6: User Story 4 - Historial de movimientos / kárdex (Priority: P2)

**Goal**: La pantalla Movimientos muestra el historial filtrable por producto, tipo y rango de fechas, paginado de 100 en 100; desde Productos se abre ya filtrado; permite registrar un movimiento.

**Independent Test**: Registrar movimientos de dos productos; filtrar por uno y por rango de fechas y ver solo los esperados, del más reciente al más antiguo; no existe editar ni borrar.

### Implementation for User Story 4

- [X] T048 [P] [US4] Crear `SearchMovementsQuery(Guid? ProductId, MovementType? Type, DateTime? FromUtc, DateTime? ToUtcExclusive, int Page = 1)` y `SearchMovementsHandler` en `src/Pos.Application/Inventory/SearchMovements/`: si `FromUtc > ToUtcExclusive` devolver `ValidationFailed` en el campo `DateRange` ("La fecha inicial no puede ser posterior a la final."); si no, llamar `SearchMovementsAsync`, resolver `CreatedByName` de cada fila ("Sistema" o primeros 8 caracteres del id) y devolver `Result<MovementPage>`; registrar en `src/Pos.Application/DependencyInjection.cs`
- [X] T049 [US4] Crear `MovementsProductFilter(Guid ProductId, string Name)` y `MovementsViewModel`/`MovementsView` en `src/Pos.Desktop/Inventory/` (depende de T048, T041, T024, T022): filtros Producto (selector con búsqueda que invoca `SearchStockQuery(text, StockFilter.All, IncludeInactive: true)` y botón para quitar), Tipo (Todos o uno de los 4), Desde y Hasta con `CalendarDatePicker` opcionales (convertir fechas locales a UTC: 00:00 del día inicial hasta las 00:00 del día siguiente al final), botón "Registrar movimiento" siempre habilitado que abre el formulario eligiendo producto; columnas Fecha y hora (local `dd/MM/yyyy HH:mm`), Producto (nombre y SKU), Tipo, Cantidad (con signo + o − según el tipo), Existencia resultante, Unidad, Motivo, Referencia y Usuario; paginación de 100; **sin acciones de fila** (no editar ni borrar); implementar `INavigationArgumentReceiver` para `MovementsProductFilter` (fija el producto, limpia tipo y fechas, página 1); tras guardar un movimiento recargar
- [X] T050 [US4] En `src/Pos.Desktop/Inventory/InventoryModule.cs` sustituir la página `AddComingSoonPage` de `inventory.movements` por `MovementsViewModel`/`MovementsView`, agregar los textos de Movimientos a `src/Pos.Desktop/Resources/Strings.resx`, y agregar la acción de fila "Ver movimientos" al listado de productos (`src/Pos.Desktop/Products/`, visible solo si el producto controla inventario) para navegar a `inventory.movements` con `MovementsProductFilter(productId, name)` (FR-019)

**Checkpoint**: US4 funcional (quickstart §3 paso 5)

---

## Phase 7: User Story 5 - Alertas de existencia baja (Priority: P2)

**Goal**: Las tarjetas de Inicio muestran los conteos reales de productos activos en existencia baja y sin existencia y llevan a Existencias ya filtrada.

**Independent Test**: Con un producto de mínimo 5 y existencia 5, y otro con existencia 0, las tarjetas muestran 1 y 1 y al abrirlas se listan los productos correctos; un producto inactivo sin existencia no cuenta.

### Implementation for User Story 5

- [X] T051 [P] [US5] Crear `GetStockAlertsHandler` en `src/Pos.Application/Inventory/GetStockAlerts/` que devuelve `Result<StockAlertCounts>` desde `CountAlertsAsync` (FR-021); registrar en `src/Pos.Application/DependencyInjection.cs`
- [X] T052 [US5] Crear `LowStockCard` y `OutOfStockCard` en `src/Pos.Desktop/Inventory/` (depende de T051, T023): dejan de heredar de `ComingSoonCard`, cargan `GetStockAlerts` cada vez que se muestra Inicio y muestran el conteo ("0" navegable si no hay), con `NavigateTo = inventory.stock` y `NavigationArgument` igual a `StockFilter.Low` o `StockFilter.Out`
- [X] T053 [US5] Retirar `LowStockCard` y `OutOfStockCard` de `src/Pos.Desktop/Home/Cards/ComingSoonCard.cs`, registrar las nuevas tarjetas en `InventoryModule`/la inyección de dependencias en lugar de las anteriores, y eliminar el estado vacío "disponible más adelante" de esas tarjetas

**Checkpoint**: Todas las historias funcionales (quickstart §3 pasos 7 y 8)

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T054 Ejecutar `dotnet build -v q` y `dotnet test --verbosity quiet` desde la raíz: 0 errores, 0 advertencias, todo en verde (incluida `Pos.ArchitectureTests`)
- [ ] T055 (pendiente: verificado solo el arranque con migración real v0.2.0 → actual en una carpeta temporal y el log sin errores; falta recorrer las pantallas a mano) Recorrer manualmente `specs/004-inventory-management/quickstart.md` §3 con `dotnet run --project src/Pos.Desktop` y §4 (revisión del SQL de la migración) y corregir lo que difiera
- [X] T056 [P] Revisar que los mensajes al operador estén en español sin detalles técnicos (FR-022), que los textos nuevos estén en `Strings.resx` y que fallas inesperadas pasen por `OperationRunner` sin cerrar la aplicación
- [X] T057 [P] Actualizar la documentación de operación/soporte del repositorio (README o docs existentes) con el módulo de inventario y la base de ejemplo `v0.3.0.db`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; **bloquea** todas las historias
- **US1, US2, US3 (P1)**: dependen de Foundational. US2 no depende de US1; US3 usa el formulario de US2 (T041) para abrir movimientos
- **US4, US5 (P2)**: dependen de Foundational; US4 usa el formulario de US2 (T041); US5 usa la pantalla de US3 como destino (T046, `StockFilter`)
- **Polish (Phase 8)**: depende de las historias deseadas

### User Story Dependencies

- **US1**: independiente tras Foundational
- **US2**: independiente tras Foundational (la prueba manual necesita un producto con inventario, que crea US1)
- **US3**: requiere T041 de US2 para la acción de registrar
- **US4**: requiere T041 de US2; la acción "Ver movimientos" (T050) requiere T033 de US1
- **US5**: requiere T046 de US3 para que la navegación llegue filtrada

### Within Each Phase

- Dominio antes que Application, Application antes que Infrastructure/Desktop cuando hay dependencia declarada
- T018 → T019 → T025; T014 → T021; T041 → T046/T049
- Pruebas obligatorias escritas junto a la implementación; correr solo el proyecto modificado

### Parallel Opportunities

- Foundational: T002, T003, T004, T005 en paralelo; T009–T011 en paralelo tras su tipo; T012, T013, T015, T016, T017, T020, T022, T024 en paralelo
- US2: T034–T037 en paralelo; T038 en paralelo con las pruebas
- US3: T043, T044 y T045 en paralelo
- US4 (T048) y US5 (T051) pueden avanzar en paralelo con US3 una vez hecho T041

---

## Parallel Example: Foundational

```bash
Task: "T002 Crear Quantity en src/Pos.Domain/Common/Quantity.cs"
Task: "T003 DecimalPlaces en src/Pos.Domain/Products/UnitOfMeasure.cs"
Task: "T004 MovementType en src/Pos.Domain/Inventory/MovementType.cs"
Task: "T005 StockStatus en src/Pos.Domain/Inventory/StockStatus.cs"
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US3, todas P1)

1. Phase 1 y Phase 2 (fundación, migración incluida)
2. Phase 3 (US1): configurar productos → validar
3. Phase 4 (US2): registrar movimientos → validar consistencia
4. Phase 5 (US3): consultar existencias → **STOP y validar**: ya se puede operar el inventario

### Incremental Delivery

1. Fundación → US1 → US2 → US3 (MVP de inventario operable)
2. US4 (kárdex) → US5 (alertas en Inicio)
3. Polish y recorrido del quickstart

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- Los movimientos son inmutables: nunca agregar `Update`/`Remove` de `InventoryMovement`
- Nunca redondear cantidades; usar `Quantity` y milésimas (`long`) de extremo a extremo
- No se prueban ViewModels ni vistas (constitución v1.2.0)
- Confirmar tras cada tarea o grupo lógico
