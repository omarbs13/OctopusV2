---

description: "Lista de tareas para implementar la Fundación del POS"
---

# Tasks: Fundación del POS con flujo de referencia de Productos

**Input**: documentos de diseño en `specs/001-pos-foundation/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: sí se incluyen. Los exigen la especificación (criterios SC-001 a SC-009) y la
constitución (Principio VI: TDD en Domain y Application; toda regla y todo caso de uso con
pruebas). Las pruebas de cada historia se escriben primero y deben fallar antes de implementar.

**Organization**: las tareas se agrupan por historia de usuario. US1 = H1 (arranque), US2 = H2
(registrar), US3 = H3 (buscar), US4 = H4 (editar), US5 = H5 (borrar), US6 = H6 (diagnóstico).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: historia a la que pertenece la tarea (US1…US6)
- Todas las rutas son relativas a la raíz del repositorio

## Path Conventions

- Producción: `src/Pos.Domain/`, `src/Pos.Application/`, `src/Pos.Infrastructure/`,
  `src/Pos.Desktop/`
- Pruebas: `tests/Pos.<Proyecto>.Tests/` y `tests/Pos.ArchitectureTests/`
- Dentro de cada proyecto, el código se organiza por funcionalidad (`Products/`, `Startup/`,
  `Diagnostics/`)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: crear la solución vacía, la configuración común de compilación y la CI.

- [X] T001 Crear `global.json` con el SDK `10.0.100` y `rollForward: latestFeature`, y `Pos.slnx` en la raíz con las carpetas de solución `src` y `tests`
- [X] T002 Crear `Directory.Build.props` en la raíz con `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `Version=0.1.0` y `InvariantGlobalization=false`
- [X] T003 Crear `Directory.Packages.props` en la raíz con `ManagePackageVersionsCentrally=true` y las versiones de: Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent y Avalonia.Fonts.Inter (12.1.x); CommunityToolkit.Mvvm (8.4.x); Microsoft.Extensions.Hosting y Microsoft.Extensions.Logging.Abstractions (10.0.x); Microsoft.EntityFrameworkCore.Sqlite y Microsoft.EntityFrameworkCore.Design (10.0.x); FluentValidation (12.x); Serilog (4.x); Serilog.Extensions.Hosting, Serilog.Sinks.File y Serilog.Formatting.Compact; xunit.v3; Microsoft.NET.Test.Sdk; NetArchTest.Rules (1.3.x)
- [X] T004 [P] Crear `.editorconfig` en la raíz con las convenciones de C# (namespaces con alcance de archivo, `var` cuando el tipo es evidente, campos privados `_camelCase`, severidad `warning` para las reglas de estilo)
- [X] T005 Crear los proyectos de producción y agregarlos a `Pos.slnx`: `src/Pos.Domain/Pos.Domain.csproj` (classlib, sin referencias), `src/Pos.Application/Pos.Application.csproj` (referencia Domain; paquetes FluentValidation y Microsoft.Extensions.Logging.Abstractions), `src/Pos.Infrastructure/Pos.Infrastructure.csproj` (referencia Application y Domain; paquetes EF Core Sqlite, y Design con `PrivateAssets=all`) y `src/Pos.Desktop/Pos.Desktop.csproj` (WinExe; referencia Application e Infrastructure; paquetes Avalonia, CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting y los de Serilog; `AvaloniaUseCompiledBindingsByDefault=true`)
- [X] T006 Crear los proyectos de prueba y agregarlos a `Pos.slnx`: `tests/Pos.Domain.Tests`, `tests/Pos.Application.Tests`, `tests/Pos.Infrastructure.Tests`, `tests/Pos.Desktop.Tests` y `tests/Pos.ArchitectureTests`, todos con xunit.v3 y Microsoft.NET.Test.Sdk. Cada uno referencia su proyecto de producción; ArchitectureTests referencia los cuatro y NetArchTest.Rules
- [X] T007 [P] Crear `.config/dotnet-tools.json` con la herramienta local `dotnet-ef` 10.0.x
- [X] T008 [P] Crear `.github/workflows/ci.yml` con una matriz `ubuntu-latest` y `windows-latest`: `actions/setup-dotnet` con `global-json-file: global.json`, `dotnet tool restore`, `dotnet restore`, `dotnet build --no-restore` y `dotnet test --no-build`, y resultados de pruebas como artefacto
- [X] T009 Actualizar `.gitignore` en la raíz para ignorar `bin/`, `obj/`, `TestResults/`, `*.db-wal` y `*.db-shm`, pero **no** `tests/Pos.Infrastructure.Tests/SampleDatabases/*.db`. Verificar que `dotnet build` y `dotnet test` en la raíz terminan con 0 advertencias

**Checkpoint**: la solución vacía compila y prueba desde la raíz en Windows y Linux.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: dominio de Producto, persistencia, reglas de arquitectura, logging y el shell de la
UI, que todas las historias necesitan.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase.

### Pruebas fundacionales (escribir primero; deben fallar)

- [X] T010 [P] Escribir las pruebas de arquitectura en `tests/Pos.ArchitectureTests/LayerDependencyTests.cs` con NetArchTest (research §20): Domain no depende de `Pos.Application`, `Pos.Infrastructure`, `Pos.Desktop`, `Microsoft.EntityFrameworkCore`, `Avalonia` ni `Serilog`; Application no depende de `Pos.Infrastructure`, `Pos.Desktop`, `Microsoft.EntityFrameworkCore` ni `Avalonia`; Infrastructure no depende de `Pos.Desktop`; los tipos de `Pos.Desktop` fuera del namespace `Pos.Desktop.Composition` no dependen de `Pos.Infrastructure`, `Microsoft.EntityFrameworkCore` ni `Microsoft.Data.Sqlite`
- [X] T011 [P] Escribir `tests/Pos.ArchitectureTests/ProjectReferenceTests.cs`, que lee los `.csproj` de `src/` y verifica las referencias directas permitidas: Domain ninguna; Application solo Domain; Infrastructure Application y Domain; Desktop Application e Infrastructure
- [X] T012 [P] Escribir `tests/Pos.Domain.Tests/Common/MoneyTests.cs`. `Money.TryParse` acepta solo `^\d{1,6}(\.\d{1,2})?$` después de recortar los extremos: `"0"`, `"89.5"` (8950 centavos), `"999999.99"` y `"  12.30 "` son válidos; `"1,234.50"`, `"12,50"`, `"12.345"`, `"-1"`, `"$10"`, `"1e3"`, `"1000000"`, `""` y `"12. 5"` se rechazan; nunca redondea; hay igualdad por valor; `FromCents` rechaza valores fuera de 0 a 99,999,999
- [X] T013 [P] Escribir `tests/Pos.Domain.Tests/Common/TextNormalizerTests.cs`: `"Café Molido"` → `"cafe molido"`; `"ÑANDÚ"` → `"nandu"`; `"Crème brûlée"` → `"creme brulee"`; los caracteres especiales no alfabéticos se conservan
- [X] T014 [P] Escribir `tests/Pos.Domain.Tests/Products/ProductTests.cs` para `Product.Create`, `Update` y `Delete`:
  - Nombre obligatorio, recortado, 1 a 200 caracteres (201 → `DomainException`).
  - SKU obligatorio, 1 a 50 caracteres, sin espacios, guardado en mayúsculas invariantes.
  - Código de barras opcional; si existe, `^\d{8,14}$` (7 y 15 dígitos se rechazan; una cadena vacía queda como `null`).
  - Un producto nuevo es activo, con `Version = 1` y `Id` GUID v7.
  - `NameSearch` se recalcula al cambiar el nombre.
  - `Delete` asigna `DeletedAt` y es idempotente.

### Implementación fundacional

- [X] T015 [P] Implementar `src/Pos.Domain/Common/DomainException.cs` y `src/Pos.Domain/Common/TextNormalizer.cs` (`FormD`, quitar `UnicodeCategory.NonSpacingMark`, `ToLowerInvariant`)
- [X] T016 [P] Implementar el value object `src/Pos.Domain/Common/Money.cs` como `readonly record struct` con `long Cents`, `FromCents(long)` (0 a 99,999,999) y `TryParse(string, out Money)` con la expresión regular `^\d{1,6}(\.\d{1,2})?$`, sin redondeo
- [X] T017 Implementar `src/Pos.Domain/Products/Product.cs` según [data-model.md](data-model.md): propiedades `Id`, `Name`, `NameSearch`, `Sku`, `Barcode`, `Price`, `IsActive`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt` y `Version`, con setters privados; `Create`, `Update`, `Delete(DateTime utcNow)` e invariantes. Depende de T015 y T016
- [X] T018 [P] Implementar en `src/Pos.Application/Abstractions/`: `Result.cs` (`Result` y `Result<T>`), `Error.cs` (`ValidationFailed(IReadOnlyList<FieldError>)`, `Duplicate(string Field)`, `NotFound`, `Conflict` y `ExportFailed(string Message)`) y `FieldError.cs`, según [contracts/use-cases.md](contracts/use-cases.md)
- [X] T019 [P] Implementar los puertos en `src/Pos.Application/Abstractions/`: `IClock.cs` (`DateTime UtcNow`), `ICurrentUser.cs` (`Guid UserId`), `IAppPaths.cs` (`DataDirectory`, `DatabaseFile`, `AutoBackupsDirectory`, `PreMigrationBackupsDirectory`, `CorruptDirectory`, `LogsDirectory`, `LockFile`) e `IAppInfo.cs` (`Version`, `OperatingSystem`)
- [X] T020 [P] Implementar en `src/Pos.Infrastructure/Platform/`: `SystemClock.cs`; `SystemCurrentUser.cs` (GUID fijo `00000000-0000-7000-8000-000000000001`); y `AppPaths.cs`, cuya raíz es `Environment.SpecialFolder.LocalApplicationData/Pos`, se puede sobrescribir con la variable `POS_DATA_DIR`, crea las carpetas `data/`, `backups/auto/`, `backups/pre-migration/`, `backups/corrupt/` y `logs/` con `Path.Combine`, y usa `app.lock` como archivo de bloqueo
- [X] T021 Implementar `src/Pos.Infrastructure/Persistence/PosDbContext.cs` y `src/Pos.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`:
  - Tabla `Products`.
  - `Name` `HasMaxLength(200)`, `NameSearch` `HasMaxLength(200)`, `Sku` `HasMaxLength(50)` y `Barcode` `HasMaxLength(14)`, todos `NOT NULL` salvo `Barcode`.
  - `Price` mapeado a `PriceCents INTEGER` con un convertidor.
  - `Version` con `IsConcurrencyToken()`.
  - Convertidor de `DateTime` que fuerza `DateTimeKind.Utc`.
  - Índices: `IX_Products_Sku` único con filtro `"DeletedAt" IS NULL`; `IX_Products_Barcode` único con filtro `"DeletedAt" IS NULL AND "Barcode" IS NOT NULL`; `IX_Products_NameSearch` con filtro `"DeletedAt" IS NULL`.
- [X] T022 Implementar `src/Pos.Infrastructure/Persistence/AuditingInterceptor.cs` (`SaveChangesInterceptor`): en las entidades `Added` asigna `CreatedAt`/`CreatedBy` y `UpdatedAt`/`UpdatedBy` desde `IClock` e `ICurrentUser`; en las `Modified` asigna `UpdatedAt`/`UpdatedBy` y hace `Version += 1`
- [X] T023 Implementar `src/Pos.Infrastructure/Persistence/PosDbContextFactory.cs` (`IDesignTimeDbContextFactory`) y `src/Pos.Infrastructure/DependencyInjection.cs` (`AddInfrastructure(IServiceCollection)`), que registra `PosDbContext` y `IDbContextFactory<PosDbContext>` (con `AddDbContextFactory`, que necesita T043) con `Data Source={IAppPaths.DatabaseFile}` y el interceptor, además de `SystemClock`, `SystemCurrentUser` y `AppPaths`
- [X] T024 Crear `src/Pos.Application/DependencyInjection.cs` con `AddApplication(IServiceCollection)` vacío (lo irán completando las historias) e invocarlo desde `src/Pos.Desktop/Composition/HostBuilder.cs`
- [X] T025 Generar la migración inicial con `dotnet ef migrations add InitialCreate --project src/Pos.Infrastructure --output-dir Persistence/Migrations`, revisar el SQL con `dotnet ef migrations script` (SQLite no admite `--idempotent`) y confirmar que se crean la tabla `Products` y los tres índices parciales, en `src/Pos.Infrastructure/Persistence/Migrations/`
- [X] T026 [P] Crear `tests/Pos.Infrastructure.Tests/TestSupport/TempDataDirectory.cs` (carpeta temporal única que se elimina al hacer `Dispose` y expone un `IAppPaths`) y `tests/Pos.Infrastructure.Tests/TestSupport/TestDb.cs` (crea un `PosDbContext` sobre un archivo SQLite temporal, con reloj y usuario de prueba, y aplica las migraciones)
- [X] T027 [P] Crear en `tests/Pos.Infrastructure.Tests/TestSupport/MigrationScenarios/` un `ScenarioDbContext` (hereda de `PosDbContext` y usa `MigrationsAssembly` del proyecto de pruebas) con tres migraciones escritas a mano: `S1_Initial` (tabla `Products` igual a `InitialCreate`), `S2_AddColumn` (agrega la columna `Notes TEXT NULL`) y `S2_Failing` (ejecuta un SQL válido y luego `migrationBuilder.Sql("SELECT RAISE(ABORT, 'fallo simulado')")`). Agregar la fábrica `ScenarioMaintenance.Create(paths, targetMigration)` que construye un `SqliteDatabaseMaintenance` limitado a la migración objetivo
- [X] T028 [P] Implementar `src/Pos.Desktop/Common/IDialogService.cs` y `src/Pos.Desktop/Common/DialogService.cs`, con los métodos `ShowMessageAsync`, `ConfirmAsync` (Cancelar como opción predeterminada) y `PickSaveFileAsync(suggestedName, extension)` usando el `StorageProvider` de Avalonia
- [X] T029 [P] Crear `src/Pos.Desktop/Resources/Strings.resx` (español) con los textos comunes, incluido el error genérico: "Ocurrió un error inesperado. La información quedó registrada para soporte. Puede intentar de nuevo."
- [X] T030 Escribir `tests/Pos.Desktop.Tests/Common/OperationRunnerTests.cs`: cuando la operación lanza una excepción, se registra con las propiedades `Operation`, `UserId` (de `ICurrentUser`) y los identificadores recibidos, se muestra el mensaje genérico por medio de `IDialogService` y la excepción no se propaga
- [X] T031 Implementar `src/Pos.Desktop/Common/OperationRunner.cs` con `RunAsync(string operation, IReadOnlyDictionary<string, object?> context, Func<Task> action)`, que agrega `Operation`, `UserId` (`ICurrentUser.UserId`) y las propiedades de `context` al `LogContext` de Serilog, captura, registra y muestra el mensaje genérico (research §16). Depende de T028 a T030
- [X] T032 Implementar `src/Pos.Desktop/Composition/Program.cs`, `App.axaml` y `App.axaml.cs`, y `src/Pos.Desktop/Composition/HostBuilder.cs`:
  - Serilog escribe en `{LogsDirectory}/pos-.log` con `RollingInterval.Day`, `retainedFileCountLimit: 31`, `shared: true` y `CompactJsonFormatter`, enriquecido con la versión, la máquina y el sistema operativo.
  - `Microsoft.Extensions.Hosting` registra `AddInfrastructure`, los casos de uso, los ViewModels, `DialogService` y `OperationRunner`.
  - Manejadores globales: el de excepciones no controladas del `Dispatcher` de Avalonia 12 (**verificar el nombre del evento en la documentación de Avalonia 12**), `TaskScheduler.UnobservedTaskException` y `AppDomain.CurrentDomain.UnhandledException`.
- [X] T033 Implementar el shell en `src/Pos.Desktop/Shell/MainWindow.axaml(.cs)` y `src/Pos.Desktop/Shell/MainViewModel.cs`: navegación lateral (Productos y Acerca de) por ViewModel actual; título con la versión; atajos Ctrl+N y Ctrl+F reservados para la pantalla de Productos ([contracts/ui.md](contracts/ui.md))
- [X] T034 Ejecutar `dotnet build` y `dotnet test` desde la raíz: las pruebas T010 a T014 y T030 pasan con 0 advertencias

**Checkpoint**: el dominio, la persistencia, el logging y el shell están listos; empiezan las
historias de usuario.

---

## Phase 3: User Story 1 - Arranque confiable de la aplicación (Priority: P1) 🎯 MVP

**Goal**: la aplicación abre de forma segura: crea la base, migra con respaldo previo, impide una
segunda instancia, rechaza una base más nueva, restaura si la migración falla, ofrece restaurar
una base dañada y hace respaldos automáticos (FR-001 a FR-008a).

**Independent Test**: se provocan los escenarios de [contracts/startup.md](contracts/startup.md)
(base nueva, migración pendiente, base más nueva, falla de migración, base dañada, segunda
instancia) y se verifica cada resultado sin usar ninguna otra historia.

### Tests for User Story 1 ⚠️

> Escribir primero; deben fallar antes de implementar.

- [X] T035 [P] [US1] Escribir `tests/Pos.Application.Tests/Startup/DatabaseStartupTests.cs` con dobles de `IDatabaseMaintenance`, `IBackupService` e `IClock`. Cubre cada rama de [contracts/startup.md](contracts/startup.md) y el orden exacto de las llamadas:
  - Base inexistente → `MigrateAsync` → `EnableWal` → `Ready`.
  - `Corrupted` → `Corrupted(latestAutoBackup)`.
  - `Inaccessible(Locked | PermissionDenied)`.
  - `Newer` → `NewerDatabase` sin ninguna llamada de escritura.
  - `Pending` con poco espacio → `InsufficientSpace` sin respaldo ni migración.
  - `Pending` → `CreateAsync(PreMigration)` **antes** de `MigrateAsync`.
  - Falla de migración → `RestoreAsync(respaldo pre-migración)` → `MigrationFailed`.
  - Respaldo automático solo si el último tiene más de 24 h; si falla, no bloquea el `Ready`.
- [X] T036 [P] [US1] Escribir `tests/Pos.Infrastructure.Tests/Startup/SqliteDatabaseMaintenanceTests.cs` con SQLite real en `TempDataDirectory`:
  - Base inexistente → `DatabaseExists() == false`.
  - Después de migrar, `UpToDate`.
  - Un registro falso `99990101000000_Future` en `__EFMigrationsHistory` → `Newer`.
  - Cabecera del archivo sobrescrita → `Corrupted`.
  - `EnableWalAsync` deja `PRAGMA journal_mode` en `wal`.
- [X] T037 [P] [US1] Escribir `tests/Pos.Infrastructure.Tests/Startup/SqliteBackupServiceTests.cs`:
  - `CreateAsync(Automatic)` produce `backups/auto/pos-<yyyyMMdd-HHmmss>Z.db`, que se abre con `quick_check = ok` y contiene los mismos productos.
  - La retención conserva 7 automáticos y 5 pre-migración.
  - No quedan archivos `.tmp` después de un error.
  - `RestoreAsync` recupera los datos exactos y elimina los `-wal` y `-shm` residuales.
  - `QuarantineCurrentDatabaseAsync` mueve `pos.db`, `-wal` y `-shm` a `backups/corrupt/<fecha>Z/`.
- [X] T038 [P] [US1] Escribir `tests/Pos.Infrastructure.Tests/Startup/StartupIntegrationTests.cs`, que ejecuta `DatabaseStartup` con los adaptadores reales y cubre SC-006:
  - Base nueva → `Ready` y archivo creado.
  - Migración pendiente: base creada con `S1_Initial` y 3 productos, luego arranque con destino `S2_AddColumn` → existe un respaldo en `backups/pre-migration/`, `Ready` y los 3 productos intactos.
  - Base más nueva → `NewerDatabase` y el archivo queda byte a byte igual.
  - Falla de migración: base con `S1_Initial` y 3 productos, luego arranque con destino `S2_Failing` → `MigrationFailed`, sin la columna `Notes` y con los 3 productos idénticos (comparados byte a byte con el respaldo).
  - Base dañada con respaldo → restauración confirmada, luego `Ready` con los datos del respaldo.
- [X] T039 [P] [US1] Escribir `tests/Pos.Infrastructure.Tests/Startup/SingleInstanceGuardTests.cs`: la primera instancia obtiene el bloqueo; una segunda en el mismo proceso de prueba no lo obtiene y su mensaje `activate` llega a la primera; al hacer `Dispose` se libera el bloqueo
- [X] T040 [P] [US1] Escribir `tests/Pos.Desktop.Tests/Startup/StartupPresenterTests.cs`: cada `StartupResult` produce el mensaje y la acción de la tabla de [contracts/startup.md](contracts/startup.md). Con `Corrupted(backup)` y confirmación se invocan `QuarantineCurrentDatabaseAsync`, `RestoreAsync` y un nuevo `RunAsync`; sin confirmación, salida

### Implementation for User Story 1

- [X] T041 [P] [US1] Definir en `src/Pos.Application/Startup/`: `IDatabaseMaintenance.cs` (`DatabaseExists`, `CheckIntegrityAsync` → `IntegrityStatus` Ok, Corrupted o `Inaccessible(Locked | PermissionDenied)`, `GetMigrationStateAsync` → `UpToDate`, `Pending(names)` o `Newer(unknownNames)`, `MigrateAsync`, `EnableWalAsync`, `DatabaseSizeBytes`, `AvailableFreeSpaceBytes`), `IBackupService.cs` (`CreateAsync(BackupKind)`, `GetLatestAsync(BackupKind)`, `RestoreAsync(BackupInfo)`, `QuarantineCurrentDatabaseAsync`, `CreateTemporaryCopyAsync`), `BackupKind.cs` (`Automatic`, `PreMigration`), `BackupInfo.cs` (`Path`, `Kind`, `CreatedAtUtc`) y `StartupResult.cs` (`Ready`, `NewerDatabase`, `MigrationFailed`, `InsufficientSpace`, `Inaccessible(reason)`, `Corrupted(BackupInfo?)`), según [contracts/ports.md](contracts/ports.md)
- [X] T042 [US1] Implementar `src/Pos.Application/Startup/DatabaseStartup.cs` con `RunAsync(CancellationToken)`, siguiendo los pasos 1 a 7 de [contracts/startup.md](contracts/startup.md). Exige un espacio libre de al menos `2 × DatabaseSizeBytes()` y respalda automáticamente solo si el último tiene más de 24 h. También implementar `BackupOnCloseAsync` con un límite de 15 s que registra y nunca lanza. Depende de T041
- [X] T043 [P] [US1] Implementar `src/Pos.Infrastructure/Startup/SqliteDatabaseMaintenance.cs`, que recibe `IDbContextFactory<PosDbContext>` para poder usar el `ScenarioDbContext` en las pruebas: `PRAGMA quick_check`; traducir `SqliteException` con los códigos 11 (`SQLITE_CORRUPT`) y 26 (`SQLITE_NOTADB`) a `Corrupted`, 5 y 6 (`BUSY` y `LOCKED`) a `Locked`, y 8 (`READONLY`) o `UnauthorizedAccessException` a `PermissionDenied`; estado de migraciones con `GetAppliedMigrationsAsync` y `GetMigrations`; `MigrateAsync`; `PRAGMA journal_mode=WAL`; y espacio libre con `DriveInfo`
- [X] T044 [P] [US1] Implementar `src/Pos.Infrastructure/Startup/SqliteBackupService.cs` (research §14 y §15):
  - `SqliteConnection.BackupDatabase` hacia `pos-<yyyyMMdd-HHmmss>Z.db.tmp`, y luego renombrar.
  - Retención de 7 (`Automatic`) y 5 (`PreMigration`).
  - La fecha se obtiene del nombre del archivo.
  - `RestoreAsync` hace `SqliteConnection.ClearAllPools()`, restaura con la API de backup desde el respaldo y elimina `-wal` y `-shm`.
  - `QuarantineCurrentDatabaseAsync`.
  - `CreateTemporaryCopyAsync` a la carpeta temporal del sistema.
- [X] T045 [P] [US1] Implementar `src/Pos.Infrastructure/Startup/SingleInstanceGuard.cs` (research §12): `FileStream` sobre `app.lock` con `FileShare.None`, mantenido durante toda la vida del proceso; un `NamedPipeServerStream` con nombre derivado de `Environment.UserName` y `"Pos"` que escucha `activate` y expone el evento `ActivationRequested`; y `TrySignalExisting()` para la segunda instancia
- [X] T046 [US1] Registrar `DatabaseStartup`, `SqliteDatabaseMaintenance` y `SqliteBackupService` en `src/Pos.Infrastructure/DependencyInjection.cs` y en `src/Pos.Application/DependencyInjection.cs`
- [X] T047 [US1] Implementar `src/Pos.Desktop/Startup/StartupPresenter.cs`, que traduce `StartupResult` a los diálogos de [contracts/startup.md](contracts/startup.md) (textos en `Strings.resx`, fecha del respaldo en hora local), incluida la restauración confirmada de una base dañada y el nuevo intento
- [X] T048 [US1] Integrar en `src/Pos.Desktop/Composition/Program.cs` y `App.axaml.cs` el siguiente orden: `SingleInstanceGuard` (si no se obtiene el bloqueo: señal `activate`, aviso y salida) → Serilog y host → `DatabaseStartup.RunAsync` y `StartupPresenter` → `MainWindow`. `ActivationRequested` restaura y activa la ventana en el hilo de UI, y el evento `Closing` de la ventana invoca `BackupOnCloseAsync`
- [X] T049 [US1] Registrar en el log (Serilog) cada paso del arranque con su resultado y su duración; el detalle técnico de cada error se escribe antes de mostrar el mensaje, en `src/Pos.Application/Startup/DatabaseStartup.cs`

**Checkpoint**: la aplicación arranca de forma segura en todos los escenarios; T035 a T040 pasan.

---

## Phase 4: User Story 2 - Registrar productos (Priority: P1)

**Goal**: el operador da de alta productos válidos, que aparecen de inmediato, con errores por
campo y sin duplicados por doble clic (FR-009 a FR-015, FR-023 a FR-025).

**Independent Test**: se registra un producto válido y aparece en la lista y persiste tras
reiniciar; los datos inválidos muestran errores por campo sin perder lo capturado; un doble clic
registra un solo producto.

### Tests for User Story 2 ⚠️

- [X] T050 [P] [US2] Escribir `tests/Pos.Application.Tests/Products/CreateProductValidatorTests.cs`, con un error por campo (`Name`, `Sku`, `Barcode`, `Price`) y mensajes en español:
  - Nombre vacío o de 201 caracteres.
  - SKU vacío, de 51 caracteres o con espacios.
  - Código de barras con letras, de 7 o de 15 dígitos.
  - Precio `"1,234.50"`, `"12.345"`, `"-1"` o `"1000000"`.
- [X] T051 [P] [US2] Escribir `tests/Pos.Application.Tests/Products/CreateProductHandlerTests.cs` con un doble de `IProductRepository`:
  - Caso válido → `Result<ProductDto>` con SKU en mayúsculas, nombre recortado, `IsActive = true` y `Version = 1`.
  - SKU existente (sin distinguir mayúsculas) → `Duplicate("Sku")`.
  - Código de barras existente → `Duplicate("Barcode")`.
  - `SaveOutcome.Duplicate` en una carrera → `Duplicate`.
- [X] T052 [P] [US2] Escribir `tests/Pos.Infrastructure.Tests/Products/ProductRepositoryCreateTests.cs` con SQLite real:
  - Alta y lectura con auditoría (`CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` del reloj y usuario de prueba, `DateTimeKind.Utc`).
  - `PriceCents` guardado como entero.
  - Insertar directamente un SKU duplicado entre no borrados → `SaveOutcome.Duplicate("Sku")`.
  - Nombre con acentos y caracteres especiales (`"Jalapeño «Extra» 100% & más"`) se guarda y se lee sin alteraciones.
- [X] T053 [P] [US2] Escribir `tests/Pos.Desktop.Tests/Products/ProductEditorViewModelCreateTests.cs`:
  - Un guardado válido invoca el caso de uso una vez y cierra el editor.
  - Dos ejecuciones inmediatas de `SaveCommand` (doble clic) invocan el caso de uso **una sola vez**.
  - Con `ValidationFailed`, cada mensaje llega a su propiedad de error y los campos conservan su valor.
  - Con `Duplicate("Sku")`, aparece el mensaje "Ya existe un producto con este SKU." en el SKU.

### Implementation for User Story 2

- [X] T054 [P] [US2] Implementar `src/Pos.Application/Products/ProductDto.cs`, `ProductListItemDto.cs` y `ProductMapping.cs` según [contracts/use-cases.md](contracts/use-cases.md) (el precio en `long PriceCents`)
- [X] T055 [P] [US2] Implementar `src/Pos.Application/Products/IProductRepository.cs` (`GetAsync`, `SkuExistsAsync(sku, excludingId)`, `BarcodeExistsAsync(barcode, excludingId)`, `SearchAsync`, `Add` y `SaveChangesAsync(int? expectedVersion)` → `SaveOutcome` `Saved`, `Conflict` o `Duplicate(Field)`) y `SaveOutcome.cs`, según [contracts/ports.md](contracts/ports.md)
- [X] T056 [US2] Implementar `src/Pos.Application/Products/ProductRules.cs`, con reglas compartidas de FluentValidation y mensajes en español: nombre obligatorio, máximo 200 caracteres tras recortar; SKU obligatorio, máximo 50 caracteres, sin espacios; código de barras opcional con `^\d{8,14}$`; precio con `Money.TryParse`, mensaje "Capture el precio con dígitos y punto decimal, por ejemplo 1234.50"
- [X] T057 [US2] Implementar `src/Pos.Application/Products/CreateProduct/CreateProductCommand.cs`, `CreateProductValidator.cs` y `CreateProductHandler.cs`: normalizar, validar, verificar unicidad, `Product.Create`, `Add` y `SaveChangesAsync(null)`, y devolver `Result<ProductDto>`. Depende de T054 a T056
- [X] T058 [US2] Implementar `src/Pos.Infrastructure/Products/ProductRepository.cs` (parte de alta y lectura): `GetAsync` excluye borrados; `SkuExistsAsync` y `BarcodeExistsAsync` filtran por `DeletedAt == null` y `Id != excludingId`; `SaveChangesAsync` traduce `SqliteException` con `SqliteExtendedErrorCode == 2067` (`SQLITE_CONSTRAINT_UNIQUE`) a `Duplicate`, según el índice, y `DbUpdateConcurrencyException` a `Conflict`
- [X] T059 [US2] Registrar `ProductRepository` en `src/Pos.Infrastructure/DependencyInjection.cs`, y `CreateProductHandler` y los validadores en `src/Pos.Application/DependencyInjection.cs`
- [X] T060 [US2] Implementar `src/Pos.Desktop/Products/ProductEditorViewModel.cs` (modo alta): propiedades `Name`, `Sku`, `Barcode` y `PriceText` con sus errores por campo; `SaveCommand` como `AsyncRelayCommand` sin ejecuciones concurrentes y deshabilitado mientras `IsRunning`; invocación a través de `OperationRunner`; y evento `Saved(ProductDto)`
- [X] T061 [US2] Implementar `src/Pos.Desktop/Products/ProductEditorView.axaml(.cs)` según [contracts/ui.md](contracts/ui.md): campos con límites de 200 y 50 caracteres, error debajo de cada campo, foco en el primer campo con error, Enter guarda, Esc cancela y textos desde `Strings.resx`
- [X] T062 [US2] Implementar lo mínimo de `src/Pos.Desktop/Products/ProductsViewModel.cs` y `ProductsView.axaml(.cs)` para esta historia: botón "Nuevo producto" y Ctrl+N abren el editor; al guardar, la lista se refresca y selecciona el producto (la búsqueda completa llega en US3)
- [X] T063 [US2] Convertidor `src/Pos.Desktop/Common/MoneyConverter.cs`: centavos → `CultureInfo.GetCultureInfo("es-MX")` con `"C2"` (`$1,234.50`) para mostrar, y centavos → `"0.00"` con punto y sin separador de miles para editar

**Checkpoint**: registrar productos funciona de punta a punta; T050 a T053 pasan.

---

## Phase 5: User Story 3 - Consultar y buscar productos (Priority: P1)

**Goal**: listado de productos visibles y búsqueda sin mayúsculas ni acentos por nombre y SKU,
con código de barras exacto o parcial según la entrada, filtro de inactivos y precio en pesos
(FR-016 a FR-018).

**Independent Test**: con un catálogo de muestra se buscan fragmentos con y sin acentos, un SKU y
un código de barras completo y parcial; se verifican el filtro de inactivos, el mensaje de "sin
resultados" y el formato `$1,234.50`.

### Tests for User Story 3 ⚠️

- [X] T064 [P] [US3] Escribir `tests/Pos.Infrastructure.Tests/Products/ProductSearchTests.cs` con SQLite real:
  - `"cafe molido"` encuentra "Café Molido".
  - `"lec-001"` encuentra el SKU `LEC-001`.
  - `"7501234567890"` encuentra **solo** ese código entre `7501234567890` y `7501234567891`; `"7501"` encuentra ambos.
  - Los borrados nunca aparecen.
  - Los inactivos solo aparecen con `includeInactive`.
  - `"%"` y `"_"` se buscan literalmente.
  - El orden es por nombre normalizado.
  - Con más de 200 coincidencias devuelve 200 y `HasMore = true`.
- [X] T065 [P] [US3] Escribir `tests/Pos.Infrastructure.Tests/Products/ProductPerformanceTests.cs` (SC-011) con 10,000 productos sembrados: (a) `SearchAsync` sobre un nombre parcial responde en menos de 1 s; (b) `CreateProductHandler` seguido de `SearchProductsHandler` con el SKU recién creado encuentra el producto en menos de 1 s en total
- [X] T066 [P] [US3] Escribir `tests/Pos.Application.Tests/Products/SearchProductsHandlerTests.cs`: normaliza el texto (sin acentos, minúsculas); detecta un código de barras completo con `^\d{8,14}$`; un texto vacío devuelve todos los visibles
- [X] T067 [P] [US3] Escribir `tests/Pos.Desktop.Tests/Products/ProductsViewModelSearchTests.cs`: la búsqueda espera 250 ms tras la última tecla y Enter busca de inmediato; `IsEmpty` muestra "No se encontraron productos."; `HasMore` muestra el aviso de 200; el filtro "Mostrar inactivos" vuelve a buscar con `IncludeInactive = true`

### Implementation for User Story 3

- [X] T068 [US3] Implementar `src/Pos.Application/Products/SearchProducts/SearchProductsQuery.cs`, `SearchProductsResult.cs` y `SearchProductsHandler.cs` (límite 200, normalización con `TextNormalizer`)
- [X] T069 [US3] Implementar `SearchAsync` en `src/Pos.Infrastructure/Products/ProductRepository.cs`: `DeletedAt == null`; `IsActive` salvo que `includeInactive`; `EF.Functions.Like(NameSearch, pattern, "\\")` o `Like(Sku, patternUpper, "\\")`; código de barras por igualdad si `^\d{8,14}$` y con `Like` si no; escapar `%`, `_` y `\`; `OrderBy(NameSearch)`; `Take(limit + 1)` para calcular `HasMore`; proyección a `ProductListItemDto` sin seguimiento
- [X] T070 [US3] Completar `src/Pos.Desktop/Products/ProductsViewModel.cs`: `SearchText` con espera de 250 ms, `SearchNowCommand` (Enter), `IncludeInactive`, `Items`, `IsEmpty`, `HasMore` y `SelectedItem`; búsqueda inicial al abrir; todo a través de `OperationRunner`
- [X] T071 [US3] Completar `src/Pos.Desktop/Products/ProductsView.axaml(.cs)`: caja de búsqueda con foco inicial y Ctrl+F; casilla "Mostrar inactivos"; lista con las columnas Nombre, SKU, Código de barras, Precio (`MoneyConverter`) y Estado (visible solo con inactivos); textos de vacío y de `HasMore`
- [X] T072 [US3] Registrar `SearchProductsHandler` en `src/Pos.Application/DependencyInjection.cs`

**Checkpoint**: US1, US2 y US3 (el MVP P1 completo) funcionan; T064 a T067 pasan.

---

## Phase 6: User Story 4 - Editar productos (Priority: P2)

**Goal**: modificar productos con las mismas validaciones, marcar como inactivo y rechazar
guardados con versión desactualizada, ofreciendo recargar (FR-019 y FR-020).

**Independent Test**: se edita un producto y se ven los cambios; se simulan dos ediciones
concurrentes y la segunda se rechaza con la opción de recargar.

### Tests for User Story 4 ⚠️

- [X] T073 [P] [US4] Escribir `tests/Pos.Application.Tests/Products/UpdateProductHandlerTests.cs`:
  - Caso válido → `ProductDto` con `Version + 1`.
  - Mismas validaciones que el alta.
  - La unicidad excluye al propio producto (guardar sin cambiar el SKU es válido).
  - Un producto inexistente o borrado → `NotFound`.
  - `SaveOutcome.Conflict` → `Conflict`.
- [X] T074 [P] [US4] Escribir `tests/Pos.Infrastructure.Tests/Products/ProductConcurrencyTests.cs` (SC-005): dos contextos cargan la misma versión; el primero guarda; el segundo, con `expectedVersion` desactualizada, obtiene `SaveOutcome.Conflict` y la base conserva los datos del primero
- [X] T075 [P] [US4] Escribir `tests/Pos.Desktop.Tests/Products/ProductEditorViewModelEditTests.cs`:
  - Carga con `GetProduct`.
  - Con `Conflict` aparece el diálogo de recarga: si se acepta, los campos se reemplazan con los datos actuales y la nueva versión; si no, se conserva lo capturado.
  - Con `NotFound` aparece un mensaje, se cierra el editor y se refresca la lista.
  - Desmarcar "Activo" y guardar hace que el producto desaparezca de la lista por defecto.
- [X] T076 [P] [US4] Escribir `tests/Pos.Application.Tests/Products/GetProductHandlerTests.cs`: devuelve `ProductDto` con todos los campos y la `Version` actual; `NotFound` si el producto no existe; `NotFound` si está borrado

### Implementation for User Story 4

- [X] T077 [P] [US4] Implementar `src/Pos.Application/Products/GetProduct/GetProductQuery.cs` y `GetProductHandler.cs` (`NotFound` si no existe o está borrado)
- [X] T078 [US4] Implementar `src/Pos.Application/Products/UpdateProduct/UpdateProductCommand.cs` (`Id`, `ExpectedVersion`, `Name`, `Sku`, `Barcode`, `PriceText` e `IsActive`), `UpdateProductValidator.cs` (reutiliza `ProductRules`) y `UpdateProductHandler.cs` (`product.Update` y `SaveChangesAsync(ExpectedVersion)`)
- [X] T079 [US4] En `src/Pos.Infrastructure/Products/ProductRepository.cs`, `SaveChangesAsync(expectedVersion)` asigna `Entry(product).Property(p => p.Version).OriginalValue = expectedVersion` antes de guardar, para que EF detecte el conflicto
- [X] T080 [US4] Extender `src/Pos.Desktop/Products/ProductEditorViewModel.cs` con el modo edición: carga por `Id`, casilla `IsActive` visible solo al editar, `ExpectedVersion`, y manejo de `Conflict` (diálogo con recarga) y de `NotFound` según [contracts/ui.md](contracts/ui.md)
- [X] T081 [US4] En `src/Pos.Desktop/Products/ProductsView.axaml(.cs)` y `ProductsViewModel.cs`, agregar el botón "Editar" y el doble clic en la fila para abrir el editor en modo edición
- [X] T082 [US4] Registrar `GetProductHandler` y `UpdateProductHandler` en `src/Pos.Application/DependencyInjection.cs`

**Checkpoint**: editar funciona, incluido el conflicto de concurrencia; T073 a T076 pasan.

---

## Phase 7: User Story 5 - Borrar productos (Priority: P2)

**Goal**: borrado lógico con confirmación y reutilización del SKU y del código de barras (FR-021
y FR-022).

**Independent Test**: se borra un producto, desaparece de la lista y la búsqueda con su registro
conservado, y se registra otro con el mismo SKU y código de barras.

### Tests for User Story 5 ⚠️

- [X] T083 [P] [US5] Escribir `tests/Pos.Application.Tests/Products/DeleteProductHandlerTests.cs`: asigna `DeletedAt = IClock.UtcNow`; `NotFound` si no existe o ya está borrado; `Conflict` con versión desactualizada
- [X] T084 [P] [US5] Escribir `tests/Pos.Infrastructure.Tests/Products/ProductSoftDeleteTests.cs`: después de borrar, la fila sigue en `Products` con `DeletedAt` no nulo y `UpdatedBy` asignado; no aparece en `SearchAsync`; un producto nuevo con el mismo `Sku` y `Barcode` se guarda sin `Duplicate` gracias a los índices parciales
- [X] T085 [P] [US5] Escribir `tests/Pos.Desktop.Tests/Products/ProductsViewModelDeleteTests.cs`: el diálogo de confirmación lleva el nombre y el SKU; si se cancela, no se invoca el caso de uso; si se confirma, se invoca y se refresca la lista; con `Conflict` o `NotFound` aparece un mensaje y se refresca

### Implementation for User Story 5

- [X] T086 [US5] Implementar `src/Pos.Application/Products/DeleteProduct/DeleteProductCommand.cs` (`Id`, `ExpectedVersion`) y `DeleteProductHandler.cs` (`product.Delete(clock.UtcNow)` y `SaveChangesAsync(ExpectedVersion)`)
- [X] T087 [US5] En `src/Pos.Desktop/Products/ProductsViewModel.cs` y `ProductsView.axaml`, agregar `DeleteCommand` con `IDialogService.ConfirmAsync("¿Borrar el producto «{Nombre}» ({SKU})? Dejará de aparecer en el catálogo.")`, con Cancelar como opción predeterminada, a través de `OperationRunner`
- [X] T088 [US5] Registrar `DeleteProductHandler` en `src/Pos.Application/DependencyInjection.cs`

**Checkpoint**: el CRUD de Productos está completo; T083 a T085 pasan.

---

## Phase 8: User Story 6 - Diagnóstico para soporte (Priority: P3)

**Goal**: pantalla "Acerca de" con la versión y la carpeta de datos, y exportación de un zip con
los logs recientes y un respaldo consistente (FR-027 y FR-028).

**Independent Test**: se abre "Acerca de", se exporta el diagnóstico a una carpeta y se verifica
que el zip contiene `info.json`, `logs/` y un `pos.db` con `quick_check = ok`.

### Tests for User Story 6 ⚠️

- [X] T089 [P] [US6] Escribir `tests/Pos.Infrastructure.Tests/Diagnostics/ZipDiagnosticsExporterTests.cs` (SC-009):
  - El zip contiene `info.json` (`appVersion`, `os`, `dataDirectory`, `exportedAtUtc`), solo los logs de los últimos 7 días (se leen aunque el archivo esté abierto para escritura) y `pos.db`, que se abre con `quick_check = ok` y los productos esperados.
  - Si el destino no tiene permisos, devuelve `ExportFailed` y no deja ningún archivo en el destino ni en el temporal.
- [X] T090 [P] [US6] Escribir `tests/Pos.Desktop.Tests/About/AboutViewModelTests.cs`: muestra la versión, la carpeta de datos y el sistema operativo; al exportar, sugiere el nombre `pos-diagnostico-<yyyyMMdd-HHmm>.zip`, muestra un indicador mientras se ejecuta y muestra un mensaje de éxito con la ruta o un mensaje de error comprensible
- [X] T091 [P] [US6] Escribir `tests/Pos.Application.Tests/Diagnostics/ExportDiagnosticsHandlerTests.cs` con un doble de `IDiagnosticsExporter`: éxito → `Result<string>` con la ruta final; si el exportador falla → `ExportFailed` con un mensaje comprensible y sin excepción propagada; una ruta de destino vacía → `ValidationFailed`
- [X] T092 [P] [US6] Escribir `tests/Pos.Application.Tests/Diagnostics/GetAppInfoHandlerTests.cs`: combina `IAppInfo.Version`, `IAppInfo.OperatingSystem` e `IAppPaths.DataDirectory` en `AppInfoDto`

### Implementation for User Story 6

- [X] T093 [P] [US6] Implementar `src/Pos.Infrastructure/Diagnostics/AssemblyAppInfo.cs` (versión desde `AssemblyInformationalVersionAttribute`, sistema operativo desde `RuntimeInformation.OSDescription`)
- [X] T094 [US6] Implementar `src/Pos.Application/Diagnostics/IDiagnosticsExporter.cs`, `ExportDiagnostics/ExportDiagnosticsCommand.cs` y `ExportDiagnosticsHandler.cs`, y `GetAppInfo/GetAppInfoHandler.cs` con `AppInfoDto(Version, DataDirectory, OperatingSystem)`
- [X] T095 [US6] Implementar `src/Pos.Infrastructure/Diagnostics/ZipDiagnosticsExporter.cs` (research §18): zip en `Path.GetTempPath()`; logs de 7 días copiados con `FileShare.ReadWrite`; respaldo con `IBackupService.CreateTemporaryCopyAsync`; `info.json`; mover al destino; y limpiar el temporal en caso de error
- [X] T096 [US6] Implementar `src/Pos.Desktop/About/AboutViewModel.cs` y `AboutView.axaml(.cs)`: versión, carpeta de datos con "Copiar ruta", sistema operativo y botón "Exportar diagnóstico…" con `IDialogService.PickSaveFileAsync`, indicador de progreso y mensajes, todo a través de `OperationRunner`
- [X] T097 [US6] Registrar `AssemblyAppInfo`, `ZipDiagnosticsExporter`, `ExportDiagnosticsHandler`, `GetAppInfoHandler` y `AboutViewModel` en `src/Pos.Infrastructure/DependencyInjection.cs`, `src/Pos.Application/DependencyInjection.cs` y `src/Pos.Desktop/Composition/HostBuilder.cs`, y conectar la navegación "Acerca de" en `src/Pos.Desktop/Shell/MainViewModel.cs`

**Checkpoint**: todas las historias funcionan de forma independiente; T089 a T092 pasan.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: base de ejemplo, errores inesperados de punta a punta, documentación y validación
final.

- [X] T098 [P] Escribir `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseGenerator.cs`, una prueba que solo corre con `POS_GENERATE_SAMPLE_DB=1` y crea `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.1.0.db` con 20 productos de muestra: activos, inactivos, un borrado, uno con acentos y uno sin código de barras
- [X] T099 Generar y versionar `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.1.0.db` ejecutando `POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"`, y marcarlo como `CopyToOutputDirectory=PreserveNewest` en `tests/Pos.Infrastructure.Tests/Pos.Infrastructure.Tests.csproj` (SC-007)
- [X] T100 Escribir `tests/Pos.Infrastructure.Tests/SampleDatabases/SampleDatabaseUpgradeTests.cs`: por cada `SampleDatabases/v*.db`, copiarla a una carpeta temporal, ejecutar `DatabaseStartup` (→ `Ready`) y verificar `quick_check = ok`, el conteo de productos, los valores de muestra y el producto borrado conservado
- [X] T101 [P] Escribir `tests/Pos.Desktop.Tests/Common/UnexpectedErrorTests.cs` (SC-008): una excepción provocada en los casos de uso de alta, búsqueda, edición, borrado y exportación queda registrada con su operación y sus identificadores en un sink de prueba de Serilog, y aparece el mensaje genérico; el ViewModel sigue usable y conserva lo capturado
- [X] T102 [P] Escribir `README.md` en la raíz (español): descripción; prerrequisitos (SDK de `global.json`; en Linux, `libicu` y un entorno gráfico); `dotnet tool restore`, `dotnet build`, `dotnet test` y `dotnet run --project src/Pos.Desktop` en Windows y Linux; `POS_DATA_DIR`; enlaces a `docs/`
- [X] T103 [P] Escribir `docs/carpeta-de-datos.md`: rutas por sistema operativo, estructura (`data/`, `backups/auto`, `backups/pre-migration`, `backups/corrupt`, `logs/` y `app.lock`), retención de respaldos y cómo restaurar manualmente
- [X] T104 [P] Escribir `docs/migraciones.md`: Code First; `dotnet ef migrations add <Nombre> --project src/Pos.Infrastructure --output-dir Persistence/Migrations`; revisión del SQL con `dotnet ef migrations script <desde> <hasta>` (SQLite no admite `--idempotent`; atención a las reconstrucciones de tablas de SQLite); las migraciones publicadas nunca se modifican; la secuencia de arranque; cómo generar la base de ejemplo de cada versión; `HasData` frente al asistente de primer arranque
- [X] T105 [P] Escribir `docs/agregar-funcionalidad.md`, una guía paso a paso usando Productos como ejemplo: especificación con Spec Kit → dominio con TDD → caso de uso (Command, Validator y Handler con `Result`) → puerto y repositorio → migración → ViewModel con `OperationRunner` → vista con compiled bindings → registro en DI → pruebas por capa → verificación de arquitectura
- [X] T106 Revisar que ningún texto al operador esté fijo en XAML ni en los ViewModels, y que todos estén en `src/Pos.Desktop/Resources/Strings.resx`
- [ ] T107 Ejecutar `dotnet build` y `dotnet test` desde la raíz en Linux y en Windows (o con la CI verde en ambos): 0 errores y 0 advertencias (SC-001)
  - Estado 2026-09-29: verificado en Linux (Debug y Release: 0 advertencias, 260 pruebas aprobadas y 1 omitida a propósito). Falta Windows: el repositorio no tiene remoto, así que la CI no se ha ejecutado.
- [ ] T108 Ejecutar la validación manual de [quickstart.md](quickstart.md), secciones 2 a 5, con la red desconectada, en Windows y en Linux, y medir que el arranque sin migraciones toma menos de 5 s (SC-003, SC-010 y SC-012)
  - Estado 2026-09-29: en Linux se verificaron el arranque con base existente (base lista en 1.4 s), la base más nueva (rechazada y sin cambios) y la carpeta sin permisos (Inaccessible), además de las pantallas renderizadas sin pantalla física. Faltan el recorrido manual de la sección 3 con teclado y mouse, y todo en Windows.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (fase 1)**: no depende de nada.
- **Foundational (fase 2)**: depende de Setup y **bloquea todas las historias**.
- **US1 (fase 3)**: depende de Foundational.
- **US2 (fase 4)**: depende de Foundational. Es independiente de US1 para las pruebas, pero la
  aplicación real necesita el arranque de US1 para abrir.
- **US3 (fase 5)**: depende de Foundational y de las piezas de US2 `IProductRepository` (T055),
  los DTOs (T054) y la pantalla mínima de Productos (T062).
- **US4 (fase 6)**: depende de US2 (editor, `ProductRules` y repositorio).
- **US5 (fase 7)**: depende de US3 (lista con selección) y del repositorio de US2.
- **US6 (fase 8)**: depende de Foundational y de `IBackupService` de US1 (T041 y T044).
- **Polish (fase 9)**: depende de todas las historias. T100 depende de T042 y T099.

### User Story Dependencies

```text
Setup ─► Foundational ─┬─► US1 (arranque) ─────────────┬─► US6 (diagnóstico)
                       │                               │
                       └─► US2 (registrar) ─► US3 (buscar) ─► US5 (borrar)
                                         └─► US4 (editar)
                                                       └─► Polish
```

### Within Each User Story

- Las pruebas se escriben primero y deben fallar.
- Orden: puertos y DTOs → casos de uso → adaptadores de Infrastructure → ViewModels → vistas →
  registro en DI.
- Cada historia debe cerrarse en su checkpoint antes de pasar a la siguiente prioridad.

### Parallel Opportunities

- Setup: T004, T007 y T008 en paralelo.
- Foundational: pruebas T010 a T014; implementación T015 y T016, T018 a T020, T026 a T029.
- US1: pruebas T035 a T040; adaptadores T043 a T045 en paralelo tras T041.
- US1 y US2 pueden avanzar en paralelo después de Foundational (tocan archivos distintos).
- US6 puede avanzar en paralelo con US3, US4 y US5 cuando termine T044.
- Polish: T098, T101 y T102 a T105 en paralelo.

---

## Parallel Example: User Story 1

```bash
# Pruebas de US1 en paralelo:
Task: "DatabaseStartupTests en tests/Pos.Application.Tests/Startup/DatabaseStartupTests.cs"
Task: "SqliteDatabaseMaintenanceTests en tests/Pos.Infrastructure.Tests/Startup/SqliteDatabaseMaintenanceTests.cs"
Task: "SqliteBackupServiceTests en tests/Pos.Infrastructure.Tests/Startup/SqliteBackupServiceTests.cs"
Task: "SingleInstanceGuardTests en tests/Pos.Infrastructure.Tests/Startup/SingleInstanceGuardTests.cs"

# Adaptadores de US1 en paralelo (después de T041):
Task: "SqliteDatabaseMaintenance en src/Pos.Infrastructure/Startup/SqliteDatabaseMaintenance.cs"
Task: "SqliteBackupService en src/Pos.Infrastructure/Startup/SqliteBackupService.cs"
Task: "SingleInstanceGuard en src/Pos.Infrastructure/Startup/SingleInstanceGuard.cs"
```

## Parallel Example: User Story 2

```bash
Task: "CreateProductValidatorTests en tests/Pos.Application.Tests/Products/CreateProductValidatorTests.cs"
Task: "CreateProductHandlerTests en tests/Pos.Application.Tests/Products/CreateProductHandlerTests.cs"
Task: "ProductRepositoryCreateTests en tests/Pos.Infrastructure.Tests/Products/ProductRepositoryCreateTests.cs"
Task: "ProductEditorViewModelCreateTests en tests/Pos.Desktop.Tests/Products/ProductEditorViewModelCreateTests.cs"
```

---

## Implementation Strategy

### MVP First

El MVP son las tres historias P1 (US1, US2 y US3): una aplicación que arranca de forma segura y
permite registrar y buscar productos.

1. Fases 1 y 2: Setup y Foundational.
2. Fase 3 (US1): **validar** los escenarios de arranque.
3. Fase 4 (US2): **validar** el alta de punta a punta.
4. Fase 5 (US3): **validar** la búsqueda. Aquí termina el MVP.

### Incremental Delivery

1. MVP (US1 a US3), luego demo.
2. US4 (editar) y US5 (borrar): CRUD completo.
3. US6 (diagnóstico): lista para soporte en campo.
4. Polish: base de ejemplo v0.1.0, documentación y validación en ambos sistemas operativos.

---

## Notes

- [P] significa archivos distintos y sin dependencias pendientes.
- Cada tarea con etiqueta de historia se puede rastrear hasta su historia en [spec.md](spec.md).
- Verifica que las pruebas fallan antes de implementar (TDD, Principio VI).
- Haz un commit por tarea o por grupo lógico, con Conventional Commits.
- Todo defecto que aparezca durante la implementación incluye una prueba que lo reproduce
  (Principio VI).
