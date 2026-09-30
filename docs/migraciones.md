# Migraciones de la base de datos

Reglas y procedimiento para cambiar el esquema de la base local (constitución, Principio IV).

## Reglas

- **Code First**: el modelo en C# (entidades de `Pos.Domain` y configuraciones en
  `src/Pos.Infrastructure/Persistence/Configurations/`) es la fuente de verdad. No se escriben
  scripts SQL de esquema a mano.
- **Una migración publicada nunca se modifica ni se elimina.** Las correcciones se hacen con
  migraciones nuevas. "Publicada" significa que llegó a `main` o que se entregó a algún cliente.
- **El SQL generado se revisa antes de integrarlo**, con especial atención a las
  reconstrucciones de tablas (ver más abajo).
- **Cada versión publicada conserva una base de ejemplo**, y una prueba automática migra todas
  a la versión actual.
- **Siembra de datos**: los catálogos fijos (por ejemplo, unidades de medida) se siembran con
  `HasData` en la configuración de la entidad. Los datos propios de cada instalación (sucursal,
  primer usuario...) se crean en el asistente de primer arranque, nunca con `HasData`.

## Crear una migración

```bash
dotnet tool restore
dotnet ef migrations add <NombreDescriptivo> --project src/Pos.Infrastructure --output-dir Persistence/Migrations
```

Usa nombres en inglés que describan el cambio: `AddProductCategory`, `AddSaleTables`...

Las migraciones se generan en `src/Pos.Infrastructure/Persistence/Migrations/`. Esa carpeta está
marcada como código generado en `.editorconfig`, así que las reglas de estilo no aplican ahí.
Tampoco se editan a mano.

## Revisar el SQL

SQLite **no** admite scripts idempotentes (`--idempotent`). Genera el script entre la última
migración publicada y la nueva:

```bash
# Todo el esquema desde cero
dotnet ef migrations script --project src/Pos.Infrastructure -o migracion.sql

# Solo lo que cambia entre dos migraciones
dotnet ef migrations script <UltimaPublicada> <Nueva> --project src/Pos.Infrastructure -o migracion.sql
```

Qué revisar:

1. **Reconstrucciones de tabla.** SQLite no puede alterar columnas, cambiar tipos ni quitar
   restricciones; EF Core lo resuelve creando `ef_temp_<Tabla>`, copiando los datos, borrando la
   tabla original y renombrando. Verifica que el `INSERT INTO ... SELECT` copie **todas** las
   columnas con los valores correctos, sobre todo si agregas una columna `NOT NULL` (necesita un
   valor por defecto para las filas existentes).
2. **Índices parciales.** Los índices únicos de SKU y código de barras filtran por
   `"DeletedAt" IS NULL`. Si una migración los recrea, el filtro debe mantenerse.
3. **Pérdida de datos.** EF Core advierte con "An operation was scaffolded that may result in the
   loss of data". No lo ignores: ajusta la migración (con una migración nueva) o documenta por
   qué es seguro.

## Cómo se aplican las migraciones

Al arrancar, la aplicación sigue este orden obligatorio (ver
`src/Pos.Application/Startup/DatabaseStartup.cs`):

1. Verifica que haya una sola instancia en ejecución.
2. Verifica la integridad de la base (`PRAGMA quick_check`).
3. Detecta si la base es **más nueva** que la aplicación: tiene migraciones aplicadas que esta
   versión no conoce. En ese caso no la abre ni la modifica.
4. Si hay migraciones pendientes, comprueba que haya espacio libre (al menos el doble del tamaño
   de la base) y la **respalda** en `backups/pre-migration/` con la API de backup de SQLite.
5. **Migra.** Si falla, restaura el respaldo, registra el error, informa al operador y no
   continúa.

## Base de ejemplo de cada versión

Antes de publicar una versión nueva (cuando `Version` cambia en `Directory.Build.props`):

```bash
POS_GENERATE_SAMPLE_DB=1 dotnet test --project tests/Pos.Infrastructure.Tests -- --filter-class "Pos.Infrastructure.Tests.SampleDatabases.SampleDatabaseGenerator"
```

Esto crea `tests/Pos.Infrastructure.Tests/SampleDatabases/v<versión>.db` con los datos de
`SampleData`: 20 productos, incluidos inactivos, uno borrado y uno con acentos. El generador se
niega a sobrescribir un archivo existente. Agrega el `.db` al commit; nunca lo modifiques
después.

`SampleDatabaseUpgradeTests` toma **cada** `v*.db`, lo migra a la versión actual con la secuencia
real de arranque y verifica la integridad y los datos. Si una migración nueva rompe una base
antigua, esta prueba falla.

Cuando una funcionalidad agregue tablas nuevas, amplía `SampleData` para que la base de ejemplo
de la siguiente versión también tenga datos en ellas, y agrega verificaciones en
`SampleDatabaseUpgradeTests`.
