---

description: "Lista de tareas: Módulo de ventas"
---

# Tasks: Módulo de ventas

**Input**: Documentos de diseño en `specs/005-sales-module/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/use-cases.md, contracts/ui.md, quickstart.md

**Tests**: Solo los que exige la constitución v1.2.0 (Principio VI, pruebas mínimas): reglas con cálculo
(`SaleMath`, `Cart`, `Checkout`, `StockLevel`, `ProductStock`), validaciones de integridad (cancelación,
idempotencia, folio), consistencia de inventario, atomicidad, estado SQL vs dominio, Inicio vs listado,
rendimiento, migración y arquitectura (quickstart §2). **No** se escriben pruebas de ViewModels ni de vistas.
Al implementar, ejecutar solo el proyecto de pruebas modificado (`dotnet test tests/<Proyecto> --verbosity quiet`);
compilar con `dotnet build -v q` (0 advertencias).

**Organization**: Tareas agrupadas por historia de usuario. Código en inglés, textos de interfaz en español
(`src/Pos.Desktop/Resources/Strings.resx`).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: Historia a la que pertenece (US1..US7)

## Path Conventions

Solución por capas: `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`;
pruebas en `tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`, `tests/Pos.Infrastructure.Tests`,
`tests/Pos.ArchitectureTests`. Antes de editar un archivo existente, leerlo y seguir su estilo (predicados
estáticos, `Result`, `FieldError`, patrones de `Inventory` y `Products`). Detalle de cada tipo y regla:
[data-model.md](data-model.md); firmas de casos de uso y puertos: [contracts/use-cases.md](contracts/use-cases.md);
pantallas y atajos: [contracts/ui.md](contracts/ui.md).

---

## Phase 1: Setup

**Purpose**: Verificar el punto de partida

- [X] T001 Verificar que `dotnet build -v q` y `dotnet test --verbosity quiet` pasan en la rama `005-sales-module` antes de cambiar código, y revisar cómo `src/Pos.Domain/Inventory/`, `src/Pos.Application/Inventory/RegisterMovement/`, `src/Pos.Infrastructure/Inventory/InventoryRepository.cs` y `src/Pos.Desktop/Inventory/` estructuran dominio, caso de uso, repositorio, listado paginado y formulario (patrón a replicar)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Existencia con signo, tipos de movimiento, dominio de ventas, esquema, migración, puertos y errores que necesitan todas las historias

**⚠️ CRITICAL**: Ninguna historia puede empezar hasta terminar esta fase

### Dominio: existencia con signo (research §3)

- [X] T002 [P] Crear `StockLevel` en `src/Pos.Domain/Inventory/StockLevel.cs`: `readonly record struct` con `long Thousandths` con signo, rango ±`Quantity.MaxStockThousandths` (fuera de rango lanza `DomainException`), `Zero`, `IsNegative`, `FromThousandths(long)`, `From(Quantity)`, operadores `+`/`-` con `Quantity` (con `checked` y validación de rango), y comparación con `Quantity` (`StockLevel < Quantity`, `>`, `<=`, `>=`)
- [X] T003 Modificar `src/Pos.Domain/Inventory/MovementType.cs` (depende de T002): agregar `Sale` (código `SALE`, `IsIncrease` falso, no requiere motivo) y `SaleCancellation` (código `SALE_CANCEL`, `IsIncrease` verdadero, no requiere motivo) a enum, `IsIncrease`, `ToCode` y `FromCode`
- [X] T004 Modificar `ProductStock` e `InventoryMovement` en `src/Pos.Domain/Inventory/ProductStock.cs` e `InventoryMovement.cs` (depende de T002, T003): `OnHand` y `ResultingStock` pasan de `Quantity` a `StockLevel` (las columnas `OnHand`/`ResultingStock` siguen siendo `INTEGER`); `WouldGoNegative(StockLevel, Quantity)` y `WouldExceedMaximum(StockLevel, Quantity)` reciben el nuevo tipo (con existencia negativa, todo `AdjustOut` se rechaza); agregar `IsShort(StockLevel onHand, Quantity requested)` (`requested > onHand`); agregar `RecordSale(Quantity quantity, UnitOfMeasure unit, string reference)` → `InventoryMovement` (exige `TracksInventory`, cantidad > 0 y con los decimales de la unidad, **puede dejar la existencia negativa**, tipo `SALE`, referencia = folio, hasta `Reference` 50) y `RecordSaleCancellation(Quantity quantity, string reference)` (suma sin revisar estado ni configuración actual del producto, tipo `SALE_CANCEL`, valida `WouldExceedMaximum`). `Record(...)` conserva sus reglas y rechaza los tipos `Sale` y `SaleCancellation`. Solo dominio, más el cambio mínimo en los llamadores existentes para que compile (conversiones `StockLevel`↔`Quantity`); los cambios de comportamiento en Application, Infrastructure y Desktop se hacen en T020, T021 y T022
- [X] T005 [P] Modificar `StockStatusRule.Evaluate(StockLevel onHand, Quantity? minimum)` en `src/Pos.Domain/Inventory/StockStatus.cs` (depende de T002): `Out` si `onHand <= 0` (FR-026); `Low` si `onHand > 0` y hay mínimo y `onHand <= minimum`; `Normal` en otro caso. Solo dominio, más el cambio mínimo en sus llamadores para que compile; el predicado SQL se cambia en T020

### Dominio: ventas (research §1, §2, §6)

- [X] T006 [P] Crear `SaleMath.LineAmount(Quantity quantity, Money unitPrice)` → `Money` en `src/Pos.Domain/Sales/SaleMath.cs`: `(quantity.Thousandths * unitPrice.Cents + 500) / 1000` en `long` con `checked`, redondeo "mitad hacia arriba" una sola vez por línea (research §2); lanza `DomainException` si el resultado excede `Money.MaxCents`
- [X] T007 [P] Crear `PaymentMethod` (`Cash` = `CASH`, `Card` = `CARD`, `Transfer` = `TRANSFER`, con `ToCode`/`FromCode`) y `SaleStatus` (`Completed` = `COMPLETED`, `Cancelled` = `CANCELLED`, con `ToCode`/`FromCode`) en `src/Pos.Domain/Sales/PaymentMethod.cs` y `SaleStatus.cs`; y `Folio` en `src/Pos.Domain/Sales/Folio.cs`: `Format(long number)` → `V-000123` (`V-{0:000000}`) y `TryParse(string? text, out long number)` que acepta `V-000123`, `v123` o `123` (número > 0)
- [X] T008 Crear `CartLine` y `Cart` en `src/Pos.Domain/Sales/CartLine.cs` y `Cart.cs` (depende de T006): `CartLine` con `ProductId`, `Name`, `Sku`, `UnitCode`, `DecimalPlaces`, `TracksInventory`, `UnitPrice` (`Money`), `Quantity`, `Amount` (`Money` = `SaleMath.LineAmount`), `IsUnavailable` y `UnavailableReason` (`Inactive` o `Deleted`). `Cart` con `DraftId` (`Guid.CreateVersion7()`), `Lines` (orden de captura), `Total` (suma exacta de `Amount`, no puede exceder `Money.MaxCents`), `Add(product, quantity = 1)` (si el producto ya está incrementa esa línea, FR-003; rechaza no vendibles con `DomainException`), `SetQuantity(productId, Quantity)` (cantidad > 0, con los decimales de la unidad, importe de línea y total ≤ `Money.MaxCents`; si falla lanza `DomainException` y conserva el valor anterior), `Remove(productId)`, `Clear()` (nuevo `DraftId`), `ApplyCurrentPrices(prices)` (reemplaza precios y marca `IsUnavailable`), `CanCheckout` (tiene líneas, total > 0 y ninguna línea no disponible) y `Restore(Guid draftId, lines)` estático
- [X] T009 [P] Crear `Checkout` en `src/Pos.Domain/Sales/Checkout.cs` (depende de T007): `Total`, a lo más un pago en efectivo con `Received` y cero o más `Card`/`Transfer` con `Amount` (> 0, ≤ `Pending` al agregarlo) y `Reference` opcional (hasta 50, recortada); `NonCashTotal`, `CashApplied = Total − NonCashTotal`, `Change = Received − CashApplied` (solo efectivo genera cambio), `Shortfall = max(0, Total − NonCashTotal − Received)`, `Pending`, `CanConfirm` (`Shortfall == 0` y, con efectivo, `Change >= 0`), `SetCashReceived(Money)`, `QuickAmount` (exacto = `Pending` sin contar el efectivo; billetes 20, 50, 100, 200, 500 y 1000 reemplazan el recibido), `RemovePayment`, `ToPayments()` (monto aplicado, recibido y cambio; la suma de montos aplicados = total). Un pago no efectivo que exceda el pendiente lanza `DomainException` (research §6)
- [X] T010 Crear `SaleLine`, `SalePayment` y `Sale` en `src/Pos.Domain/Sales/SaleLine.cs`, `SalePayment.cs` y `Sale.cs` (depende de T003, T006, T007, T009): campos y restricciones tal como en [data-model.md](data-model.md): `Sale` con `Id` (v7), `FolioNumber`, `DraftId`, `Total` (`Money`), `Status`, `CancellationReason` (hasta 250, obligatorio si está cancelada), `CancelledAt`/`CancelledBy`, `CreatedAt/By`, `UpdatedAt/By`, `Version`, `Lines`, `Payments`; `SaleLine` con `Position`, copias `ProductName` (200), `ProductSku` (50), `UnitCode` (3), `DecimalPlaces`, `UnitPrice`, `QuantityThousandths`, `Amount`, `SaleMovementId` y `CancellationMovementId` (ambos `Guid?`); `SalePayment` con `Method`, `AmountCents` (> 0), `ReceivedCents`/`ChangeCents` (`long?`, solo `CASH`) y `Reference` (hasta 50, solo `CARD`/`TRANSFER`). `Sale.Register(long folioNumber, Guid draftId, lines, payments)` valida: al menos una línea, total > 0, suma de importes de línea = total = suma de `AmountCents` de pagos, a lo más un pago en efectivo; `Sale.Cancel(string reason, DateTime utcNow, Guid userId)` exige estado `Completed` (si no, `DomainException`, FR-035), motivo no vacío tras recortar y ≤ 250 (FR-033), y deja `Cancelled` con motivo, fecha (UTC) y usuario

### Pruebas de dominio (obligatorias por la constitución)

- [X] T011 [P] Escribir `tests/Pos.Domain.Tests/Sales/SaleMathTests.cs`: `LineAmount(0.333 kg, $10.05)` = $3.35 (3.34665 → 3.35); `LineAmount(0.5, $0.01)` = $0.01 (mitad hacia arriba); un resultado que excede `Money.MaxCents` lanza `DomainException`; el total de un `Cart` es la suma exacta de importes de línea (SC-008)
- [X] T012 [P] Escribir `tests/Pos.Domain.Tests/Sales/CartTests.cs`: agregar el mismo producto incrementa la línea; una cantidad con decimales en pieza se rechaza y conserva el valor anterior; cantidad 0 o negativa se rechaza; un producto inactivo no se agrega; `CanCheckout` es falso con una línea no disponible o total 0 (FR-003, FR-005, FR-007)
- [X] T013 [P] Escribir `tests/Pos.Domain.Tests/Sales/CheckoutTests.cs`: total $85.50 con $100 en efectivo da cambio $14.50; $50 efectivo + $150 tarjeta sobre $200 da cambio 0 y `CanConfirm`; con $150 pagados sobre $200 hay `Shortfall` $50 y no se puede confirmar; una tarjeta mayor que el pendiente se rechaza; `QuickAmount` exacto usa el pendiente (US3 escenarios 1 a 5)
- [X] T014 [P] Escribir `tests/Pos.Domain.Tests/Sales/SaleTests.cs`: `Register` rechaza pagos cuya suma ≠ total; `Cancel` sin motivo se rechaza y cancelar dos veces lanza `DomainException`; `Cancel` deja estado, motivo, fecha y usuario
- [X] T015 [P] Ampliar `tests/Pos.Domain.Tests/Inventory/ProductStockTests.cs` y `StockStatusRuleTests.cs`: `RecordSale` de 5 con existencia 3 deja −2; `AdjustOut` con existencia −2 se rechaza; `RecordSaleCancellation` restaura sin revisar el estado del producto; `StockStatusRule` con −2 da `Out`; `IsShort` en el límite (cantidad = existencia no es faltante)

### Persistencia y migración (research §3, §4, §7, §10, §13)

- [X] T016 [P] Crear `SaleDraft` (`Slot` = 1, `DraftId`, `LinesJson`, `UpdatedAt`) en `src/Pos.Domain/Sales/SaleDraft.cs` y `AuditEntry` (`Id` v7, `Action` 40, `EntityType` 40, `EntityId`, `Details` 500, `CreatedAt`, `CreatedBy`) en `src/Pos.Domain/Audit/AuditEntry.cs`; ambos según [data-model.md](data-model.md)
- [X] T017 Crear configuraciones EF en `src/Pos.Infrastructure/Persistence/Configurations/` (depende de T010, T016): `SaleConfiguration` (tabla `Sales`; `TotalCents` `INTEGER`; `Status` texto 10 con `ToCode`/`FromCode`; `Version` como token de concurrencia; índices únicos `IX_Sales_Folio` sobre `FolioNumber` e `IX_Sales_DraftId` sobre `DraftId`; `IX_Sales_CreatedAt (CreatedAt, Id)` y `IX_Sales_Status_CreatedAt (Status, CreatedAt)`), `SaleLineConfiguration` (`SaleLines`; único `(SaleId, Position)`; FK a `Sales`, `Products` y `InventoryMovements`, **todas con `DeleteBehavior.Restrict`**, porque las ventas nunca se borran; `IX_SaleLines_Product (ProductId)`), `SalePaymentConfiguration` (`SalePayments`; `Method` texto), `SaleDraftConfiguration` (`SaleDrafts`, PK `Slot`) y `AuditEntryConfiguration` (`AuditEntries`; índice `(EntityType, EntityId)`). Modificar `ProductStockConfiguration` e `InventoryMovementConfiguration` para el tipo `StockLevel` (mismas columnas) y ampliar `HasMaxLength` de `Type` si hace falta (`SALE_CANCEL` cabe en 12)
- [X] T018 Modificar `src/Pos.Infrastructure/Persistence/PosDbContext.cs` (depende de T017): agregar `DbSet` de `Sales`, `SaleLines`, `SalePayments`, `SaleDrafts` y `AuditEntries`; aplicar las configuraciones; ampliar el guardián de inmutabilidad para rechazar `Modified` y `Deleted` de `AuditEntry` (mensaje en español como el de los movimientos)
- [X] T019 Generar la migración `SalesModule` con `dotnet ef migrations add SalesModule` en `src/Pos.Infrastructure/Persistence/Migrations/` (depende de T018) y **revisar el SQL generado**: solo debe contener `CREATE TABLE`/`CREATE INDEX` de las 5 tablas nuevas y no reconstruir `Products`, `ProductStocks` ni `InventoryMovements` (Principio IV, research §13). Subir la versión a 0.4.0 en `Directory.Build.props`
- [X] T020 [P] Actualizar `src/Pos.Infrastructure/Inventory/InventoryRepository.cs` (depende de T004 y T005): el predicado SQL de "sin existencia" pasa de `OnHand == 0` a `OnHand <= 0` en `FilterByStatus` y los conteos; agregar `GetStocksAsync(IReadOnlyCollection<Guid> productIds, CancellationToken)` (con seguimiento) a `IInventoryRepository` en `src/Pos.Application/Inventory/IInventoryRepository.cs`; `StockItemDto`, `MovementDto` y los mensajes de `src/Pos.Application/Inventory/InventoryMessages.cs` aceptan existencias negativas
- [X] T021 [P] Modificar `RegisterMovementValidator`/`RegisterMovementHandler` en `src/Pos.Application/Inventory/RegisterMovement/` (depende de T003 y T004): solo aceptan `Initial`, `Receipt`, `AdjustIn` y `AdjustOut`; `Sale` y `SaleCancellation` dan `ValidationFailed` con `InventoryMessages.TypeInvalid`
- [X] T022 [P] Actualizar `src/Pos.Desktop/Inventory/MovementTypeLabels.cs` y `Strings.resx`: "Salida por venta" (`Sale`) y "Cancelación de venta" (`SaleCancellation`); `MovementsViewModel` muestra el signo correcto y `StockViewModel`/`StockStatusConverters` muestran las existencias negativas con el estado "sin existencia"

### Puertos y errores de Application

- [X] T023 [P] Agregar a `src/Pos.Application/Abstractions/Error.cs` los errores `SaleChanged(IReadOnlyList<SaleLineReview> Lines)` (creando en el mismo archivo, o en `src/Pos.Application/Sales/SaleLineReview.cs`, el record `SaleLineReview(Guid ProductId, long CurrentPriceCents, NotSellableReason? NotSellableReason, bool InsufficientStock, long? OnHandThousandths)` y el enum `NotSellableReason` (`Inactive`, `Deleted`), para que T023 compile sin depender de T024), `AlreadyRegistered(Guid SaleId, string Folio)` e `InvalidState(string Message)`; crear `src/Pos.Application/Abstractions/IAuditLog.cs` con `Add(string action, string entityType, Guid entityId, string? details)`
- [X] T024 Crear en `src/Pos.Application/Sales/` los DTOs y puertos (depende de T010, T023; `SaleLineReview` y `NotSellableReason` ya existen por T023): `SaleDtos.cs` (`SaleProductDto`, `ProductLookup`, `LookupKind`, `DraftLineDto`, `RecoveredDraft`, `RecoveredLineDto`, `SaleReview`, `ConfirmedSale`, `SaleListItemDto`, `SalePage`, `SaleDetailDto`, `SalesDashboard`, `DayWindow`, `DayTotal`, `TopProduct`), `ISaleRepository.cs`, `ISaleDraftStore.cs`, `SaleMessages.cs` y `SaleFields.cs` con las firmas de [contracts/use-cases.md](contracts/use-cases.md); mensajes de error en español
- [X] T025 [P] Ampliar `src/Pos.Application/Products/IProductRepository.cs` e implementarlo en `src/Pos.Infrastructure/Products/ProductRepository.cs`: `FindForSaleAsync(string code, CancellationToken)` (exacto por código de barras o SKU normalizado, incluye inactivos y borrados), `SearchForSaleAsync(string nameText, int limit, CancellationToken)` (activos primero, sin borrados; usa `ProductTextFilter`) y `GetManyAsync(IReadOnlyCollection<Guid> ids, bool includeDeleted, CancellationToken)`. Extender `tests/Pos.Application.Tests/TestSupport/InMemoryProductRepository.cs` para los métodos nuevos
- [X] T026 [P] Implementar `AuditLog` en `src/Pos.Infrastructure/Audit/AuditLog.cs` (agrega `AuditEntry` al `PosDbContext` del ámbito sin guardar, para que viaje en la transacción del caso de uso) y registrar solo `IAuditLog` en `src/Pos.Infrastructure/DependencyInjection.cs`; `ISaleDraftStore` se registra en T034 e `ISaleRepository` en T043, junto con sus clases

**Checkpoint**: `dotnet build -v q` sin advertencias; `dotnet test tests/Pos.Domain.Tests --verbosity quiet` en verde.

---

## Phase 3: User Story 1 - Capturar la venta (Priority: P1) 🎯 MVP

**Goal**: El operador arma la venta escaneando o buscando productos, con cantidades y total exactos, solo con teclado o táctil.

**Independent Test**: abrir el Punto de venta, escanear dos productos distintos y uno repetido, buscar un tercero por nombre, cambiar una cantidad y quitar una línea, verificando líneas, importes y total (quickstart §3 paso 1).

- [X] T027 [P] [US1] Crear `FindProductsForSale` en `src/Pos.Application/Sales/FindProductsForSale/` (`FindProductsForSaleQuery(string Text)`, `FindProductsForSaleHandler`) según research §8: texto vacío → `ValidationFailed`; primero coincidencia exacta de código de barras o SKU (incluye inactivos y borrados, con `NotSellableReason`); si no hay, búsqueda por nombre con hasta 20 resultados (activos primero, sin borrados); devuelve `ProductLookup` (`ExactMatch`, `NameMatches` o `None`) con precio, unidad, decimales, `TracksInventory` y existencia. Registrar en `DependencyInjection.cs` de Application
- [X] T028 [US1] Crear `src/Pos.Desktop/Sales/SalesModule.cs` (depende de T027): `AddSalesModule()` con el grupo "Ventas" (`sales`, `Icon.Sales`, orden 5) y la página "Punto de venta" (`sales.pos`, orden 0); reemplazar `AddSalesPlaceholders()` en `src/Pos.Desktop/Composition/HostBuilder.cs` por `AddSalesModule()` (las tarjetas de Inicio se sustituyen en US7; mientras tanto conservar los placeholders registrándolos desde el módulo); agregar el ícono `Icon.Sales` si falta en `Resources/Icons.axaml` y los textos de menú en `Strings.resx`
- [X] T029 [US1] Crear `ScanQueue` en `src/Pos.Desktop/Sales/ScanQueue.cs`: `Channel<string>` con un solo consumidor que atiende las lecturas en orden (research §8); el texto se toma y se limpia de forma síncrona en el evento de tecla, para que las lecturas seguidas no se mezclen
- [X] T030 [US1] Crear `PointOfSaleViewModel` en `src/Pos.Desktop/Sales/PointOfSaleViewModel.cs` (`PageViewModel`, depende de T008, T027, T029): mantiene un `Cart`; comandos capturar (Enter: `ExactMatch` agrega, varios abren el selector, `None` muestra el aviso "No se encontró ningún producto con el código {código}"), buscar (F2), cambiar cantidad (F4, o `*` solo cuando el campo de captura está vacío; con texto escrito `*` es un carácter más; una cantidad inválida muestra el mensaje y conserva el valor anterior), quitar línea (Supr), cancelar venta (F8, con confirmación "¿Cancelar la venta en curso?" mediante `IDialogService`) y mover selección; expone total, número de artículos y `CanCheckout`; los productos no vendibles muestran "{Nombre} está inactivo y no se puede vender" o "{Nombre} fue eliminado y no se puede vender". Toda llamada a casos de uso pasa por `UseCases`/`OperationRunner`. Sin cálculos: solo presenta el `Cart`
- [X] T031 [P] [US1] Crear `ProductChooserView`/`ProductChooserViewModel` en `src/Pos.Desktop/Sales/`: lista de resultados navegable con ↑/↓, Enter elige y Esc cierra; registrar con `AddComponentView` (patrón de `MovementEditorView`)
- [X] T032 [US1] Crear `PointOfSaleView.axaml` y `.axaml.cs` en `src/Pos.Desktop/Sales/` (depende de T030, T031) según [contracts/ui.md](contracts/ui.md): campo de captura con foco permanente, tabla de líneas (nombre, SKU, cantidad, precio, importe; fondo de advertencia para líneas no disponibles), total en grande (≥ 48 px), botones táctiles de alto mínimo 48 px (Buscar, Cantidad, Quitar, Cobrar, Cancelar venta) y barra inferior con "F2 Buscar · F4 Cantidad · Supr Quitar · F12 Cobrar · F8 Cancelar"; `KeyBinding` para los atajos; usar `MoneyConverter` y `QuantityConverter`
- [X] T033 [US1] Agregar el atajo global **F9** en `src/Pos.Desktop/Shell/MainWindow.axaml` (`KeyBinding` hacia `Navigator.NavigateAsync("sales.pos")` desde `MainViewModel`) y mostrar "F9" junto a la opción del menú en `MenuView.axaml`; agregar todos los textos de esta historia a `src/Pos.Desktop/Resources/Strings.resx`

**Checkpoint**: la captura funciona de punta a punta sin cobro (quickstart §3 paso 1).

---

## Phase 4: User Story 2 - La venta en curso no se pierde (Priority: P1)

**Goal**: Un cierre inesperado no pierde la venta en curso; al reabrir se ofrece recuperarla.

**Independent Test**: agregar líneas, terminar el proceso de forma abrupta, reabrir y verificar que se ofrece recuperar con las mismas líneas y cantidades (quickstart §3 paso 2).

- [X] T034 [US2] Implementar `SqliteSaleDraftStore` en `src/Pos.Infrastructure/Sales/SqliteSaleDraftStore.cs` (research §7): `LoadAsync`; `SaveAsync(draftId, lines)` con upsert de la fila única `Slot = 1` (`LinesJson` = `[{productId, quantityThousandths, unitPriceCents}]` en orden, `UpdatedAt` con `IClock`) que **no hace nada si ya existe una venta con ese `DraftId`**; `Remove()` (marca el borrador para borrado dentro de la unidad de trabajo del caso de uso) y `DiscardAsync`. Registrarlo en `DependencyInjection.cs` de Infrastructure. `SqliteConnectionStrings` ya fija `DefaultTimeout = 5` (espera de 5 s por el candado de escritura), así que una escritura del borrador concurrente con `ConfirmSale` espera en lugar de fallar; no cambiar ese valor
- [X] T035 [P] [US2] Crear `SaveSaleDraft` (`SaveSaleDraftCommand(Guid DraftId, IReadOnlyList<DraftLineDto> Lines)`; sin líneas equivale a descartar), `GetSaleDraft` (devuelve `RecoveredDraft?` con datos actuales del producto y `NotSellableReason?` por línea; los productos inexistentes se omiten con un aviso en el log) y `DiscardSaleDraft` en `src/Pos.Application/Sales/SaveSaleDraft/`, `GetSaleDraft/` y `DiscardSaleDraft/`; registrar en `DependencyInjection.cs`. Un fallo al guardar se registra con `LogWarning` (`DraftId`, número de líneas) y no propaga excepción (FR-013)
- [X] T036 [US2] Crear `DraftAutosaver` en `src/Pos.Desktop/Sales/DraftAutosaver.cs`: guarda tras **cada** cambio, sin debounce ni retraso (research §7, SC-004); serializa las escrituras (una a la vez y siempre la más reciente), expone `FlushAsync()` para esperar la pendiente y nunca lanza (registra el error y sigue, FR-013)
- [X] T037 [US2] Integrar el borrador en `PointOfSaleViewModel` (`src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`, depende de T035, T036): guardar tras cada cambio relevante (línea agregada, cantidad, quitar); al activarse por primera vez en la sesión, llamar `GetSaleDraft` y mostrar "Hay una venta sin terminar con N artículos por $X. ¿Desea recuperarla?" (Recuperar predeterminado / Descartar) con `Cart.Restore`; las líneas no disponibles se señalan y bloquean el cobro (US2 escenario 3); cancelar la venta (F8) y descartar llaman `DiscardSaleDraft`; implementar `ILeaveGuard` que solo espera `FlushAsync()` (sin preguntar)

**Checkpoint**: recuperación verificada manualmente con `kill -9` (quickstart §3 paso 2).

---

## Phase 5: User Story 3 - Cobrar (Priority: P1)

**Goal**: El operador cobra con efectivo, tarjeta, transferencia o pago mixto, con cambio y faltante correctos.

**Independent Test**: cobrar una venta de total conocido en efectivo con monto mayor y verificar el cambio; pago mixto; intentar confirmar con faltante (quickstart §3 pasos 3 y 4). La confirmación real se conecta en US4.

- [X] T038 [P] [US3] Crear `ReviewSale` en `src/Pos.Application/Sales/ReviewSale/` (`ReviewSaleQuery(IReadOnlyList<ReviewLineInput>)`, `ReviewSaleHandler`) según research §5: solo lee; por línea devuelve precio vigente, `NotSellableReason?` y, para productos con inventario, `InsufficientStock` y existencia (cantidad > existencia); registrar en `DependencyInjection.cs`
- [X] T039 [US3] Agregar `Cart.ApplyCurrentPrices` a la revisión previa en `PointOfSaleViewModel` (`src/Pos.Desktop/Sales/PointOfSaleViewModel.cs`, depende de T038): al pulsar Cobrar (F12) ejecutar `ReviewSale`, aplicar precios al `Cart` (el total se actualiza y se muestra antes de cobrar, Edge Case de cambio de precio), señalar líneas no vendibles (mensaje "Quite las líneas señaladas para cobrar" y Cobrar deshabilitado), y con `InsufficientStock` pedir confirmación de la advertencia de existencia insuficiente antes de abrir el cobro
- [X] T040 [US3] Crear `CheckoutViewModel` en `src/Pos.Desktop/Sales/CheckoutViewModel.cs` (depende de T009): envuelve un `Checkout` de dominio (sin cálculos propios); efectivo con campo "Recibido" y botones Exacto/$20/$50/$100/$200/$500/$1,000 (teclas **F5** y **1–6** cuando el foco no está en un campo de texto), tarjeta/transferencia con "Monto" (inicia en el pendiente) y "Referencia" opcional y lista de pagos con quitar (Supr); muestra pagado, pendiente/cambio en grande y "Faltan $X"; mensaje "El monto con tarjeta o transferencia no puede exceder el pendiente de $X"; Esc regresa a la venta conservando los pagos mientras no cambie el total; `ConfirmCommand` (Enter o F12) deshabilitado si `!CanConfirm` o mientras corre (FR-020); el delegado de confirmación se inyecta desde `PointOfSaleViewModel`
- [X] T041 [US3] Crear `CheckoutView.axaml` y `.axaml.cs` en `src/Pos.Desktop/Sales/` (depende de T040) según [contracts/ui.md](contracts/ui.md): diálogo modal sobre el Punto de venta, controles táctiles de alto mínimo 48 px; registrar `AddComponentView<CheckoutViewModel, CheckoutView>()` en `SalesModule.cs`; textos en `Strings.resx` y etiquetas de forma de pago en `src/Pos.Desktop/Sales/PaymentMethodLabels.cs`
- [X] T042 [US3] Abrir el cobro desde `PointOfSaleViewModel` (depende de T039, T040, T041): tras la revisión, mostrar `CheckoutView` con el total actual; al cancelar el cobro volver a la venta sin perder líneas ni pagos capturados

**Checkpoint**: el cobro muestra cambio, faltante y pago mixto correctos; aún no registra la venta.

---

## Phase 6: User Story 4 - Registrar la venta y descontar inventario (Priority: P1)

**Goal**: Al confirmar el cobro, venta, líneas, pagos, movimientos y borrado del borrador se guardan en una sola transacción, con folio consecutivo.

**Independent Test**: registrar una venta con un producto que controla inventario y uno que no; verificar folio, líneas, pagos, un movimiento de salida y la nueva existencia; simular una falla y verificar que no quedó nada parcial (quickstart §3 pasos 3 a 6).

- [X] T043 [US4] Implementar `SaleRepository` en `src/Pos.Infrastructure/Sales/SaleRepository.cs` (métodos de escritura primero, depende de T017, T024): `ExistsForDraftAsync`, `FindByDraftAsync`, `NextFolioNumberAsync` (`MAX(FolioNumber) + 1`, 1 si no hay ventas, se llama dentro de la transacción de escritura), `GetAsync(Guid id)` (con líneas y pagos, con seguimiento), `Add(Sale)` y `SaveChangesAsync` que captura `DbUpdateConcurrencyException` y `SqliteException` de restricción única (códigos 1555/2067) y devuelve `Conflict` o `Duplicate("DraftId" | "Folio")` según el índice violado (mismo patrón que `InventoryRepository.SaveChangesAsync`); registrar `ISaleRepository` en `DependencyInjection.cs` de Infrastructure
- [X] T044 [US4] Crear `ConfirmSale` en `src/Pos.Application/Sales/ConfirmSale/` (`ConfirmSaleCommand`, `ConfirmLineInput`, `PaymentInput`, `ConfirmSaleValidator`, `ConfirmSaleHandler`, depende de T004, T010, T025, T043): validar la forma con FluentValidation (al menos 1 línea, productos sin repetir, pagos válidos); `IWriteTransactions.BeginAsync`; si existe venta con `DraftId` devolver `AlreadyRegistered`; cargar productos con `GetManyAsync(includeDeleted: true)` y devolver `SaleChanged` si algún precio ≠ `ExpectedUnitPriceCents` o algún producto no es vendible; validar cantidades contra los decimales de la unidad; construir `Checkout` y exigir `CanConfirm` (si no, `ValidationFailed` con el faltante); folio `NextFolioNumberAsync`; por cada línea con inventario cargar/crear `ProductStock` (`GetStocksAsync`, `ProductStock.Start` si no existe), `RecordSale(quantity, unit, Folio.Format(folio))` (se permite existencia negativa) y guardar el id del movimiento en `SaleLine.SaleMovementId`; `Sale.Register`; `ISaleDraftStore.Remove()`; guardar y `CommitAsync`. `Conflict` o `Duplicate("DraftId")` → `AlreadyRegistered` (`FindByDraftAsync`), `Duplicate("Folio")` → `Conflict`. Registrar con `LogInformation` "Venta registrada" (`SaleId`, `Folio`, líneas, `TotalCents`) y en error `DraftId` y número de líneas, sin referencias de pago (Principio VIII). Registrar en `DependencyInjection.cs` de Application
- [X] T045 [US4] Conectar la confirmación en el Punto de venta (`src/Pos.Desktop/Sales/PointOfSaleViewModel.cs` y `CheckoutViewModel.cs`, depende de T037, T042, T044): `FlushAsync()` del borrador y `ConfirmSale` con `Cart.DraftId`; éxito o `AlreadyRegistered` → mostrar "Venta V-000123 registrada · Cambio $X" (cambio en grande) hasta el siguiente escaneo o Enter, luego `Cart.Clear()` (nuevo `DraftId`) y el foco en el campo de captura; `SaleChanged` → cerrar el cobro, aplicar la revisión y mostrar "Los precios o la disponibilidad cambiaron; revise el total antes de cobrar"; `Conflict` o falla inesperada → "No se pudo registrar la venta. La venta se conservó; intente de nuevo." con carrito y borrador intactos
- [X] T046 [P] [US4] Ampliar `tests/Pos.Infrastructure.Tests/TestSupport/InventoryTestSupport.cs` (o crear `tests/Pos.Infrastructure.Tests/TestSupport/SalesTestSupport.cs`) con ayudantes para crear productos con inventario, armar un `ConfirmSaleCommand` y resolver `ConfirmSaleHandler` sobre SQLite real, siguiendo `TestDb`
- [X] T047 [P] [US4] Escribir `tests/Pos.Infrastructure.Tests/Sales/SaleConsistencyTests.cs` (obligatoria): con una secuencia reproducible (semilla fija) de ventas (algunas con existencia insuficiente), cancelaciones, entradas y ajustes sobre varios productos, la existencia de cada producto es igual a la suma con signo de sus movimientos y Σ importes de línea = total = Σ pagos (FR-027, SC-006, invariantes 1 y 2 de data-model)
- [X] T048 [P] [US4] Escribir `tests/Pos.Infrastructure.Tests/Sales/SaleAtomicityTests.cs`: una falla forzada antes del `CommitAsync` deja 0 filas nuevas en `Sales`, `SaleLines`, `SalePayments`, `InventoryMovements` y `AuditEntries` y el borrador intacto; al reintentar se asigna el mismo folio (SC-003, FR-022)
- [X] T049 [P] [US4] Escribir `tests/Pos.Infrastructure.Tests/Sales/SaleFolioAndIdempotencyTests.cs`: dos confirmaciones concurrentes con `DraftId` distintos producen folios n y n+1; confirmar dos veces el mismo `DraftId` produce una sola venta y la segunda devuelve `AlreadyRegistered` con el mismo folio (SC-005, FR-020)
- [X] T050 [P] [US4] Escribir `tests/Pos.Infrastructure.Tests/Sales/SaleDraftTests.cs`: al confirmar no queda borrador y un `SaveSaleDraft` atrasado con el mismo `DraftId` no lo revive; `SaveSaleDraft` sin líneas equivale a descartar; un `SaveSaleDraft` lanzado mientras otra conexión retiene la transacción de escritura espera y termina bien (no falla con `SQLITE_BUSY`)
- [X] T051 [P] [US4] Escribir `tests/Pos.Infrastructure.Tests/Sales/SaleSnapshotTests.cs`: cambiar nombre, SKU y precio del producto después de vender no altera `SaleLines` (SC-007); un producto sin inventario no genera movimiento y `SaleMovementId` es nulo (US4 escenario 2); `Sale.CreatedBy` y `CreatedBy` de los movimientos son `SystemUser.Id` (FR-030)
- [X] T052 [P] [US4] Ampliar `tests/Pos.Infrastructure.Tests/Inventory/StockStatusQueryTests.cs`: el filtro "sin existencia" y el conteo de `CountAlertsAsync` incluyen las existencias negativas y coinciden con `StockStatusRule` (FR-026)

**Checkpoint**: MVP funcional (US1 a US4): se captura, cobra y registra sin perder la venta. Ejecutar `dotnet test tests/Pos.Domain.Tests` y `tests/Pos.Infrastructure.Tests`.

---

## Phase 7: User Story 5 - Consultar ventas realizadas (Priority: P2)

**Goal**: Listar ventas con filtros y paginación, y ver el detalle de cada una.

**Independent Test**: con ventas de varios días y una cancelada, filtrar por rango de fechas, por folio y por estado, y abrir el detalle (quickstart §3 paso 7).

- [X] T053 [US5] Completar `SaleRepository` en `src/Pos.Infrastructure/Sales/SaleRepository.cs` con `SearchAsync` (filtros de rango UTC `[desde, hasta)`, folio exacto y estado; orden `CreatedAt` descendente y `Id` descendente; páginas de 100; formas de pago distintas por venta) y `GetDetailAsync` (encabezado, líneas y pagos con los valores guardados)
- [X] T054 [P] [US5] Crear `SearchSales` (`SearchSalesQuery(DateTime? FromUtc, DateTime? ToUtcExclusive, string? FolioText, SaleStatus? Status, int Page = 1)`; un folio inválido o un rango invertido → `ValidationFailed`; `FolioText` se interpreta con `Folio.TryParse`) y `GetSale` (`NotFound` si no existe) en `src/Pos.Application/Sales/SearchSales/` y `GetSale/`; registrar en `DependencyInjection.cs`
- [X] T055 [US5] Crear `SalesHistoryViewModel` y `SalesHistoryView.axaml` en `src/Pos.Desktop/Sales/` (depende de T054) según [contracts/ui.md](contracts/ui.md): filtros Desde/Hasta (fechas locales convertidas a `[desde 00:00, hasta+1 00:00)` en UTC), Folio y Estado (Todas/Completadas/Canceladas), paginación de 100 igual que `MovementsViewModel`, columnas Folio, Fecha (`dd/MM/yyyy HH:mm` local), Total, Formas de pago y Estado (canceladas atenuadas); registrar la página "Ventas realizadas" (`sales.history`, orden 10) en `SalesModule.cs`
- [X] T056 [US5] Crear `SaleDetailViewModel` y `SaleDetailView.axaml` en `src/Pos.Desktop/Sales/` (depende de T054): encabezado con folio, fecha y estado (con motivo, fecha y usuario si está cancelada), líneas (nombre, SKU, cantidad, precio, importe) y pagos (forma, monto, recibido, cambio, referencia); abre con Enter o doble clic desde el listado; registrar con `AddComponentView`

**Checkpoint**: el listado y el detalle funcionan con datos reales.

---

## Phase 8: User Story 6 - Cancelar una venta registrada (Priority: P2)

**Goal**: Cancelar una venta completa con motivo, regresando el inventario y dejando rastro en la bitácora.

**Independent Test**: cancelar una venta con un producto que controla inventario y verificar estado, motivo, movimiento de regreso, existencia restaurada y bitácora; intentar cancelar de nuevo (quickstart §3 paso 7).

- [X] T057 [US6] Crear `CancelSale` en `src/Pos.Application/Sales/CancelSale/` (`CancelSaleCommand(Guid SaleId, int ExpectedVersion, string Reason)`, validador y `CancelSaleHandler`, depende de T004, T010, T043): validar motivo no vacío y ≤ 250 (`ValidationFailed`); en una transacción de escritura cargar la venta (`NotFound`), comparar `ExpectedVersion` (`Conflict`), `Sale.Cancel` (una venta ya cancelada → `InvalidState` "Esta venta ya está cancelada"); por cada línea con `SaleMovementId` cargar la existencia del producto (aunque esté inactivo o borrado), `RecordSaleCancellation(cantidad, folio)` y guardar el id en `SaleLine.CancellationMovementId`; `IAuditLog.Add("SALE_CANCELLED", "Sale", saleId, "Folio V-… . Motivo: …")`; guardar y confirmar. Registrar en el log la cancelación (`SaleId`, `Folio`) y en `DependencyInjection.cs`
- [X] T058 [US6] Crear `CancelSaleViewModel` y `CancelSaleView.axaml` en `src/Pos.Desktop/Sales/` (depende de T057) y el botón "Cancelar venta" en `SaleDetailView` (solo si está completada): formulario con "Motivo" (obligatorio, máximo 250) y la confirmación "Se regresarán las existencias y la venta quedará cancelada"; errores `InvalidState` ("Esta venta ya está cancelada") y `Conflict` ("La venta cambió; vuelva a abrirla"); al cancelar refrescar el detalle y el listado; registrar con `AddComponentView` y textos en `Strings.resx`
- [X] T059 [P] [US6] Escribir `tests/Pos.Infrastructure.Tests/Sales/CancelSaleTests.cs`: cancelar restaura la existencia con movimientos `SALE_CANCEL` iguales a los `SALE` (incluso si el producto quedó inactivo o borrado) y escribe una entrada `SALE_CANCELLED`; cancelar dos veces devuelve `InvalidState`; una versión vieja devuelve `Conflict`; un fallo forzado no deja cambios; `AuditEntries` rechaza modificar o borrar

**Checkpoint**: cancelar deja historial, existencia y bitácora consistentes.

---

## Phase 9: User Story 7 - Inicio con datos de ventas (Priority: P2)

**Goal**: Las tarjetas de Inicio muestran ventas reales del día, de los últimos 7 días y los productos más vendidos, sin canceladas.

**Independent Test**: registrar ventas hoy y días previos, cancelar una, abrir Inicio y verificar totales y ranking (quickstart §3 paso 8).

- [X] T060 [US7] Completar `SaleRepository.GetDashboardAsync` en `src/Pos.Infrastructure/Sales/SaleRepository.cs`: por cada `DayWindow` total y número de ventas con `Status = COMPLETED`, y el top 5 de productos por cantidad vendida en el periodo completo (agrupa por `ProductId` y muestra el nombre de la línea más reciente), con el mismo predicado que `SearchAsync` (SC-009)
- [X] T061 [P] [US7] Crear `GetSalesDashboard` en `src/Pos.Application/Sales/GetSalesDashboard/` (`SalesDashboardQuery(IReadOnlyList<DayWindow> Days)`, handler) y registrarlo en `DependencyInjection.cs`; `DayWindow(DateOnly LocalDate, DateTime FromUtc, DateTime ToUtcExclusive)`
- [X] T062 [P] [US7] Crear `ChartCard` en `src/Pos.Desktop/Home/ChartCard.cs` (`DashboardCard` con `IReadOnlyList<ChartBar> Bars`, `ChartBar(string Label, string ValueText, double Ratio)`) y su plantilla en `src/Pos.Desktop/Home/HomeView.axaml` que dibuja barras con `Border` proporcional (sin librería de gráficas, research §11)
- [X] T063 [US7] Crear `src/Pos.Desktop/Sales/SalesCards.cs` (depende de T062, T063): `SalesTodayChart` ("$X" y "N ventas"; con cero muestra "$0.00 · 0 ventas", no estado vacío; navega a "Ventas realizadas" filtrada por hoy), `SalesLast7DaysChart` (7 barras con día corto, `Order` 120; estado vacío "Sin ventas en los últimos 7 días" si los 7 días están en 0) y `TopProductsChart` (hasta 5 barras, `Order` 130; estado vacío "Aún no hay ventas"), con `Order` 110 para la primera; las tres comparten una sola llamada a `GetSalesDashboard` por activación y calculan las ventanas de día local con `TimeZoneInfo.Local`. Registrar con `AddDashboardCard<T>()` en `SalesModule.cs` y retirar `SalesTodayChart`, `SalesLast7DaysChart`, `TopProductsChart` de `src/Pos.Desktop/Home/Cards/ComingSoonCard.cs` y `AddSalesPlaceholders` de `src/Pos.Desktop/Home/HomeModule.cs`
- [X] T064 [P] [US7] Escribir `tests/Pos.Infrastructure.Tests/Sales/SalesDashboardTests.cs`: excluye las canceladas, pone 0 en los días sin ventas, ordena el top por cantidad y coincide con `SearchAsync` para el mismo rango (SC-009)

**Checkpoint**: Inicio refleja las ventas reales.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T065 [P] Escribir `tests/Pos.Infrastructure.Tests/Sales/SalesPerformanceTests.cs` (patrón de `InventoryPerformanceTests`): confirmar una venta de 50 líneas con 10,000 productos tarda menos de 2 s; una página de 100 ventas con 50,000 ventas, con filtros, tarda menos de 2 s; `FindProductsForSale` por código de barras exacto con 10,000 productos tarda menos de 100 ms (SC-002)
- [X] T066 Generar `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.4.0.db` con `SampleDatabaseGenerator.cs` (ventas completadas y canceladas, una existencia negativa y un borrador) y ampliar `SampleDatabaseUpgradeTests.cs` para que migre v0.1.0 a v0.4.0 a la versión actual, verifique la integridad de los datos y compruebe que la migración `SalesModule` no reconstruyó tablas existentes (obligatoria, Principio IV y VI)
- [X] T067 [P] Ejecutar `dotnet test tests/Pos.ArchitectureTests --verbosity quiet` y corregir violaciones (Desktop solo referencia tipos de `Pos.Domain` sin acceder a `DbContext` ni a Infrastructure; `Pos.Application` no depende de EF Core)
- [X] T068 [P] Documentar en `docs/` (junto a la documentación de operación existente) el flujo de ventas para soporte: atajos, folio, recuperación de la venta en curso, cancelación, existencia negativa, y cómo consultar `AuditEntries` desde el respaldo; actualizar `README.md` con la versión 0.4.0
- [ ] T069 Recorrer manualmente [quickstart.md](quickstart.md) §3 completo (incluido el modo solo táctil, SC-001) y §4 (migración de una base 004), y corregir lo que falle
- [X] T070 Compuerta final (Principio VI: durante la implementación se ejecutan solo los proyectos modificados; la suite completa se corre aquí y en integración continua): ejecutar `dotnet build -v q` y `dotnet test --verbosity quiet` desde la raíz: 0 errores, 0 advertencias y todas las pruebas en verde en la suite completa

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → historias. La Fase 2 bloquea todas las historias.
- **US1** (captura) no depende de otra historia.
- **US2** (borrador) depende de US1 (integra en `PointOfSaleViewModel`).
- **US3** (cobro) depende de US1; su confirmación real se conecta en US4 (T045).
- **US4** (registro) depende de la Fase 2, de US3 para la conexión en pantalla (T045) y de US2 para el borrado del borrador (T034).
- **US5** (consulta) depende de la Fase 2 y de US4 solo para tener datos.
- **US6** (cancelación) depende de US4 (T043/T044) y de US5 (T056, botón en el detalle).
- **US7** (Inicio) depende de US4; comparte `SaleRepository` con US5.
- **Polish** al final.

### Within Each Story

Dominio → puerto → repositorio → caso de uso → ViewModel → vista → pruebas de persistencia. Los archivos compartidos (`PointOfSaleViewModel.cs`, `SaleRepository.cs`, `SalesModule.cs`, `Strings.resx`, `DependencyInjection.cs`) se editan en orden, no en paralelo.

### Parallel Opportunities

- Fase 2: T002, T006, T007 y T016 al inicio; T011 a T015 (pruebas de dominio) en cuanto existan sus tipos; T020, T021, T022, T025 y T026 entre sí.
- US4: T046 a T052 (pruebas) en paralelo cuando T044 esté listo.
- US5/US7: T054 y T062/T063 en paralelo con el trabajo de otras historias en archivos distintos.

---

## Implementation Strategy

### MVP First (US1 a US4)

1. Fases 1 y 2.
2. US1 → US2 → US3 → US4 en orden: al terminar US4 hay un POS que vende, cobra, descuenta inventario y no pierde la venta.
3. Validar con quickstart §3 pasos 1 a 6 antes de seguir.

### Incremental Delivery

US5 (consultar) → US6 (cancelar) → US7 (Inicio) → Polish. Cada historia agrega valor sin romper las anteriores.

---

## Notes

- Cada tarea cita el archivo exacto; antes de editar un archivo existente, leerlo.
- Las restricciones de campo (longitudes, nulos, enums) están en [data-model.md](data-model.md) y se citan en T010, T016 y T017; no se decide nada de esquema al implementar.
- Los tipos `Sale` y `SaleCancellation` **no** se pueden registrar desde el formulario de movimientos (T021).
- Confirmar tras cada tarea o grupo lógico con `dotnet build -v q` (0 advertencias).
