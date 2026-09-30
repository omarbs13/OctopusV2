# Research: Fundación del POS

Decisiones técnicas de la Fase 0 para [plan.md](plan.md). Cada una indica qué se eligió, por qué y
qué alternativas se descartaron. Las versiones de paquetes se fijan en `Directory.Packages.props`.

## 1. Estructura de la solución y compilación

- **Decision**: una solución `Pos.slnx` en la raíz del repositorio, con los proyectos en `src/` y
  las pruebas en `tests/`. `global.json` fija el SDK 10.0.1xx con `rollForward: latestFeature`.
  `Directory.Build.props` centraliza `TargetFramework=net10.0`, `Nullable=enable`,
  `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`,
  `EnforceCodeStyleInBuild=true` y la versión de la aplicación. `Directory.Packages.props` activa
  la gestión central de paquetes.
- **Rationale**: con la solución en la raíz, `dotnet build` y `dotnet test` funcionan sin
  argumentos (FR-029). El formato `.slnx` es el predeterminado de .NET 10.
- **Alternatives considered**: `.sln` clásico (más verboso, sin ventaja); solución dentro de
  `src/` (obliga a pasar la ruta en cada comando).

## 2. Proyectos de prueba

- **Decision**: `Pos.Domain.Tests`, `Pos.Application.Tests`, `Pos.Infrastructure.Tests`,
  `Pos.Desktop.Tests` (ViewModels, sin UI) y `Pos.ArchitectureTests`. Todos con xUnit v3.
  **Nota de implementación:** el SDK de .NET 10 ya no permite ejecutar xUnit v3 con VSTest
  desde `dotnet test`, así que `global.json` activa Microsoft.Testing.Platform
  (`"test": { "runner": "Microsoft.Testing.Platform" }`) y no se usan
  `Microsoft.NET.Test.Sdk` ni `xunit.runner.visualstudio`.
- **Rationale**: un proyecto de pruebas por capa, como pide el Principio II. Los ViewModels se
  prueban como clases normales con dobles de prueba, así que no hace falta un motor de UI.
- **Alternatives considered**: xUnit 2.x (en mantenimiento, sin evolución); Avalonia.Headless
  para probar vistas (no aporta a esta funcionalidad, porque las vistas no tienen lógica).

## 3. Dinero

- **Decision**: value object `Money` en Domain que guarda `long Cents` (moneda única, MXN). Se
  crea con `Money.FromCents(long)` o `Money.TryParse(string)`. `TryParse` acepta solo
  `^\d{1,6}(\.\d{1,2})?$`: dígitos y un punto decimal opcional con 1 o 2 decimales, hasta
  999999.99, y rechaza comas, signos, símbolos de moneda y espacios internos (se recortan los de
  los extremos). En SQLite se almacena como `INTEGER`.
- **Rationale**: Principio IV (enteros en centavos, nunca `double` ni `float`) y la
  clarificación 5 (solo dígitos y punto). Un parseo estricto con expresión regular nunca redondea.
- **Alternatives considered**: `decimal` con conversión a centavos al guardar (el dominio
  quedaría expuesto a más de 2 decimales); `decimal.Parse` con cultura (acepta comas según la
  cultura).

## 4. Resultados de los casos de uso

- **Decision**: tipo propio `Result` / `Result<T>` en Application, con errores de negocio tipados:
  `ValidationFailed` (lista de campo y mensaje), `NotFound`, `Conflict` (concurrencia) y
  `Duplicate` (campo). Las excepciones solo representan fallas inesperadas.
- **Rationale**: Principio III. El tipo es pequeño y no justifica una dependencia externa
  (Principio VII).
- **Alternatives considered**: FluentResults o ErrorOr (dependencia extra para un tipo de 50
  líneas); excepciones para errores de negocio (lo prohíbe la constitución).

## 5. Validación de entrada

- **Decision**: FluentValidation en Application, con un validador por comando
  (`CreateProductValidator`, `UpdateProductValidator`) que comparten reglas en
  `ProductRules`. El precio llega como texto y se valida con `Money.TryParse`. El nombre se recorta
  y el SKU se pasa a mayúsculas antes de validar. Las invariantes también se verifican en el
  constructor y en los métodos de `Product`, así que el dominio nunca queda en un estado
  inválido.
- **Rationale**: lo exigen las restricciones técnicas. Los errores por campo permiten mostrarlos
  junto a cada control (FR-014).
- **Alternatives considered**: DataAnnotations (errores menos expresivos y acopladas a la UI).

## 6. Identificadores, auditoría y tiempo

- **Decision**: `Guid.CreateVersion7()` generado en Domain al crear la entidad, guardado como
  `TEXT` en SQLite. Los puertos `IClock` (`UtcNow`) e `ICurrentUser` (`UserId`) se definen en
  Application. `SystemCurrentUser` devuelve el GUID fijo `00000000-0000-7000-8000-000000000001` y
  se registra en DI; el usuario real lo sustituirá sin cambiar los casos de uso (FR-024). Un
  interceptor de EF Core (`AuditingInterceptor`) llena `CreatedAt/By` y `UpdatedAt/By` a partir de
  `IClock` e `ICurrentUser`.
- **Rationale**: Principio IV. El interceptor evita repetir la auditoría en cada caso de uso.
- **Alternatives considered**: IDs autoincrementales (los prohíbe la constitución); llenar la
  auditoría a mano en los casos de uso (repetitivo y fácil de olvidar).

## 7. Concurrencia optimista

- **Decision**: propiedad `int Version` configurada como `IsConcurrencyToken()`. El interceptor la
  incrementa en toda entidad `Modified`. El caso de uso de edición recibe la versión que vio el
  operador y la establece como valor original antes de guardar. Una
  `DbUpdateConcurrencyException` se traduce a `Conflict` en el repositorio.
- **Rationale**: FR-020 y SC-005. SQLite no tiene `rowversion` automático, así que un entero
  manual es la opción más simple y fácil de probar.
- **Alternatives considered**: comparar `UpdatedAt` (tiene problemas de precisión y reloj); no
  controlar la concurrencia (lo prohíbe la constitución).

## 8. Unicidad de SKU y código de barras entre no borrados

- **Decision**: índices únicos parciales de SQLite: `IX_Products_Sku` con filtro
  `"DeletedAt" IS NULL`, e `IX_Products_Barcode` con filtro
  `"DeletedAt" IS NULL AND "Barcode" IS NOT NULL`. El caso de uso consulta antes para dar un
  mensaje por campo (`Duplicate`), y el repositorio traduce la violación del índice
  (`SQLITE_CONSTRAINT_UNIQUE`) a `Duplicate` por si ocurre una carrera.
- **Rationale**: FR-011, FR-012 y FR-022. La base garantiza la regla aunque falle la
  verificación previa.
- **Alternatives considered**: validar solo en la aplicación (no garantiza unicidad); índice único
  total (impediría reutilizar códigos de productos borrados).

## 9. Búsqueda sin mayúsculas ni acentos

- **Decision**: columna `NameSearch` con el nombre normalizado: `FormD`, sin marcas diacríticas
  (`UnicodeCategory.NonSpacingMark`) y en minúsculas invariantes. La calcula `Product` en Domain
  mediante `TextNormalizer`. La consulta se normaliza igual y se busca con `LIKE` escapado
  (`ESCAPE '\'`) sobre `NameSearch` y `Sku`. Para el código de barras: si la consulta cumple
  `^\d{8,14}$`, se busca por igualdad; si no, con `LIKE`. Los filtros por `DeletedAt IS NULL` e
  `IsActive` se aplican según FR-016. Los resultados se ordenan por `NameSearch` y se limitan a
  200 filas, con un aviso de "hay más resultados, refine la búsqueda".
- **Rationale**: FR-017 y la clarificación 4. El `LIKE` de SQLite solo ignora mayúsculas en
  ASCII y no ignora acentos. Con 10,000 productos, recorrer la tabla toma milisegundos, muy por
  debajo de SC-011.
- **Alternatives considered**: FTS5 (complejidad innecesaria a esta escala); una intercalación
  personalizada con `SqliteConnection.CreateCollation` (complica las migraciones y el uso de
  índices).

## 10. Doble clic en "Guardar"

- **Decision**: los comandos de guardado son `AsyncRelayCommand` de CommunityToolkit.Mvvm, que por
  defecto no permiten ejecuciones concurrentes, y el botón se deshabilita mientras
  `IsRunning`. Tras un alta exitosa, el formulario se cierra o se limpia. La unicidad del SKU en
  la base es la segunda barrera.
- **Rationale**: FR-015 con el mecanismo más simple. Se prueba en `Pos.Desktop.Tests`.
- **Alternatives considered**: claves de idempotencia por solicitud (YAGNI en una app local de
  un solo usuario).

## 11. Carpeta de datos

- **Decision**: la raíz es `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)/Pos`, es
  decir `%LOCALAPPDATA%\Pos` en Windows y `~/.local/share/Pos` en Linux. Se puede sobrescribir con
  la variable de entorno `POS_DATA_DIR`, que usan las pruebas y soporte. Estructura:
  `data/pos.db`, `backups/auto/`, `backups/pre-migration/`, `backups/corrupt/`, `logs/` y
  `app.lock`. Todas las rutas se construyen con `Path.Combine`.
- **Rationale**: FR-001 y Principio V.
- **Alternatives considered**: la carpeta de instalación (en Windows no tiene permisos de
  escritura); `ApplicationData` roaming (inadecuada para una base de datos).

## 12. Instancia única y traer la ventana al frente

- **Decision**: `SingleInstanceGuard` abre `app.lock` con `FileShare.None` y lo mantiene abierto
  durante toda la vida del proceso; el sistema operativo libera el bloqueo si el proceso muere.
  La primera instancia escucha en un `NamedPipeServerStream` (nombre derivado de usuario y
  aplicación; en Linux, .NET lo implementa con sockets de dominio Unix). Si la segunda instancia
  no obtiene el bloqueo, envía `activate` por el pipe, muestra un aviso y termina. La primera
  instancia, al recibir el mensaje, restaura y activa la ventana en el hilo de UI.
- **Rationale**: FR-002. Funciona igual en Windows y Linux, y el bloqueo de archivo no deja
  estados huérfanos tras un cierre abrupto.
- **Alternatives considered**: `Mutex` con nombre (su semántica difiere entre plataformas y no
  permite comunicarse con la otra instancia); comprobar la lista de procesos (frágil).

## 13. Secuencia de arranque de la base

- **Decision**: `DatabaseStartup`, en Application, orquesta los puertos `IDatabaseMaintenance`
  y `IBackupService` en el orden de la constitución (Principio IV):
  1. La instancia única ya se verificó antes de construir el host.
  2. Si `pos.db` no existe: migrar para crear la base y terminar.
  3. `quick_check` de SQLite: si falla (o hay `SQLITE_CORRUPT` o `SQLITE_NOTADB`), el resultado
     es `Corrupted`, con la fecha del respaldo automático más reciente si existe.
  4. Base más nueva: si hay migraciones aplicadas que el ensamblado no conoce
     (`GetAppliedMigrations()` menos `GetMigrations()` no vacío), el resultado es `NewerDatabase`
     y la base no se toca.
  5. Si hay migraciones pendientes: verificar espacio libre (al menos el doble del tamaño de la
     base, con `DriveInfo`), respaldar en `backups/pre-migration/` con
     `SqliteConnection.BackupDatabase` y migrar con `Database.MigrateAsync()`. Si falla, cerrar
     las conexiones (`SqliteConnection.ClearAllPools()`), restaurar desde el respaldo con la misma
     API de backup, eliminar los archivos `-wal` y `-shm` residuales y devolver `MigrationFailed`.
  6. Activar `PRAGMA journal_mode=WAL` (persiste en el archivo).
  7. Respaldo automático de arranque, si el último tiene más de 24 h (clarificación 2).

  El resultado es un tipo cerrado (`Ready`, `NewerDatabase`, `MigrationFailed`, `Corrupted`,
  `Inaccessible`, `InsufficientSpace`) que Desktop traduce a mensajes (ver
  [contracts/startup.md](contracts/startup.md)).
- **Rationale**: FR-001 a FR-008a y SC-006. La lógica de orden vive en Application y se prueba con
  dobles de prueba; los adaptadores se prueban contra SQLite real.
- **Alternatives considered**: `EnsureCreated` (incompatible con migraciones); copiar el archivo
  con `File.Copy` para respaldar (no es consistente con WAL y conexiones abiertas).

## 14. Restauración de una base dañada

- **Decision**: con `Corrupted` y un respaldo disponible, Desktop pregunta al operador mostrando la
  fecha. Si confirma, `IBackupService.RestoreLatestAutomatic()` mueve `pos.db`, `-wal` y `-shm` a
  `backups/corrupt/<fecha-utc>/` y restaura el respaldo con la API de backup. Después se repite la
  secuencia de arranque, porque el respaldo puede requerir migraciones. Si el operador no confirma
  o no hay respaldo, se muestra el mensaje de contactar a soporte y la aplicación termina.
- **Rationale**: clarificación 1 y FR-008a.
- **Alternatives considered**: restaurar sin preguntar (lo descartó el usuario).

## 15. Respaldos automáticos y retención

- **Decision**: `backups/auto/pos-<yyyyMMdd-HHmmss>Z.db`. Al arrancar (paso 7 de la secuencia) y
  al cerrar la ventana principal, si el más reciente tiene más de 24 h, se respalda y se conservan
  los 7 más nuevos. En `pre-migration/` se conservan 5. El respaldo al cerrar tiene un límite de
  15 s; si lo excede o falla, se registra en el log y la aplicación cierra igual. Los respaldos se
  escriben primero como `.tmp` y se renombran al terminar, para que un respaldo a medias nunca
  parezca válido.
- **Rationale**: FR-007 y clarificación 2.
- **Alternatives considered**: respaldos por temporizador (no los pidió el usuario).

## 16. Logging y manejo global de errores

- **Decision**: Serilog con `Serilog.Extensions.Hosting` (integra `ILogger<T>`),
  `Serilog.Sinks.File` en `logs/pos-.log` con rotación diaria, 31 archivos, `shared: true` y formato
  CLEF (`Serilog.Formatting.Compact`). Se enriquece con la versión, la máquina y el sistema
  operativo. Los eventos de `Microsoft.*` (por ejemplo, cada comando SQL de EF Core) se registran
  solo desde el nivel Warning. Los ViewModels ejecutan sus operaciones con `OperationRunner.RunAsync(nombre, ...)`,
  que agrega la operación, el usuario actual (`ICurrentUser.UserId`) y los identificadores al
  `LogContext`, captura cualquier excepción, la
  registra y muestra un mensaje genérico en español sin cerrar el formulario. Como respaldo
  adicional: `Dispatcher.UIThread.UnhandledException` (marca `Handled`),
  `TaskScheduler.UnobservedTaskException` y `AppDomain.CurrentDomain.UnhandledException` (este
  último solo registra, porque en .NET no puede evitar el cierre; por eso se prohíben las tareas
  "fire-and-forget" sin `OperationRunner`).
- **Rationale**: FR-026, SC-008 y Principio VIII. CLEF conserva la estructura y hay herramientas
  para leerlo.
- **Alternatives considered**: texto plano (se pierde la estructura); solo manejadores globales
  (el hilo de UI puede quedar en un estado inconsistente y se pierde el contexto de la operación).
- **Verificado en la implementación**: en Avalonia 12 el evento es
  `Dispatcher.UIThread.UnhandledException`, con `e.Handled`.

## 17. Formato de moneda

- **Decision**: `CultureInfo.GetCultureInfo("es-MX")` con el formato `"C2"`, lo que produce
  `$1,234.50`. Se centraliza en un convertidor de Desktop (`MoneyConverter`).
  `InvariantGlobalization` permanece desactivado; en Linux se requiere ICU (`libicu`), lo cual
  se documenta en los prerrequisitos.
- **Rationale**: FR-018.
- **Alternatives considered**: formatear a mano (reinventa lo que ya hace la cultura).

## 18. Exportación de diagnóstico

- **Decision**: `DiagnosticsExporter`, en Infrastructure, crea un zip temporal en la carpeta
  `tmp` del sistema con:
  - `info.json`: versión, sistema operativo, ruta de datos y fecha UTC.
  - `logs/`: los archivos de los últimos 7 días, leídos con `FileShare.ReadWrite`.
  - `pos.db`: un respaldo generado con la API de backup.

  Al terminar, mueve el zip al destino que eligió el usuario. Si falla, borra el temporal y
  devuelve un error. Así nunca queda un zip incompleto en el destino.
- **Rationale**: FR-028 y SC-009.
- **Alternatives considered**: escribir directamente en el destino (puede dejar archivos
  incompletos).

## 19. Migraciones y base de ejemplo

- **Decision**:
  - `dotnet-ef` es una herramienta local (`.config/dotnet-tools.json`).
  - `PosDbContextFactory` (`IDesignTimeDbContextFactory`) está en Infrastructure.
  - Las migraciones viven en `src/Pos.Infrastructure/Persistence/Migrations/`.
  - El SQL de cada migración se revisa con `dotnet ef migrations script <desde> <hasta>`, que se
    documenta en `docs/migraciones.md`. **Nota de implementación:** SQLite no admite
    `--idempotent`.
  - Las bases de ejemplo se guardan en `tests/Pos.Infrastructure.Tests/SampleDatabases/v<versión>.db`.
    Las genera la prueba opcional `SampleDatabaseGenerator`, que solo corre con
    `POS_GENERATE_SAMPLE_DB=1` (`dotnet test ... -- --filter-class ...`).
  - `SampleDatabaseUpgradeTests` copia cada base de ejemplo, ejecuta `DatabaseStartup` y verifica
    `quick_check`, el conteo de productos y los valores de muestra.
- **Rationale**: Principio IV (migraciones Code First, bases de ejemplo por versión, revisión del
  SQL) y SC-007.
- **Alternatives considered**: `dotnet-ef` global (no es reproducible); bases de ejemplo creadas
  con SQL a mano (lo prohíbe la constitución).
- **HasData y asistente de primer arranque**: esta funcionalidad no tiene catálogos fijos ni
  datos propios de la instalación, así que no hay siembra. Se documenta el patrón para las
  funcionalidades futuras.

## 20. Pruebas de arquitectura

- **Decision**: NetArchTest.Rules verifica que:
  - Domain no depende de Application, Infrastructure, Desktop, EF Core, Avalonia ni Serilog.
  - Application no depende de Infrastructure, Desktop, EF Core ni Avalonia.
  - Infrastructure no depende de Desktop.
  - Los tipos de `Pos.Desktop` fuera del namespace `Pos.Desktop.Composition` no dependen de
    `Pos.Infrastructure`, `Microsoft.EntityFrameworkCore` ni `Microsoft.Data.Sqlite`.

  Además, una prueba de referencias de proyecto lee los `.csproj` para confirmar las
  dependencias directas permitidas.
- **Rationale**: Principio II y SC-002.
- **Alternatives considered**: ArchUnitNET (más potente, pero NetArchTest es lo que fija la
  constitución).

## 21. Integración continua

- **Decision**: GitHub Actions, `.github/workflows/ci.yml`, con una matriz `ubuntu-latest` y
  `windows-latest`. Pasos: `actions/setup-dotnet` con `global-json-file`, `dotnet restore`,
  `dotnet build --no-restore` y `dotnet test --no-build`, y se publican los resultados de las
  pruebas como artefacto.
- **Rationale**: FR-031 y Principio V.
- **Alternatives considered**: otros proveedores de CI. El repositorio aún no tiene remoto; si se
  usa otro proveedor, se traduce el mismo flujo.

## 22. UI y navegación

- **Decision**: Avalonia 12 con el tema Fluent y compiled bindings por defecto
  (`AvaloniaUseCompiledBindingsByDefault`). Una `MainWindow` con navegación simple por
  ViewModel actual (Productos y Acerca de), `ProductEditorView` como panel o diálogo, y un
  `IDialogService` definido en Desktop (confirmaciones, mensajes, selector de archivo) para que
  los ViewModels se puedan probar. Un contenedor de DI de `Microsoft.Extensions.Hosting` en
  `Pos.Desktop.Composition`.
- **Rationale**: restricciones técnicas y Principio III.
- **Alternatives considered**: ReactiveUI (dependencia extra; CommunityToolkit.Mvvm es lo que fija
  la constitución).

## Paquetes externos y justificación (Principio VII)

| Paquete | Proyecto | Justificación |
|---|---|---|
| FluentValidation | Application | Lo exige la constitución para la validación de entrada |
| Microsoft.Extensions.Logging.Abstractions | Application | `ILogger<T>` sin acoplarse a Serilog |
| Microsoft.EntityFrameworkCore.Sqlite | Infrastructure | Lo exige la constitución (EF Core + SQLite) |
| Microsoft.EntityFrameworkCore.Design | Infrastructure (solo diseño) | Necesario para `dotnet ef migrations` |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter | Desktop | UI exigida por la constitución |
| CommunityToolkit.Mvvm | Desktop | MVVM exigido por la constitución |
| Microsoft.Extensions.Hosting | Desktop | DI y host exigidos por la constitución |
| Serilog, Serilog.Extensions.Hosting, Serilog.Sinks.File, Serilog.Formatting.Compact | Desktop | Logging exigido por la constitución; archivos rotativos en formato estructurado |
| xunit.v3 | tests | Lo exige la constitución (se ejecuta con Microsoft.Testing.Platform) |
| NetArchTest.Rules | Pos.ArchitectureTests | Lo exige la constitución |
| dotnet-ef (herramienta local) | repositorio | Necesaria para crear migraciones y generar su SQL |
| Microsoft.Extensions.DependencyInjection.Abstractions | Application | Registro de casos de uso (`AddApplication`) sin depender del contenedor |
