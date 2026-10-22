# Research: Campos de proveedor de loseta

## 1. Lo que la spec supone vs. lo que existe

- **Hallazgo**: `Product` no tiene Marca, Acabado, Costo, Formato/Medida, M² por caja ni proveedor. Tiene Nombre,
  SKU, código de barras, `Price`, `UnitCode` (H87, KGM, GRM, LTR, MLT, MTR, XBX, XPK), inventario y categoría.
- **Decisión**: crear Marca, Medidas, Embalaje y Proveedor; no crear Acabado ni Costo.
- **Razón**: Medidas/Embalaje/Marca/Modelo/X M2/X CAJA son los datos de los archivos de proveedor; Acabado y
  Costo no intervienen en ninguna regla ni criterio. El costo de compra ya se guarda por compra (020).
- **Alternativa**: agregarlos por completitud con la lista de la historia 1. Rechazada por YAGNI; se puede
  pedir en una spec posterior. **Pendiente de confirmar con quien pidió la spec.**
- **Consecuencia en la migración**: no hay Formato ni M² por caja que migrar; todos los productos migrados
  quedan con Medidas y Embalaje vacíos. "Sin embalaje" en el resumen cuenta solo productos de caja/m².
  Ningún producto existente tiene unidad m² (no existía), así que solo cambian los de caja.

## 1b. Decisión sobre Acabado y Costo

Se dejan fuera (decisión confirmada al aplicar el análisis). La spec se corrigió para no suponerlos.

## 2. Nombre derivado y modelo

- **Decisión**: `Name` se conserva y se calcula como `Modelo` + (`" " + Medidas` si hay). `NameSearch`
  se calcula del nombre derivado.
- **Razón**: ventas, tickets, reportes, auditoría, alertas y escáner leen `Name`; cambiarlos sería riesgo
  sin beneficio. La descripción de ventas nuevas incluye modelo y medidas sin tocar `SaleLine`.
- **Alternativa**: reemplazar `Name` por `Model` en todas partes. Rechazada por alcance y riesgo.
- Longitudes: Modelo ≤ 150 al capturar, Medidas ≤ 49 (nombre ≤ 200). Los nombres migrados de 151–200 caracteres se conservan completos como Modelo y se marcan "para revisión"; el nombre derivado se trunca a 200.

## 3. Normalización e identidad

- **Decisión**: `ProductText.NormalizeModel` = `TextNormalizer.ForSearch` + mayúsculas + espacios reducidos.
  `NormalizeMeasures` = lo anterior, sin espacios y con `*` y `x` → `X` (sin tocar "x" dentro de números como
  "1.22"). Clave: `{SupplierId:N | "-"}|{modelKey}|{measuresKey}`.
- **Razón**: la comparación en SQLite no distingue acentos; se guarda la clave calculada en C#
  (columna `IdentityKey`) con índice único filtrado `WHERE DeletedAt IS NULL AND IdentityKey IS NOT NULL`.
- **Duplicados migrados**: el más antiguo conserva la clave; los demás quedan con clave nula y
  `NeedsReview = 1`. Al editar uno y volverse único, se calcula su clave y la marca se quita (FR-017a).
- **Alternativa**: índice único sobre columnas crudas. Rechazada: no normaliza acentos ni "X/*".
- Productos sin proveedor usan `-` como proveedor, así la unicidad ocurre solo entre ellos.

## 4. Migración de datos

- **Decisión**: dos pasos. (a) Migración EF: columnas nuevas, `Model = Name`, `PricePerBox = Price` y
  `PiecePrice = NULL` donde `UnitCode = 'XBX'`, `PiecePrice = Price` en los demás, `Price` renombrada y
  nulable (reconstrucción de tabla). (b) `ProductIdentityBackfill` en el arranque, después de migrar y
  dentro de la misma transacción de arranque, calcula claves, marca duplicados y escribe una fila en
  `ProductMigrationSummaries` (`Shown = 0`). No se ejecuta de nuevo si ya hay una fila de resumen.
- **Razón**: la normalización con acentos no es expresable en SQL; el arranque ya respalda antes y restaura
  si algo falla (constitución IV, orden obligatorio).
- **Restaurar un respaldo anterior** pasa por el mismo arranque, así que recibe el mismo tratamiento (FR-022).
- Prueba obligatoria: migrar la base de ejemplo de la versión anterior, comparar conteos y totales.

## 5. Modalidad de venta

- **Decisión**: `SaleMode { Piece, Box, SquareMeter }`. Se agrega la unidad `MTK` "Metro cuadrado" (3
  decimales) al catálogo fijo. Producto de loseta = `UnitCode` ∈ {XBX, MTK}. La partida de venta guarda la
  unidad vendida en `SaleLine.UnitCode` (columna ya existente), por lo que no cambia el esquema de ventas.
- Precio: Box → X CAJA, o X M2 × Embalaje si falta; SquareMeter → X M2, o X CAJA ÷ Embalaje si falta;
  Piece → precio por pieza, sin derivación (clarificación 2). Derivados redondeados al centavo.
  Sin precio ni embalaje para derivarlo, la modalidad no se ofrece.
- Partida: `SaleMath.LineAmount` (cantidad × precio, redondeado) sin cambios: 3 × 287.05 = 861.15;
  4.92 × 175.03 = 861.15.
- **Alternativa**: guardar la modalidad en una columna nueva de `SaleLine`. Rechazada: `UnitCode` ya expresa
  lo vendido y las devoluciones lo usan.

## 6. Inventario con modalidad distinta a la unidad del producto

- **Decisión**: el inventario sigue en la unidad del producto. Si se vende en otra modalidad, el descuento
  es la cantidad convertida con el Embalaje (m² ÷ embalaje = cajas, 3 decimales). Esa conversión exige que el
  producto controle inventario con Embalaje; si no lo tiene, solo se ofrece la modalidad de su unidad. La
  unidad `XBX` pasa a admitir 3 decimales para poder guardar cajas fraccionarias.
- **Riesgo / a verificar al implementar**: el cambio de decimales de `XBX` afecta capturas de movimientos y
  alertas; revisar `Quantity.FitsDecimals` y las pruebas de consistencia de inventario. Devolver m² de una
  venta usa la misma conversión.
- **Alternativa**: inventariar siempre en m². Rechazada: cambia datos existentes.

## 7. Búsqueda y filtros

- **Decisión**: la búsqueda por nombre ya ignora acentos y mayúsculas; se agregan `BrandSearch` y
  `MeasuresKey` al predicado. Un texto con forma de medida ("60 x 60") se compara con `MeasuresKey`.
  Filtros nuevos: Marca (lista distinta de marcas), Medida (lista distinta) y "Para revisión", con índices en
  esas columnas. "fort wood" encuentra todas las medidas porque el nombre derivado contiene el modelo.

## 8. Resumen de migración

- **Decisión**: tabla de una fila `ProductMigrationSummaries` (conteos + `Shown`). Tras iniciar sesión, si
  `Shown = 0` se muestra un diálogo y se marca visto al cerrarlo. Se registra también en el log (VIII).

## 9. Reportes (009)

- Se agregan Modelo, Medidas, Embalaje, X M2 y X CAJA al reporte de productos y a su exportación; los totales
  no cambian porque no se toca el cálculo.

## 10. Línea base (T001)

Antes de empezar: `dotnet build -v q` sin advertencias y `dotnet test --verbosity quiet` en verde (1,400 pruebas,
0 fallas, 4 omitidas — entre ellas el generador de bases de ejemplo). Al terminar: 1,509 pruebas, 0 fallas.

## 11. Normalización reutilizada (T002)

`TextNormalizer.ForSearch` (`src/Pos.Domain/Common/`) descompone en FormD, quita las marcas no espaciadas y pasa
a minúsculas invariantes. `ProductText.NormalizeModel` lo reutiliza (más mayúsculas y espacios reducidos) y
`NormalizeMeasures` agrega quitar espacios y unificar `x`, `X`, `×` y `*`. La "Ñ" se descompone en "N" + tilde y
`ForSearch` la quita, igual que ya hacía la búsqueda por nombre ("Ñandú" = "Nandu").

## 12. Decisiones tomadas al implementar

- **Migración en tres pasos** (esquema, datos SQL, `ProductIdentityBackfill`): SQLite no admite SQL propio
  mientras hay una reconstrucción de tabla pendiente; ver `docs/migraciones.md`.
- **`Product.Price` se conserva como propiedad derivada** (no persistida): precio de la modalidad de la unidad
  (X CAJA, X M2 o precio por pieza). Así reportes, escáner y pruebas anteriores siguen leyendo un precio sin
  cambios. La columna `PriceCents` pasó a ser el precio por pieza (nulable).
- **Importes del formulario como texto** (`PiecePriceText`, `PricePerSqmText`, `PricePerBoxText`,
  `PackagingText`): igual que el alta anterior, para señalar el campo con error; el contrato decía centavos.
- **Retorno al inventario por proporción** (`SaleStockRestore`): el movimiento `SALE` guarda lo que salió en la
  unidad del producto; cancelar o devolver reparte ese movimiento en proporción a lo devuelto, de forma
  acumulada, sin residuos y con el mismo resultado que antes cuando la unidad vendida es la del producto.
  Requirió `IInventoryRepository.GetMovementQuantitiesAsync`.
- **Venta de losetas con inventario**: solo se ofrecen las modalidades convertibles a la unidad del producto
  (m² ↔ cajas con embalaje; piezas → cajas con piezas por caja); sin embalaje, solo la de su unidad.

## 13. Precio fuera del punto de venta (T054)

Revisión de `Product.Price`: las únicas lecturas fuera de ventas eran el listado de productos
(`ProductListItemDto.PriceCents`, ahora derivado de los tres precios con `ProductPricing.DisplayPrice`) y la
auditoría (reemplazada por los campos nuevos). Existencias, escáner e inventario no muestran precio.
