---

description: "Task list for 003-product-catalog-improvements"
---

# Tasks: Mejoras al catálogo de Productos

**Input**: Design documents from `specs/003-product-catalog-improvements/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/use-cases.md](contracts/use-cases.md),
[contracts/ui.md](contracts/ui.md)

**Tests**: solo las pruebas que exigen la constitución (Principio VI: reproducción de defectos,
casos de uso, reglas de dominio y persistencia con SQLite real; Principio IV: base de ejemplo por
versión) y la spec (FR-005, SC-001 a SC-007). No se agregan pruebas de ViewModel ni de interfaz
más allá de la reproducción del defecto. Las tareas de adaptación del código de apoyo de pruebas
existente solo buscan que `dotnet build` compile; no agregan pruebas.

**Organization**: las tareas se agrupan por historia de usuario (US1 = H1 … US4 = H4 de la spec).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece (US1, US2, US3, US4)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dependencias nuevas (research §6).

- [X] T001 Agregar `<PackageVersion Include="SkiaSharp" Version="3.119.4" />` y `<PackageVersion Include="SkiaSharp.NativeAssets.Linux" Version="3.119.4" />` en `Directory.Packages.props` (misma versión que trae Avalonia 12.1.3; comentario: "debe coincidir con la versión de SkiaSharp de Avalonia")
- [X] T002 Agregar `<PackageReference Include="SkiaSharp" />` en `src/Pos.Infrastructure/Pos.Infrastructure.csproj` y `SkiaSharp.NativeAssets.Linux` en `tests/Pos.Infrastructure.Tests/Pos.Infrastructure.Tests.csproj`; verificar con `dotnet build` que no hay conflicto de versiones (NU1605) con la de Avalonia en `src/Pos.Desktop`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: contrato de listado paginado. Lo usan la corrección del filtro (US1) y la
navegación por páginas (US2); elimina el corte de 200 filas, que es la causa probable del defecto
(research §1 y §2).

**⚠️ CRITICAL**: ninguna historia puede empezar antes de terminar esta fase.

- [X] T003 Reemplazar `ProductSearchPage(Items, HasMore)` por `ProductPage(IReadOnlyList<ProductListItemDto> Items, long TotalCount, int Page, int PageSize, int TotalPages)` en `src/Pos.Application/Products/IProductRepository.cs`:
  - Constante `ProductPage.DefaultPageSize = 100`.
  - `TotalPages = max(1, ceil(TotalCount / PageSize))`.
  - En `ProductSearch`, quitar `Limit` y agregar `int Page` e `int PageSize`.
  - Agregar al puerto `Task<int?> LocatePageAsync(ProductSearch search, Guid productId, CancellationToken cancellationToken)` (devuelve nulo si el producto no es visible con esos criterios).
- [X] T004 Implementar la paginación en `ProductRepository.SearchAsync` de `src/Pos.Infrastructure/Products/ProductRepository.cs`:
  - Sobre la misma consulta filtrada (no borrados; `IsActive` solo si `!IncludeInactive`; filtros de texto actuales sin cambios), ejecutar `LongCountAsync` y después `OrderBy(p => p.NameSearch).ThenBy(p => p.Sku).Skip((Page-1)*PageSize).Take(PageSize)`.
  - Si `Page` supera las páginas existentes, recalcular con la última página válida antes de leer.
  - Devolver `ProductPage`.
- [X] T005 Implementar `LocatePageAsync` en `src/Pos.Infrastructure/Products/ProductRepository.cs`:
  - Leer `(NameSearch, Sku)` del producto; devolver nulo si no cumple el filtro y el texto.
  - Contar los productos visibles con `NameSearch < x || (NameSearch == x && Sku < y)` (comparación ordinal, igual que el `ORDER BY` de SQLite).
  - Devolver `count / PageSize + 1`.
- [X] T006 Actualizar `src/Pos.Application/Products/SearchProducts/SearchProductsQuery.cs` a `SearchProductsQuery(string? Text, bool IncludeInactive, int Page = 1, Guid? LocateProductId = null)` y reemplazar `SearchProductsResult` por `ProductPage` como salida.
- [X] T007 Actualizar `src/Pos.Application/Products/SearchProducts/SearchProductsHandler.cs`:
  - Eliminar `Limit = 200`.
  - Normalizar `Page < 1` a 1 y usar `PageSize = ProductPage.DefaultPageSize`.
  - Si `LocateProductId` tiene valor, llamar a `LocatePageAsync` y, si devuelve página, usarla en lugar de `Page`.
  - Devolver `Result<ProductPage>`.
- [X] T008 Crear `tests/Pos.Infrastructure.Tests/Products/ProductPagingTests.cs` (SQLite real, `TestDb`):
  - Páginas de 100 y orden estable por `(NameSearch, Sku)`.
  - Una página fuera de rango devuelve la última válida; 0 resultados devuelven la página 1 de 1.
  - El filtro de inactivos se combina con el texto y los borrados nunca aparecen.
  - `LocatePageAsync` con un producto visible y con uno no visible.
  - Cubre FR-003, FR-004, FR-006 a FR-012 y SC-001.
- [X] T009 Ampliar `tests/Pos.Infrastructure.Tests/Products/ProductPerformanceTests.cs`:
  - 10,000 productos, 500 de ellos inactivos.
  - La primera página, la última página, una búsqueda parcial y el cambio de filtro responden cada una en menos de 1 s, tras el calentamiento actual (SC-002).
  - Adaptar las pruebas existentes a `ProductPage`.
- [X] T010 Actualizar `ProductsViewModel.SearchAsync` en `src/Pos.Desktop/Products/ProductsViewModel.cs` para consumir `ProductPage`:
  - Nuevas propiedades observables `CurrentPage` (int, inicia en 1), `TotalCount` (long) y `TotalPages` (int).
  - Eliminar `HasMore`.
  - Enviar `CurrentPage` en la consulta y guardar en `CurrentPage` la página que devuelve el resultado (ya ajustada).
- [X] T011 Eliminar el aviso de `HasMore` en `src/Pos.Desktop/Products/ProductsView.axaml` y la clave `Products_HasMore` en `src/Pos.Desktop/Resources/Strings.resx`
- [X] T012 Adaptar el código de apoyo de pruebas existente para que compile, sin pruebas nuevas:
  - `tests/Pos.Application.Tests/TestSupport/InMemoryProductRepository.cs`: `ProductPage`, `Page`/`PageSize` y `LocatePageAsync`.
  - Las llamadas a `SearchProductsQuery`, `SearchProductsResult`, `SearchProductsHandler.Limit` y `HasMore` en `tests/`.
  - `dotnet build` en verde.

**Checkpoint**: el listado ya no se corta en 200 filas; se muestra la página 1 de 100 registros.

---

## Phase 3: User Story 1 - Corrección: mostrar inactivos (Priority: P1) 🎯 MVP

**Goal**: con la casilla desmarcada solo aparecen activos; marcada aparecen activos e inactivos,
distinguidos visualmente y con la columna Estado; los borrados nunca aparecen (FR-001 a FR-004).

**Independent Test**: catálogo con más de 200 productos activos, inactivos y borrados; alternar
la casilla con y sin búsqueda y verificar qué productos aparecen y el total de registros.

- [X] T013 [US1] Crear `tests/Pos.Desktop.Tests/Products/ProductsViewModelInactiveFilterTests.cs`, la prueba que reproduce el defecto (constitución, Principio VI; FR-005):
  - Con `DesktopTestHost`, que usa `InMemoryProductRepository` (un repositorio falso escrito a mano, no el proveedor InMemory de EF), sembrar 210 productos activos, 1 inactivo cuyo nombre ordena después de todos ("ZZ Inactivo") y 1 borrado.
  - Activar `IncludeInactive`. Verificar que `TotalCount` es 211 y que el inactivo aparece en la última página (asignar `CurrentPage = TotalPages` y ejecutar `SearchNowCommand`, porque los comandos de navegación llegan en US2).
  - Con la casilla desmarcada no aparece y `TotalCount` es 210. El borrado nunca aparece.
  - La contraparte con SQLite real es T008.
- [X] T014 [US1] Confirmar la causa del defecto:
  - Ejecutar la app con `POS_DATA_DIR` desechable, con más de 200 activos y un inactivo cuyo nombre ordena después del corte, sobre el código anterior a la Fase 2. Usar un árbol de trabajo aparte (`git worktree add ../octopus-003-repro HEAD`); **no** usar `git stash`, porque hay cambios sin commit en `Develop`. Tomar en cuenta que `HEAD` no incluye esos cambios; si el defecto depende de ellos, hacer la prueba en una copia de la carpeta del proyecto.
  - En ese árbol, escribir la versión de T013 para el código anterior (activar `IncludeInactive` y verificar que "ZZ Inactivo" está en `Items`): debe fallar. Así queda demostrado que la prueba reproduce el defecto (FR-005). T013 usa el contrato paginado, que no existe en el código anterior.
  - Si el inactivo tampoco aparece con menos de 200 productos, revisar en `src/Pos.Desktop/Products/ProductsViewModel.cs` el enlace de `IncludeInactive` y la carrera de `_searchVersion` entre `OnIncludeInactiveChanged` y la búsqueda diferida, y corregirla.
  - Documentar la causa confirmada en `specs/003-product-catalog-improvements/research.md` §1.
- [X] T015 [US1] En `src/Pos.Desktop/Products/ProductsViewModel.cs`:
  - `OnIncludeInactiveChanged` debe poner `CurrentPage = 1` y cancelar la búsqueda diferida pendiente antes de buscar (FR-004, FR-009).
  - `OnSearchTextChanged` debe poner `CurrentPage = 1` antes de la búsqueda diferida.
- [X] T016 [P] [US1] Agregar `src/Pos.Desktop/Products/InactiveOpacityConverter.cs`, que convierte `bool IsActive` en opacidad (`true` → 1.0, `false` → 0.55), siguiendo el patrón `Instance` estático de `ProductStatusConverter`.
- [X] T017 [US1] En la plantilla de fila de `src/Pos.Desktop/Products/ProductsView.axaml`:
  - Aplicar `Opacity="{Binding IsActive, Converter={x:Static vm:InactiveOpacityConverter.Instance}}"` al `Grid` de la fila.
  - Mantener la columna Estado con el texto "Activo"/"Inactivo", visible solo con `IncludeInactive`, para que la distinción no dependa solo del color (contracts/ui.md).

**Checkpoint**: H1 funciona; el defecto está corregido y su causa documentada.

---

## Phase 4: User Story 2 - Listado por páginas (Priority: P1)

**Goal**: navegar el catálogo por páginas de 100 con total de registros, página actual, total de
páginas y navegación a primera, anterior, siguiente y última (FR-006 a FR-013).

**Independent Test**: con 10,000 productos, navegar con los cuatro botones y los atajos, buscar
y cambiar el filtro (vuelve a la página 1), editar en la página 2 (se queda en la página del
producto) y borrar el único registro de la última página.

- [X] T018 [US2] En `src/Pos.Desktop/Products/ProductsViewModel.cs`, agregar los comandos `FirstPageCommand`, `PreviousPageCommand`, `NextPageCommand` y `LastPageCommand`:
  - Cada uno ajusta `CurrentPage` y llama a `SearchAsync`.
  - `CanExecute`: primera y anterior con `CurrentPage > 1`; siguiente y última con `CurrentPage < TotalPages`.
  - Usar `[NotifyCanExecuteChangedFor]` en `CurrentPage` y `TotalPages`.
- [X] T019 [US2] En `src/Pos.Desktop/Products/ProductsViewModel.cs`, agregar la propiedad calculada `PageSummary` con el formato de `Strings.Products_PageSummary` ("{0:N0} registros · Página {1} de {2}"), notificada cuando cambian `TotalCount`, `CurrentPage` o `TotalPages`.
- [X] T020 [US2] En `ProductsViewModel.OnEditorSavedAsync` de `src/Pos.Desktop/Products/ProductsViewModel.cs`:
  - Buscar con `LocateProductId = product.Id` para mostrar y seleccionar el producto guardado en su página (FR-012).
  - Si no es visible con el texto actual, conservar el comportamiento existente: limpiar la búsqueda y volver a buscar con `LocateProductId`.
  - Tras borrar (`DeleteAsync`), recargar la página actual; el caso de uso ya ajusta a la última página válida.
- [X] T021 [P] [US2] Agregar en `src/Pos.Desktop/Resources/Strings.resx`:
  - `Products_PageSummary` = "{0:N0} registros · Página {1} de {2}".
  - `Products_FirstPage` = "Primera página (Alt+Inicio)".
  - `Products_PreviousPage` = "Página anterior (Alt+RePág)".
  - `Products_NextPage` = "Página siguiente (Alt+AvPág)".
  - `Products_LastPage` = "Última página (Alt+Fin)".
- [X] T022 [P] [US2] Agregar a `src/Pos.Desktop/Resources/Icons.axaml` las geometrías `Icon.PageFirst`, `Icon.PagePrevious`, `Icon.PageNext` e `Icon.PageLast` (Material Design Icons: `page-first`, `chevron-left`, `chevron-right`, `page-last`, Apache 2.0, igual que los íconos existentes).
- [X] T023 [US2] En `src/Pos.Desktop/Products/ProductsView.axaml`, agregar la barra de paginación en la fila 3 del `Grid` del listado:
  - Un `TextBlock` con `PageSummary` y cuatro `Button` con ícono.
  - `ToolTip.Tip` y `AutomationProperties.Name` con las cadenas de T021.
  - Área de toque de al menos 44 px (002).
  - `KeyBinding` en `UserControl.KeyBindings`: `Alt+Home`, `Alt+PageUp`, `Alt+PageDown` y `Alt+End`.
- [X] T024 [US2] Verificar que `CurrentPage`, `TotalCount` y `TotalPages` se conservan al salir y regresar a Productos, según el mecanismo de estado de pantalla de `src/Pos.Desktop/Common/PageViewModel.cs` (002), y que `OnActivatedAsync` refresca la página actual sin volver a la 1.

**Checkpoint**: H1 y H2 funcionan de forma independiente.

---

## Phase 5: User Story 3 - Validaciones reforzadas (Priority: P1)

**Goal**: nombre, SKU, precio y unidad obligatorios y marcados; precio mayor que 0, máximo
2 decimales y máximo 999,999.99, con o sin separador de miles y sin redondeo; mensajes
específicos junto al campo (FR-014 a FR-020; clarificaciones 1 y 2).

**Independent Test**: capturar cada campo obligatorio vacío y los precios `0`, `0.01`,
`999,999.99`, `1000000`, `999999.991`, `1,234.50`, `12,50`, `1,23.45` y `$10`; editar un producto
existente con precio 0.

### Dominio

- [X] T025 [P] [US3] Reemplazar `Money.TryParse` por `Money.Parse(string? text) → MoneyParseResult` en `src/Pos.Domain/Common/Money.cs`:
  - `MoneyParseResult` es un `readonly record struct` con `Money? Value` y `MoneyParseError? Error`.
  - `enum MoneyParseError { Empty, Format, TooManyDecimals, TooLarge }`.
  - Tras recortar, aceptar `^(?<pesos>\d+|\d{1,3}(,\d{3})+)(\.(?<dec>\d+))?$`.
  - Quitar las comas de `pesos`. Más de 2 dígitos en `dec` → `TooManyDecimals`; más de `MaxCents` → `TooLarge`.
  - Nunca redondear. Conservar `ToEditableString()` sin separador.
- [X] T026 [P] [US3] Crear `src/Pos.Domain/Products/UnitOfMeasure.cs`:
  - `sealed record UnitOfMeasure(string Code, string Name, int SortOrder)`.
  - `const int CodeMaxLength = 3` y `const int NameMaxLength = 50`.
  - `static IReadOnlyList<UnitOfMeasure> All` = H87 Pieza 1, KGM Kilogramo 2, GRM Gramo 3, LTR Litro 4, MLT Mililitro 5, MTR Metro 6, XBX Caja 7, XPK Paquete 8.
  - `static UnitOfMeasure Default` = H87 y `static bool IsValidCode(string? code)` (comparación ordinal exacta).
- [X] T027 [US3] Actualizar `src/Pos.Domain/Products/Product.cs`:
  - Agregar `public string UnitCode { get; private set; }`, inicializada con `UnitOfMeasure.Default.Code` en el constructor privado.
  - Firmas nuevas: `Create(name, sku, barcode, price, unitCode)` y `Update(name, sku, barcode, price, unitCode, isActive)`.
  - En `Apply`, lanzar `DomainException("El precio debe ser mayor que 0.")` si `price.Cents <= 0` y `DomainException("La unidad de medida no es válida.")` si `!UnitOfMeasure.IsValidCode(unitCode)`.
  - Agregar `public static bool IsValidPrice(Money price) => price.Cents > 0`.
- [X] T028 [P] [US3] Ampliar `tests/Pos.Domain.Tests/Common/MoneyTests.cs` para `Money.Parse` (SC-003):
  - Aceptados: `"0.01"`, `"1234.5"` (123450 centavos), `"1,234.50"`, `"999,999.99"`, `" 12.30 "`.
  - `Format`: `"12,50"`, `"1,23.45"`, `"12,3456"`, `"$10"`, `"-1"`, `"1 234"`, `"1.2.3"`.
  - `TooManyDecimals`: `"999999.991"`, `"0.001"`. `TooLarge`: `"1000000"`, `"1,000,000.00"`. `Empty`: `null`, `""`, `"  "`.
  - Nunca redondea.
- [X] T029 [P] [US3] Ampliar `tests/Pos.Domain.Tests/Products/ProductTests.cs` y crear `tests/Pos.Domain.Tests/Products/UnitOfMeasureTests.cs`:
  - `Create` y `Update` rechazan precio 0 y unidad inválida (`"XX"`, `""`, `"h87"`) y aceptan 1 centavo.
  - El catálogo tiene 8 unidades con claves únicas y `Default.Code == "H87"`.

### Application

- [X] T030 [P] [US3] En `src/Pos.Application/Products/ProductMessages.cs`:
  - Agregar `PriceNotPositive` = "El precio debe ser mayor que 0.", `PriceTooManyDecimals` = "El precio admite máximo 2 decimales.", `PriceTooLarge` = "El precio máximo es $999,999.99." y `UnitRequired` = "La unidad de medida es obligatoria.".
  - Cambiar `PriceFormat` a "Capture el precio como 1234.50 o 1,234.50.".
  - En `src/Pos.Application/Products/ProductFields.cs`, agregar `UnitCode = "UnitCode"`.
- [X] T031 [US3] Actualizar `ProductRules.Apply` en `src/Pos.Application/Products/ProductRules.cs`:
  - Nuevo parámetro `Func<T, string?> unitCode`.
  - Regla de precio con `Cascade(Stop)`: vacío → `PriceRequired`; `Money.Parse` con `Format` / `TooManyDecimals` / `TooLarge` → el mensaje correspondiente; valor válido con `!Product.IsValidPrice` → `PriceNotPositive`.
  - Regla de unidad: `Must(UnitOfMeasure.IsValidCode)` → `UnitRequired`, con `OverridePropertyName(ProductFields.UnitCode)`.
  - Las reglas de nombre, SKU y código de barras no cambian (FR-018).
- [X] T032 [US3] Agregar `string UnitCode` a `CreateProductCommand` y `UpdateProductCommand` (`src/Pos.Application/Products/CreateProduct/CreateProductCommand.cs`, `src/Pos.Application/Products/UpdateProduct/UpdateProductCommand.cs`):
  - Pasar `c => c.UnitCode` en `CreateProductValidator` y `UpdateProductValidator`.
  - En `CreateProductHandler` y `UpdateProductHandler`, reemplazar `Money.TryParse` por `Money.Parse(...).Value!.Value` y pasar `UnitCode` a `Product.Create` / `Update`.
- [X] T033 [US3] Agregar `string UnitCode` a `ProductDto` y `string UnitCode` y `string UnitName` a `ProductListItemDto` en `src/Pos.Application/Products/ProductDto.cs`. Mapearlos en `src/Pos.Application/Products/ProductMapping.cs` y proyectarlos en `ProductRepository.SearchAsync` de `src/Pos.Infrastructure/Products/ProductRepository.cs`; `UnitName` sale del join con `UnitsOfMeasure`.
- [X] T034 [US3] Adaptar el código de apoyo de pruebas existente para que compile, sin pruebas nuevas:
  - Las llamadas en `tests/` a `Product.Create/Update` (agregar `"H87"`), `Money.TryParse` → `Money.Parse` y los comandos con `UnitCode = "H87"`.
  - `InMemoryProductRepository` proyecta `UnitCode` y `UnitName`.
  - Las pruebas que esperaban precio 0 válido (FR-013 de 001) se actualizan a la regla nueva.
  - `dotnet build` en verde.
- [X] T035 [P] [US3] Crear `src/Pos.Application/Products/ListUnitsOfMeasure/ListUnitsOfMeasureHandler.cs`:
  - Devuelve `IReadOnlyList<UnitOfMeasureDto>`, con `sealed record UnitOfMeasureDto(string Code, string Name)` en el mismo archivo.
  - Orden por `SortOrder`, a partir de `UnitOfMeasure.All`, sin consultar la base.
  - Registrarlo como `AddSingleton` en `src/Pos.Application/DependencyInjection.cs`.
- [X] T036 [P] [US3] Ampliar `tests/Pos.Application.Tests/Products/CreateProductValidatorTests.cs`, crear `UpdateProductValidatorTests.cs` y crear `ListUnitsOfMeasureHandlerTests.cs` (SC-003):
  - Un caso por mensaje: `PriceRequired`, `PriceFormat`, `PriceTooManyDecimals`, `PriceTooLarge`, `PriceNotPositive` y `UnitRequired`.
  - Nombre, SKU y unidad obligatorios reportados juntos, cada uno en su campo.
  - La edición aplica las mismas reglas (FR-020).
  - El catálogo se devuelve en orden de `SortOrder`.

### Persistencia

- [X] T037 [US3] Crear `src/Pos.Infrastructure/Persistence/Configurations/UnitOfMeasureConfiguration.cs`:
  - Tabla `UnitsOfMeasure`; `Code` como PK `HasMaxLength(3)`; `Name` `HasMaxLength(50)` requerido; `SortOrder` requerido.
  - `HasData(UnitOfMeasure.All)`.
  - Registrar `DbSet<UnitOfMeasure> UnitsOfMeasure` y la configuración en `src/Pos.Infrastructure/Persistence/PosDbContext.cs`.
  - En `ProductConfiguration.cs`: `UnitCode` `HasMaxLength(3)`, `IsRequired()`, `HasDefaultValue("H87")` y `HasOne<UnitOfMeasure>().WithMany().HasForeignKey(p => p.UnitCode).OnDelete(DeleteBehavior.Restrict)`.
- [X] T038 [US3] En `src/Pos.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`, cambiar el índice `IX_Products_NameSearch` a compuesto `(NameSearch, Sku)`, con el mismo filtro `"DeletedAt" IS NULL`, para que la migración de T039 lo incluya (research §2; si la prueba de rendimiento T009 no muestra mejora frente al índice simple, revertir el índice y regenerar la migración mientras no se haya integrado).
- [X] T039 [US3] Generar la migración con `dotnet ef migrations add ProductCatalogImprovements --project src/Pos.Infrastructure --output-dir Persistence/Migrations` y revisar el SQL con `dotnet ef migrations script`:
  - La reconstrucción de `Products` copia todas las filas, incluidas las borradas.
  - Conserva `Version`, `DeletedAt` y los índices filtrados `IX_Products_Sku`, `IX_Products_Barcode` e `IX_Products_NameSearch`.
  - Llena `UnitCode` con `'H87'` e inserta las 8 unidades antes de crear la FK.
  - Dejar constancia de la revisión en la descripción del cambio (constitución, Principio IV).
- [X] T040 [US3] Ampliar `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs` (constitución, Principio IV; SC-007). Tras migrar `v0.1.0.db`, verificar:
  - Los 20 productos tienen `UnitCode = 'H87'` y hay 8 filas en `UnitsOfMeasure`.
  - Precio, estado y `DeletedAt` se conservan.
  - `IX_Products_Sku`, `IX_Products_Barcode` e `IX_Products_NameSearch` existen con su filtro (consultar `sqlite_master`).
  - `PRAGMA foreign_key_check` no devuelve filas.

### Desktop

- [X] T041 [US3] En `src/Pos.Desktop/Products/ProductEditorViewModel.cs`:
  - Agregar `Units` (`IReadOnlyList<UnitOfMeasureDto>`, cargada con `ListUnitsOfMeasureHandler` en el constructor o en la carga).
  - Agregar las propiedades observables `UnitCode` (valor inicial `"H87"` en alta) y `UnitCodeError`, e `IsUnitRequired = true`.
  - Incluir `UnitCode` en los comandos, en `ProductFormState` (cambios sin guardar) y en el mapeo de `FieldError` → `UnitCodeError` junto a los demás campos.
  - Al cargar un producto, asignar `UnitCode = product.UnitCode`.
- [X] T042 [US3] En `src/Pos.Desktop/Products/ProductEditorView.axaml`:
  - Agregar después del precio un `forms:FieldLabel` "Unidad de medida" con `IsRequired="{Binding IsUnitRequired}"` y un `ComboBox` con `ItemsSource="{Binding Units}"`, `SelectedValue="{Binding UnitCode}"`, `SelectedValueBinding="{Binding Code}"` y `DisplayMemberBinding="{Binding Name}"`.
  - Agregar el mensaje de error debajo, con el mismo estilo que los demás campos.
- [X] T043 [P] [US3] En `src/Pos.Desktop/Resources/Strings.resx`: agregar `Editor_Unit` = "Unidad de medida" y `Products_ColUnit` = "Unidad"; cambiar `Editor_PriceHint` a "Ejemplo: 1234.50 o 1,234.50".
- [X] T044 [US3] En `src/Pos.Desktop/Products/ProductsView.axaml`, agregar la columna "Unidad" (ancho 90) entre Código de barras y Precio, en el encabezado y en la plantilla de fila, enlazada a `UnitName` de `ProductListItemDto` (T033), sin consultar Domain desde la vista.

**Checkpoint**: H1, H2 y H3 funcionan; la migración aplica sobre una base de la versión anterior.

---

## Phase 6: User Story 4 - Imagen del producto (Priority: P2)

**Goal**: una imagen opcional por producto (JPG, PNG o WEBP de hasta 5 MB), optimizada a 800 px
con miniatura de 128 px; vista previa, reemplazar y quitar; incluida en los respaldos y excluida
del diagnóstico (FR-021 a FR-030; clarificación 3).

**Independent Test**: asignar, cambiar y quitar la imagen de un producto; cargar un archivo de más
de 5 MB, un GIF y un `.txt` renombrado; cancelar un cambio; restaurar un respaldo; exportar el
diagnóstico.

### Dominio y Application

- [X] T045 [P] [US4] Crear `src/Pos.Domain/Products/ProductImage.cs`:
  - `ProductId` (Guid), `Content` (`byte[]`), `Thumbnail` (`byte[]`), `Width`, `Height` (int), `ContentType` (string, `"image/webp"`) y `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`.
  - Constructor privado para EF y `internal static ProductImage Create(Guid productId, byte[] content, byte[] thumbnail, int width, int height)`.
  - `internal void Replace(byte[] content, byte[] thumbnail, int width, int height)`.
- [X] T046 [US4] Actualizar `src/Pos.Domain/Products/Product.cs`:
  - Agregar `public const long ImageMaxBytes = 5 * 1024 * 1024`, `public const int ImageMaxSide = 800`, `public const int ThumbnailMaxSide = 128` y la navegación `public ProductImage? Image { get; private set; }`.
  - `SetImage(byte[] content, byte[] thumbnail, int width, int height)`: si `Image` existe, llama a `Replace`; si no, la crea.
  - `RemoveImage()`: pone `Image = null`.
  - Ambas actualizan una marca interna para que el repositorio sepa que la imagen cambió (`bool ImageChanged`, ignorada por EF).
- [X] T047 [P] [US4] Agregar al archivo de errores `src/Pos.Application/Abstractions/Error.cs` el error `sealed record InvalidImage(InvalidImageReason Reason) : Error` con `enum InvalidImageReason { TooLarge, UnsupportedFormat, Corrupt, DimensionsTooLarge }`.
- [X] T048 [P] [US4] Crear en `src/Pos.Application/Products/`:
  - `IImageProcessor.cs`: `ImageProcessingResult Process(Stream content)`, con `sealed record ImageProcessingResult(byte[]? Content, byte[]? Thumbnail, int Width, int Height, InvalidImageReason? Error)`. Es un tipo público sin validación y con fábricas `Success(...)` y `Failure(reason)`.
  - `PreparedProductImage.cs`: `sealed class` con constructor `internal`, y `Content`, `Thumbnail`, `Width` y `Height`. Solo `PrepareProductImageHandler` lo construye, así que no se usa `InternalsVisibleTo` (research §7).
  - `ProductImageChange.cs`: `abstract record ProductImageChange`, con `sealed record Keep`, `sealed record Replace(PreparedProductImage Image)` y `sealed record Remove`, y `static ProductImageChange KeepCurrent`.
- [X] T049 [US4] Crear `src/Pos.Application/Products/PrepareProductImage/PrepareProductImageCommand.cs` (`Stream Content, long Length`) y `PrepareProductImageHandler.cs`:
  - Si `Length > Product.ImageMaxBytes`, devolver `InvalidImage(TooLarge)` sin leer.
  - En otro caso, ejecutar `IImageProcessor.Process` en `Task.Run`.
  - Si `ImageProcessingResult.Error` tiene valor, devolver `InvalidImage(Error)`. Si no, construir `PreparedProductImage` con su constructor `internal` y devolver `Result<PreparedProductImage>`.
  - Agregar a `ProductMessages` los mensajes `ImageTooLarge`, `ImageUnsupportedFormat`, `ImageCorrupt` e `ImageDimensionsTooLarge` con los textos de `contracts/use-cases.md`.
  - Registrar el handler (`AddScoped`) en `src/Pos.Application/DependencyInjection.cs`.
- [X] T050 [US4] Agregar `ProductImageChange Image` a `CreateProductCommand` y `UpdateProductCommand`, y aplicarlo en `CreateProductHandler` y `UpdateProductHandler`:
  - `Replace` → `product.SetImage(...)` con los datos de `PreparedProductImage`; `Remove` → `product.RemoveImage()`; `Keep` → nada.
  - En `UpdateProductHandler`, llamar a `GetAsync(id, includeImage: image is not Keep)`.
  - Cambiar la firma de `IProductRepository.GetAsync(Guid id, bool includeImage, CancellationToken)` en `src/Pos.Application/Products/IProductRepository.cs`.
- [X] T051 [US4] En `src/Pos.Application/Products/ProductDto.cs` y `ProductMapping.cs`:
  - Agregar `byte[]? Image` a `ProductDto` (la imagen optimizada).
  - Agregar `byte[]? Thumbnail` a `ProductListItemDto`.
  - En `GetProductHandler`, llamar a `GetAsync(id, includeImage: true)`.

### Infrastructure

- [X] T052 [US4] Adaptar el código de apoyo de pruebas existente para que compile con las firmas de US4, sin pruebas nuevas (las de las fases anteriores están en T012 y T034):
  - `tests/Pos.Application.Tests/TestSupport/InMemoryProductRepository.cs`: `GetAsync(includeImage)` y `Thumbnail`.
  - `tests/Pos.Desktop.Tests/TestSupport/FakeDialogService.cs`: `PickOpenFileAsync`, con una respuesta programable.
  - Los comandos en `tests/` con `ProductImageChange.KeepCurrent`.
  - `dotnet build` en verde.
- [X] T053 [US4] Crear `src/Pos.Infrastructure/Persistence/Configurations/ProductImageConfiguration.cs`:
  - Tabla `ProductImages`; PK `ProductId`; `Content` y `Thumbnail` requeridos; `ContentType` `HasMaxLength(20)`.
  - Relación `Product.HasOne(p => p.Image).WithOne().HasForeignKey<ProductImage>(i => i.ProductId).OnDelete(DeleteBehavior.Cascade)`.
  - En `ProductConfiguration`, `Ignore(p => p.ImageChanged)`.
  - Registrar el `DbSet<ProductImage>` en `PosDbContext.cs`.
  - Si la migración de T039 aún no se ha publicado ni integrado, eliminarla y regenerar `ProductCatalogImprovements` con el modelo completo. Si ya se integró, crear una migración nueva `ProductImages`. En ambos casos, revisar el SQL.
- [X] T054 [US4] En `src/Pos.Infrastructure/Products/ProductRepository.cs`:
  - `GetAsync(id, includeImage)` hace `Include(p => p.Image)` solo si se pide.
  - `SaveChangesAsync`: si `product.ImageChanged`, marcar la entrada del producto como `Modified` (`_context.Entry(product).State = EntityState.Modified`) para que `AuditingInterceptor` incremente `Version` y `UpdatedAt` en la misma transacción.
  - `SearchAsync` proyecta `Thumbnail` con `p.Image != null ? p.Image.Thumbnail : null`, sin leer `Content`.
- [X] T055 [US4] Crear `src/Pos.Infrastructure/Products/SkiaImageProcessor.cs` (`IImageProcessor`) según research §6:
  1. Copiar el stream a memoria, hasta `Product.ImageMaxBytes`.
  2. Detectar la firma: JPEG `FF D8 FF`; PNG `89 50 4E 47 0D 0A 1A 0A`; WEBP `RIFF` + 4 bytes + `WEBP`. Otra firma → `UnsupportedFormat`.
  3. `SKCodec.Create` nulo o fallo al decodificar → `Corrupt`.
  4. Más de 50,000,000 píxeles o un lado mayor a 20,000 → `DimensionsTooLarge`.
  5. Decodificar con `GetScaledDimensions` o `SampleSize` si excede mucho los 800 px.
  6. Aplicar `codec.EncodedOrigin` (EXIF).
  7. Redimensionar sin ampliar a un lado mayor ≤ 800 y a una miniatura ≤ 128 con `SKSamplingOptions` de calidad alta, conservando el alfa.
  8. Codificar ambas en `SKEncodedImageFormat.Webp` con calidad 80. En un WEBP animado se usa el primer cuadro.
  - Registrar `AddSingleton<IImageProcessor, SkiaImageProcessor>()` en `src/Pos.Infrastructure/DependencyInjection.cs`.
- [X] T056 [US4] En `src/Pos.Infrastructure/Diagnostics/ZipDiagnosticsExporter.cs`, después de `CreateTemporaryCopyAsync` y antes de `BuildZip`:
  - Abrir la copia con `SqliteConnection` (`Pooling=False`) y ejecutar `DELETE FROM "ProductImages";` y luego `VACUUM;` (FR-029).
  - Si la tabla no existe (base anterior), ignorar el error `no such table`.
- [X] T057 [P] [US4] Crear `tests/Pos.Infrastructure.Tests/Products/SkiaImageProcessorTests.cs`, con imágenes generadas en memoria con SkiaSharp (FR-022 a FR-024; SC-005):
  - JPG, PNG y WEBP válidos producen WEBP.
  - Una imagen de 3000 × 2000 queda en 800 × 533 y su miniatura en 128 × 85; una de 100 × 50 no se amplía.
  - Un PNG con alfa conserva la transparencia en el resultado.
  - Un JPEG con orientación EXIF 6 queda rotado.
  - Un GIF devuelve `UnsupportedFormat`; un `.txt` con extensión `.jpg`, `UnsupportedFormat`; un JPEG truncado, `Corrupt`.
  - Unas dimensiones de 25,000 × 100 declaradas en la cabecera devuelven `DimensionsTooLarge`.
- [X] T058 [P] [US4] Crear `tests/Pos.Application.Tests/Products/PrepareProductImageHandlerTests.cs`, con un `IImageProcessor` falso:
  - Con `Length` mayor a 5 MB, devuelve `TooLarge` sin leer el stream ni llamar al procesador.
  - Cada `InvalidImageReason` se mapea a `InvalidImage`.
  - Un éxito produce un `PreparedProductImage` con los mismos bytes.
- [X] T059 [US4] Crear `tests/Pos.Infrastructure.Tests/Products/ProductImagePersistenceTests.cs`, con SQLite real (FR-027, FR-028 y FR-030; SC-004):
  - Crear un producto con imagen y luego:
    - Reemplazarla: queda 1 fila en `ProductImages`, con el contenido nuevo.
    - Quitarla: quedan 0 filas.
    - Cambiar solo la imagen incrementa `Version` y `UpdatedAt`.
    - `SearchAsync` devuelve `Thumbnail`.
    - Si falla el guardado por un SKU duplicado, no se guardan ni los datos ni la imagen.
  - Ampliar `tests/Pos.Infrastructure.Tests/Startup/SqliteBackupServiceTests.cs`: después de un respaldo automático se quita la imagen, se restaura el respaldo y la imagen vuelve.
  - Ampliar `tests/Pos.Infrastructure.Tests/Diagnostics/ZipDiagnosticsExporterTests.cs`: el `pos.db` del zip tiene 0 filas en `ProductImages` y conserva los productos (FR-029, SC-006).
- [X] T060 [US4] Actualizar `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs` (clase `SampleData`) y generar `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.2.0.db` con `POS_GENERATE_SAMPLE_DB=1` después de la migración final de T053 (constitución, Principio IV; SC-007):
  - Productos con unidades distintas, 1 con imagen, 1 inactivo, 1 borrado y 1 con precio 0.
  - Ampliar `SampleDatabaseUpgradeTests` para que la imagen de `v0.2.0.db` se conserve al migrar.

### Desktop

- [X] T061 [P] [US4] En `src/Pos.Desktop/Common/IDialogService.cs`, agregar `Task<FileSelection?> PickOpenFileAsync(string title, IReadOnlyList<FileTypeFilter> filters)`:
  - `sealed record FileSelection(string Name, long Length, Func<Task<Stream>> OpenAsync)` y `sealed record FileTypeFilter(string Name, IReadOnlyList<string> Patterns)` en `src/Pos.Desktop/Common/FileSelection.cs`.
  - Implementarlo en `src/Pos.Desktop/Common/DialogService.cs` con `StorageProvider.OpenFilePickerAsync` (`AllowMultiple = false`), siguiendo el patrón de `PickSaveFileAsync`. El tamaño se toma de `IStorageFile.GetBasicPropertiesAsync().Size`.
- [X] T062 [P] [US4] Crear `src/Pos.Desktop/Common/ThumbnailConverter.cs` (`IValueConverter`, patrón `Instance`):
  - `byte[]` → `Bitmap` con `new Bitmap(new MemoryStream(bytes))`.
  - Nulo → devolver `null`.
  - Excepción de decodificación → devolver `null` y registrar un warning de Serilog con contexto `Operation = "MostrarMiniatura"`, sin volcar bytes.
  - Así, la vista decide el ícono neutro según si el `Image` tiene fuente (T066), tanto sin imagen como con miniatura dañada.
- [X] T063 [P] [US4] Agregar el ícono `Icon.ImageOff` (Material Design Icons `image-off-outline`) en `src/Pos.Desktop/Resources/Icons.axaml` y en `src/Pos.Desktop/Resources/Strings.resx`:
  - `Editor_Image` = "Imagen".
  - `Editor_SelectImage` = "Seleccionar imagen…".
  - `Editor_ChangeImage` = "Cambiar imagen…".
  - `Editor_RemoveImage` = "Quitar imagen".
  - `Editor_NoImage` = "Sin imagen".
  - `Editor_ImageFilter` = "Imágenes (*.jpg, *.jpeg, *.png, *.webp)".
  - `Editor_ProcessingImage` = "Procesando imagen…".
  - `Products_ColImage` = "Imagen".
- [X] T064 [US4] En `src/Pos.Desktop/Products/ProductEditorViewModel.cs`:
  - Agregar `PreviewImage` (`byte[]?`), `ImageError` (`string?`), `IsProcessingImage` (bool) y `HasImage` (calculada) como propiedades observables, y un campo `ProductImageChange _imageChange = KeepCurrent`.
  - `SelectImageCommand` abre `PickOpenFileAsync` con los patrones `*.jpg`, `*.jpeg`, `*.png` y `*.webp` y llama a `PrepareProductImageHandler` mediante `OperationRunner`, con `IsProcessingImage = true` durante el proceso.
    - Si tiene éxito: `PreviewImage = prepared.Content`, `_imageChange = Replace(prepared)` y se limpia `ImageError`.
    - Si falla: `ImageError` recibe el mensaje de `InvalidImage` y la imagen anterior no cambia.
  - `RemoveImageCommand`: `PreviewImage = null` y `_imageChange = Remove`.
  - Enviar `_imageChange` en los comandos de guardado y deshabilitar "Guardar" mientras `IsProcessingImage`.
  - Incluir el cambio de imagen en `ProductFormState` para la detección de cambios sin guardar (002).
  - Al cargar, `PreviewImage = product.Image` y `_imageChange = KeepCurrent`.
- [X] T065 [US4] En `src/Pos.Desktop/Products/ProductEditorView.axaml`, agregar la sección "Imagen" después de "Unidad de medida":
  - Un `Border` de 200 × 200 con `Image Source="{Binding PreviewImage, Converter={x:Static common:ThumbnailConverter.Instance}}"` (`Stretch="Uniform"`).
  - Un marcador con `Icon.ImageOff` y "Sin imagen" cuando `!HasImage`.
  - Un `ProgressBar IsIndeterminate` visible con `IsProcessingImage`.
  - Los botones "Seleccionar imagen…" / "Cambiar imagen…" (según `HasImage`) y "Quitar imagen" (habilitado con `HasImage`).
  - `ImageError` debajo, con el estilo de error de los campos.
- [X] T066 [US4] En `src/Pos.Desktop/Products/ProductsView.axaml`, agregar la primera columna "Imagen" (ancho 48) en el encabezado y en la plantilla de fila:
  - `Image` de 40 × 40 con `x:Name="Thumb"` y `Source="{Binding Thumbnail, Converter={x:Static common:ThumbnailConverter.Instance}}"`.
  - Un `PathIcon` con `Icon.ImageOff` visible cuando el `Image` no tiene fuente (`IsVisible="{Binding #Thumb.Source, Converter={x:Static ObjectConverters.IsNull}}"`), lo que cubre tanto un producto sin imagen como una miniatura dañada (spec, casos límite).
  - Aplicar el mismo criterio al marcador "Sin imagen" del editor (T065).

**Checkpoint**: las cuatro historias funcionan.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T067 [P] Actualizar `docs/carpeta-de-datos.md`: las imágenes viven dentro de `data/pos.db` (tabla `ProductImages`); el tamaño estimado por respaldo (de 300 a 450 MB con alrededor de 5,000 imágenes); los respaldos y restauraciones las incluyen; el paquete de diagnóstico no.
- [X] T068 [P] Actualizar `docs/agregar-funcionalidad.md` con el patrón de listado paginado (`ProductPage`, `LocatePageAsync`, barra de paginación y reinicio a la página 1 al buscar o filtrar) como referencia para futuros catálogos.
- [X] T069 [P] Actualizar `docs/migraciones.md` con el ejemplo de revisión de la reconstrucción de `Products` en `ProductCatalogImprovements`: qué verificar en el SQL generado.
- [X] T070 [P] Agregar en `specs/001-pos-foundation/contracts/ui.md` una nota de que el listado y el editor de Productos se rigen ahora por `specs/003-product-catalog-improvements/contracts/ui.md`, y en `specs/001-pos-foundation/spec.md` una nota en FR-013 de que la regla de precio fue reemplazada por FR-015/FR-016 de 003.
- [X] T071 Ejecutar `dotnet build` y `dotnet test` desde la raíz (0 errores, 0 advertencias; las pruebas existentes siguen en verde) y la validación manual de `specs/003-product-catalog-improvements/quickstart.md` §3 en Linux; confirmar a mano con 10,000 productos (la medición automática es T009) que cambiar de página, buscar y cambiar el filtro responde en menos de 1 s.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias. Solo la necesita US4, pero conviene hacerla al inicio para
  detectar conflictos de versión.
- **Foundational (Fase 2)**: depende de Setup y **bloquea todas las historias**.
- **US1 (Fase 3)**: depende de la Fase 2.
- **US2 (Fase 4)**: depende de la Fase 2. Comparte `ProductsViewModel.cs` y `ProductsView.axaml`
  con US1, así que conviene hacerla después de US1.
- **US3 (Fase 5)**: depende de la Fase 2 (para T033 en `ProductRepository`). Incluye el índice
  compuesto de la paginación (T038), porque llega a la base con la migración de T039.
- **US4 (Fase 6)**: depende de US3, porque comparte la migración (T053 la regenera) y las firmas
  de los comandos. T060 genera `v0.2.0.db` con la migración final.
- **Polish (Fase 7)**: después de las historias.
- **Cada fase termina con `dotnet build` y `dotnet test` en verde.** Las adaptaciones del código
  de apoyo de pruebas van junto al cambio de firmas que las provoca: T012 (Fase 2), T034 (US3)
  y T052 (US4).

### Within Each User Story

- Domain → Application → Infrastructure → Desktop.
- T027 antes de T031 y T032; T045 y T046 antes de T050 y T053 a T055; T048 antes de T049 y T055;
  T061 antes de T064.
- Cambios en el mismo archivo son secuenciales:
  - `ProductsViewModel.cs`: T010 → T015 → T018 → T019 → T020.
  - `ProductsView.axaml`: T011 → T017 → T023 → T044 → T066.
  - `ProductEditorViewModel.cs`: T041 → T064.
  - `Product.cs`: T027 → T046.
  - `ProductRepository.cs`: T004 → T005 → T033 → T054.

### Parallel Opportunities

- Fase 2: T008 y T009 en paralelo con T010 y T011.
- Fase 5: T025, T026 y T030 (tres archivos distintos) juntas; luego T028, T029, T035 y T043 en
  paralelo con T031 a T033; T036 después de T031.
- Fase 6: T045, T047, T048, T061, T062 y T063 juntas; luego T055 (Infrastructure) en paralelo
  con T064 a T066 (Desktop); T057 y T058 en paralelo con T055.
- Fase 7: T067 a T070 juntas.

## Parallel Example: User Story 4

```text
# Primero, en paralelo (archivos independientes):
T045 ProductImage.cs (Domain)
T047 InvalidImage en Error.cs (Application)
T048 IImageProcessor / PreparedProductImage / ProductImageChange (Application)
T061 PickOpenFileAsync (Desktop/Common)
T062 ThumbnailConverter (Desktop/Common)
T063 Icono y cadenas (Desktop/Resources)

# Después, en paralelo:
T055 SkiaImageProcessor (Infrastructure)
T064–T066 Editor y listado (Desktop)
```

## Implementation Strategy

### MVP First (User Story 1)

1. Fase 1 y Fase 2: paginación en la base; elimina el corte de 200 filas.
2. Fase 3 (US1): confirmar la causa, reiniciar la página y distinguir los inactivos.
3. **Detenerse y validar**: el defecto está corregido. Por la constitución (Principio I), la
   estabilidad va primero y esto puede entregarse solo.

### Incremental Delivery

1. MVP (US1).
2. US2: navegación completa por páginas.
3. US3: validaciones y unidad de medida (primera migración).
4. US4: imágenes (misma migración si no se publicó; si no, una nueva).
5. Polish: documentación y validación final.

### Notes

- Las pruebas se limitan a lo que exigen la constitución y la spec:
  - T013 reproduce el defecto (FR-005). T014 demuestra que su equivalente falla con el código anterior.
  - T008 y T009 cubren la paginación y el rendimiento (SC-001, SC-002).
  - T028, T029 y T036 cubren las reglas de validación (SC-003).
  - T057 a T059 cubren la imagen, el respaldo y el diagnóstico (SC-004 a SC-006).
  - T040 y T060 cubren la base de ejemplo (SC-007).
- Una migración integrada o publicada nunca se modifica.
- Hacer commit después de cada tarea o grupo lógico.
