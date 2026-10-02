---

description: "Task list for 016 Categorías de productos"
---

# Tasks: Categorías de productos

**Input**: Design documents from `/specs/016-product-categories/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (application-ports.md, ui.md), quickstart.md

**Tests**: Incluidas, solo las que fija la política mínima de la constitución v1.2.0 y enumera research §17 (validaciones de integridad, cuadre de importes, migración, inventario y arquitectura). Sin pruebas de ViewModels, vistas, mapeos ni exportadores. La persistencia se prueba sobre SQLite real en `tests/Pos.Infrastructure.Tests`. Al implementar, ejecutar solo las pruebas del proyecto modificado (`dotnet test tests/<Proyecto> --verbosity quiet`).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US3)
- Include exact file paths in descriptions

## Path Conventions

Arquitectura por capas (Principio II): `src/Pos.Domain`, `src/Pos.Application`, `src/Pos.Infrastructure`, `src/Pos.Desktop`, y `tests/Pos.*.Tests`. Carpeta de funcionalidad `Categories/` en cada capa; los cambios del producto quedan en `Products/` y los de reportes en `Reports/`.

Convenciones transversales (aplican a todas las tareas):

- Importes en centavos (`long`), cantidades en milésimas (`long`), porcentajes en puntos base (`long`, 10 000 = 100 %). Nunca `double`/`float`.
- Identificadores `Guid.CreateVersion7`; fechas UTC.
- Cada caso de uso devuelve `Result`/`Result<T>` y verifica el permiso con `IAccessControl`: `ManageProducts` para el catálogo, `ViewProducts` para `ListCategoryOptions`; los reportes conservan sus permisos actuales (FR-022). Sin permisos ni módulos licenciados nuevos (research §6).
- Los ViewModels no calculan importes ni porcentajes (Principio III).
- Mensajes al operador en español, sin detalles técnicos; los textos fijos están en contracts/application-ports.md y contracts/ui.md.
- Los handlers que escriben del catálogo (T022–T025) reciben `ILogger<T>` y registran con Serilog (Principio VIII), con `CategoryId` y usuario: `Information` en alta, edición, cambio de estado y eliminación; `Warning` en rechazos (`Duplicate`, `Conflict`, `CategoryInUse`). Mismo patrón que src/Pos.Application/Discounts/Coupons/SaveCoupon/SaveCouponHandler.cs. `CategoryNotAssignable` en T036 también se registra como `Warning`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Preparar la versión y confirmar la línea base

- [X] T001 Cambiar `<Version>` de `0.10.0` a `0.11.0` en Directory.Build.props
- [X] T002 Verificar la línea base: `dotnet build -v q` y `dotnet test --verbosity quiet` terminan sin errores ni advertencias antes de empezar

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entidad `Category`, referencia en `Product`, filtro común, puerto, persistencia y la migración única `ProductCategories`, que todas las historias necesitan

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Domain

- [X] T003 [P] Crear el agregado `Category` en src/Pos.Domain/Categories/Category.cs según data-model.md: `Id` (`Guid.CreateVersion7`), `Name` ("Recortado, 1–50 caracteres", `NameMaxLength = 50`), `NameKey` (`TextNormalizer.ForSearch(Name)`), `Description` ("Recortada, hasta 200; vacía → nula", `DescriptionMaxLength = 200`), `IsActive` (`true` al crear), `CreatedAt/By`, `UpdatedAt/By`, `DeletedAt`, `Version` (inicia en 1), `IsDeleted`. Métodos: `Create(name, description)`, `Update(name, description)`, `Deactivate()`, `Activate()` (idempotentes), `Delete(utcNow)` (exige `DateTimeKind.Utc`, como `Product.Delete`), y estáticos `NormalizeName`, `NormalizeDescription`, `IsValidName`, `IsValidDescription`. Inválido lanza `DomainException` con mensaje en español
- [X] T004 [P] Agregar `Guid? CategoryId` a `Product` en src/Pos.Domain/Products/Product.cs: parámetro opcional `Guid? categoryId = null` al final de `Create(...)` y de `Update(...)`; el dominio solo guarda el valor (si la categoría está activa lo valida el caso de uso, FR-011)
- [X] T005 [P] Crear `ShareMath.BasisPoints(long part, long total)` en src/Pos.Domain/Reports/ShareMath.cs: `(part × 10 000 + total / 2) / total` (mitad hacia arriba), 0 si `total = 0` (research §10)

### Domain tests

- [X] T006 [P] Pruebas de `Category` en tests/Pos.Domain.Tests/Categories/CategoryTests.cs: nombre "  Bebidas " → "Bebidas"; vacío, solo espacios y 51 caracteres lanzan; 50 caracteres pasa; descripción "   " → nula y 201 caracteres lanza; `NameKey` de "BEBÍDAS" = "bebidas"
- [X] T007 [P] Pruebas de `ShareMath` en tests/Pos.Domain.Tests/Reports/ShareMathTests.cs: 1 000.00 de 2 000.00 = 5 000 pb; 1 de 3 = 3 333 pb; 2 de 3 = 6 667 pb; total 0 = 0

### Application (puertos, DTOs, errores, bitácora)

- [X] T008 [P] Agregar en src/Pos.Application/Abstractions/Error.cs los errores `ConfirmationRequired(int ProductCount)`, `CategoryInUse(int ProductCount)` y `CategoryNotAssignable` (categoría inactiva, borrada o inexistente al guardar un producto)
- [X] T009 [P] Agregar `CategoryCreated = "CATEGORY_CREATED"`, `CategoryUpdated = "CATEGORY_UPDATED"`, `CategoryDeactivated = "CATEGORY_DEACTIVATED"`, `CategoryActivated = "CATEGORY_ACTIVATED"`, `CategoryDeleted = "CATEGORY_DELETED"` y `CategoryEntity = "Category"` en src/Pos.Application/Audit/AuditActions.cs, y sus etiquetas en español en src/Pos.Desktop/Administration/AuditLogViewModel.cs
- [X] T010 [P] Crear `CategoryFilter` en src/Pos.Application/Categories/CategoryFilter.cs: `readonly record struct` con `Kind` (`All`, `Uncategorized`, `Only`) y `Guid? CategoryId` (solo con `Only`); estáticos `All`, `Uncategorized`, `Only(Guid id)` (research §8)
- [X] T011 [P] Crear src/Pos.Application/Categories/CategoryDtos.cs (`CategoryListItemDto(Id, Name, Description?, IsActive, ProductCount, Version)`, `CategoryDto` con los mismos campos, `CategoryOptionDto(Id, Name, IsActive)`, enum `CategoryStatusFilter { Active, Inactive, All }`), src/Pos.Application/Categories/CategoryFields.cs (`Name`, `Description`, `Category`) y src/Pos.Application/Categories/CategoryMessages.cs con los textos exactos: "Ya existe una categoría con ese nombre", "La categoría tiene {0} productos. Seguirán asignados a ella, pero no podrá asignarse a productos nuevos", "No se puede eliminar: la categoría tiene {0} productos. Reasígnalos o desactívala", "La categoría elegida ya no está disponible. Elige otra", "La categoría cambió desde que la abriste. Vuelve a abrirla", "Sin categoría", "(inactiva)"
- [X] T012 Crear el puerto `ICategoryRepository` en src/Pos.Application/Categories/ICategoryRepository.cs según contracts/application-ports.md: `GetAsync(id)` (no borrada, con seguimiento), `NameExistsAsync(nameKey, excludingId)`, `CountProductsAsync(id)` (productos no borrados), `SearchAsync(nameKey?, CategoryStatusFilter)`, `ListOptionsAsync(includeInactive)`, `ClearFromDeletedProductsAsync(id)`, `Add(category)`, `SaveChangesAsync(category, expectedVersion)` → `SaveOutcome` (depende de T003, T011)

### Infrastructure (persistencia y migración)

- [X] T013 [P] Crear `CategoryConfiguration` en src/Pos.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs: tabla `Categories`; `Id` sin generación; `Name` y `NameKey` `HasMaxLength(50)` requeridos; `Description` `HasMaxLength(200)` nula; `IsActive`, auditoría requeridos; `Version` token de concurrencia; `Ignore(IsDeleted)`; índice único `IX_Categories_NameKey` con filtro `"DeletedAt" IS NULL`; índice `IX_Categories_IsActive_NameKey` (`IsActive`, `NameKey`) con filtro `"DeletedAt" IS NULL`
- [X] T014 Configurar `Product.CategoryId` en src/Pos.Infrastructure/Persistence/Configurations/ProductConfiguration.cs como columna nula **sin relación ni clave foránea** (research §2; comentar el motivo) e índice `IX_Products_CategoryId` con filtro `"DeletedAt" IS NULL`; agregar `DbSet<Category> Categories` en src/Pos.Infrastructure/Persistence/PosDbContext.cs y confirmar que la asignación de auditoría y `Version` del contexto cubre a `Category` (depende de T003, T004, T013)
- [X] T015 Crear la migración con `dotnet ef migrations add ProductCategories --project src/Pos.Infrastructure --output-dir Persistence/Migrations` y revisar el SQL con `dotnet ef migrations script DiscountsAndCoupons ProductCategories --project src/Pos.Infrastructure -o migracion.sql`: solo `CREATE TABLE "Categories"`, sus 2 índices, `ALTER TABLE "Products" ADD "CategoryId" TEXT NULL` y su índice; **ningún** `ef_temp_` (depende de T014)
- [X] T016 Implementar `CategoryRepository` en src/Pos.Infrastructure/Categories/CategoryRepository.cs: `SearchAsync` ordena por `NameKey`, filtra por `NameKey.Contains` y estado, y calcula `ProductCount` con una subconsulta sobre productos no borrados; `ListOptionsAsync` excluye borradas; `ClearFromDeletedProductsAsync` usa `ExecuteUpdateAsync` para poner `CategoryId = null` en productos con `DeletedAt != null`; `SaveChangesAsync` traduce `DbUpdateConcurrencyException` → `Conflict` y la violación de `IX_Categories_NameKey` → `Duplicate("Name")` (mismo patrón que src/Pos.Infrastructure/Products/ProductRepository.cs); registrarlo en src/Pos.Infrastructure/DependencyInjection.cs (depende de T012, T014)
- [X] T017 [P] Crear `CategoryQueryExtensions.WhereCategory(this IQueryable<Product>, CategoryFilter)` en src/Pos.Infrastructure/Categories/CategoryQueryExtensions.cs: `All` sin filtro, `Uncategorized` → `CategoryId == null`, `Only(id)` → `CategoryId == id` (depende de T010, T014)
- [X] T018 Prueba de migración en tests/Pos.Infrastructure.Tests/SampleDatabases/ProductCategoriesMigrationTests.cs: al migrar `v0.10.0.db`, existe la tabla `Categories` vacía, todos los productos conservan sus datos y tienen `CategoryId` nulo, y el SQL de la migración no contiene `ef_temp_` (depende de T015)

**Checkpoint**: Base lista: compila, migra sin reconstrucciones y las historias pueden empezar

---

## Phase 3: User Story 1 - Catálogo de categorías (Priority: P2) 🎯 MVP

**Goal**: El Administrador crea, edita, desactiva, reactiva, elimina, busca y filtra categorías en "Catálogos > Categorías"

**Independent Test**: quickstart.md §3: crear, duplicar con mayúsculas y acentos, validar longitudes, desactivar con y sin productos, eliminar con y sin productos, buscar y filtrar, editar en dos sesiones

### Tests for User Story 1

- [X] T019 [US1] Pruebas sobre SQLite real en tests/Pos.Infrastructure.Tests/Categories/CategoryUseCaseTests.cs: crear "Bebidas" y luego "bebidas" y "  BEBÍDAS " → `Duplicate("Name")`, también con "Bebidas" inactiva; crear tras eliminar "Bebidas" sí procede; desactivar con un producto y `Confirmed = false` → `ConfirmationRequired(1)` sin cambios, con `Confirmed = true` → inactiva y el producto conserva `CategoryId`; desactivar sin productos no pide confirmación; eliminar con un producto **inactivo** → `CategoryInUse(1)`; eliminar con solo un producto borrado → se elimina y el producto queda con `CategoryId` nulo; `UpdateCategory` con versión vieja → `Conflict`; cada operación escribe su acción `CATEGORY_*` en la bitácora

### Implementation for User Story 1

- [X] T020 [P] [US1] Crear `SearchCategories` (`SearchCategoriesQuery(Text?, CategoryStatusFilter Status = Active)` → `IReadOnlyList<CategoryListItemDto>`; normaliza el texto con `TextNormalizer.ForSearch`) en src/Pos.Application/Categories/SearchCategories/SearchCategoriesQuery.cs y SearchCategoriesHandler.cs
- [X] T021 [P] [US1] Crear `GetCategory` (`GetCategoryQuery(Id)` → `CategoryDto` con `ProductCount`, o `NotFound`) en src/Pos.Application/Categories/GetCategory/GetCategoryQuery.cs y GetCategoryHandler.cs
- [X] T022 [P] [US1] Crear `CreateCategory` (`CreateCategoryCommand(Name, Description?)` → `Guid`) con `CreateCategoryValidator` (FluentValidation: nombre recortado de 1 a 50, descripción hasta 200, errores por campo con `CategoryFields`) en src/Pos.Application/Categories/CreateCategory/: comprueba `NameExistsAsync` antes de guardar, traduce `Duplicate` del repositorio y registra `CATEGORY_CREATED`
- [X] T023 [P] [US1] Crear `UpdateCategory` (`UpdateCategoryCommand(Id, Name, Description?, ExpectedVersion)`) con `UpdateCategoryValidator` en src/Pos.Application/Categories/UpdateCategory/: mismas validaciones y unicidad excluyendo su id; `NotFound`, `Conflict`; registra `CATEGORY_UPDATED`
- [X] T024 [P] [US1] Crear `SetCategoryActive` (`SetCategoryActiveCommand(Id, IsActive, ExpectedVersion, Confirmed = false)`) en src/Pos.Application/Categories/SetCategoryActive/: al desactivar, dentro de `IWriteTransactions` (para que el número del aviso no quede desfasado frente a una asignación simultánea), cuenta productos no borrados y, si hay y `Confirmed` es falso, devuelve `ConfirmationRequired(n)` sin cambiar nada; activar nunca pide confirmación; registra `CATEGORY_DEACTIVATED` o `CATEGORY_ACTIVATED` (FR-005, FR-006)
- [X] T025 [P] [US1] Crear `DeleteCategory` (`DeleteCategoryCommand(Id, ExpectedVersion)`) en src/Pos.Application/Categories/DeleteCategory/: dentro de `IWriteTransactions` cuenta productos no borrados → `CategoryInUse(n)`; si es 0, `Delete(utcNow)` con `IClock`, `ClearFromDeletedProductsAsync`, guarda, registra `CATEGORY_DELETED` y confirma (research §4, FR-007)
- [X] T026 [P] [US1] Crear `ListCategoryOptions` (`ListCategoryOptionsQuery(IncludeInactive)` → `IReadOnlyList<CategoryOptionDto>` ordenado por nombre; permiso `ViewProducts`) en src/Pos.Application/Categories/ListCategoryOptions/
- [X] T027 [US1] Registrar los 7 handlers y los validadores en src/Pos.Application/DependencyInjection.cs (depende de T020–T026)
- [X] T028 [P] [US1] Agregar en src/Pos.Desktop/Resources/Strings.resx (y su Designer) `Nav_Categories` = "Categorías" y los textos `Category_*` de contracts/ui.md (encabezados Nombre, Descripción, Productos, Estado; Nueva categoría; Activas, Inactivas, Todas; Editar, Desactivar, Reactivar, Eliminar; "¿Eliminar la categoría {0}?"; "Sin datos en este período")
- [X] T029 [US1] Crear `CategoryFormViewModel` y `CategoryFormView.axaml(.cs)` en src/Pos.Desktop/Categories/: diálogo con Nombre (`MaxLength` 50) y Descripción (`MaxLength` 200, varias líneas), muestra `ValidationFailed` por campo y `Duplicate` en Nombre; invoca `CreateCategory` o `UpdateCategory` (depende de T022, T023, T028)
- [X] T030 [US1] Crear `CategoriesViewModel` y `CategoriesView.axaml(.cs)` en src/Pos.Desktop/Categories/ según contracts/ui.md: búsqueda, filtro de estado (Activas por defecto), tabla Nombre/Descripción/Productos/Estado, acciones Editar, Desactivar/Reactivar (ante `ConfirmationRequired` muestra el aviso con el número y reintenta con `Confirmed = true`; Cancelar no cambia nada), Eliminar (confirmación y mensaje de `CategoryInUse`); `Conflict` muestra el aviso y recarga (depende de T020, T021, T024, T025, T029)
- [X] T031 [US1] Crear `CategoriesModule.AddCategoriesModule()` en src/Pos.Desktop/Categories/CategoriesModule.cs: página `catalogs.categories`, título `Strings.Nav_Categories`, en el grupo `ProductsModule.GroupId` con orden 1 y `permission: Permission.ManageProducts`, más el registro de `CategoryFormViewModel` y su vista; invocarlo en src/Pos.Desktop/Composition/HostBuilder.cs después de `AddProductsModule` (depende de T030)

**Checkpoint**: El catálogo de categorías funciona y se prueba solo

---

## Phase 4: User Story 2 - Asignar categoría a producto (Priority: P2)

**Goal**: Campo "Categoría" opcional en el formulario de producto, columna y filtro en el listado de productos

**Independent Test**: quickstart.md §4: producto sin categoría, asignar y quitar, categoría inactiva en el selector, filtro combinado con búsqueda, venta de producto sin categoría, desactivación concurrente

### Tests for User Story 2

- [X] T032 [US2] Pruebas sobre SQLite real en tests/Pos.Infrastructure.Tests/Categories/ProductCategoryAssignmentTests.cs: crear producto sin categoría → válido con `CategoryId` nulo; crear con categoría inactiva o borrada → `CategoryNotAssignable`; actualizar otro dato de un producto con categoría inactiva sin cambiarla → se guarda y la conserva; cambiar a otra inactiva → `CategoryNotAssignable`; `SearchProducts` con `CategoryFilter.Only` y `Uncategorized` combinados con texto devuelve solo los que corresponden; el conteo de `SearchCategories` sube y baja al asignar y quitar
- [X] T033 [P] [US2] Ajustar las pruebas existentes de tests/Pos.Application.Tests/Products/ (`CreateProductHandlerTests`, `UpdateProductHandlerTests`, `UpdateProductInventoryTests`, `SearchProductsHandlerTests`, `GetProductHandlerTests`) a las firmas nuevas de comandos, DTOs y dobles de `IProductRepository`/`ICategoryRepository`, sin agregar casos nuevos

### Implementation for User Story 2

- [X] T034 [P] [US2] Agregar `CategoryId?`, `CategoryName?` y `CategoryIsActive` a `ProductDto`, y `CategoryName?` (nulo = "Sin categoría") y `CategoryIsActive` a `ProductListItemDto` en src/Pos.Application/Products/ProductDto.cs; agregar `CategoryFilter Category` a `ProductSearch` en src/Pos.Application/Products/IProductRepository.cs y `CategoryFilter Category = default(All)` a `SearchProductsQuery` en src/Pos.Application/Products/SearchProducts/SearchProductsQuery.cs; ajustar src/Pos.Application/Products/ProductMapping.cs
- [X] T035 [US2] En src/Pos.Infrastructure/Products/ProductRepository.cs: `SearchAsync` y `LocatePageAsync` aplican `WhereCategory(search.Category)` y proyectan nombre y estado de la categoría con `LEFT JOIN` a `Categories`; la lectura de un producto para `GetProduct` trae también su categoría (depende de T017, T034)
- [X] T036 [US2] Agregar `Guid? CategoryId` a `CreateProductCommand` y `UpdateProductCommand` en src/Pos.Application/Products/CreateProduct/CreateProductCommand.cs y src/Pos.Application/Products/UpdateProduct/UpdateProductCommand.cs; en CreateProductHandler.cs y UpdateProductHandler.cs, cuando `CategoryId` no es nulo y (en la edición) es distinto del guardado, abrir `IWriteTransactions` y verificar con `ICategoryRepository.GetAsync` que existe, no está borrada y está activa, o devolver `CategoryNotAssignable` (FR-011, research §4); pasar `categoryId` a `Product.Create/Update` (depende de T004, T008, T012)
- [X] T037 [US2] Pasar `SearchProductsQuery.Category` a `ProductSearch` en src/Pos.Application/Products/SearchProducts/SearchProductsHandler.cs y devolver la categoría en src/Pos.Application/Products/GetProduct/GetProductHandler.cs (depende de T034)
- [X] T038 [US2] Crear el control `CategoryPicker` (`CategoryPicker.axaml(.cs)` y `CategoryPickerViewModel.cs`) en src/Pos.Desktop/Categories/ (research §15): modo **asignación** ("Sin categoría" + activas por nombre; la categoría actual inactiva se agrega como "{nombre} (inactiva)" solo si es el valor actual) y modo **filtro** ("Todas", "Sin categoría" y todas las no borradas, inactivas con "(inactiva)"), expone el `CategoryFilter` o el `Guid?` elegido; con más de 15 opciones habilita `IsEditable` para escribir y filtrar; opciones vía `ListCategoryOptions`; agregar a src/Pos.Desktop/Resources/Strings.resx la etiqueta "Categoría" y las opciones "Todas" y "Sin categoría" si no existen ya en T028 (depende de T026)
- [X] T039 [US2] En src/Pos.Desktop/Products/ProductEditorViewModel.cs y ProductEditorView.axaml: agregar el campo "Categoría" con `CategoryPicker` en modo asignación, cargar la categoría actual y enviar `CategoryId`; ante `CategoryNotAssignable` mostrar el mensaje en el campo y recargar las opciones (depende de T036, T038)
- [X] T040 [US2] En src/Pos.Desktop/Products/ProductsViewModel.cs y ProductsView.axaml: columna "Categoría" ("Sin categoría" si es nula; "(inactiva)" si corresponde) y filtro `CategoryPicker` en modo filtro junto a la búsqueda, combinado con texto e "Incluir inactivos" (FR-012) (depende de T035, T037, T038)

**Checkpoint**: Productos con y sin categoría se guardan, se filtran y se venden sin cambios

---

## Phase 5: User Story 3 - Reportes por categoría (Priority: P2)

**Goal**: Filtro por categoría en "Reportes > Ventas" e "Inventario", sección "Ventas por categoría" con desglose de productos y exportaciones

**Independent Test**: quickstart.md §5: cuadre 1,000/600/400 contra el total sin filtro, desglose de productos, filtro con venta mixta, reclasificación, inventario filtrado, exportaciones y permisos

### Tests for User Story 3

- [X] T041 [US3] Pruebas sobre SQLite real en tests/Pos.Infrastructure.Tests/Reports/SalesByCategoryReportTests.cs (usar los ayudantes de tests/Pos.Infrastructure.Tests/Reports/ReportTestSupport.cs): con ventas de "Bebidas", "Botanas" y sin categoría, una venta mixta, un descuento de línea, un descuento global, una devolución parcial y una venta cancelada → Σ `Categories.AmountCents` = `Totals.TotalCents` del reporte sin filtro (SC-002); para cada categoría Σ productos = categoría en importe y unidades (SC-003); porcentajes con `ShareMath`; filtro `Only(Bebidas)` → total = importe de Bebidas, la venta mixta cuenta 1 vez con solo sus líneas de Bebidas en el detalle, `PaymentsBreakdownAvailable = false`; mover un producto de "Botanas" a "Bebidas" → sus ventas pasadas cuentan en "Bebidas"
- [X] T042 [P] [US3] Pruebas sobre SQLite real en tests/Pos.Infrastructure.Tests/Reports/InventoryByCategoryReportTests.cs: con filtro `Only(Lácteos)` y `Uncategorized`, las tarjetas (total, activos, existencia baja, sin existencia) y la tabla cuentan solo esos productos; la fila trae `CategoryName`; orden `InventoryReportSort.Category`
- [X] T043 [P] [US3] Ajustar las pruebas existentes tests/Pos.Infrastructure.Tests/Reports/SalesReportReaderTests.cs, InventoryReportReaderTests.cs, ReportWritersTestData.cs y tests/Pos.Application.Tests/Reports/ a las firmas nuevas de `SalesReport`, `SalesTotals` e `InventoryReportRow`, sin agregar casos nuevos

### Implementation for User Story 3

- [X] T044 [P] [US3] En src/Pos.Application/Reports/GetSalesReport/SalesReportDtos.cs: agregar `CategoryFilter Category = default(All)` a `SalesReportQuery`; `bool PaymentsBreakdownAvailable = true` a `SalesTotals`; los records `CategorySales(Guid? CategoryId, string Name, bool IsActive, long UnitsThousandths, long AmountCents, long ShareBasisPoints, IReadOnlyList<ProductSales> Products)` y `ProductSales(Guid ProductId, string Name, string Sku, string UnitName, int DecimalPlaces, long UnitsThousandths, long AmountCents)`, y `IReadOnlyList<CategorySales> Categories` en `SalesReport`
- [X] T045 [P] [US3] En src/Pos.Application/Reports/GetInventoryReport/InventoryReportDtos.cs: agregar `CategoryFilter Category = default(All)` a `InventoryReportQuery`, `Category` a `InventoryReportSort`, y `string? CategoryName` y `bool CategoryIsActive` a `InventoryReportRow`; actualizar el comentario: el filtro de categoría sí afecta tarjetas y gráfica
- [X] T046 [US3] En src/Pos.Infrastructure/Reports/SalesReportReader.cs con `Category` distinto de `All` (research §11): ventas `Completed` del período (y cajero) con al menos una línea cuyo producto cumple `WhereCategory`; total = Σ (`SaleLine.AmountCents` − Σ `SaleReturnLine.AmountCents` de esa línea) de las líneas de la categoría; cantidad = esas ventas; promedio mitad hacia arriba; descuento = Σ (`OriginalAmountCents − AmountCents`) de esas líneas; formas de pago en 0 con `PaymentsBreakdownAvailable = false`; gráfica por día, comparativo y filas del detalle (importe = Σ `AmountCents` de las líneas de la categoría, sin restar devoluciones, como la fila sin filtro). Con `All`, el comportamiento actual no cambia (depende de T017, T044)
- [X] T047 [US3] En src/Pos.Infrastructure/Reports/SalesReportReader.cs agregar el cálculo de `Categories` (research §9, §12): **una** consulta sobre líneas de ventas `Completed` del período (y cajero y categoría) agrupada por `ProductId` con unidades netas (`QuantityThousandths − ReturnedQuantity`) e importe neto (`AmountCents − Σ SaleReturnLine.AmountCents`), `LEFT JOIN` a `Products` (categoría vigente, nombre, SKU, unidad; si el producto está borrado, el último `SaleLine.ProductName`) y a `Categories` (nombre, `IsActive`); agrupar por categoría en memoria ("Sin categoría" para `CategoryId` nulo), descartar grupos con importe y unidades 0, ordenar categorías por importe desc y productos por unidades desc, importe desc y nombre; `ShareBasisPoints` en 0 (depende de T046)
- [X] T048 [US3] En src/Pos.Application/Reports/GetSalesReport/GetSalesReportHandler.cs calcular `ShareBasisPoints` de cada categoría con `ShareMath.BasisPoints(amount, Σ amounts)` (depende de T005, T044)
- [X] T049 [US3] En src/Pos.Infrastructure/Reports/InventoryReportReader.cs aplicar `WhereCategory(query.Category)` en la consulta de productos **antes** de calcular `InventoryCounts`, proyectar `CategoryName`/`CategoryIsActive` con `LEFT JOIN` a `Categories`, y ordenar por `InventoryReportSort.Category` (nombre de categoría con "Sin categoría" al final, luego `NameSearch`) (research §13) (depende de T017, T045)
- [X] T050 [US3] En src/Pos.Application/Reports/Export/ReportDocumentBuilder.cs y src/Pos.Application/Reports/Export/ReportTexts.cs (research §14): agregar "Categoría: {nombre}" o "Categoría: Sin categoría" a `FilterTexts` cuando el filtro no es `All`; en ventas, la tabla "Ventas por categoría" (columnas Categoría, Unidades, Importe, % del total) con una fila por categoría, sus productos con sangría en el nombre y la fila de total, y "—" en formas de pago cuando `PaymentsBreakdownAvailable` es falso; en inventario, la columna "Categoría"; confirmar que src/Pos.Application/Reports/Export/ExportRequest.cs lleva los criterios con `Category` (depende de T044, T045)
- [X] T051 [US3] En src/Pos.Desktop/Reports/SalesReportViewModel.cs y SalesReportView.axaml: filtro `CategoryPicker` en modo filtro junto a período y cajero; tarjetas de formas de pago con "—" y la nota "No se desglosa por categoría" cuando `PaymentsBreakdownAvailable` es falso; sección "Ventas por categoría" (Categoría, Unidades, Importe, % del total con un decimal, filas expandibles con los productos y su unidad, fila de total) según contracts/ui.md; "Sin datos en este período" si no hay ventas; pasar el filtro de categoría a los criterios de exportación en src/Pos.Desktop/Reports/ReportExportCoordinator.cs (FR-021); agregar a src/Pos.Desktop/Resources/Strings.resx los textos de la sección, la columna y la nota de formas de pago (depende de T038, T048)
- [X] T052 [US3] En src/Pos.Desktop/Reports/InventoryReportViewModel.cs y InventoryReportView.axaml: filtro `CategoryPicker` en modo filtro que recarga tarjetas, gráfica y tabla, y columna "Categoría" ordenable; pasar el filtro a la exportación en src/Pos.Desktop/Reports/ReportExportCoordinator.cs si arma los criterios (depende de T038, T049)

**Checkpoint**: Las tres historias funcionan; los reportes por categoría cuadran con los reportes sin filtro

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Base de ejemplo 0.11.0, documentación y verificación final

- [X] T053 Ampliar `SampleData` en tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs con una categoría activa con productos, una inactiva con un producto, una borrada sin productos y productos sin categoría
- [X] T054 Generar tests/Pos.Infrastructure.Tests/SampleDatabases/v0.11.0.db con `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"` y agregarla al commit (nunca modificarla después) (depende de T053)
- [X] T055 Ampliar tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs: al migrar de `v0.1.0.db` a `v0.11.0.db` existe `Categories`, los productos anteriores a 0.11.0 tienen `CategoryId` nulo, y en `v0.11.0.db` ningún producto no borrado apunta a una categoría borrada (data-model.md, invariante 3)
- [X] T056 Confirmar que las pruebas de consistencia de inventario (tests/Pos.Infrastructure.Tests/Sales/SaleConsistencyTests.cs y tests/Pos.Infrastructure.Tests/Returns/InventoryConsistencyAfterReturnsTests.cs), las de rendimiento de reportes (tests/Pos.Infrastructure.Tests/Reports/SalesReportPerformanceTests.cs, que debe seguir bajo 2 s también con `CategoryFilter.Only`) y las de arquitectura (tests/Pos.ArchitectureTests/) pasan con la carpeta `Categories/` nueva
- [X] T057 [P] Escribir docs/categorias.md: reglas del catálogo (nombre único sin mayúsculas ni acentos, inactiva, eliminación), categoría vigente, ausencia de clave foránea y cómo se protege la integridad, limpieza de productos borrados al eliminar, y permisos usados
- [X] T058 [P] Actualizar docs/reportes.md (filtro de categoría, sección "Ventas por categoría", cuadre con devoluciones y descuentos, filas del detalle sin restar devoluciones frente al total neto (FR-015), formas de pago sin desglose, unidades mezcladas, ventas de productos borrados que pasan a "Sin categoría" al eliminar su categoría) y agregar la sección "0.11.0: categorías de productos (`ProductCategories`)" en docs/migraciones.md (tabla, columna, índices, sin reconstrucción, `ProductCategoriesMigrationTests`)
- [ ] T059 Verificación final: `dotnet build -v q` sin errores ni advertencias, `dotnet test --verbosity quiet` en verde, y recorrer manualmente quickstart.md §2–§6

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup; BLOQUEA todas las historias (la migración única `ProductCategories` necesita `Category` y `Product.CategoryId`)
- **US1 (Phase 3)**: depende de Foundational
- **US2 (Phase 4)**: depende de Foundational y de `ListCategoryOptions` de US1 (T026) para el selector; las pruebas de asignación crean categorías con el repositorio, sin la pantalla de US1
- **US3 (Phase 5)**: depende de Foundational y de `CategoryPicker` de US2 (T038); los lectores y sus pruebas (T041–T050) solo necesitan Foundational y pueden avanzar en paralelo con US1 y US2
- **Polish (Phase 6)**: depende de todas las historias (la base de ejemplo 0.11.0 se genera al final)

### Within Each User Story

- Pruebas de la historia junto con los casos de uso, antes de la interfaz
- Domain → puertos/DTOs → infraestructura → caso de uso → DI → ViewModel/vista
- `SalesReportReader.cs` (T046, T047), `ProductRepository.cs` (T035) y `Strings.resx` (T028 y textos de US2/US3) se editan en varias tareas: esas tareas son secuenciales entre sí

### Parallel Opportunities

- Phase 2: T003, T004, T005 (Domain); T006, T007 (pruebas); T008–T011 (Application); T013 y T017 en paralelo con el resto
- US1: T020–T026 juntas (carpetas distintas); T028 en paralelo
- US2: T033 y T034 juntas
- US3: T042, T043, T044 y T045 juntas
- Polish: T057 y T058 en paralelo

---

## Parallel Example: Foundational

```bash
Task: "Crear el agregado Category en src/Pos.Domain/Categories/Category.cs"
Task: "Agregar CategoryId a Product en src/Pos.Domain/Products/Product.cs"
Task: "Crear ShareMath en src/Pos.Domain/Reports/ShareMath.cs"
Task: "Crear CategoryFilter en src/Pos.Application/Categories/CategoryFilter.cs"
Task: "Crear CategoryConfiguration en src/Pos.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs"
```

## Parallel Example: User Story 1

```bash
Task: "Crear SearchCategories en src/Pos.Application/Categories/SearchCategories/"
Task: "Crear CreateCategory en src/Pos.Application/Categories/CreateCategory/"
Task: "Crear SetCategoryActive en src/Pos.Application/Categories/SetCategoryActive/"
Task: "Crear DeleteCategory en src/Pos.Application/Categories/DeleteCategory/"
Task: "Agregar textos Category_* en src/Pos.Desktop/Resources/Strings.resx"
```

## Parallel Example: User Story 3

```bash
Task: "Pruebas de inventario por categoría en tests/Pos.Infrastructure.Tests/Reports/InventoryByCategoryReportTests.cs"
Task: "Ampliar SalesReportDtos en src/Pos.Application/Reports/GetSalesReport/SalesReportDtos.cs"
Task: "Ampliar InventoryReportDtos en src/Pos.Application/Reports/GetInventoryReport/InventoryReportDtos.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup
2. Phase 2: Foundational (incluye la migración)
3. Phase 3: US1, el catálogo de categorías
4. **STOP and VALIDATE**: quickstart.md §3
5. El catálogo solo aporta valor cuando se asigna: en la práctica conviene entregar US1 + US2 juntas

### Incremental Delivery

1. Setup + Foundational → base migrada, sin cambios visibles
2. US1 → catálogo de categorías (validar §3)
3. US2 → asignación y filtro de productos (validar §4)
4. US3 → reportes, sección por categoría y exportaciones (validar §5 y §6)
5. Polish → base de ejemplo 0.11.0, documentación y verificación final

---

## Notes

- [P] = archivos distintos, sin dependencias pendientes
- [Story] vincula cada tarea con su historia para trazabilidad
- Hacer commit al terminar cada tarea o grupo lógico
- Detenerse en cada checkpoint para validar la historia de forma independiente
- Nunca modificar una migración ni una base de ejemplo ya publicadas
