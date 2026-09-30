---

description: "Lista de tareas para la estructura de navegación y formularios del POS"
---

# Tasks: Estructura de navegación y formularios del POS

**Input**: documentos de diseño en `specs/002-navigation-forms/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: sí se incluyen. Los exigen los criterios de aceptación 3, 4 y 6 de la especificación
(SC-003 a SC-007) y la constitución (Principio VI). Las pruebas de cada historia se escriben primero
y deben fallar antes de implementar.

**Organization**: por historia de usuario. US1 = pantalla de carga, US2 = Inicio, US3 = menú,
US4 = patrón de formularios, US5 = cambios sin guardar.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece la tarea (US1…US5)
- Todas las rutas son relativas a la raíz del repositorio

## Path Conventions

Solución existente de la fundación: `src/Pos.{Domain,Application,Infrastructure,Desktop}/` y
`tests/Pos.*.Tests/`. Las piezas nuevas de UI van en `src/Pos.Desktop/{Navigation,Forms,Home,Splash}/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: recursos compartidos de UI.

- [X] T001 [P] Crear `src/Pos.Desktop/Resources/Icons.axaml` (`ResourceDictionary` de `StreamGeometry`, con comentario de atribución a Material Design Icons, Apache 2.0) con las claves `Icon.Home`, `Icon.Catalog`, `Icon.Product`, `Icon.Inventory`, `Icon.Stock`, `Icon.Movements`, `Icon.Help`, `Icon.Info`, `Icon.Menu`, `Icon.Back`, `Icon.Sales`, `Icon.Chart`, `Icon.Warning`, y agregarlo a `Application.Resources` en `src/Pos.Desktop/Composition/App.axaml`
- [X] T002 [P] Crear `src/Pos.Desktop/Resources/Logo.axaml` con el logotipo predeterminado como `DrawingImage` (clave `Logo.Default`), vectorial y legible de 32 a 160 px, e incluirlo en `App.axaml`
- [X] T003 [P] Agregar a `src/Pos.Desktop/Resources/Strings.resx` los textos de navegación y menú (`Nav_Home`, `Nav_Catalogs`, `Nav_Products`, `Nav_Inventory`, `Nav_Stock`, `Nav_Movements`, `Nav_Help`, `Nav_About`, `Menu_Toggle`), de la pantalla "disponible más adelante" (`ComingSoon_Title`, `ComingSoon_Message`), de la pantalla de carga (`Splash_Starting`, `Splash_CheckingDatabase`, `Splash_BackingUp`, `Splash_Migrating`, `Splash_Restoring`, `Splash_Finishing`), de Inicio (`Home_Title`, `Card_ActiveProducts`, `Card_LowStock`, `Card_OutOfStock`, `Chart_SalesToday`, `Chart_SalesLast7Days`, `Chart_TopProducts`, `Card_InventoryPending`, `Card_SalesPending`, `Card_Unavailable`) y de formularios (`Form_BackToList`, `Form_Required`, `Unsaved_Title`, `Unsaved_Message`, `Unsaved_Save`, `Unsaved_Discard`, `Unsaved_KeepEditing`), con los textos en español de [contracts/](contracts/)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: registro de módulos, localizador de vistas y `Navigator`, que usan Inicio, el menú y
los formularios.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase.

### Pruebas (escribir primero; deben fallar)

- [X] T004 [P] Escribir `tests/Pos.Desktop.Tests/Navigation/NavigationRegistryTests.cs`:
  - Los grupos y las opciones registrados se ordenan por `Order`.
  - Una opción sin grupo queda en el primer nivel.
  - Un `Id` duplicado o un `GroupId` inexistente lanza `InvalidOperationException` al construir el registro.
  - Un grupo sin opciones no aparece.
  - `RegisteredViewLocator.Build(vm)` devuelve la vista registrada para el tipo del ViewModel, y `Match` es falso para tipos no registrados.
- [X] T005 [P] Escribir `tests/Pos.Desktop.Tests/Navigation/NavigatorTests.cs`:
  - `NavigateAsync("home")` asigna `CurrentEntryId` y `CurrentPage`, e invoca `OnActivatedAsync`.
  - Navegar a la opción actual no vuelve a activarla.
  - Si la pantalla actual implementa `ILeaveGuard` y devuelve falso, no se navega y el resultado es falso.
  - Una opción inexistente no navega y se registra en el log.
  - Las pantallas son singletons: al volver a una pantalla se obtiene la misma instancia.
  - `CanLeaveCurrentAsync` delega en `ILeaveGuard`.

### Implementación

- [X] T006 [P] Crear en `src/Pos.Desktop/Navigation/`: `NavigationGroup.cs` (`record(string Id, string Title, string Icon, int Order)`), `NavigationEntry.cs` (`record(string Id, string Title, string Icon, int Order, string? GroupId, Type ViewModelType)`), `ViewRegistration.cs` (`record(Type ViewModelType, Func<Control> CreateView)`) e `ILeaveGuard.cs` (`Task<bool> CanLeaveAsync()`), según [data-model.md](data-model.md)
- [X] T007 Implementar `src/Pos.Desktop/Navigation/NavigationRegistry.cs`: a partir de los `NavigationGroup` y `NavigationEntry` registrados en DI construye el árbol ordenado (máximo dos niveles), valida `Id` únicos y `GroupId` existentes (si no, `InvalidOperationException` con un mensaje claro), y descarta los grupos sin opciones
- [X] T008 Implementar `src/Pos.Desktop/Navigation/RegisteredViewLocator.cs` (`IDataTemplate` construido a partir de las `ViewRegistration` de DI; `Match` por tipo exacto del ViewModel) e instalarlo una sola vez en `Application.DataTemplates` desde `src/Pos.Desktop/Composition/App.axaml.cs`. Quitar de `App.axaml` los `DataTemplate` de Productos, Editor y Acerca de
- [X] T009 Implementar `src/Pos.Desktop/Navigation/Navigator.cs` según [contracts/navigation.md](contracts/navigation.md): `CurrentEntryId`, `CurrentPage`, `NavigateAsync`, `CanLeaveCurrentAsync` y el evento `CurrentChanged`. Las pantallas se resuelven como singletons desde `IServiceProvider`; una opción inexistente se registra con `ILogger` y devuelve falso
- [X] T010 Implementar `src/Pos.Desktop/Navigation/NavigationServiceCollectionExtensions.cs`: `AddNavigationGroup(id, title, icon, order)`, `AddPage<TViewModel, TView>(id, title, icon, order, groupId)` (registra el ViewModel como singleton, la `NavigationEntry` y la `ViewRegistration`), `AddComponentView<TViewModel, TView>()` (solo la vista, para ViewModels que no son pantalla, como el editor) y `AddNavigationCore()` (`NavigationRegistry`, `Navigator`, `RegisteredViewLocator`)
- [X] T011 Crear los módulos de las pantallas existentes: `src/Pos.Desktop/Products/ProductsModule.cs` (`AddProductsModule()`: grupo `catalogs` "Catálogos", orden 10, `Icon.Catalog`; pantalla `catalogs.products`; vista del editor con `AddComponentView`) y `src/Pos.Desktop/About/AboutModule.cs` (`AddHelpModule()`: grupo `help` "Ayuda", orden 90; pantalla `help.about`). Cambiar `src/Pos.Desktop/Composition/HostBuilder.cs` para que use `AddNavigationCore()` y los módulos, en lugar de los registros sueltos de `PageViewModel`
- [X] T012 Cambiar `src/Pos.Desktop/Shell/MainViewModel.cs` para que exponga `Navigator` (y la pantalla actual desde `Navigator.CurrentPage`) en lugar de `Pages`/`SelectedPage`, y `src/Pos.Desktop/Shell/MainWindow.axaml` para que muestre `Navigator.CurrentPage` en el área de contenido (el menú temporal se reemplaza en US3). Ajustar las pruebas existentes que usaban `SelectedPage`
- [X] T013 Agregar `RunQuietlyAsync(string operation, Func<Task> action, IReadOnlyDictionary<string, object?>? context)` a `src/Pos.Desktop/Common/OperationRunner.cs` (registra con el mismo contexto, pero sin diálogo; devuelve `bool`) con su prueba en `tests/Pos.Desktop.Tests/Common/OperationRunnerTests.cs`
- [X] T014 Ejecutar `dotnet build` y `dotnet test`: 0 advertencias; T004, T005 y las pruebas existentes pasan

**Checkpoint**: la aplicación funciona igual que antes, pero navega por medio de `Navigator` y del
registro de módulos.

---

## Phase 3: User Story 1 - Pantalla de carga (Priority: P1) 🎯 MVP

**Goal**: una pantalla de carga con logotipo, nombre, versión y el paso en curso durante el
arranque; un logotipo reemplazable por archivo (FR-001 a FR-004).

**Independent Test**: se abre la aplicación con una base existente, con una migración pendiente y
con un error de arranque; se verifican la pantalla de carga, los textos de cada paso y el
resultado ([contracts/splash.md](contracts/splash.md)).

### Tests for User Story 1 ⚠️

- [X] T015 [P] [US1] Escribir `tests/Pos.Application.Tests/Startup/DatabaseStartupProgressTests.cs` con los dobles existentes de `StartupFakes.cs`:
  - Base existente al día → `[CheckingDatabase, Finishing]` (más `BackingUp` si toca el respaldo automático).
  - Migración pendiente → `[CheckingDatabase, BackingUp, Migrating, Finishing]`.
  - Falla de migración → incluye `Restoring` después de `Migrating`.
  - `RecoverFromBackupAsync` informa `Restoring`.
  - `progress` nulo funciona igual que antes.
- [X] T016 [P] [US1] Escribir `tests/Pos.Desktop.Tests/Splash/SplashViewModelTests.cs`:
  - Muestra el nombre y la versión de `IAppInfo`, y el texto `Splash_Starting` al inicio.
  - Cada `StartupStep` informado cambia `StepText` al texto de [contracts/splash.md](contracts/splash.md).
  - `WaitMinimumAsync` espera hasta completar 800 ms desde que se creó, pero no agrega espera si ya pasó ese tiempo (usa `TimeProvider` falso).
- [X] T017 [P] [US1] Escribir `tests/Pos.Desktop.Tests/Splash/BrandingAssetsTests.cs` con una carpeta temporal: sin `logo.png` → logotipo predeterminado; con un PNG válido → se carga ese; con un archivo que no es imagen → predeterminado más una advertencia en el log

### Implementation for User Story 1

- [X] T018 [P] [US1] Crear `src/Pos.Application/Startup/StartupStep.cs` (enum `CheckingDatabase`, `BackingUp`, `Migrating`, `Restoring`, `Finishing`). Agregar `IProgress<StartupStep>? progress = null` a `RunAsync` y `RecoverFromBackupAsync` en `IDatabaseStartup.cs` y `DatabaseStartup.cs`, e informar cada paso **antes** de ejecutarlo según [contracts/splash.md](contracts/splash.md). Actualizar `FakeDatabaseStartup` en `tests/Pos.Desktop.Tests/Startup/StartupPresenterTests.cs`
- [X] T019 [US1] Agregar `LogoFile` (`<datos>/logo.png`) a `src/Pos.Application/Abstractions/IAppPaths.cs`, a `src/Pos.Infrastructure/Platform/AppPaths.cs` y a `tests/Pos.Desktop.Tests/TestSupport/FakeAppPaths.cs`
- [X] T020 [P] [US1] Implementar `src/Pos.Desktop/Splash/IBrandingAssets.cs` y `BrandingAssets.cs`: `IImage Logo`, que carga `IAppPaths.LogoFile` como `Bitmap` si existe y es válido, y si no, usa el recurso `Logo.Default`. Registra una advertencia si el archivo es inválido
- [X] T021 [US1] Implementar `src/Pos.Desktop/Splash/SplashViewModel.cs` (`Logo`, `AppName`, `Version`, `StepText`; implementa `IProgress<StartupStep>` publicando en el hilo de UI; `WaitMinimumAsync()` con una duración mínima de 800 ms y un `TimeProvider` inyectable)
- [X] T022 [US1] Implementar `src/Pos.Desktop/Splash/SplashWindow.axaml(.cs)`: sin decoraciones (`SystemDecorations="None"`), centrada, `ShowInTaskbar="False"`, tamaño fijo de unos 420 × 300, logotipo de máximo 160 × 160 con `Stretch="Uniform"`, nombre, versión, `ProgressBar` indeterminada y `StepText`
- [X] T023 [US1] Cambiar `src/Pos.Desktop/Startup/StartupPresenter.cs` para que reciba `IProgress<StartupStep>?` en `RunAsync` y lo pase a `IDatabaseStartup.RunAsync` y `RecoverFromBackupAsync`. Hacer que `src/Pos.Desktop/Common/DialogService.cs` use como dueña la ventana activa, incluida la de carga, cuando no hay `MainWindow`
- [X] T024 [US1] Integrar en `src/Pos.Desktop/Composition/App.axaml.cs`: mostrar `SplashWindow` en cuanto inicia Avalonia → `StartupPresenter.RunAsync(splashViewModel)` → si es exitoso, `WaitMinimumAsync`, crear `MainWindow`, `Navigator.NavigateAsync("home")` (hasta que exista Inicio en US2, a la primera pantalla registrada), mostrarla y cerrar la pantalla de carga → si falla, cerrar la pantalla de carga y `Shutdown(1)`. Registrar `SplashViewModel` e `IBrandingAssets` en `HostBuilder.cs`
- [X] T025 [US1] Documentar `logo.png` (formato PNG, fondo transparente recomendado, se muestra a un máximo de 160 × 160) en `docs/carpeta-de-datos.md`

**Checkpoint**: al abrir la aplicación se ve la pantalla de carga con el paso en curso y después la
ventana principal; los errores de arranque de la fundación siguen funcionando.

---

## Phase 4: User Story 2 - Pantalla de inicio (Priority: P1)

**Goal**: Inicio es la primera pantalla, con la tarjeta de productos activos con datos reales y
las tarjetas y gráficas de existencias y ventas en estado vacío; los módulos registran tarjetas
sin modificar Inicio (FR-005 a FR-011).

**Independent Test**: con un catálogo de muestra, se abre la aplicación y se verifican el conteo
real, los estados vacíos y la navegación desde la tarjeta
([contracts/dashboard.md](contracts/dashboard.md)).

### Tests for User Story 2 ⚠️

- [X] T026 [P] [US2] Escribir `tests/Pos.Application.Tests/Products/CountActiveProductsHandlerTests.cs` con `InMemoryProductRepository` (agregarle `CountActiveAsync`): 12 activos, 3 inactivos y 2 borrados → 12; sin productos → 0
- [X] T027 [P] [US2] Escribir `tests/Pos.Infrastructure.Tests/Products/ProductCountTests.cs` con SQLite real: cuenta solo `IsActive = 1` y `DeletedAt IS NULL`
- [X] T028 [P] [US2] Escribir `tests/Pos.Desktop.Tests/Home/HomeViewModelTests.cs`:
  - Al activarse, carga todas las tarjetas registradas en paralelo, ordenadas por `Order` y separadas en métricas y gráficas.
  - `ActiveProductsCard` queda en `Ready` con "12".
  - Existencias y ventas quedan en `Empty` con `Card_InventoryPending` y `Card_SalesPending`, y `Value` nulo (ningún número).
  - Una tarjeta que lanza una excepción queda en `Error` con `Card_Unavailable`, se registra con `Operation = "CargarTarjeta"` y el título, no muestra diálogo y no afecta a las demás.
  - Activar "Productos activos" navega a `catalogs.products`; activar una tarjeta `Empty` no navega.
  - Volver a Inicio después de un alta actualiza el conteo.
- [X] T029 [P] [US2] Escribir `tests/Pos.Desktop.Tests/Navigation/ModuleRegistrationTests.cs` (SC-007): un contenedor con los módulos reales más un módulo de prueba (`AddNavigationGroup("test", ...)`, `AddPage<TestPageViewModel, TestPageView>("test.page", ...)`, `AddDashboardCard<TestCard>()`) muestra el grupo, la opción y la tarjeta en `NavigationRegistry` y en `HomeViewModel.Cards`, sin cambios en otras clases

### Implementation for User Story 2

- [X] T030 [P] [US2] Agregar `Task<long> CountActiveAsync(CancellationToken)` a `src/Pos.Application/Products/IProductRepository.cs` y a `src/Pos.Infrastructure/Products/ProductRepository.cs` (`IsActive && DeletedAt == null`), y crear `src/Pos.Application/Products/CountActiveProducts/CountActiveProductsHandler.cs` (`Task<Result<long>> HandleAsync(CancellationToken)`), registrado con `AddScoped` en `src/Pos.Application/DependencyInjection.cs`
- [X] T031 [P] [US2] Crear `src/Pos.Desktop/Home/DashboardCard.cs`, según [contracts/dashboard.md](contracts/dashboard.md): clase abstracta observable con `Title`, `Icon`, `Order`, `Kind` (`Metric` o `Chart`), `State` (`Loading`, `Ready`, `Empty` o `Error`), `Value`, `EmptyMessage`, `NavigateTo`, `IsNavigable` (verdadero solo con `Ready` y destino) y `protected abstract Task LoadCoreAsync()`. `LoadAsync()` pone `Loading`, ejecuta `LoadCoreAsync` con `OperationRunner.RunQuietlyAsync("CargarTarjeta", …, {Card = Title})` y, si falla, pone `Error`. Agregar la extensión `AddDashboardCard<T>()` en `NavigationServiceCollectionExtensions.cs`
- [X] T032 [US2] Crear las tarjetas en `src/Pos.Desktop/Home/Cards/`:
  - `ActiveProductsCard.cs`: métrica; `CountActiveProductsHandler` por medio de `UseCases`; valor formateado con separador de miles es-MX; destino `catalogs.products`.
  - `ComingSoonCard.cs`: base para tarjetas `Empty` con el mensaje recibido.
  - `LowStockCard.cs` y `OutOfStockCard.cs`: métricas con `Card_InventoryPending`.
  - `SalesTodayChart.cs`, `SalesLast7DaysChart.cs` y `TopProductsChart.cs`: gráficas con `Card_SalesPending`.
- [X] T033 [US2] Implementar `src/Pos.Desktop/Home/HomeViewModel.cs` (`PageViewModel` con `Cards`, `Metrics` y `Charts` a partir de `IEnumerable<DashboardCard>` ordenadas; `OnActivatedAsync` carga todas en paralelo; `ActivateCardCommand` navega con `Navigator` si `IsNavigable`)
- [X] T034 [US2] Implementar `src/Pos.Desktop/Home/HomeView.axaml(.cs)`:
  - Métricas en un `WrapPanel`, como tarjetas de al menos 220 × 120 con ícono, título y valor grande.
  - Gráficas en una cuadrícula de dos columnas que baja a una en ventanas angostas.
  - Una tarjeta en `Empty` se ve atenuada, con su ícono y el mensaje, sin ejes ni datos.
  - Una tarjeta en `Error` muestra `Card_Unavailable`.
  - Una tarjeta navegable se activa con clic, toque o Enter (es un `Button` con estilo de tarjeta).
- [X] T035 [US2] Crear `src/Pos.Desktop/Home/HomeModule.cs` (`AddHomeModule()`: pantalla `home` de primer nivel, orden 0, `Icon.Home`), agregar `ActiveProductsCard` en `AddProductsModule()`, crear `src/Pos.Desktop/Inventory/InventoryModule.cs` (`AddInventoryModule()`: solo las tarjetas de existencias por ahora; el menú de Inventario llega en US3) y `src/Pos.Desktop/Home/SalesPlaceholders.cs` (`AddSalesPlaceholders()`). Registrarlos en `HostBuilder.cs` y hacer que `App.axaml.cs` navegue a `home` al abrir la ventana principal

**Checkpoint**: la aplicación abre en Inicio con datos reales y estados vacíos; T026 a T029 pasan.

---

## Phase 5: User Story 3 - Menú lateral colapsable con submenús (Priority: P1)

**Goal**: el menú de dos niveles, colapsable, con menú flotante, tooltips, marca de la opción
actual, preferencias persistentes, auto-contracción y Ctrl+B (FR-012 a FR-020b).

**Independent Test**: se expande y contrae el menú, se abren grupos, se navega contraído con el
menú flotante, se reinicia y se conserva el estado, y la ventana angosta lo contrae
([contracts/navigation.md](contracts/navigation.md)).

### Tests for User Story 3 ⚠️

- [X] T036 [P] [US3] Escribir `tests/Pos.Infrastructure.Tests/Platform/JsonFilePreferencesStoreTests.cs`:
  - Guardar y leer un registro devuelve el mismo valor.
  - Una clave inexistente → `null`.
  - Un JSON dañado → `null` y una advertencia en el log.
  - Escribe `.tmp` y renombra (no queda ningún `.tmp`).
  - Las claves se guardan en `<datos>/preferences/<clave>.json`.
- [X] T037 [P] [US3] Escribir `tests/Pos.Desktop.Tests/Navigation/MenuViewModelTests.cs` con un `IPreferencesStore` en memoria:
  - Estructura inicial: Inicio; Catálogos > Productos; Inventario > Existencias, Movimientos; Ayuda > Acerca de.
  - `ToggleCommand` alterna `IsUserCollapsed` y guarda `{collapsed}`.
  - `ToggleGroupCommand` alterna `IsExpanded` y guarda `expandedGroups`.
  - Una preferencia inexistente o dañada → expandido, con solo el grupo actual abierto.
  - Los ids de grupos que ya no existen se ignoran.
  - `SetWindowWidth(999)` → `IsAutoCollapsed` e `IsCollapsed`, sin guardar la preferencia; `SetWindowWidth(1000)` → se vuelve al estado del operador.
  - Seleccionar una opción navega con `Navigator`; `IsCurrent` en la opción e `IsCurrentGroup` en su grupo se actualizan en ambos estados.
  - Si `Navigator` rechaza la navegación, la marca no cambia.
- [X] T038 [P] [US3] Escribir `tests/Pos.Desktop.Tests/Navigation/PageStateRetentionTests.cs` (FR-020b): en Productos con búsqueda "leche", el filtro de inactivos y una selección, navegar a Inicio y volver conserva los tres y refresca los datos; si el producto seleccionado se borró mientras tanto, la selección queda vacía sin error
- [X] T039 [P] [US3] Escribir `tests/Pos.Desktop.Tests/Navigation/ComingSoonTests.cs`: navegar a `inventory.stock` e `inventory.movements` muestra `ComingSoonViewModel` con el título de la opción y el mensaje `ComingSoon_Message`

### Implementation for User Story 3

- [X] T040 [P] [US3] Crear `src/Pos.Application/Abstractions/IPreferencesStore.cs` (`T? Load<T>(string key) where T : class`, `void Save<T>(string key, T value)`), agregar `PreferencesDirectory` (`<datos>/preferences`) a `IAppPaths`, `AppPaths` (incluido `EnsureDirectories`) y `FakeAppPaths`, e implementar `src/Pos.Infrastructure/Platform/JsonFilePreferencesStore.cs` (System.Text.Json; `.tmp` y luego renombrar; un JSON inválido o una E/S fallida → `null` y una advertencia con `ILogger`), registrado como singleton en `src/Pos.Infrastructure/DependencyInjection.cs`
- [X] T041 [US3] Implementar `src/Pos.Desktop/Navigation/MenuViewModel.cs` y `MenuItemViewModel.cs` (grupo y opción) según [data-model.md](data-model.md): `IsUserCollapsed`, `IsAutoCollapsed`, `IsCollapsed`, `ToggleCommand`, `ToggleGroupCommand`, `SelectEntryCommand`, `SetWindowWidth(double)` con un umbral de **1000**, preferencias en la clave `navigation` con el registro `NavigationPreferences(bool Collapsed, string[] ExpandedGroups)`, y sincronización con `Navigator.CurrentChanged`
- [X] T042 [US3] Implementar `src/Pos.Desktop/Navigation/MenuView.axaml(.cs)`:
  - Botón ☰ (`Icon.Menu`) con tooltip `Menu_Toggle` (Ctrl+B).
  - Expandido (240 px): grupos con encabezado que se puede pulsar y flecha; opciones con ícono y texto, con sangría.
  - Contraído (56 px): solo íconos; cada grupo es un `Button` con `MenuFlyout` de sus opciones, y cada opción de primer nivel es un botón.
  - `ToolTip.Tip` en todos los ítems contraídos.
  - Estilos de opción actual (fondo de acento) y de grupo actual (marca lateral y negrita).
  - `MinHeight` y `MinWidth` de 44 en todos los ítems; navegable con Tab y Enter.
- [X] T043 [US3] Cambiar `src/Pos.Desktop/Shell/MainWindow.axaml(.cs)` y `MainViewModel.cs`: un `SplitView` en modo `CompactInline` con `MenuView` en el panel (`IsPaneOpen` = no `IsCollapsed`, `OpenPaneLength` 240, `CompactPaneLength` 56) y `Navigator.CurrentPage` en el contenido; `KeyBinding` Ctrl+B → `Menu.ToggleCommand`; el cambio de `Bounds.Width` llama a `Menu.SetWindowWidth`
- [X] T044 [US3] Crear `src/Pos.Desktop/Navigation/ComingSoonViewModel.cs` y `ComingSoonView.axaml(.cs)` (ícono grande atenuado, título de la opción y `ComingSoon_Message`) y la extensión `AddComingSoonPage(id, title, icon, order, groupId)`. Completar `AddInventoryModule()` en `src/Pos.Desktop/Inventory/InventoryModule.cs` con el grupo `inventory` "Inventario" (orden 20, `Icon.Inventory`) y las opciones `inventory.stock` (Existencias, `Icon.Stock`) e `inventory.movements` (Movimientos, `Icon.Movements`)
- [X] T045 [US3] Revisar que `ProductsViewModel.OnActivatedAsync` y `AboutViewModel.OnActivatedAsync` refresquen los datos sin reiniciar la búsqueda, los filtros ni la selección (FR-020b), y que una selección inexistente quede en `null`, en `src/Pos.Desktop/Products/ProductsViewModel.cs`

**Checkpoint**: el menú completo funciona y recuerda su estado; T036 a T039 pasan.

---

## Phase 6: User Story 4 - Patrón estándar de formularios (Priority: P1)

**Goal**: `FormViewModel` y `FormHost` con presentación en panel lateral o pantalla completa,
campos obligatorios marcados, validación al guardar y Productos migrado al patrón (FR-021 a
FR-027).

**Independent Test**: Productos abre como panel lateral; un formulario grande de prueba abre a
pantalla completa con el menú visible y "Regresar al listado"; en ambos se prueban Guardar,
Cancelar, la validación al guardar y el foco en el primer error
([contracts/forms.md](contracts/forms.md)).

### Tests for User Story 4 ⚠️

- [X] T046 [P] [US4] Escribir `tests/Pos.Desktop.Tests/Forms/FormViewModelTests.cs` con un formulario de prueba mínimo:
  - `IsDirty` es falso al abrir, verdadero al cambiar un campo y falso al volver al valor original.
  - Un cambio que se guardaría igual (por ejemplo, un SKU en minúsculas) no marca cambios.
  - `SaveCommand` no admite ejecuciones simultáneas (doble clic → un solo guardado).
  - Un guardado exitoso emite `Saved` y reinicia el estado original.
  - Salir de un campo (cambiar una propiedad) no asigna errores; los errores solo aparecen al guardar.
- [X] T047 [P] [US4] Crear `tests/Pos.Desktop.Tests/Forms/SampleLargeFormViewModel.cs` (12 campos en dos secciones, 4 obligatorios, con errores al guardar si los obligatorios están vacíos) y escribir `tests/Pos.Desktop.Tests/Forms/LargeFormTests.cs` (SC-004): una pantalla de prueba con `FormHost` abre el formulario con `FullScreen` → `ActivePresentation == FullScreen` y `IsListVisible == false`; "Regresar al listado" (`CancelCommand`) sin cambios lo cierra y vuelve a mostrar el listado; el mismo formulario abierto con `SidePanel` se comporta igual (FR-023)
- [X] T048 [P] [US4] Escribir `tests/Pos.Desktop.Tests/Products/ProductFormPatternTests.cs`: Productos abre alta y edición con `SidePanel` (FR-027); `IsRequired` de Nombre, SKU y Precio es verdadero y de Código de barras falso; guardar con todo vacío muestra todos los errores y `FocusField = Name`

### Implementation for User Story 4

- [X] T049 [P] [US4] Crear en `src/Pos.Desktop/Forms/`: `FormPresentation.cs` (enum `SidePanel`, `FullScreen`) y `FormViewModel.cs` según [contracts/forms.md](contracts/forms.md) (`Title`, `SaveCommand` y `CancelCommand` generados, `IsDirty`, `FocusField`, los eventos `Saved` y `Closed`, `protected abstract object CaptureState()`, `protected abstract Task<bool> SaveCoreAsync()` y `protected void ResetOriginalState()`). En esta historia `CancelCommand` cierra sin confirmar; la confirmación llega en US5
- [X] T050 [US4] Crear `src/Pos.Desktop/Forms/FormHost.cs` (`ActiveForm`, `ActivePresentation`, `IsListVisible` = no `FullScreen` activo, `OpenAsync(form, presentation)`, `CloseActiveAsync()`; se suscribe a `Saved` y `Closed` del formulario para cerrarlo) y exponerlo en `PageViewModel` como `FormHost Forms` opcional
- [X] T051 [US4] Crear `src/Pos.Desktop/Forms/FormHostView.axaml(.cs)`: un control con la propiedad `ListContent`. Con `SidePanel` muestra `ListContent` y el formulario en un panel a la derecha (borde, 16 px de relleno y ancho según el contenido). Con `FullScreen` oculta `ListContent` y muestra una barra superior con `Icon.Back` + `Form_BackToList` (→ `CancelCommand`) y el formulario ocupando el área de contenido con desplazamiento
- [X] T052 [P] [US4] Crear `src/Pos.Desktop/Forms/FieldLabel.axaml(.cs)` (propiedades `Text` e `IsRequired`; con `IsRequired`, un asterisco en color de acento y `AutomationProperties.Name` con "obligatorio")
- [X] T053 [US4] Migrar `src/Pos.Desktop/Products/ProductEditorViewModel.cs` a `FormViewModel`: `CaptureState()` devuelve un registro con los valores normalizados (`Product.NormalizeName`, `NormalizeSku` y `NormalizeBarcode`, los centavos de `Money.TryParse` o el texto recortado, e `IsActive`); `ResetOriginalState()` al crear, al cargar, al recargar tras un conflicto y después de guardar; propiedades `IsNameRequired`, `IsSkuRequired`, `IsPriceRequired` (verdaderas) e `IsBarcodeRequired` (falsa). Conservar todo el comportamiento probado en la fundación (las pruebas existentes deben seguir pasando sin cambiar sus afirmaciones)
- [X] T054 [US4] Cambiar `src/Pos.Desktop/Products/ProductsViewModel.cs` para que use `FormHost` (`OpenAsync(editor, FormPresentation.SidePanel)`) en lugar de la propiedad `Editor`, y `src/Pos.Desktop/Products/ProductsView.axaml` para que use `FormHostView` con el listado en `ListContent`, y `src/Pos.Desktop/Products/ProductEditorView.axaml` para que use `FieldLabel` con `IsRequired`. Ajustar las pruebas existentes de Productos que usaban `page.Editor` para que usen `page.Forms.ActiveForm`

**Checkpoint**: Productos funciona con el patrón y el formulario grande de prueba pasa; T046 a
T048 y las pruebas de la fundación pasan.

---

## Phase 7: User Story 5 - Confirmación al salir con cambios sin guardar (Priority: P1)

**Goal**: preguntar Guardar, Descartar o Seguir editando al cancelar, navegar, cerrar el panel o
cerrar la aplicación con cambios; no preguntar si no hubo cambios o si se revirtieron (FR-028 a
FR-030).

**Independent Test**: se modifica un formulario y se prueban los cuatro disparadores con las tres
opciones; se repite sin cambios y con cambios revertidos ([contracts/forms.md](contracts/forms.md)).

### Tests for User Story 5 ⚠️

- [X] T055 [P] [US5] Agregar `AskUnsavedChangesAsync()` y su respuesta configurable a `tests/Pos.Desktop.Tests/TestSupport/FakeDialogService.cs` (con un contador de preguntas) y escribir `tests/Pos.Desktop.Tests/Forms/UnsavedChangesTests.cs` (SC-005), con el editor de Productos y el formulario grande de prueba:
  - Cancelar con cambios → pregunta. Save válido → guarda y cierra; Save con errores → sigue abierto con errores; Discard → cierra sin guardar; KeepEditing → sigue abierto con lo capturado.
  - Navegar con el menú (`Navigator.NavigateAsync("home")`) con cambios → pregunta; KeepEditing → la pantalla actual no cambia; Discard → navega; Save válido → guarda y navega.
  - Abrir otro producto desde el listado con cambios → pregunta.
  - Cerrar la aplicación (`MainViewModel.CanCloseAsync`) con cambios → pregunta; KeepEditing → falso.
  - Sin cambios, o con cambios revertidos → ningún disparador pregunta (contador en 0).
  - Guardar desde la confirmación con un error inesperado → sigue abierto con lo capturado y el mensaje genérico.
  - Navegar a la opción ya abierta con cambios no pregunta ni reinicia el formulario.

### Implementation for User Story 5

- [X] T056 [P] [US5] Crear `src/Pos.Desktop/Forms/UnsavedChangesChoice.cs` (`Save`, `Discard`, `KeepEditing`), agregar `AskUnsavedChangesAsync()` a `src/Pos.Desktop/Common/IDialogService.cs` y generalizar `src/Pos.Desktop/Common/DialogWindow.cs` a una lista de botones (texto, valor, acento, predeterminado) para implementarlo en `DialogService.cs`: `Unsaved_Title` y `Unsaved_Message` con Guardar (acento), Descartar y Seguir editando (predeterminado para Enter y Esc)
- [X] T057 [US5] Implementar `ConfirmLeaveAsync()` en `src/Pos.Desktop/Forms/FormViewModel.cs` según la tabla de [contracts/forms.md](contracts/forms.md) y hacer que `CancelCommand` la use antes de emitir `Closed`
- [X] T058 [US5] En `src/Pos.Desktop/Forms/FormHost.cs`, `OpenAsync` confirma la salida del formulario activo antes de reemplazarlo, y `CloseActiveAsync` confirma antes de cerrar. Hacer que `PageViewModel` implemente `ILeaveGuard` delegando en `Forms?.CloseActiveAsync()` (verdadero si no hay formulario)
- [X] T059 [US5] Agregar `CanCloseAsync()` a `src/Pos.Desktop/Shell/MainViewModel.cs` (→ `Navigator.CanLeaveCurrentAsync()`) y cambiar `OnMainWindowClosing` en `src/Pos.Desktop/Composition/App.axaml.cs`: cancelar el primer cierre, `await CanCloseAsync()`; si es falso, no respaldar y dejar la ventana abierta; si es verdadero, respaldo al cerrar y cierre (el flujo de la fundación)

**Checkpoint**: ningún disparador pierde cambios sin preguntar; T055 pasa.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T060 [P] Actualizar `docs/agregar-funcionalidad.md`: registrar un módulo con `Add<Modulo>Module()` (grupo, página y vista, tarjetas), `AddComponentView`, heredar de `FormViewModel` (`CaptureState` y `SaveCoreAsync`), elegir `SidePanel` o `FullScreen` (regla de 8 campos) y usar `FieldLabel`
- [X] T061 [P] Actualizar `docs/carpeta-de-datos.md` con `preferences/navigation.json` (formato; se puede borrar para restablecer el menú) y `logo.png`
- [X] T062 [P] Actualizar `specs/001-pos-foundation/contracts/ui.md` con una nota: la navegación lateral y el editor en panel se rigen ahora por `specs/002-navigation-forms/contracts/`
- [X] T063 Revisar que ningún texto al operador esté fijo en XAML o en los ViewModels nuevos (todo en `Strings.resx`) y que ninguna tarjeta muestre números en estado `Empty`
- [X] T064 Renderizar sin pantalla física (con el proyecto auxiliar del scratchpad) la pantalla de carga, Inicio, el menú expandido y contraído con un menú flotante abierto, Productos con el panel y el formulario grande de prueba, y revisar visualmente el diseño a 1200 y a 900 px de ancho
- [X] T065 Ejecutar `dotnet build` y `dotnet test` desde la raíz: 0 advertencias y todas las pruebas aprobadas (SC-003 a SC-007)
- [ ] T066 Ejecutar el recorrido manual de [quickstart.md](quickstart.md), sección 2, en Linux y en Windows, midiendo que la pantalla de carga aparece en menos de 1 s (SC-001, SC-002 y SC-008)
  - Estado 2026-09-30: en Linux, la aplicación real arranca sin errores en el log y abre Inicio; la pantalla de carga se muestra antes del primer paso de la base, que ocurre a +1.19 s del inicio del proceso (límite superior que incluye la inicialización de .NET y Avalonia; falta medir con precisión la aparición de la ventana contra SC-002). Las pantallas se revisaron renderizadas sin pantalla física a 1200 y 900 px. Faltan el recorrido manual con teclado y mouse y todo en Windows.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (fase 1)**: no depende de nada.
- **Foundational (fase 2)**: depende de Setup y **bloquea todas las historias**.
- **US1 (fase 3)**: depende de Foundational.
- **US2 (fase 4)**: depende de Foundational. Es independiente de US1, salvo el ajuste de T035 en `App.axaml.cs`, que toca el mismo archivo que T024: hacer US1 antes o coordinar.
- **US3 (fase 5)**: depende de Foundational. Usa `home` (US2) solo para la prueba de conservación de estado (T038).
- **US4 (fase 6)**: depende de Foundational.
- **US5 (fase 7)**: depende de US4 (`FormViewModel` y `FormHost`) y de `Navigator` (Foundational). Para el disparador del menú se prueba con `Navigator`, no requiere la vista de US3.
- **Polish (fase 8)**: depende de todas.

```text
Setup ─► Foundational ─┬─► US1 (carga) ──┐
                       ├─► US2 (inicio) ─┤
                       ├─► US3 (menú) ───┼─► Polish
                       └─► US4 (formularios) ─► US5 (cambios sin guardar)
```

### Within Each User Story

- Las pruebas se escriben primero y deben fallar.
- Orden: Application e Infrastructure → ViewModels → vistas → registro del módulo y `App`.

### Parallel Opportunities

- Setup: T001, T002 y T003.
- Foundational: pruebas T004 y T005; T006 en paralelo con las pruebas.
- Después de Foundational: US1, US2, US3 y US4 pueden avanzar en paralelo (archivos distintos, salvo `App.axaml.cs` y `HostBuilder.cs`, que se tocan en orden).
- Dentro de cada historia: todas las pruebas [P] juntas; T018 y T020 (US1); T030 y T031 (US2); T040 (US3); T049 y T052 (US4).

---

## Parallel Example: User Story 2

```bash
Task: "CountActiveProductsHandlerTests en tests/Pos.Application.Tests/Products/CountActiveProductsHandlerTests.cs"
Task: "ProductCountTests en tests/Pos.Infrastructure.Tests/Products/ProductCountTests.cs"
Task: "HomeViewModelTests en tests/Pos.Desktop.Tests/Home/HomeViewModelTests.cs"
Task: "ModuleRegistrationTests en tests/Pos.Desktop.Tests/Navigation/ModuleRegistrationTests.cs"
```

## Parallel Example: User Story 4

```bash
Task: "FormViewModelTests en tests/Pos.Desktop.Tests/Forms/FormViewModelTests.cs"
Task: "SampleLargeFormViewModel + LargeFormTests en tests/Pos.Desktop.Tests/Forms/"
Task: "ProductFormPatternTests en tests/Pos.Desktop.Tests/Products/ProductFormPatternTests.cs"
```

---

## Implementation Strategy

### MVP First

Todas las historias son P1, pero un primer incremento útil es **Foundational + US1 + US2 + US3**:
la aplicación abre con pantalla de carga, llega a Inicio con datos reales y se navega con el menú
nuevo.

1. Fases 1 y 2.
2. US1 → **validar** la pantalla de carga con los escenarios de arranque.
3. US2 → **validar** Inicio.
4. US3 → **validar** el menú y sus preferencias.

### Incremental Delivery

1. MVP (navegación completa).
2. US4 → patrón de formularios con Productos migrado.
3. US5 → confirmación de cambios sin guardar.
4. Polish → documentación, revisión visual y validación en ambos sistemas.

---

## Notes

- [P] significa archivos distintos y sin dependencias pendientes.
- Las pruebas de la fundación (`001-pos-foundation`) deben seguir pasando. Si una se ajusta (por
  ejemplo, `page.Editor` → `page.Forms.ActiveForm`), se cambia la forma de acceder, no lo que se
  afirma.
- Sin migraciones: esta funcionalidad no cambia el esquema, así que no genera una base de ejemplo
  nueva.
- Commits con Conventional Commits por tarea o por grupo lógico.
