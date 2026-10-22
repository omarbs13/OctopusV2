# Data Model: Campos de proveedor de loseta

## Product (cambios)

| Campo | Tipo | Reglas |
|---|---|---|
| `Model` | texto ≤150 en captura (hasta 200 si viene de la migración), obligatorio | Recortado; admite símbolos (`&`, `-`, `/`, paréntesis). Se muestra tal cual. |
| `ModelKey` | texto | Normalizado (mayúsculas, sin acentos, espacios reducidos). Calculado. |
| `Measures` | texto ≤49, nulo | Original tal cual ("33 X 33", "120*240", "1.22 X 2.44 X .03"). |
| `MeasuresKey` | texto, nulo | Sin espacios, `X`/`x`/`*` → `X`. Calculado. |
| `Brand` | texto ≤100, nulo | Normalizado en `BrandSearch`. |
| `SupplierId` | GUID, nulo | Sin clave foránea (como `CategoryId`); proveedor activo al asignarlo. |
| `PackagingThousandths` | long, nulo | Embalaje en m² ×1000; > 0, hasta 3 decimales; "NA"/vacío → nulo. |
| `PiecesPerBox` | int, nulo | > 0 si existe. No deriva precios. |
| `PricePerSqmCents` | long, nulo | X M2, > 0 si existe. |
| `PricePerBoxCents` | long, nulo | X CAJA, > 0 si existe. |
| `PiecePriceCents` | long, nulo | Antes `Price`. Obligatorio si la unidad no es XBX/MTK. |
| `IdentityKey` | texto, nulo | `{SupplierId:N\|-}\|{ModelKey}\|{MeasuresKey}`; nulo solo en duplicados migrados. |
| `NeedsReview` | bool | Duplicado migrado, sin modelo válido, o loseta sin embalaje. |
| `Name` / `NameSearch` | existentes | Derivados: `Model [+ " " + Measures]`. |

**Invariantes**
- Loseta (`UnitCode` ∈ {XBX, MTK}): al menos uno de X M2 / X CAJA.
- No loseta: `PiecePriceCents` > 0 y X M2 / X CAJA / Embalaje ignorados.
- Advertencia (no bloqueo): ambos precios y embalaje y |X CAJA − X M2 × Embalaje| > 2 % de X M2 × Embalaje.
- Índice único filtrado: `IdentityKey` donde `DeletedAt IS NULL AND IdentityKey IS NOT NULL`.
- `Version`, auditoría y borrado lógico sin cambios.

**Índices nuevos**: `IdentityKey` (único filtrado), `BrandSearch`, `MeasuresKey`, `NeedsReview`.

## UnitOfMeasure (cambios)

- Nueva: `MTK` "Metro cuadrado", orden 9, 3 decimales (HasData).
- `XBX` "Caja": 0 → 3 decimales (ver research §6).

## ProductMigrationSummary (nueva, una fila)

`Id`, `MigratedCount`, `WithoutModelCount`, `WithoutPackagingCount`, `DuplicateCount`, `CreatedAtUtc`, `Shown`.

## Sin cambios de esquema

`SaleLine` (usa `UnitCode`, `ProductName`, `UnitPriceCents` ya existentes), inventario, compras.

## Transiciones

- Duplicado migrado: `NeedsReview=1, IdentityKey=NULL` → al guardar con una clave libre y modelo válido: `IdentityKey` calculada, marca quitada.
- Loseta sin embalaje, o modelo de más de 150 caracteres: `NeedsReview=1` solo por la migración → se quita al guardar el producto (sin embalaje = "no aplica").
