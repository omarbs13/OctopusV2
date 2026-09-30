# Contrato: puertos de Application

Interfaces que define `Pos.Application` e implementa `Pos.Infrastructure` (Principio II). Cada una
tiene una implementación real, que se prueba contra SQLite o el sistema de archivos reales, y
dobles de prueba en `Pos.Application.Tests`.

| Puerto | Responsabilidad | Implementación |
|---|---|---|
| `IClock` | `DateTime UtcNow` | `SystemClock` |
| `ICurrentUser` | `Guid UserId` | `SystemCurrentUser` (GUID fijo, FR-024) |
| `IProductRepository` | Persistencia del agregado `Product` | `ProductRepository` (EF Core) |
| `IDatabaseMaintenance` | Estado, verificación y migración de la base | `SqliteDatabaseMaintenance` |
| `IBackupService` | Crear, listar, restaurar y podar respaldos | `SqliteBackupService` |
| `IAppPaths` | Rutas de la carpeta de datos | `AppPaths` (respeta `POS_DATA_DIR`) |
| `IAppInfo` | Versión y sistema operativo | `AssemblyAppInfo` |
| `IDiagnosticsExporter` | Crear el zip de diagnóstico | `ZipDiagnosticsExporter` |
| `IDatabaseStartup` | Secuencia de arranque y cierre (la usa Desktop) | `DatabaseStartup` (en Application) |

`ISingleInstanceGuard` **no** es un puerto de Application: se usa antes de construir el host, así
que vive en Infrastructure y lo invoca directamente la raíz de composición de Desktop.

## IProductRepository

Es específico del agregado; no existen repositorios genéricos (Principio VII).

```text
Task<Product?> GetAsync(Guid id, CancellationToken ct)                                  // excluye borrados
Task<bool> SkuExistsAsync(string sku, Guid? excludingId, CancellationToken ct)
Task<bool> BarcodeExistsAsync(string barcode, Guid? excludingId, CancellationToken ct)
Task<ProductSearchPage> SearchAsync(ProductSearch search, CancellationToken ct)
void Add(Product product)
Task<SaveOutcome> SaveChangesAsync(Product product, int? expectedVersion, CancellationToken ct)
```

`ProductSearch(NameText, SkuText, BarcodeText, BarcodeExact, IncludeInactive, Limit)` llega ya
normalizado por `SearchProductsHandler`; `ProductSearchPage(Items, HasMore)`.

`SaveOutcome` tiene `Status` (`Saved`, `Conflict` o `Duplicate`) y, si es duplicado, `DuplicateField`. Traduce
`DbUpdateConcurrencyException` y la violación de índices únicos, así que ninguna excepción de EF
Core llega a Application.

## IDatabaseMaintenance

```text
bool DatabaseExists()
Task<IntegrityState> CheckIntegrityAsync(ct)           // Ok | Corrupted | Locked | PermissionDenied
Task<MigrationState> GetMigrationStateAsync(ct)        // MigrationState(Status: UpToDate | Pending | Newer, Migrations)
Task MigrateAsync(ct)                                  // crea o actualiza el esquema
Task EnableWalAsync(ct)
long DatabaseSizeBytes()
long AvailableFreeSpaceBytes()
```

Los problemas de acceso durante una migración o un respaldo (bloqueo, permisos, disco lleno) se
lanzan como `DatabaseAccessException(DatabaseProblem)`, definida en Application, para que
Application no dependa de SQLite.

## IBackupService

```text
Task<BackupInfo> CreateAsync(BackupKind kind, ct)           // escribe .tmp y renombra; aplica la retención
Task<BackupInfo?> GetLatestAsync(BackupKind kind, ct)
Task RestoreAsync(BackupInfo backup, ct)                    // restaura con la API de backup y limpia -wal/-shm
Task QuarantineCurrentDatabaseAsync(ct)                     // mueve la base a backups/corrupt/<fecha>/
Task<string> CreateTemporaryCopyAsync(ct)                   // para el diagnóstico
```

## IAppPaths

```text
string DataDirectory       // raíz
string DatabaseFile        // data/pos.db
string AutoBackupsDirectory, PreMigrationBackupsDirectory, CorruptDirectory, LogsDirectory, LockFile
```
