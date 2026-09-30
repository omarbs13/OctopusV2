# Research: Manejo de inventario de productos

Decisiones técnicas de la fase 0. No quedan NEEDS CLARIFICATION.

## 1. Representación de cantidades

- **Decision**: value object `Quantity` en `Pos.Domain.Common`. Guarda un `long` en milésimas
  (`Thousandths`) y se almacena en SQLite como `INTEGER`. La unidad de medida indica cuántos
  decimales se aceptan al capturar (0 o 3). `Quantity.Parse(text, decimalPlaces)` nunca redondea,
  igual que `Money.Parse`.
- **Rationale**:
  - EF Core con SQLite guarda `decimal` como `TEXT`. Con eso, `SUM`, `<=` y `ORDER BY` se
    evalúan mal o no se traducen, y el filtro por estado (FR-020) y la prueba de consistencia
    (SC-001) dependen justo de comparar y sumar en SQL.
  - Un entero en milésimas suma y compara exacto. Además sigue el mismo criterio que el dinero
    en centavos (Principio IV).
- **Alternatives considered**:
  - `decimal` con conversión a `TEXT`: las comparaciones en SQL son incorrectas.
  - `double`: no es exacto; 0.1 + 0.2 ≠ 0.3.
  - Guardar enteros para pieza y milésimas para el resto: dos escalas en una misma columna invitan
    a errores y complican cambiar de unidad un producto sin movimientos.

## 2. Límites de cantidad

- **Decision**:
  - Una cantidad capturada va de 0.001 (o 1 en unidades enteras) a 9,999,999.999
    (`Quantity.MaxCaptureThousandths = 9_999_999_999`).
  - La existencia no puede superar 999,999,999.999. Una entrada que la excedería se rechaza con
    mensaje.
- **Rationale**: evita desbordes y cifras absurdas por error de captura (un código de barras
  pegado en el campo de cantidad). Sigue el mismo patrón que `Money.MaxCents`.
- **Alternatives considered**: sin límite. Un error de dedo podría dejar existencias
  inservibles, y la única forma de corregirlas sería con otro movimiento igual de grande.

## 3. Decimales por unidad de medida

- **Decision**: se agrega `DecimalPlaces` (0 o 3) a `UnitOfMeasure` y a la tabla
  `UnitsOfMeasure` sembrada con `HasData`:

  | Unidad | Decimales |
  |---|---|
  | KGM Kilogramo | 3 |
  | LTR Litro | 3 |
  | MTR Metro | 3 |
  | H87 Pieza, GRM Gramo, MLT Mililitro, XBX Caja, XPK Paquete | 0 |

- **Rationale**:
  - El catálogo de 003 ya existe, es obligatorio en todo producto y tiene 8 unidades, no las 5
    de la descripción original. Reducirlo obligaría a migrar datos de clientes.
  - Gramo y mililitro se cuentan en enteros en la práctica: 0.5 g no aparece en un POS.
  - La regla vive en el catálogo; no se escribe una lista de claves en el código de negocio.
- **Alternatives considered**:
  - Una unidad "solo para inventario" separada de la de venta: duplica el concepto y exige
    factores de conversión, que quedan fuera de alcance.
  - Tratar gramo y mililitro con 3 decimales: no aporta nada y permite capturar 0.001 g.

## 4. Dónde vive la existencia actual

- **Decision**: tabla aparte `ProductStocks` (1 a 0..1 con `Products`). El agregado
  `ProductStock` lleva `OnHand`, `MovementCount` y `Version`. La fila se crea con el primer
  movimiento; si no existe, la existencia es 0 y el producto no tiene movimientos.
  - La configuración de inventario (`TracksInventory`, `MinimumStock`) sí vive en `Products`,
    porque se edita en el formulario del producto.
- **Rationale**:
  - Si la existencia viviera en `Products`, cada movimiento incrementaría `Products.Version`.
    El editor de productos (que usa concurrencia optimista con la versión que vio el operador)
    reportaría conflictos falsos al guardar un producto que recibió mercancía mientras el
    formulario estaba abierto. Las pantallas conservan su estado, así que ese escenario es
    realista.
  - "Sin fila" = "sin movimientos" hace trivial la regla del inventario inicial (FR-015) y la
    del bloqueo de unidad (FR-006).
- **Alternatives considered**:
  - `Products.OnHand` excluido de la auditoría: requiere excepciones en `AuditingInterceptor` y
    deja la versión del producto sin reflejar un cambio real.
  - Calcular la existencia con `SUM` en cada consulta: con 100,000 movimientos, filtrar por
    estado sobre 10,000 productos se vuelve lento y no hay dónde apoyar la concurrencia.

## 5. Transacción y concurrencia al registrar un movimiento

- **Decision**:
  - `RegisterMovement` abre una transacción de escritura (`BEGIN IMMEDIATE`) antes de leer
    producto y existencia; valida, crea el movimiento, actualiza `ProductStock` y confirma.
    Cualquier excepción revierte todo (criterio de aceptación 1).
  - Doble barrera en la base:
    - `ProductStocks.Version` es token de concurrencia.
    - El índice único `(ProductId, Sequence)` en `InventoryMovements` impide dos movimientos con
      el mismo número de secuencia.
  - La edición de producto (`UpdateProduct`) usa la misma transacción de escritura para comprobar
    "tiene movimientos" y guardar, así que no puede colarse un movimiento entre la comprobación y
    el cambio de unidad.
- **Rationale**:
  - En SQLite, una transacción diferida que lee y después escribe puede fallar con
    `SQLITE_BUSY` o, peor, validar contra una existencia que otro escritor ya cambió.
  - `BEGIN IMMEDIATE` toma el candado de escritura desde el inicio y serializa a los escritores.
    Con `DefaultTimeout = 5` el segundo espera en lugar de fallar.
  - `Microsoft.Data.Sqlite` inicia `BeginTransaction()` sin `deferred` como `BEGIN IMMEDIATE`, y
    `Database.BeginTransactionAsync()` de EF Core lo usa. Una prueba de infraestructura lo
    verifica con dos contextos concurrentes (ver quickstart).
  - Se expone como puerto `IWriteTransactions` en Application, con implementación en
    Infrastructure; los casos de uso no ven EF Core.
- **Alternatives considered**:
  - Solo concurrencia optimista con reintento: obliga a reintentar en la UI y deja más código de
    error que no aporta a un solo equipo.
  - Triggers en SQLite: contradice "sin scripts SQL manuales de esquema" (Principio IV).

## 6. Orden y secuencia de movimientos

- **Decision**:
  - Cada movimiento lleva `Sequence` (1, 2, 3…) por producto, asignado desde
    `ProductStock.MovementCount + 1`.
  - El kárdex de un producto se ordena por `Sequence` descendente.
  - El historial general se ordena por `CreatedAt` descendente y después por `Id` descendente.
- **Rationale**:
  - `Guid.CreateVersion7` no es monótono dentro del mismo milisegundo, y `CreatedAt` puede
    empatar, así que ninguno de los dos da un orden confiable del kárdex.
  - La secuencia da un orden exacto y además protege la integridad por su índice único.
  - La prueba de consistencia recorre la secuencia y verifica que cada `ResultingStock` sea el
    anterior ± la cantidad.
- **Alternatives considered**: ordenar solo por fecha. Dos movimientos del mismo milisegundo
  harían que el kárdex mostrara existencias resultantes fuera de orden.

## 7. Tipos de movimiento en la base

- **Decision**: enum `MovementType` con códigos de texto estables: `INITIAL`, `RECEIPT`,
  `ADJUST_IN` y `ADJUST_OUT`. Se guarda como `TEXT` con una conversión explícita. La cantidad se
  guarda siempre positiva y el signo lo da el tipo.
- **Rationale**:
  - El texto sobrevive a reordenar el enum y se lee claro en un respaldo abierto por soporte
    (Principio VIII).
  - El futuro módulo de ventas agregará `SALE` sin tocar los valores existentes.
- **Alternatives considered**:
  - Entero: frágil ante cambios del enum.
  - Cantidad con signo: permitiría capturar entradas negativas, y el sentido ya lo da el tipo
    (FR-007).

## 8. Estado de existencia y consultas

- **Decision**:
  - `StockStatus` (`Normal`, `Low`, `Out`) se calcula en el dominio con
    `StockStatusRule.Evaluate(onHand, minimum)`.
  - La misma regla se expresa como predicado en la consulta de Infrastructure, para filtrar y
    contar en SQL:
    - `Out`: `OnHand = 0`, o sin fila en `ProductStocks`.
    - `Low`: `OnHand > 0` y `MinimumStock` no nulo y `OnHand <= MinimumStock`.
  - Una prueba de infraestructura compara ambos cálculos sobre los mismos datos.
- **Rationale**:
  - SC-005 exige paginar y filtrar en la base.
  - SC-006 exige que las tarjetas cuenten con el mismo predicado que usa el listado. Ambas
    consultas comparten una función de filtro en el repositorio.
- **Alternatives considered**: guardar el estado en una columna. Es un dato derivado más que
  mantener sincronizado.

## 9. Navegación con argumento (tarjetas y "ver historial")

- **Decision**:
  - `Navigator.NavigateAsync(string entryId, object? argument = null)`.
  - Si la pantalla destino implementa `INavigationArgumentReceiver`, recibe el argumento antes de
    `OnActivatedAsync`, aunque ya sea la pantalla actual.
  - Argumentos:
    - `StockFilter.Low` y `StockFilter.Out` para Existencias.
    - `MovementsProductFilter(productId, name)` para Movimientos.
  - `DashboardCard` agrega `NavigationArgument`.
- **Rationale**: los criterios de aceptación 6 y FR-019 piden llegar a pantallas ya filtradas. El
  navegador actual solo recibe el id de la opción.
- **Alternatives considered**: un servicio de "filtro pendiente" compartido. Es un estado global
  implícito y más difícil de razonar.

## 10. Usuario del movimiento

- **Decision**:
  - El movimiento guarda `CreatedBy` (el `ICurrentUser.UserId`), que asigna el
    `AuditingInterceptor` existente.
  - Para mostrarlo, Application resuelve el nombre: el usuario de sistema se muestra como
    "Sistema"; cualquier otro, con los primeros 8 caracteres de su id, hasta que exista el módulo
    de usuarios.
  - `SystemUser` (id y nombre) pasa a `Pos.Application.Abstractions` para que Application no
    dependa de Infrastructure.
- **Rationale**: todavía no hay tabla de usuarios. No se inventa una; el día que exista, solo
  cambia el resolvedor.
- **Alternatives considered**: una tabla `Users` mínima. Queda fuera de alcance (YAGNI).

## 11. Campos de auditoría del movimiento (desviación justificada)

- **Decision**:
  - `InventoryMovements` lleva `CreatedAt` y `CreatedBy`, pero no `UpdatedAt`, `UpdatedBy`,
    `DeletedAt` ni `Version`.
  - `ProductStocks` lleva auditoría completa y `Version`, pero no `DeletedAt`: su ciclo de vida
    es el del producto.
- **Rationale**:
  - FR-010 declara el movimiento inmutable, y el propio Principio IV prohíbe borrar movimientos
    de inventario.
  - Columnas que nunca se escriben serían engañosas: sugieren que un movimiento puede editarse o
    borrarse.
  - La inmutabilidad se hace cumplir:
    - En el dominio: sin setters públicos ni métodos de modificación.
    - En el repositorio: no ofrece `Update` ni `Remove`.
    - Con una prueba de persistencia que verifica que modificar o borrar un movimiento por el
      contexto lanza excepción. Un guardián en `SaveChanges` rechaza entradas `Modified` o
      `Deleted` de `InventoryMovement`.
- **Alternatives considered**: incluir las columnas por uniformidad. Contradice FR-010 y agrega
  una vía de anulación que la spec ya resuelve con un movimiento contrario.

## 12. Productos existentes y migración

- **Decision**: migración `InventoryManagement`:
  - `Products`: `TracksInventory INTEGER NOT NULL DEFAULT 0` y `MinimumStock INTEGER NULL`. Se
    agregan con `ALTER TABLE ADD COLUMN`, sin reconstruir la tabla.
  - `UnitsOfMeasure`: `DecimalPlaces INTEGER NOT NULL DEFAULT 0`, más `UpdateData` a 3 para KGM,
    LTR y MTR.
  - Tablas nuevas `ProductStocks` e `InventoryMovements` con sus índices.
  - Se genera la base de ejemplo `v0.3.0.db`, y la prueba de actualización cubre `v0.1.0` y
    `v0.2.0` hasta la versión actual (FR-023).
- **Rationale**: todos los productos existentes quedan como "no controla inventario", sin
  existencia ni movimientos. Es el comportamiento que pide FR-023.
- **Alternatives considered**: ninguna; es el cambio mínimo.

## 13. Rendimiento

- **Decision**: índices:
  - `InventoryMovements (ProductId, Sequence)`, único.
  - `InventoryMovements (CreatedAt, Id)`, para el historial general y el rango de fechas.
  - `InventoryMovements (Type, CreatedAt)`, para el filtro por tipo.
  - `Products (TracksInventory, IsActive)`, filtrado a `DeletedAt IS NULL`.

  Prueba de rendimiento con 10,000 productos y 100,000 movimientos, siguiendo el patrón de
  `ProductPerformanceTests`.
- **Rationale**: SC-005 pide menos de 2 s por página. `COUNT` y `LIMIT/OFFSET` sobre 100,000
  filas indexadas en SQLite quedan muy por debajo.
- **Alternatives considered**: paginación por cursor. Es innecesaria a este volumen y rompe el
  patrón de "página N de M" ya establecido en Productos.
