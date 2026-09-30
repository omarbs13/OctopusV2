# Contrato: secuencia de arranque

La secuencia cubre FR-001 a FR-008a y SC-006. La orquesta `DatabaseStartup.RunAsync()` en
`Pos.Application/Startup/`; la verificación de instancia única la hace antes `Program.Main` en
Desktop.

## Flujo

```text
Program.Main
 ├─ SingleInstanceGuard.TryAcquire()
 │    └─ no ─► enviar "activate" a la instancia existente, mostrar aviso ─► salir (código 0)
 ├─ configurar Serilog y construir el host
 └─ DatabaseStartup.RunAsync()
      1. ¿Existe la base?  no ─► MigrateAsync (crea) ─► 6
      2. CheckIntegrity:  Corrupted ─► Corrupted(latestAutoBackup?)
                          Inaccessible ─► Inaccessible(reason)
      3. GetMigrationState:  Newer ─► NewerDatabase (sin tocar la base)
                             UpToDate ─► 6
      4. ¿Espacio libre ≥ 2 × tamaño de la base?  no ─► InsufficientSpace
         CreateAsync(PreMigration)  error de E/S ─► InsufficientSpace o Inaccessible
      5. MigrateAsync  error ─► RestoreAsync(respaldo pre-migración) ─► MigrationFailed
      6. EnableWal
      7. Si el último respaldo automático tiene más de 24 h: CreateAsync(Automatic)
         (un error solo se registra en el log, no bloquea)
      ─► Ready
```

## Resultados y respuesta de Desktop

| Resultado | Mensaje al operador (resumen) | Acción de Desktop |
|---|---|---|
| `Ready` | — | Abre la ventana principal |
| `NewerDatabase` | "Los datos fueron creados por una versión más reciente del POS. Instale la versión correcta." | Diálogo y salida |
| `MigrationFailed` | "No se pudo actualizar la base de datos. Se restauraron sus datos anteriores. Contacte a soporte." | Diálogo y salida |
| `InsufficientSpace` | "No hay espacio suficiente en disco para actualizar de forma segura. Libere espacio y vuelva a abrir." | Diálogo y salida |
| `Inaccessible(Locked)` | "La base de datos está en uso por otro programa." | Diálogo y salida |
| `Inaccessible(PermissionDenied)` | "No hay permisos para escribir en la carpeta de datos: {ruta}." | Diálogo y salida |
| `Corrupted(backup)` | "La base de datos está dañada. ¿Restaurar el respaldo del {fecha local}?" | Si confirma: `QuarantineCurrentDatabaseAsync`, luego `RestoreAsync` y reiniciar la secuencia desde 1. Si no confirma: mensaje de soporte y salida |
| `Corrupted(null)` | "La base de datos está dañada y no hay respaldos. Contacte a soporte." | Diálogo y salida |

Toda salida por error registra el detalle técnico en el log antes de mostrar el mensaje. Los
mensajes nunca incluyen detalles técnicos (Principio I).

## Cierre

Al cerrar la ventana principal, si el último respaldo automático tiene más de 24 h, se ejecuta
`CreateAsync(Automatic)` con un límite de 15 s. Si falla o excede el límite, se registra en el log
y la aplicación cierra igual.

## Pruebas obligatorias

- **`Pos.Application.Tests`** (con dobles de prueba): cada rama del flujo y el orden exacto de las
  llamadas, en especial que el respaldo ocurra antes de migrar y que no haya ninguna llamada de
  escritura con `Newer`.
- **`Pos.Infrastructure.Tests`** (con SQLite real en carpetas temporales):
  - Base nueva.
  - Migración pendiente, creando una base con una migración anterior.
  - Base más nueva, insertando un registro falso en `__EFMigrationsHistory`.
  - Falla de migración con restauración, verificando que los datos sean idénticos.
  - Base dañada, sobrescribiendo la cabecera del archivo, con restauración confirmada.
- **`SampleDatabaseUpgradeTests`**: todas las bases de ejemplo migran a la versión actual.
