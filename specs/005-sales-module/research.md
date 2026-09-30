# Research: Módulo de ventas

**Feature**: `005-sales-module` | **Date**: 2026-09-30 | **Plan**: [plan.md](plan.md)

Cada sección registra una decisión, su justificación y las alternativas descartadas. No queda
ningún NEEDS CLARIFICATION.

## §1. Dónde vive la venta en curso y sus cálculos

**Decision**: la venta en curso es un objeto de dominio puro, `Cart` (`Pos.Domain/Sales`). Agrega,
incrementa, cambia cantidad, quita líneas y calcula importes y total. El ViewModel del Punto de
venta conserva una instancia y solo presenta su estado. El cobro usa otro objeto de dominio puro,
`Checkout` (§6).

**Rationale**: el Principio III prohíbe calcular totales en ViewModels. Como es un objeto en
memoria, capturar no necesita la base de datos, así que agregar una línea responde de inmediato
(SC-002). Desktop ya usa tipos de Domain para vistas previas (`MovementEditorViewModel`), y las
pruebas de arquitectura lo permiten.

**Alternatives considered**:

- Un caso de uso por cada tecla (agregar o quitar línea): obliga a ir a la base por cada escaneo y
  a guardar un estado intermedio en el servidor.
- Calcular en el ViewModel: viola el Principio III.

## §2. Importe de línea y redondeo (FR-028)

**Decision**: `SaleMath.LineAmount(Quantity, Money)`:

- Calcula `cantidad(milésimas) × precio(centavos) / 1000` en `long` con `checked`.
- Redondea "mitad hacia arriba" (mitad se aleja de cero; como todo es positivo, `(p + 500) / 1000`).
- Se redondea una sola vez por línea. El total es la suma exacta de los importes de línea.
- El importe de línea y el total no pueden exceder `Money.MaxCents` ($999,999.99). Una cantidad
  que lo exceda se rechaza con un mensaje claro.

**Rationale**: la cantidad (en milésimas) y el precio (en centavos) son enteros, así que el producto
es exacto antes de redondear. El máximo de captura (9,999,999,999 × 99,999,999) cabe en `long`.
Limitar el total al máximo de `Money` mantiene el value object sin cambios.

**Alternatives considered**:

- `decimal` con `MidpointRounding.AwayFromZero`: da el mismo resultado, pero mezcla tipos. Los
  enteros son el criterio del proyecto.
- Redondeo bancario: contradice la regla documentada en Assumptions.

## §3. Existencia negativa por ventas (FR-025, FR-026)

**Decision**: nuevo value object con signo, `StockLevel` (`Pos.Domain/Inventory`):

- Guarda milésimas `long` en el rango ±`Quantity.MaxStockThousandths`.
- `ProductStock.OnHand` e `InventoryMovement.ResultingStock` pasan de `Quantity` a `StockLevel`.
- La columna `OnHand` ya es `INTEGER` con signo, así que no cambia el esquema.
- `Quantity` sigue sin negativos: es la cantidad capturada.

Reglas:

- `ProductStock.RecordSale(...)` puede dejar la existencia negativa.
- `Record(AdjustOut)` sigue rechazando todo lo que deje la existencia bajo cero. Con existencia
  negativa, cualquier salida manual se rechaza.
- `StockStatusRule.Evaluate` trata `<= 0` como "sin existencia". El predicado SQL de
  `InventoryRepository.FilterByStatus` cambia de `== 0` a `<= 0`, y el de "baja" y "normal" exige
  `> 0` como ya lo hacía.
- El mensaje `WouldGoNegative` y el formato de cantidades aceptan negativos. `QuantityConverter`
  ya los formatea.

**Rationale**: si se permitieran negativos en `Quantity`, se debilitaría la validación de todas las
capturas. Con un tipo separado, el compilador obliga a revisar cada lugar que lee la existencia.

**Alternatives considered**:

- Bloquear la venta sin existencia: contradice FR-025.
- Guardar la existencia real aparte del "disponible": duplica el estado y rompe FR-027.

## §4. Registro atómico, folio consecutivo e idempotencia (FR-020 a FR-022)

**Decision**: `ConfirmSaleHandler` ejecuta todo en una transacción de `IWriteTransactions`
(`BEGIN IMMEDIATE`):

1. Recarga los productos de las líneas y verifica precios, estado y configuración (§5).
2. Calcula el folio como `MAX(FolioNumber) + 1`. `Sales.FolioNumber` tiene un índice único.
3. Por cada línea con inventario, llama a `ProductStock.RecordSale` y agrega el movimiento `SALE`.
   La referencia del movimiento es el folio.
4. Agrega la venta, sus líneas y sus pagos.
5. Borra la fila del borrador (§7).
6. Guarda cambios y confirma la transacción.

Para la idempotencia:

- Cada borrador tiene un `DraftId` (GUID v7) que viaja en el comando y se guarda en
  `Sales.DraftId`, con índice único.
- Si ya existe una venta con ese `DraftId`, el caso de uso devuelve esa venta
  (`AlreadyRegistered`, con el folio) y no crea otra.
- Además, el comando de la UI se deshabilita mientras corre.

**Rationale**:

- `BEGIN IMMEDIATE` serializa a los escritores, así que `MAX + 1` no tiene carreras. El índice
  único es la segunda barrera (SC-005).
- Un intento fallido revierte todo, así que no consume folio ni deja huecos.
- El índice único de `DraftId` cubre el doble toque y el reintento después de un error de red o de
  disco en el que la venta sí quedó registrada.

**Alternatives considered**:

- Una tabla contador: es otra fila que mantener y no aporta nada frente a `MAX` con índice.
- Un autoincremental de SQLite: el Principio IV lo prohíbe para entidades de negocio, y además deja
  huecos si se revierte la transacción.
- Solo deshabilitar el botón: no protege contra reintentos ni contra cierres a mitad.

## §5. Precio y disponibilidad al confirmar (FR-007, Edge Cases)

**Decision**: el paso "Cobrar" tiene dos momentos.

1. **Revisión previa** (`ReviewSale`), al pulsar Cobrar. Recarga los productos y devuelve, por
   línea:
   - El precio vigente.
   - Si el producto ya no se puede vender (inactivo o borrado).
   - Si falta existencia (cantidad mayor que la existencia de un producto con inventario).

   El ViewModel aplica los precios al `Cart` (`Cart.ApplyCurrentPrices`), que recalcula el total.
   Señala las líneas no vendibles y bloquea el cobro hasta quitarlas. Si falta existencia, muestra
   la advertencia y pide confirmación antes de abrir el cobro.
2. **Confirmación** (`ConfirmSale`). Repite la verificación dentro de la transacción. Si un precio
   difiere del que envió el cliente, o una línea dejó de ser vendible, responde `SaleChanged` sin
   guardar nada. La UI entonces vuelve a revisar y muestra el total nuevo antes de cobrar. La falta
   de existencia no bloquea la confirmación, porque ya se advirtió.

**Rationale**: el operador siempre ve el total que va a cobrar. La base no acepta un precio viejo
aunque cambie entre la revisión y la confirmación (con una sola terminal es improbable, pero el
costo es bajo).

**Alternatives considered**:

- Tomar en silencio el precio vigente al confirmar: el total cobrado no coincidiría con el que vio
  el operador.
- Congelar el precio del momento de agregar: contradice el Edge Case de la spec.

## §6. Reglas de pago (FR-014 a FR-018)

**Decision**: el value object `Checkout(total)` con una lista de pagos:

- A lo más un pago en efectivo, con `Received`. Cero o más pagos con tarjeta o transferencia, con
  `Amount` y `Reference` opcional (hasta 50 caracteres).
- Cada pago no en efectivo debe ser mayor que 0 y no exceder el saldo pendiente en el momento de
  agregarlo. La suma de los pagos no en efectivo nunca excede el total.
- Efectivo aplicado = total − pagos no en efectivo. Cambio = recibido − efectivo aplicado. El cobro
  se puede confirmar solo si el pendiente es 0 y el cambio es ≥ 0.
- Faltante = total − (no efectivo + recibido), cuando es positivo.
- Monto rápido: "exacto" pone como recibido el saldo pendiente. Un billete (20, 50, 100, 200, 500 o
  1000) reemplaza el recibido por ese valor; no lo acumula.
- En `SalePayments` se guarda: `Method`, `AmountCents` (monto aplicado a la venta), `ReceivedCents`
  y `ChangeCents` (solo en efectivo), y `Reference`. La suma de `AmountCents` es igual al total.

**Rationale**: con un solo pago en efectivo, "solo el efectivo da cambio" queda sin ambigüedad y
el reparto es determinista. Si se guarda el monto aplicado, los reportes por forma de pago cuadran
con el total.

**Alternatives considered**:

- Varios pagos en efectivo: no aportan nada en mostrador y complican el cálculo del cambio.
- Guardar solo lo recibido: los cortes por forma de pago sumarían más que las ventas.

## §7. Borrador de la venta en curso (FR-010 a FR-013, SC-004)

**Decision**: el borrador vive en SQLite, en la tabla `SaleDrafts`:

- Una sola fila (`Slot = 1`) con `DraftId`, `LinesJson` (producto, cantidad y precio visto) y
  `UpdatedAt`.
- Se guarda con `SaveSaleDraft` (una sola escritura con upsert) después de cada cambio relevante.
- En Desktop, `DraftAutosaver` guarda tras cada cambio, sin debounce, y serializa las escrituras: una a la vez y siempre la más reciente.
  Así los escaneos rápidos no saturan la base.
- Un error al guardar se registra y no interrumpe la captura (FR-013).
- `SaveSaleDraft` ignora un borrador cuyo `DraftId` ya tiene una venta registrada. Así, una
  escritura atrasada no revive una venta cobrada.
- `ConfirmSale` borra el borrador en la misma transacción que registra la venta. Antes de
  confirmar, el ViewModel espera a que termine la escritura pendiente.

**Rationale**:

- La transacción de SQLite en WAL es durable ante cortes de luz, a diferencia de un archivo JSON
  con renombrado. Sobre todo, permite borrar el borrador de forma atómica junto con la venta, así
  que al reabrir después de un cierre durante la confirmación hay exactamente uno de dos estados:
  venta completa sin borrador, o borrador sin venta (Edge Case).
- Los pagos capturados no se guardan en el borrador. Se capturan en segundos y la spec solo pide
  líneas y cantidades.

**Alternatives considered**:

- Un archivo en `IPreferencesStore`: no se puede borrar en la misma transacción que la venta, así
  que podría ofrecer recuperar una venta ya cobrada.
- Tablas normalizadas de líneas del borrador: más esquema para datos temporales. JSON es suficiente
  y no se consulta.

## §8. Búsqueda para vender y escaneo rápido

**Decision**:

- `FindProductsForSale(text)` busca primero una coincidencia exacta de código de barras o de SKU.
  Si no hay, busca por nombre (misma normalización que Productos), con un máximo de 20 resultados.
  Devuelve cada producto con precio, unidad, decimales, si controla inventario y si se puede
  vender, con su motivo (`Inactive` o `Deleted`).
- Una coincidencia exacta única se agrega directo. Varias (por ejemplo, el código de barras de uno
  igual al SKU de otro) abren la lista para elegir. Si no hay ninguna, se muestra un aviso y la
  venta no cambia.
- Los borrados solo se buscan por código exacto, para poder informar el motivo.
- En Desktop, el campo de captura procesa cada Enter como una lectura. Las lecturas pasan por una
  cola (`Channel`) que un solo consumidor atiende en orden. El texto se toma y se limpia de forma
  síncrona en el evento de tecla, así que las lecturas seguidas no se mezclan.

**Rationale**: el código de barras y el SKU ya tienen índices únicos filtrados (003), así que la
búsqueda exacta es inmediata (SC-002). La cola conserva el orden sin bloquear la UI.

**Alternatives considered**:

- Reutilizar `SearchProducts`: está paginada y pensada para el catálogo; no informa el motivo de no
  venta ni prioriza la coincidencia exacta.

## §9. Cancelación de venta (FR-033 a FR-035)

**Decision**: `CancelSaleHandler`, en una transacción de escritura:

- Carga la venta con sus líneas y verifica el estado (`Completed`) y la versión esperada.
- Invoca `Sale.Cancel(reason, utcNow, userId)`. El motivo es obligatorio y admite hasta 250
  caracteres.
- Por cada línea que tuvo movimiento `SALE`, registra en la existencia del mismo producto un
  movimiento `SALE_CANCEL` por la misma cantidad y lo liga a la línea. Esto pasa aunque el producto
  esté inactivo o borrado, o si hoy ya no controla inventario (la regla de 004 impide dejar de
  controlarlo con movimientos). Una línea sin movimiento original no genera regreso.
- Agrega una entrada `SALE_CANCELLED` a la bitácora de auditoría.
- Guarda todo en la misma transacción.

**Rationale**:

- Regresar exactamente lo que salió mantiene FR-027 sin depender de la configuración actual del
  producto. Esto resuelve el punto que dejó abierto `/speckit-clarify`.
- La versión de la venta y el `BEGIN IMMEDIATE` impiden una doble cancelación. El estado se revisa
  dentro de la transacción.

**Alternatives considered**:

- Recalcular según la configuración actual del producto: rompería la consistencia si cambió.
- Borrar los movimientos originales: el Principio IV lo prohíbe.

## §10. Bitácora de auditoría (Principio IX)

**Decision**: tabla nueva `AuditEntries`, inmutable, con:

- `Id` (GUID v7), `Action` (código de texto, por ejemplo `SALE_CANCELLED`), `EntityType`,
  `EntityId` y `Details` (texto hasta 500: folio y motivo).
- `CreatedAt` y `CreatedBy`, que asigna el interceptor existente.

`IAuditLog.Add(...)` es un puerto de Application y se guarda con la unidad de trabajo del caso de
uso. El guardián de `PosDbContext` rechaza modificar o borrar entradas, igual que los movimientos.
En esta fase no hay pantalla para consultarla (no la pide la spec); queda en la base y en el
respaldo de soporte.

**Rationale**: es la primera operación sensible del sistema. Una tabla genérica y mínima sirve
para las siguientes (cajón, cortes) sin rediseño.

**Alternatives considered**:

- Solo el log de Serilog: no es transaccional con la cancelación y rota.

## §11. Inicio con datos reales (FR-036, SC-009)

**Decision**: el caso de uso `GetSalesDashboard(days)`:

- Recibe las ventanas de día local ya convertidas a UTC `[desde, hasta)`. Desktop las calcula con
  la zona local, igual que los filtros de 004.
- Devuelve, por ventana, el total y el número de ventas completadas, y el top 5 de productos por
  cantidad vendida en los últimos 7 días (agrupa por `ProductId` y muestra el nombre guardado en la
  línea más reciente).
- Consulta solo `Status = COMPLETED`, con el mismo predicado que "Ventas realizadas" (SC-009).

Las gráficas se dibujan con controles de Avalonia (barras con `Border` y proporción), sin librería
de gráficas. `DashboardCard` gana una subclase `ChartCard` con `Bars` (etiqueta, valor formateado
y proporción de 0 a 1).

**Rationale**:

- Agrupar por día local en SQL no es fiable con horario de verano. Siete rangos UTC calculados en
  Desktop son exactos.
- El volumen (cientos de ventas por día) permite agregar con `GROUP BY` sencillos.
- Agregar una dependencia de gráficas no se justifica para tres barras (Principio VII).

**Alternatives considered**:

- LiveCharts u OxyPlot: son una dependencia nueva, pesada y ligada a Skia, para tres gráficas
  simples.

## §12. Rendimiento (SC-002)

**Decision**:

- La captura no toca la base salvo en la búsqueda exacta (índice) y el guardado del borrador, que
  va en segundo plano.
- La confirmación es una transacción con, a lo más, 50 líneas, 50 movimientos, 50 existencias y
  algunos pagos, en un solo `SaveChanges`.
- "Ventas realizadas" usa los índices `(CreatedAt, Id)`, `(Status, CreatedAt)` y `FolioNumber`
  (único).
- Pruebas de rendimiento con SQLite real:
  - Confirmar una venta de 50 líneas en menos de 2 s con 10,000 productos.
  - Listar una página de 100 ventas con 50,000 ventas en menos de 2 s.

**Rationale**: siguen el patrón de las pruebas de rendimiento de 003 y 004.

## §13. Versión y base de ejemplo

**Decision**:

- La versión pasa a 0.4.0.
- Se genera `v0.4.0.db` con ventas completadas y canceladas, una existencia negativa y un
  borrador.
- La prueba de actualización migra v0.1.0 a v0.4.0.
- La migración `SalesModule` solo crea tablas nuevas (`Sales`, `SaleLines`, `SalePayments`,
  `SaleDrafts` y `AuditEntries`). La liga entre movimiento y venta está en `SaleLines` (claves
  foráneas hacia `InventoryMovements`), así que no se reconstruye ninguna tabla existente.

**Rationale**: agregar una columna con clave foránea a `InventoryMovements` obligaría a EF Core a
reconstruir esa tabla en SQLite. Poner la liga del lado nuevo evita ese riesgo en bases de clientes.

**Alternatives considered**:

- `InventoryMovements.SaleId`: causa la reconstrucción de la tabla, y el Principio IV pide
  revisarla y evitarla.
- Una columna sin clave foránea: pierde integridad referencial.
