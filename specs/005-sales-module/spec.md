# Feature Specification: Módulo de ventas

**Feature Branch**: `005-sales-module`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "Módulo de ventas: captura de venta, cobro, registro de la venta y
descuento automático de inventario. Permitir al operador vender de forma rápida y confiable:
capturar productos, cobrar y registrar la venta, descontando el inventario en la misma operación.
Es el flujo más crítico del POS: la venta nunca debe perderse ni quedar a medias."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capturar la venta (Priority: P1)

Desde la pantalla "Punto de venta" (menú, grupo "Ventas", con acceso directo por teclado) el
operador agrega productos escaneando el código de barras (el lector funciona como teclado) o
buscando por nombre o SKU. Cada línea muestra nombre, cantidad, precio unitario e importe; el
operador puede cambiar la cantidad o quitar la línea. El total se actualiza al instante y se
muestra en grande. Todo el flujo se opera solo con teclado (atajos visibles para buscar, cobrar,
quitar línea y cancelar la venta) y también en pantalla táctil.

**Why this priority**: sin captura no hay venta; es el punto de entrada del flujo más crítico y la
velocidad de captura define la experiencia en mostrador.

**Independent Test**: abrir el Punto de venta, escanear dos productos distintos y uno repetido,
buscar un tercero por nombre, cambiar una cantidad y quitar una línea; verificar líneas, importes
y total en cada paso, sin haber implementado el cobro.

**Acceptance Scenarios**:

1. **Given** una venta vacía, **When** el operador escanea el código de barras de un producto
   activo, **Then** se agrega una línea con cantidad 1, su precio unitario y su importe, y el total
   se actualiza.
2. **Given** una venta con el producto A, **When** el operador escanea otra vez el producto A,
   **Then** la cantidad de esa línea aumenta en 1 y no se crea una línea nueva.
3. **Given** una venta en curso, **When** el operador busca por nombre o SKU y elige un resultado,
   **Then** el producto se agrega igual que si se hubiera escaneado.
4. **Given** un producto cuya unidad de medida admite decimales (por ejemplo kilogramo), **When** el
   operador captura la cantidad 0.750, **Then** se acepta; con una unidad de cantidades enteras
   (por ejemplo pieza) se rechaza una cantidad con decimales.
5. **Given** una línea en la venta, **When** el operador cambia su cantidad o la quita, **Then** el
   importe de la línea y el total se recalculan de inmediato.
6. **Given** un producto inactivo o borrado, **When** el operador lo escanea o lo busca, **Then** no
   se agrega a la venta y se le informa el motivo.
7. **Given** un código de barras que no corresponde a ningún producto, **When** se escanea,
   **Then** se muestra un aviso claro y la venta no cambia.
8. **Given** una venta con líneas, **When** el operador elige cancelar la venta, **Then** el sistema
   pide confirmación y solo al confirmar vacía la venta en curso.
9. **Given** la pantalla de Punto de venta, **When** el operador usa únicamente el teclado, **Then**
   puede buscar, cambiar cantidad, quitar línea, cobrar y cancelar mediante atajos que están
   visibles en pantalla.

---

### User Story 2 - La venta en curso no se pierde (Priority: P1)

La venta en curso se guarda localmente de forma continua. Si la aplicación se cierra
inesperadamente (falla, corte de luz), al volver a abrir el Punto de venta se ofrece recuperar la
venta que estaba en curso.

**Why this priority**: la venta nunca debe perderse; con el cliente esperando en mostrador, volver
a capturar todo es inaceptable.

**Independent Test**: agregar líneas a una venta, cerrar el proceso de la aplicación de forma
abrupta, reabrir y verificar que se ofrece recuperar la venta con las mismas líneas y cantidades.

**Acceptance Scenarios**:

1. **Given** una venta en curso con líneas, **When** la aplicación se cierra inesperadamente y se
   vuelve a abrir, **Then** el Punto de venta ofrece recuperar la venta con sus líneas y cantidades.
2. **Given** la oferta de recuperación, **When** el operador la rechaza, **Then** la venta guardada
   se descarta y el Punto de venta inicia vacío.
3. **Given** una venta recuperada en la que un producto ya no está disponible para venta (inactivo
   o borrado desde entonces), **When** se recupera, **Then** esa línea se señala al operador y no
   se puede cobrar hasta quitarla.
4. **Given** una venta recién cobrada o cancelada por el operador, **When** se reabre la
   aplicación, **Then** no se ofrece recuperar nada.

---

### User Story 3 - Cobrar (Priority: P1)

El operador cobra la venta con efectivo, tarjeta o transferencia, incluyendo pago mixto (varias
formas de pago en la misma venta). En efectivo captura el monto recibido y ve el cambio; hay
botones de monto rápido (monto exacto y billetes comunes). Tarjeta y transferencia se cobran fuera
del POS (terminal bancaria o app del banco): el POS solo registra el monto y, opcionalmente, una
referencia. El total pagado debe cubrir el total de la venta; no se puede confirmar con faltante.
Tras confirmar el cobro, la pantalla queda lista para la siguiente venta.

**Why this priority**: cobrar es lo que cierra la venta; sin este paso no hay ingreso registrado.

**Independent Test**: con una venta de total conocido, cobrar en efectivo con un monto mayor y
verificar el cambio; cobrar con pago mixto efectivo + tarjeta; intentar confirmar con un faltante y
verificar que se impide.

**Acceptance Scenarios**:

1. **Given** una venta con total $85.50, **When** el operador cobra en efectivo con $100.00 recibidos,
   **Then** se muestra un cambio de $14.50 y el cobro puede confirmarse.
2. **Given** una venta con total $200.00, **When** el operador registra $50.00 en efectivo y $150.00
   con tarjeta (referencia opcional), **Then** el total pagado cubre la venta y el cobro puede
   confirmarse; el cambio es $0.00.
3. **Given** una venta con total $200.00, **When** el total pagado es $150.00, **Then** se muestra
   el faltante y no se permite confirmar.
4. **Given** un pago con tarjeta o transferencia cuyo monto excede el saldo pendiente, **When** el
   operador intenta confirmarlo, **Then** se rechaza: solo el efectivo puede generar cambio.
5. **Given** el paso de cobro con efectivo, **When** el operador pulsa un botón de monto rápido,
   **Then** el monto recibido se llena con ese valor (el de monto exacto usa el saldo pendiente).
6. **Given** un cobro confirmado con éxito, **When** termina el registro, **Then** se muestra el
   folio y el cambio a entregar, y el Punto de venta queda vacío y listo para la siguiente venta.
7. **Given** el cobro, **When** el operador lo cancela antes de confirmar, **Then** regresa a la
   venta en curso sin perder líneas ni pagos capturados que aún no se confirmaron como venta.

---

### User Story 4 - Registrar la venta y descontar inventario (Priority: P1)

Al confirmar el cobro se registran, en una única operación indivisible, la venta, sus líneas, sus
pagos y los movimientos de inventario. Si algo falla, no se guarda nada y la venta en curso se
conserva para reintentar. Cada venta recibe un folio consecutivo legible por instalación (por
ejemplo V-000001), además de su identificador interno. Las líneas conservan una copia del nombre,
SKU y precio del producto al momento de la venta. Por cada línea de un producto que controla
inventario se genera un movimiento "Salida por venta" ligado a la venta; los productos que no
controlan inventario no generan movimiento. Se permite vender sin existencia suficiente, con una
advertencia; la existencia puede quedar negativa únicamente por ventas.

**Why this priority**: es el corazón de la integridad del sistema: dinero, historial e inventario
deben quedar siempre consistentes.

**Independent Test**: registrar una venta con un producto que controla inventario y uno que no;
verificar folio, líneas, pagos, un solo movimiento de salida y la nueva existencia; luego simular
una falla durante el registro y verificar que no quedó ningún dato parcial.

**Acceptance Scenarios**:

1. **Given** una venta cobrada, **When** se confirma, **Then** existen la venta con su folio, sus
   líneas, sus pagos y un movimiento "Salida por venta" por cada línea de producto que controla
   inventario, y la existencia de cada uno baja en la cantidad vendida.
2. **Given** una venta con un servicio (no controla inventario), **When** se confirma, **Then** la
   línea se registra pero no se genera movimiento de inventario.
3. **Given** una falla simulada en cualquier punto del registro, **When** se intenta confirmar,
   **Then** no queda venta, ni líneas, ni pagos, ni movimientos, ni folio consumido, y la venta en
   curso se conserva para reintentar con un mensaje comprensible.
4. **Given** varias ventas registradas, **When** se consultan sus folios, **Then** son consecutivos
   (V-000001, V-000002, …) y no se repiten.
5. **Given** una venta registrada, **When** el operador modifica después el nombre, SKU o precio del
   producto, **Then** las líneas de la venta ya registrada conservan los valores originales.
6. **Given** una venta con una cantidad mayor que la existencia de un producto, **When** el operador
   intenta cobrar, **Then** se muestra una advertencia de existencia insuficiente y, tras aceptarla,
   la venta se registra y la existencia queda negativa.
7. **Given** un producto con existencia negativa, **When** se consulta en Existencias, **Then** se
   muestra en el estado "sin existencia".
8. **Given** cualquier conjunto de ventas y cancelaciones, **When** se compara, **Then** la
   existencia de cada producto es igual a la suma de sus movimientos.

---

### User Story 5 - Consultar ventas realizadas (Priority: P2)

La pantalla "Ventas realizadas" lista las ventas con folio, fecha, total, formas de pago y estado;
permite filtrar por rango de fechas, folio y estado, con paginación de 100 registros. Al abrir una
venta se ve su detalle con líneas y pagos.

**Why this priority**: permite revisar lo vendido y es la base para cancelar; no bloquea vender.

**Independent Test**: con ventas de varios días y una cancelada, filtrar por rango de fechas, por
folio exacto y por estado; abrir el detalle de una venta y verificar líneas y pagos.

**Acceptance Scenarios**:

1. **Given** más de 100 ventas, **When** se abre "Ventas realizadas", **Then** se muestran las más
   recientes primero en páginas de 100 y se puede navegar entre páginas.
2. **Given** ventas de distintos días, **When** el operador filtra por rango de fechas, **Then** solo
   se listan las ventas de ese rango (según hora local).
3. **Given** un folio, **When** el operador lo busca, **Then** se muestra esa venta.
4. **Given** ventas completadas y canceladas, **When** se filtra por estado, **Then** solo se listan
   las del estado elegido.
5. **Given** una venta en el listado, **When** el operador abre su detalle, **Then** ve sus líneas
   (nombre, SKU, cantidad, precio, importe) y sus pagos (forma, monto, referencia).

---

### User Story 6 - Cancelar una venta registrada (Priority: P2)

El operador puede cancelar una venta completa ya registrada, con confirmación y motivo
obligatorio. La cancelación no borra la venta: la marca como cancelada y genera movimientos de
inventario "Cancelación de venta" que regresan las cantidades. Queda registrada en la bitácora de
auditoría. Una venta cancelada no puede cancelarse otra vez.

**Why this priority**: corrige errores de captura o cobro conservando el historial; no bloquea el
flujo principal.

**Independent Test**: cancelar una venta con un producto que controla inventario, verificar estado,
motivo, movimiento de regreso, existencia restaurada y registro en bitácora; intentar cancelarla de
nuevo y verificar que se impide.

**Acceptance Scenarios**:

1. **Given** una venta completada, **When** el operador la cancela con un motivo y confirma,
   **Then** queda con estado "Cancelada" (con motivo, fecha y usuario) y por cada línea con
   inventario se genera un movimiento "Cancelación de venta" que suma la cantidad vendida.
2. **Given** la cancelación de una venta, **When** se guarda, **Then** venta, estado y movimientos se
   registran de forma atómica y se agrega una entrada en la bitácora de auditoría.
3. **Given** el formulario de cancelación, **When** el motivo está vacío, **Then** no se permite
   confirmar.
4. **Given** una venta ya cancelada, **When** el operador intenta cancelarla otra vez, **Then** el
   sistema lo impide y lo explica.
5. **Given** una venta cancelada, **When** se consulta, **Then** sigue visible en "Ventas realizadas"
   con su detalle original y el estado "Cancelada".

---

### User Story 7 - Inicio con datos de ventas (Priority: P2)

Las tarjetas y gráficas de ventas de la pantalla de Inicio (ventas del día, últimos 7 días,
productos más vendidos) muestran datos reales, excluyendo las ventas canceladas.

**Why this priority**: da al dueño una visión rápida del negocio; depende de que ya existan ventas.

**Independent Test**: registrar ventas hoy y en días previos, cancelar una, abrir Inicio y verificar
que los totales y el ranking reflejan solo las ventas no canceladas.

**Acceptance Scenarios**:

1. **Given** ventas registradas hoy, **When** se abre Inicio, **Then** "ventas del día" muestra la
   suma de sus totales y el número de ventas, sin incluir canceladas.
2. **Given** ventas de los últimos 7 días, **When** se abre Inicio, **Then** la gráfica muestra el
   total vendido por día (hora local), con cero en los días sin ventas.
3. **Given** ventas con distintos productos, **When** se abre Inicio, **Then** "productos más
   vendidos" los ordena por cantidad vendida, excluyendo ventas canceladas.
4. **Given** una venta que se cancela, **When** se vuelve a abrir Inicio, **Then** sus importes y
   cantidades ya no cuentan.
5. **Given** que no existen ventas, **When** se abre Inicio, **Then** las tarjetas muestran cero y
   las gráficas un estado vacío, sin errores.

---

### Edge Cases

- Escanear rápidamente varios códigos seguidos (el lector escribe como teclado): ninguno se pierde
  ni se mezcla con el texto de otro; el orden se respeta.
- Un mismo código de barras o SKU que devuelve más de un producto: se muestra la lista para elegir
  en lugar de agregar uno arbitrario.
- Cantidad cero, negativa o vacía al editar una línea: se rechaza y se conserva el valor anterior.
- Cantidad con más decimales de los que admite la unidad: se rechaza; no se redondea en silencio.
- Cobrar una venta sin líneas o con total cero: no se permite.
- Cambio de precio de un producto mientras hay una venta en curso o recuperada: la línea usa el
  precio vigente al confirmar el cobro y el operador ve el total actualizado antes de cobrar.
- Producto desactivado o borrado mientras está en la venta en curso: la línea se señala y no se
  puede cobrar hasta quitarla.
- Falla al guardar el borrador de la venta en curso: se registra en el log y el operador puede
  seguir vendiendo; no se interrumpe la captura.
- Cierre inesperado justo durante la confirmación del cobro: al reabrir, la venta o quedó registrada
  completa (y no se ofrece recuperar) o no quedó nada (y se ofrece recuperar el borrador); nunca a
  medias ni duplicada.
- Doble pulsación o doble toque en "Confirmar cobro": se registra una sola venta.
- Redondeo del importe de línea (cantidad decimal × precio): siempre por la misma regla
  documentada; la suma de importes de línea es exactamente el total de la venta.
- Efectivo recibido menor que el saldo pendiente tras otros pagos: se muestra faltante; no se
  confirma.
- Cancelar una venta cuyo producto ya está inactivo, borrado o cambió de configuración de
  inventario: la cancelación regresa las cantidades a ese producto igualmente.
- Dos ventas que se confirman casi al mismo tiempo: los folios siguen siendo consecutivos y únicos.

## Requirements *(mandatory)*

### Functional Requirements

**Captura**

- **FR-001**: El sistema MUST ofrecer una pantalla "Punto de venta" en el menú, dentro del grupo
  "Ventas", con acceso directo por teclado.
- **FR-002**: El sistema MUST permitir agregar un producto escaneando su código de barras o
  buscándolo por nombre o SKU.
- **FR-003**: El sistema MUST incrementar la cantidad de la línea existente cuando se agrega un
  producto que ya está en la venta.
- **FR-004**: El sistema MUST mostrar por cada línea nombre, cantidad, precio unitario e importe, y
  permitir cambiar la cantidad o quitar la línea.
- **FR-005**: El sistema MUST validar las cantidades contra los decimales permitidos por la unidad
  de medida del producto y rechazar cantidades no positivas.
- **FR-006**: El sistema MUST mostrar el total en tamaño destacado y actualizarlo al instante ante
  cualquier cambio de la venta.
- **FR-007**: El sistema MUST permitir vender únicamente productos activos y no borrados, e informar
  al operador cuando un producto no puede venderse.
- **FR-008**: Todo el flujo (buscar, cambiar cantidad, quitar línea, cobrar, cancelar) MUST poder
  operarse solo con teclado, con atajos visibles en pantalla, y MUST poder operarse también con
  pantalla táctil.
- **FR-009**: El sistema MUST pedir confirmación antes de cancelar la venta en curso.

**Venta en curso**

- **FR-010**: El sistema MUST guardar de forma local y continua la venta en curso, con cada cambio
  relevante (líneas, cantidades).
- **FR-011**: Al abrir el Punto de venta con una venta en curso guardada de una sesión anterior, el
  sistema MUST ofrecer recuperarla o descartarla.
- **FR-012**: El sistema MUST eliminar el borrador de la venta en curso al registrarse la venta o al
  cancelarla el operador.
- **FR-013**: Un fallo al guardar el borrador MUST registrarse en el log y NO MUST interrumpir la
  captura.

**Cobro**

- **FR-014**: El sistema MUST admitir las formas de pago efectivo, tarjeta y transferencia, y pago
  mixto con varias formas en una misma venta.
- **FR-015**: Para efectivo, el sistema MUST capturar el monto recibido y calcular y mostrar el
  cambio; solo el efectivo puede generar cambio.
- **FR-016**: Para tarjeta y transferencia, el sistema MUST registrar el monto y una referencia
  opcional, sin integrarse con la terminal bancaria; el monto no puede exceder el saldo pendiente.
- **FR-017**: El sistema MUST impedir confirmar el cobro mientras el total pagado no cubra el total
  de la venta, mostrando el faltante.
- **FR-018**: El sistema MUST ofrecer botones de monto rápido para efectivo: monto exacto y
  denominaciones comunes.
- **FR-019**: Tras confirmar el cobro, el sistema MUST mostrar el folio y el cambio a entregar y
  dejar el Punto de venta listo para la siguiente venta.
- **FR-020**: El sistema MUST impedir registrar más de una venta por una misma confirmación
  (protección contra doble pulsación).

**Registro e inventario**

- **FR-021**: Al confirmar el cobro, el sistema MUST registrar en una única transacción la venta, sus
  líneas, sus pagos y los movimientos de inventario; ante cualquier falla MUST no guardar nada,
  conservar la venta en curso y mostrar un mensaje comprensible sin detalles técnicos.
- **FR-022**: El sistema MUST asignar a cada venta un folio consecutivo legible, único por
  instalación (formato V-000001), además de su identificador interno; un intento fallido no
  consume folio.
- **FR-023**: Cada línea MUST conservar copia del nombre, SKU y precio unitario del producto al
  momento de la venta, e independiente de cambios posteriores del producto.
- **FR-024**: Por cada línea de un producto que controla inventario, el sistema MUST generar un
  movimiento de tipo "Salida por venta" ligado a la venta; los productos que no controlan
  inventario NO MUST generar movimiento.
- **FR-025**: El sistema MUST permitir vender con existencia insuficiente, mostrando una advertencia
  previa; la existencia MAY quedar negativa únicamente por ventas.
- **FR-026**: Los productos con existencia negativa MUST mostrarse en el estado "sin existencia".
- **FR-027**: La existencia de cada producto MUST ser siempre igual a la suma de sus movimientos,
  incluyendo salidas por venta y cancelaciones.
- **FR-028**: Todos los importes MUST manejarse como dinero en centavos; el importe de línea es
  cantidad × precio unitario, redondeado a centavos con una regla única y documentada, y el total
  de la venta es la suma de los importes de línea.
- **FR-029**: Los precios MUST considerarse finales; el sistema NO MUST calcular ni desglosar
  impuestos en esta fase.
- **FR-030**: Mientras no exista autenticación, las ventas y cancelaciones MUST registrarse con el
  usuario de sistema.

**Consulta y cancelación**

- **FR-031**: El sistema MUST ofrecer una pantalla "Ventas realizadas" con folio, fecha, total,
  formas de pago y estado, con filtros por rango de fechas, folio y estado, y paginación de 100
  registros.
- **FR-032**: El sistema MUST mostrar el detalle de una venta con sus líneas y sus pagos.
- **FR-033**: El sistema MUST permitir cancelar una venta completa registrada, con confirmación y
  motivo obligatorio.
- **FR-034**: La cancelación MUST conservar la venta marcándola como cancelada (con motivo, fecha y
  usuario), generar movimientos de inventario "Cancelación de venta" que regresen las cantidades y
  registrarse en la bitácora de auditoría, todo de forma atómica.
- **FR-035**: El sistema NO MUST permitir cancelar una venta ya cancelada.

**Inicio**

- **FR-036**: Las tarjetas y gráficas de ventas de Inicio (ventas del día, últimos 7 días, productos
  más vendidos) MUST mostrar datos reales excluyendo ventas canceladas, usando el día local del
  operador.

### Key Entities

- **Venta**: operación de venta registrada. Tiene folio consecutivo legible, identificador interno,
  fecha y hora, total, estado (completada o cancelada), y, si se cancela, motivo, fecha y usuario de
  cancelación. Contiene líneas y pagos; nunca se borra físicamente.
- **Línea de venta**: renglón de una venta. Guarda copia del nombre, SKU y precio unitario del
  producto al vender, la cantidad, el importe y la referencia al producto.
- **Pago de venta**: forma de pago aplicada a una venta (efectivo, tarjeta o transferencia), con su
  monto, referencia opcional y, en efectivo, monto recibido y cambio.
- **Venta en curso (borrador)**: venta aún no cobrada guardada localmente, con sus líneas, que puede
  recuperarse tras un cierre inesperado. Solo existe una por instalación.
- **Movimiento de inventario** (existente): se extiende con los tipos "Salida por venta" y
  "Cancelación de venta", ligados a la venta que los origina.
- **Producto** (existente): aporta nombre, SKU, código de barras, precio, unidad de medida (y sus
  decimales), estado y si controla inventario.
- **Folio**: consecutivo por instalación sin huecos ni repeticiones para ventas registradas.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un operador puede completar una venta de principio a fin (capturar productos, cobrar
  y quedar listo para la siguiente) usando únicamente el teclado, y también usando únicamente
  pantalla táctil.
- **SC-002**: Agregar un producto por código de barras actualiza líneas y total en menos de 0.5
  segundos (la búsqueda por código exacto tarda menos de 100 ms), y confirmar el cobro y ver el folio toma menos de 2 segundos con una venta
  de hasta 50 líneas.
- **SC-003**: Ante una falla simulada durante el registro de la venta, en el 100% de los intentos no
  queda ni venta, ni pagos, ni movimientos parciales, y la venta en curso se conserva para
  reintentar.
- **SC-004**: Tras cerrar la aplicación de forma inesperada con una venta en curso, en el 100% de
  los casos se ofrece recuperarla al reabrir con todas las líneas y cantidades de su último
  guardado completo (cada cambio se guarda de inmediato, sin retraso intencional), sin que el
  operador la recapture.
- **SC-005**: Los folios de todas las ventas registradas son consecutivos y sin repeticiones, incluso
  ante ventas confirmadas casi simultáneamente.
- **SC-006**: Para todo producto, la existencia mostrada coincide con la suma de sus movimientos
  después de cualquier serie de ventas y cancelaciones.
- **SC-007**: Modificar nombre, SKU o precio de un producto no cambia ningún dato de las ventas ya
  registradas.
- **SC-008**: Los importes de línea, total, pagos y cambio son exactos al centavo en todos los casos
  de prueba, incluidas cantidades decimales, pago mixto y faltante de pago.
- **SC-009**: Las cifras de ventas de Inicio coinciden con las de "Ventas realizadas" para el mismo
  periodo, sin contar ventas canceladas.

## Assumptions

- Un solo almacén y una sola terminal por instalación, sin control de sesiones de cajero; el
  operador es el usuario de sistema hasta que exista autenticación.
- La captura de la venta y la consulta de existencias parten de los productos, unidades de medida e
  inventario ya existentes (003 y 004); no se modifica el catálogo.
- El lector de códigos se comporta como teclado y termina cada lectura con Enter; no se requiere
  integración con dispositivos.
- Los billetes de los botones de monto rápido siguen la moneda vigente de la instalación (peso
  mexicano: 20, 50, 100, 200, 500 y 1000), además del monto exacto.
- El importe de línea se redondea a centavos con la regla de "mitad hacia arriba" (mitad se aleja
  de cero), aplicada una sola vez por línea; se documenta en las reglas del dominio.
- Se admite una sola venta en curso por instalación; las ventas en espera están fuera de alcance.
- El precio de cada línea se toma del producto al agregarlo y se vuelve a validar al confirmar; si
  cambió, el operador ve el total actualizado antes de cobrar.
- "Existencia negativa" solo puede originarse por ventas; los ajustes y salidas manuales de 004
  siguen sin permitir dejar la existencia por debajo de cero.
- La cancelación de una venta es total y no tiene límite de antigüedad en esta fase; no existen
  permisos por rol.
- Los filtros de fecha y los cortes por día usan la hora local del equipo; las fechas se guardan en
  UTC.
- Impresión de tickets y cajón de dinero se especifican aparte. Quedan fuera de alcance: descuentos,
  promociones, clientes, crédito, devoluciones parciales, ventas en espera, apertura y corte de
  caja, impuestos y facturación.
