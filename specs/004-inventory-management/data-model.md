# Data Model: Manejo de inventario de productos

Amplía el modelo de 003. Todas las cantidades son `Quantity`: un `long` en milésimas que se guarda
como `INTEGER` (research §1). Las fechas se guardan en UTC.

## Quantity (value object, `Pos.Domain.Common`) ➕

| Miembro | Descripción |
|---|---|
| `long Thousandths` | Cantidad en milésimas; 1 pieza = 1000 y 1.250 kg = 1250 |
| `Zero` | 0 |
| `MaxCaptureThousandths` | 9,999,999.999 (research §2) |
| `MaxStockThousandths` | 999,999,999.999 |
| `Parse(string? text, int decimalPlaces, bool allowZero = false) → QuantityParseResult` | Recorta espacios. Acepta dígitos con punto decimal opcional y separador de miles con coma, igual que `Money`. Nunca redondea. `allowZero` se usa en la existencia mínima; sin él, 0 devuelve `NotPositive` |
| `FitsDecimals(int decimalPlaces)` | Indica si no tiene más decimales que los permitidos (`Thousandths % 10^(3-dp) == 0`) |
| `ToEditableString(int decimalPlaces)` | "12" o "1.250" |
| `+`, `-`, comparación | Aritmética exacta sobre milésimas |

**Persistencia**: EF Core no traduce a SQL comparaciones sobre una propiedad con conversor, así que las entidades
guardan las milésimas en propiedades `long` (`OnHandThousandths`, `QuantityThousandths`,
`ResultingStockThousandths`, `MinimumStockThousandths`) mapeadas a las columnas `OnHand`, `Quantity`,
`ResultingStock` y `MinimumStock`; `Quantity` es una propiedad calculada sobre ellas.

`QuantityParseError` puede valer:

- `Empty`
- `Format`
- `TooManyDecimals`: "las piezas no admiten decimales" o "máximo 3 decimales".
- `TooLarge`
- `NotPositive`: la cantidad 0 en un movimiento. Para la existencia mínima, 0 sí es válido.

## UnitOfMeasure ✏️ (`Pos.Domain.Products`, tabla `UnitsOfMeasure`)

| Campo | Tipo | Cambio |
|---|---|---|
| `Code` | TEXT(3) PK | — |
| `Name` | TEXT(50) | — |
| `SortOrder` | INTEGER | — |
| `DecimalPlaces` | INTEGER NOT NULL DEFAULT 0 | ➕ 3 para KGM, LTR y MTR; 0 para las demás (research §3) |

Sigue sembrada con `HasData`, así que la migración emite `UpdateData` para las tres unidades con
decimales.

## Product ✏️ (`Pos.Domain.Products`, tabla `Products`)

| Campo | Tipo | Reglas |
|---|---|---|
| `TracksInventory` | bool, INTEGER NOT NULL DEFAULT 0 | ➕ FR-001. Por omisión `false`, y así quedan los productos existentes (FR-023) |
| `MinimumStock` | `Quantity?`, INTEGER NULL (milésimas) | ➕ FR-004. Opcional y ≥ 0; debe respetar los decimales de `UnitCode` (FR-003). Si `TracksInventory = false`, se guarda nulo |

Cambios de comportamiento:

- `Create(...)` y `Update(...)` reciben `tracksInventory` y `minimumStock`.
- `Update(...)` recibe además `hasMovements`. Con `hasMovements = true` lanza `DomainException` si
  cambia `UnitCode` o si `TracksInventory` pasa de `true` a `false` (FR-006). Application valida
  lo mismo antes y devuelve un `ValidationFailed` de campo.
- Predicados estáticos en el estilo actual:
  - `IsValidMinimumStock(Quantity? minimum, UnitOfMeasure unit)`.
  - `CanChangeInventorySettings(bool hasMovements, string currentUnit, string newUnit, bool currentTracks, bool newTracks)`.

Índice nuevo: `IX_Products_TracksInventory` sobre `(TracksInventory, IsActive)` con filtro
`DeletedAt IS NULL`.

## ProductStock ➕ (`Pos.Domain.Inventory`, tabla `ProductStocks`)

Existencia actual de un producto. La fila se crea con su primer movimiento; si no existe, la
existencia es 0 y el producto no tiene movimientos (research §4).

| Campo | Tipo | Reglas |
|---|---|---|
| `ProductId` | GUID, PK y FK → `Products.Id` (Restrict) | 1 a 0..1 con el producto |
| `OnHand` | `Quantity`, INTEGER NOT NULL | ≥ 0 y ≤ `MaxStockThousandths` (FR-011) |
| `MovementCount` | INTEGER NOT NULL | Número de movimientos; la secuencia del siguiente es `MovementCount + 1` |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | auditoría | Los asigna `AuditingInterceptor` |
| `Version` | INTEGER, token de concurrencia | Se incrementa en cada movimiento |

No tiene `DeletedAt`: su ciclo de vida es el del producto (research §11).

**Operación del dominio**:
`ProductStock.Record(MovementType type, Quantity quantity, UnitOfMeasure unit, bool productActive, bool tracksInventory, string? reason, string? reference) → InventoryMovement`

Lanza `DomainException` si:

- El producto no controla inventario o está inactivo (FR-014).
- La cantidad es ≤ 0 o tiene más decimales que la unidad (FR-003, FR-008).
- El motivo está vacío en `AdjustIn` o `AdjustOut` (FR-009).
- El tipo es `Initial` y `MovementCount > 0` (FR-015).
- `AdjustOut` dejaría `OnHand < 0` (FR-011).
- El resultado supera `MaxStockThousandths`.

Si todo es válido, actualiza `OnHand` y `MovementCount` y devuelve el movimiento con
`ResultingStock = OnHand`.

`ProductStock.Start(Guid productId)` crea una existencia en 0, sin movimientos, justo antes de
registrar el primero.

Predicados estáticos para que Application valide antes de llegar al dominio:
`CanRecordInitial(int movementCount)` y `WouldGoNegative(Quantity onHand, Quantity quantity)`.

## InventoryMovement ➕ (`Pos.Domain.Inventory`, tabla `InventoryMovements`)

Registro inmutable (FR-010). Solo lo crea `ProductStock.Record`, y no tiene métodos que lo
modifiquen.

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | GUID v7 PK | Generado en la aplicación |
| `ProductId` | GUID, FK → `Products.Id` (Restrict) | — |
| `Sequence` | INTEGER NOT NULL | 1, 2, 3… por producto; índice único `(ProductId, Sequence)` (research §6) |
| `Type` | TEXT(12) NOT NULL | `INITIAL`, `RECEIPT`, `ADJUST_IN` o `ADJUST_OUT` (research §7) |
| `Quantity` | INTEGER NOT NULL (milésimas) | > 0; el signo lo da el tipo |
| `ResultingStock` | INTEGER NOT NULL (milésimas) | Existencia después del movimiento, ≥ 0 |
| `Reason` | TEXT(250) NULL | Obligatorio en ajustes (FR-009); se recorta y vacío equivale a nulo |
| `Reference` | TEXT(50) NULL | Opcional en todos los tipos (FR-009a); se recorta |
| `CreatedAt` | TEXT (UTC) NOT NULL | Fecha del movimiento; la asigna la persistencia con `IClock` y el operador no la captura (FR-008) |
| `CreatedBy` | GUID NOT NULL | Usuario actual (FR-008) |

Sin `UpdatedAt`, `UpdatedBy`, `DeletedAt` ni `Version`: es una desviación justificada (research
§11). La persistencia rechaza cualquier entrada `Modified` o `Deleted` de este tipo.

Índices:

- `IX_InventoryMovements_Product_Sequence (ProductId, Sequence)`, único.
- `IX_InventoryMovements_CreatedAt (CreatedAt, Id)`.
- `IX_InventoryMovements_Type_CreatedAt (Type, CreatedAt)`.

## MovementType (enum, `Pos.Domain.Inventory`) ➕

| Valor | Código | Efecto | Motivo | Etiqueta en la interfaz |
|---|---|---|---|---|
| `Initial` | `INITIAL` | + | opcional | Inventario inicial |
| `Receipt` | `RECEIPT` | + | opcional | Entrada |
| `AdjustIn` | `ADJUST_IN` | + | obligatorio | Ajuste positivo |
| `AdjustOut` | `ADJUST_OUT` | − | obligatorio | Ajuste negativo |

Extensiones: `IsIncrease()` y `RequiresReason()`.

## StockStatus (enum, `Pos.Domain.Inventory`) ➕

`StockStatusRule.Evaluate(Quantity onHand, Quantity? minimum)`:

| Estado | Condición |
|---|---|
| `Out` | `onHand == 0` |
| `Low` | `onHand > 0` y `minimum` no nulo y `onHand <= minimum` |
| `Normal` | en cualquier otro caso |

La consulta de Infrastructure replica el predicado en SQL: sin fila en `ProductStocks` equivale a
`OnHand = 0` (research §8).

## Invariantes de consistencia (prueba obligatoria, Principio VI)

Para todo producto con fila en `ProductStocks`:

1. `OnHand = Σ(+Quantity de INITIAL, RECEIPT y ADJUST_IN) − Σ(Quantity de ADJUST_OUT)` (FR-012,
   SC-001).
2. `MovementCount = COUNT(movimientos)`, y las secuencias son exactamente 1..`MovementCount`.
3. `ResultingStock` del movimiento `n` = `ResultingStock(n−1) ± Quantity(n)`, y el del último es
   igual a `OnHand`.
4. Ningún `ResultingStock` es negativo.
5. Solo el movimiento con `Sequence = 1` puede ser `INITIAL`.
6. `Quantity` y `ResultingStock` respetan los decimales de la unidad del producto (SC-007).

## Relaciones

```text
UnitsOfMeasure 1 ──< Products 1 ──o ProductStocks
                        │
                        └──< InventoryMovements
```

## Migración `InventoryManagement`

Revisar el SQL generado:

- Todo debe ser `ALTER TABLE ... ADD COLUMN`, `CREATE TABLE`, `CREATE INDEX` o `UPDATE`
  (`UpdateData`).
- No debe reconstruir `Products`: las dos columnas nuevas son nullable o tienen valor por
  defecto.

Se genera la base de ejemplo `tests/Pos.Infrastructure.Tests/SampleDatabases/v0.3.0.db`. Debe
incluir un producto con inventario y movimientos de los cuatro tipos.
