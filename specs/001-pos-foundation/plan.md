# Implementation Plan: Fundación del POS con flujo de referencia de Productos

**Branch**: `001-pos-foundation` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-pos-foundation/spec.md`

## Summary

Se crea desde cero la solución del POS de escritorio con sus cuatro capas (`Pos.Domain`,
`Pos.Application`, `Pos.Infrastructure` y `Pos.Desktop`) y cinco proyectos de pruebas. Como
flujo de referencia de punta a punta, se implementa la gestión de Productos: alta, búsqueda,
edición con concurrencia optimista y borrado lógico.

Alrededor de ese flujo se construye la base operativa que exige la constitución:

- Arranque seguro: instancia única, detección de una base más nueva, respaldo previo a migrar,
  restauración si la migración falla y recuperación de una base dañada.
- Respaldos automáticos.
- Logging estructurado y manejo global de errores.
- Exportación de diagnóstico.
- Pruebas de arquitectura, CI en Windows y Linux, y documentación en español.

El enfoque técnico: EF Core Code First sobre SQLite en modo WAL, dinero en centavos, índices
únicos parciales para SKU y código de barras, búsqueda sobre una columna normalizada sin acentos,
y la secuencia de arranque orquestada en Application a través de puertos (ver
[research.md](research.md)).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.1xx, fijado en `global.json`)

**Primary Dependencies**: Avalonia 12.x (Fluent), CommunityToolkit.Mvvm 8.4.x,
Microsoft.Extensions.Hosting 10.0.x, EF Core 10.0.x (Sqlite), FluentValidation 12.x, Serilog 4.x
(Extensions.Hosting, Sinks.File, Formatting.Compact)

**Storage**: SQLite local (`data/pos.db`) en modo WAL, dentro de la carpeta de datos del usuario;
respaldos como archivos SQLite (research §11 y §15)

**Testing**: xUnit v3, NetArchTest.Rules, SQLite real en archivos temporales (nunca el proveedor
InMemory de EF Core)

**Target Platform**: escritorio Windows 10+ x64 y Linux x64 (X11 o Wayland)

**Project Type**: aplicación de escritorio (desktop-app) con arquitectura por capas

**Performance Goals**: búsqueda con 10,000 productos en menos de 1 s; un alta visible en el
listado en menos de 1 s; arranque en menos de 5 s sin migraciones pendientes (SC-011 y SC-012)

**Constraints**: 100 % offline; la UI nunca se bloquea (toda E/S es asíncrona); 0 advertencias de
compilación; respaldo al cerrar limitado a 15 s; ningún error inesperado cierra la aplicación

**Scale/Scope**: un solo puesto por instalación; hasta unos 10,000 productos; 3 pantallas
(Productos, Editor y Acerca de) más los diálogos de arranque

No quedan puntos marcados como NEEDS CLARIFICATION. Hay una verificación pendiente para la
implementación: el nombre exacto del evento de excepciones no controladas del `Dispatcher` en
Avalonia 12 (research §16).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitución v1.1.0.

| Principio o regla | Cómo lo cumple el diseño | Antes | Después del diseño |
|---|---|---|---|
| I. Venta sin conexión | No hay ninguna dependencia de red; todo es local (SC-003) | ✅ | ✅ |
| I. Transacción única | Cada caso de uso hace un solo `SaveChangesAsync` (atómico) | ✅ | ✅ |
| I. Errores no cierran la app | `OperationRunner` en cada comando, más manejadores globales (research §16) | ✅ | ✅ |
| I. WAL y respaldos | WAL al arrancar; respaldos automáticos y previos a migrar (research §13 y §15) | ✅ | ✅ |
| II. Capas y dependencias | 4 proyectos; puertos en Application; la raíz de composición es `Pos.Desktop.Composition` | ✅ | ✅ |
| II. Pruebas de arquitectura | `Pos.ArchitectureTests` con NetArchTest (research §20) | ✅ | ✅ |
| II. Organización por funcionalidad | `Products/CreateProduct/…`, `Startup/`, `Diagnostics/` | ✅ | ✅ |
| III. Lógica en el núcleo | Validación y reglas en Application y Domain; los ViewModels solo invocan casos de uso; `Result` explícito | ✅ | ✅ |
| IV. GUID v7, UTC, auditoría, versión | `Guid.CreateVersion7`, `IClock`, `AuditingInterceptor` y `Version` (data-model) | ✅ | ✅ |
| IV. Sin borrado físico | `DeletedAt`; el repositorio no expone un borrado físico | ✅ | ✅ |
| IV. Dinero en centavos | Value object `Money` con `long Cents` e `INTEGER` en la base | ✅ | ✅ |
| IV. Code First y orden de migración | Secuencia de arranque en [contracts/startup.md](contracts/startup.md), que sigue el orden obligatorio | ✅ | ✅ |
| IV. Migraciones inmutables y SQL revisado | Procedimiento en `docs/migraciones.md`; script idempotente | ✅ | ✅ |
| IV. Bases de ejemplo por versión | `SampleDatabases/v0.1.0.db` y `SampleDatabaseUpgradeTests` | ✅ | ✅ |
| IV. `HasData` y asistente | No aplica: no hay catálogos fijos ni datos propios de la instalación en esta funcionalidad | ✅ (N/A) | ✅ (N/A) |
| V. Multiplataforma | `Path.Combine` y `SpecialFolder`; bloqueo de archivo y named pipe multiplataforma; CI en ambos sistemas | ✅ | ✅ |
| V. Interfaces de hardware | Fuera de alcance; no se crean interfaces especulativas (Principio VII) | ✅ (N/A) | ✅ (N/A) |
| VI. Build y test sin advertencias | `Directory.Build.props` con `TreatWarningsAsErrors` | ✅ | ✅ |
| VI. Pruebas por caso de uso y regla | Suites por capa (quickstart §1) | ✅ | ✅ |
| VI. SQLite real, nunca InMemory | Pruebas de Infrastructure con archivos temporales | ✅ | ✅ |
| VII. YAGNI | Sin MediatR, sin repositorios genéricos, sin API, sin sincronización; cada paquete está justificado (research, tabla final) | ✅ | ✅ |
| VIII. Serilog, contexto y exportación | Archivos rotativos en CLEF, `LogContext` por operación, pantalla Acerca de y exportación | ✅ | ✅ |
| IX. Seguridad local | No hay contraseñas ni operaciones sensibles en esta funcionalidad; no hay secretos en el repositorio | ✅ (N/A parcial) | ✅ |
| Restricciones técnicas | Stack exactamente como lo fija la constitución, más `Directory.Packages.props` | ✅ | ✅ |
| Flujo de desarrollo | Especificación, clarificación y plan (este documento); tareas a continuación; documentación incluida en la entrega | ✅ | ✅ |

**Resultado**: no hay violaciones. La tabla de Complexity Tracking queda vacía.

## Project Structure

### Documentation (this feature)

```text
specs/001-pos-foundation/
├── plan.md              # Este archivo
├── research.md          # Fase 0: decisiones técnicas
├── data-model.md        # Fase 1: Product, Money, respaldos
├── quickstart.md        # Fase 1: guía de validación
├── contracts/
│   ├── use-cases.md     # Casos de uso, errores y DTOs
│   ├── ports.md         # Interfaces de Application
│   ├── startup.md       # Secuencia de arranque y mensajes
│   └── ui.md            # Pantallas y comportamiento
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
Pos.slnx
global.json
Directory.Build.props
Directory.Packages.props
.editorconfig
.config/dotnet-tools.json            # dotnet-ef
.github/workflows/ci.yml             # matriz ubuntu-latest / windows-latest
README.md
docs/
├── carpeta-de-datos.md
├── migraciones.md
└── agregar-funcionalidad.md

src/
├── Pos.Domain/
│   ├── Common/          # Money, DomainException, TextNormalizer
│   └── Products/        # Product
├── Pos.Application/
│   ├── Abstractions/    # IClock, ICurrentUser, IAppPaths, IAppInfo, Result, Error
│   ├── Products/
│   │   ├── ProductRules.cs, IProductRepository.cs, ProductDto.cs
│   │   ├── CreateProduct/   # Command, Validator, Handler
│   │   ├── UpdateProduct/
│   │   ├── DeleteProduct/
│   │   ├── GetProduct/
│   │   └── SearchProducts/
│   ├── Startup/         # DatabaseStartup, IDatabaseMaintenance, IBackupService, StartupResult
│   └── Diagnostics/     # ExportDiagnostics, GetAppInfo, IDiagnosticsExporter
├── Pos.Infrastructure/
│   ├── Persistence/     # PosDbContext, configuraciones, AuditingInterceptor, factory, Migrations/
│   ├── Products/        # ProductRepository
│   ├── Startup/         # SqliteDatabaseMaintenance, SqliteBackupService, SingleInstanceGuard
│   ├── Diagnostics/     # ZipDiagnosticsExporter, AssemblyAppInfo
│   └── Platform/        # AppPaths, SystemClock, SystemCurrentUser
└── Pos.Desktop/
    ├── Composition/     # Program, host, registro de dependencias (única zona que ve Infrastructure)
    ├── Shell/           # MainWindow, MainViewModel, navegación
    ├── Products/        # ProductsView/VM, ProductEditorView/VM
    ├── About/           # AboutView/VM
    ├── Startup/         # traducción de StartupResult a diálogos
    ├── Common/          # OperationRunner, IDialogService, MoneyConverter
    └── Resources/       # Strings.resx (es)

tests/
├── Pos.Domain.Tests/
├── Pos.Application.Tests/
├── Pos.Infrastructure.Tests/
│   └── SampleDatabases/ # v0.1.0.db
├── Pos.Desktop.Tests/
└── Pos.ArchitectureTests/
```

**Structure Decision**: una sola solución de escritorio, con cuatro proyectos de producción que
siguen el Principio II y un proyecto de pruebas por capa, más el de arquitectura. Dentro de cada
proyecto, el código se organiza por funcionalidad (`Products/`, `Startup/`, `Diagnostics/`). No
hay proyectos web ni de API (Principio VII).

## Fases siguientes

- **Fase 2** (`/speckit-tasks`): tareas ordenadas por historia (H1 y H2/H3 en P1 primero), con TDD
  en Domain y Application.
- **Orden sugerido**:
  1. Andamiaje de la solución, CI y pruebas de arquitectura.
  2. `Money` y `Product`.
  3. Persistencia y la primera migración.
  4. Secuencia de arranque.
  5. Casos de uso de Productos.
  6. UI.
  7. Diagnóstico.
  8. Base de ejemplo `v0.1.0` y documentación.

## Complexity Tracking

Sin violaciones de la constitución; no aplica.
