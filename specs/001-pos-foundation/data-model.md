# Data Model: Fundación del POS

Modelo de dominio y de persistencia de [spec.md](spec.md). Las decisiones de fondo están en
[research.md](research.md).

## Product (agregado)

Artículo del catálogo. Vive en `Pos.Domain/Products/Product.cs`.

| Campo | Tipo en dominio | Columna SQLite | Reglas |
|---|---|---|---|
| `Id` | `Guid` (v7) | `Id TEXT PK` | Lo genera `Guid.CreateVersion7()` al crear; inmutable |
| `Name` | `string` | `Name TEXT NOT NULL` | Obligatorio; se recorta; 1 a 200 caracteres (FR-010) |
| `NameSearch` | `string` | `NameSearch TEXT NOT NULL` | Derivado de `Name`: sin acentos y en minúsculas; lo recalcula el dominio al cambiar el nombre |
| `Sku` | `string` | `Sku TEXT NOT NULL` | Obligatorio; 1 a 50 caracteres; sin espacios; se guarda en mayúsculas invariantes (FR-011) |
| `Barcode` | `string?` | `Barcode TEXT NULL` | Opcional; si existe, `^\d{8,14}$` (FR-012); una cadena vacía se guarda como `null` |
| `Price` | `Money` | `PriceCents INTEGER NOT NULL` | 0 a 99,999,999 centavos (FR-013) |
| `IsActive` | `bool` | `IsActive INTEGER NOT NULL` | `true` al crear |
| `CreatedAt` | `DateTime` (UTC) | `CreatedAt TEXT NOT NULL` | Lo asigna el interceptor de auditoría |
| `CreatedBy` | `Guid` | `CreatedBy TEXT NOT NULL` | `ICurrentUser.UserId` |
| `UpdatedAt` | `DateTime` (UTC) | `UpdatedAt TEXT NOT NULL` | Igual a `CreatedAt` al crear; se actualiza en cada modificación |
| `UpdatedBy` | `Guid` | `UpdatedBy TEXT NOT NULL` | Igual que arriba |
| `DeletedAt` | `DateTime?` (UTC) | `DeletedAt TEXT NULL` | `null` mientras no se borre (FR-021) |
| `Version` | `int` | `Version INTEGER NOT NULL` | Empieza en 1; aumenta en cada modificación; token de concurrencia (FR-020) |

Las fechas se leen con un convertidor que fuerza `DateTimeKind.Utc`.

**Índices**

| Nombre | Columnas | Único | Filtro |
|---|---|---|---|
| `IX_Products_Sku` | `Sku` | Sí | `"DeletedAt" IS NULL` |
| `IX_Products_Barcode` | `Barcode` | Sí | `"DeletedAt" IS NULL AND "Barcode" IS NOT NULL` |
| `IX_Products_NameSearch` | `NameSearch` | No | `"DeletedAt" IS NULL` (sirve para ordenar) |

**Comportamiento del dominio**

- `Product.Create(name, sku, barcode, price)` valida las invariantes y devuelve un producto
  activo con `Version = 1`.
- `product.Update(name, sku, barcode, price, isActive)` vuelve a validar las invariantes.
- `product.Delete(DateTime utcNow)` asigna `DeletedAt`. Borrar un producto ya borrado no tiene
  efecto.
- Una invariante violada lanza `DomainException`. Esto no debería ocurrir en la práctica, porque
  Application valida antes; es la última barrera.

**Estados**

```text
[Activo] --Update(isActive=false)--> [Inactivo]
[Inactivo] --Update(isActive=true)--> [Activo]
[Activo | Inactivo] --Delete--> [Borrado]   (terminal en esta funcionalidad)
```

Visibilidad (FR-016): el listado muestra por defecto `Activo`; con el filtro "Mostrar
inactivos" también muestra `Inactivo`. `Borrado` nunca se muestra.

## Money (value object)

Vive en `Pos.Domain/Common/Money.cs`.

- `long Cents`, siempre de 0 a 99,999,999 en esta funcionalidad.
- `Money.FromCents(long)` y `Money.TryParse(string text, out Money)`, que acepta solo
  `^\d{1,6}(\.\d{1,2})?$` después de recortar los espacios de los extremos. Rechaza comas,
  signos, símbolos de moneda, notación científica y más de 2 decimales, y nunca redondea.
- Igualdad por valor. El formato de moneda para mostrar **no** vive en el dominio (ver
  research §17).

## Usuario de auditoría

No hay tabla. El puerto `ICurrentUser` (Application) expone `Guid UserId`. En esta funcionalidad,
`SystemCurrentUser` devuelve el GUID fijo `00000000-0000-7000-8000-000000000001`.

## Respaldo de base de datos

Son archivos, no filas. `IBackupService` los expone como `BackupInfo(Path, Kind, CreatedAtUtc)`.

| `Kind` | Carpeta | Cuándo | Retención |
|---|---|---|---|
| `Automatic` | `backups/auto/` | Al arrancar y al cerrar, si el último tiene más de 24 h | 7 |
| `PreMigration` | `backups/pre-migration/` | Antes de aplicar migraciones pendientes | 5 |
| `Diagnostic` | Solo dentro del zip | Al exportar el diagnóstico | Ninguna |

La base dañada que se reemplaza durante una restauración se conserva en
`backups/corrupt/<yyyyMMdd-HHmmss>Z/`, sin límite.

Nombre de los archivos: `pos-<yyyyMMdd-HHmmss>Z.db`. La fecha se obtiene del nombre, no del
sistema de archivos.

## Paquete de diagnóstico

Un zip con `info.json` (`appVersion`, `os`, `dataDirectory`, `exportedAtUtc`), `logs/*.log`
(últimos 7 días) y `pos.db`, que es un respaldo consistente.

## Tabla de historial de migraciones

`__EFMigrationsHistory` la gestiona EF Core. Se usa para detectar una base más nueva que la
aplicación (research §13).
