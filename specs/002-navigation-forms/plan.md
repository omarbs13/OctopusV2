# Implementation Plan: Estructura de navegación y formularios del POS

**Branch**: `002-navigation-forms` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-navigation-forms/spec.md`

## Summary

Se agrega la estructura de navegación y formularios sobre la fundación (`001-pos-foundation`):

- Una pantalla de carga que muestra el avance del arranque existente.
- Una pantalla de Inicio con tarjetas registrables por módulo. En esta fase solo "Productos
  activos" tiene datos reales; existencias y ventas muestran estado vacío.
- Un menú lateral de dos niveles, colapsable, con menú flotante y preferencias persistentes.
- Un patrón único de formularios (panel lateral o pantalla completa) con confirmación de cambios
  sin guardar.

Cada módulo se registra por DI (menú, pantallas, vistas y tarjetas), así que los módulos futuros
se integran sin modificar vistas existentes. No hay cambios de esquema ni dependencias nuevas.
Ver [research.md](research.md).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (igual que la fundación)

**Primary Dependencies**: las existentes (Avalonia 12.1, CommunityToolkit.Mvvm 8.4,
Microsoft.Extensions.Hosting, Serilog, EF Core 10 Sqlite, FluentValidation); **ninguna nueva**

**Storage**: SQLite existente, sin migraciones; preferencias del menú en
`<datos>/preferences/navigation.json`; logotipo opcional en `<datos>/logo.png`

**Testing**: xUnit v3 (Microsoft.Testing.Platform); ViewModels sin UI en `Pos.Desktop.Tests`;
SQLite real para el conteo

**Target Platform**: escritorio Windows 10+ y Linux (X11 o Wayland)

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas

**Performance Goals**: pantalla de carga visible en menos de 1 s (SC-002); cualquier opción a 2
selecciones o menos (SC-008); Inicio carga sus tarjetas en paralelo

**Constraints**: offline; UI receptiva; 0 advertencias; áreas de toque de al menos 44 px; ningún
dato inventado en Inicio

**Scale/Scope**: 5 entradas de menú en 3 grupos; 6 tarjetas; 1 formulario real (Productos) y 1
de prueba grande

No queda ningún NEEDS CLARIFICATION.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitución v1.1.0.

| Principio o regla | Cómo lo cumple el diseño | Antes | Después |
|---|---|---|---|
| I. Venta sin conexión | Nada depende de la red | ✅ | ✅ |
| I. Errores no cierran la app | Una tarjeta con error se aísla y se registra; las acciones pasan por `OperationRunner` | ✅ | ✅ |
| I. Estabilidad | La pantalla de carga solo envuelve el arranque existente, sin cambiar su orden obligatorio | ✅ | ✅ |
| II. Capas | `IPreferencesStore` y `CountActiveProducts` en Application con implementación en Infrastructure; navegación, formularios y tarjetas en Desktop fuera de `Composition` sin tocar Infrastructure; las pruebas de arquitectura siguen aplicando | ✅ | ✅ |
| II. Organización por funcionalidad | `Desktop/Navigation`, `Desktop/Forms`, `Desktop/Home`, `Desktop/Splash`; cada módulo con su `Add<Modulo>Module()` | ✅ | ✅ |
| III. Lógica en el núcleo | El conteo es un caso de uso; el estado normalizado de un formulario reutiliza reglas de Domain (`Product.Normalize*`, `Money`) sin calcular nada de negocio | ✅ | ✅ |
| IV. Integridad de datos | Sin cambios de esquema; las preferencias están fuera de la base del negocio | ✅ | ✅ |
| V. Multiplataforma | Rutas con `IAppPaths`; íconos vectoriales; sin APIs de un solo sistema operativo | ✅ | ✅ |
| VI. Calidad | Pruebas de ViewModels, del registro de módulos, del arranque con progreso y de las preferencias; formulario grande de prueba | ✅ | ✅ |
| VII. YAGNI | Sin paquete de íconos, sin librería de gráficas hasta Ventas, sin formulario grande real; ninguna dependencia nueva | ✅ | ✅ |
| VIII. Diagnóstico | Advertencias registradas para preferencias dañadas y logotipo inválido; errores de tarjetas con contexto | ✅ | ✅ |
| IX. Seguridad local | No aplica (sin credenciales ni operaciones sensibles) | ✅ | ✅ |
| Restricciones técnicas | Solo el stack existente | ✅ | ✅ |
| Flujo de desarrollo | Especificación y clarificación hechas; este plan; tareas a continuación | ✅ | ✅ |

**Resultado**: sin violaciones.

## Project Structure

### Documentation (this feature)

```text
specs/002-navigation-forms/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── navigation.md    # Registro de módulos, Navigator y menú
│   ├── forms.md         # FormViewModel, presentación y cambios sin guardar
│   ├── dashboard.md     # Tarjetas de Inicio
│   └── splash.md        # Pantalla de carga
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

Archivos nuevos (➕) y modificados (✏️):

```text
src/Pos.Application/
├── Abstractions/IPreferencesStore.cs                         ➕
├── Abstractions/IAppPaths.cs                                 ✏️ PreferencesDirectory, LogoFile
├── Startup/StartupStep.cs                                    ➕
├── Startup/DatabaseStartup.cs, IDatabaseStartup.cs           ✏️ IProgress<StartupStep>
├── Products/IProductRepository.cs                            ✏️ CountActiveAsync
└── Products/CountActiveProducts/CountActiveProductsHandler.cs ➕

src/Pos.Infrastructure/
├── Platform/AppPaths.cs                                      ✏️
├── Platform/JsonFilePreferencesStore.cs                      ➕
└── Products/ProductRepository.cs                             ✏️ CountActiveAsync

src/Pos.Desktop/
├── Navigation/   NavigationRegistry, NavigationGroup, NavigationEntry, ViewRegistration,
│                 RegisteredViewLocator, Navigator, ILeaveGuard, MenuViewModel, MenuView,
│                 NavigationServiceCollectionExtensions, ComingSoonViewModel/View        ➕
├── Forms/        FormViewModel, FormHost, FormPresentation, FormHostView, FieldLabel,
│                 UnsavedChangesChoice                                                  ➕
├── Home/         HomeViewModel/View, DashboardCard, cartas de productos, existencias y ventas ➕
├── Splash/       SplashWindow, SplashViewModel, IBrandingAssets, BrandingAssets        ➕
├── Resources/    Icons.axaml, Logo.axaml ➕; Strings.resx ✏️
├── Common/       IDialogService, DialogService, DialogWindow (tres botones), OperationRunner.RunQuietlyAsync ✏️
├── Products/     ProductEditorViewModel → FormViewModel; ProductsViewModel con FormHost e ILeaveGuard;
│                 ProductEditorView con FieldLabel; ProductsModule.cs                  ✏️/➕
├── About/        AboutModule.cs ➕
├── Shell/        MainViewModel (Navigator y Menu), MainWindow (menú, ancho, Ctrl+B)  ✏️
└── Composition/  App (pantalla de carga y cierre protegido), HostBuilder (módulos)   ✏️

tests/
├── Pos.Application.Tests/   Startup/DatabaseStartupProgressTests, Products/CountActiveProductsHandlerTests ➕
├── Pos.Infrastructure.Tests/ Platform/JsonFilePreferencesStoreTests, Products/ProductCountTests ➕
└── Pos.Desktop.Tests/        Navigation/, Forms/ (incluye SampleLargeFormViewModel), Home/, Splash/ ➕
```

**Structure Decision**: se mantiene la solución de la fundación. Las piezas transversales de UI
(`Navigation`, `Forms`, `Home` y `Splash`) viven en Desktop, fuera de `Composition`, y cada
módulo aporta un `Add<Modulo>Module()`. Solo Application e Infrastructure cambian para el
conteo, las preferencias y el progreso del arranque.

## Documentación a actualizar

- `docs/agregar-funcionalidad.md`: registrar el módulo (menú, pantalla, vista y tarjetas), el
  patrón `FormViewModel` y la elección de panel lateral o pantalla completa.
- `docs/carpeta-de-datos.md`: `preferences/` y `logo.png`.

## Complexity Tracking

Sin violaciones de la constitución; no aplica.
