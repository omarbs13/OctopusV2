# Feature Specification: Manejo de inventario de productos

**Feature Branch**: `004-inventory-management`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Manejo de inventario de productos: existencias, movimientos y alertas
de existencia baja. Controlar cuántas unidades hay de cada producto y conservar el historial de
cada cambio, como base para el futuro módulo de ventas. Un solo almacén por instalación."

## Clarifications

### Session 2026-09-29

- Q: ¿Los productos inactivos que controlan inventario aparecen en Existencias y cuentan en las
  tarjetas de Inicio? → A: No cuentan en las tarjetas; en Existencias se ocultan por omisión y se
  muestran con el filtro "incluir inactivos".
- Q: ¿Cuándo se permite registrar "inventario inicial"? → A: Solo una vez por producto y solo si
  el producto no tiene movimientos previos; los recuentos posteriores se registran como ajustes.
- Q: ¿Desde qué pantallas se abre el formulario para registrar un movimiento? → A: Desde
  Existencias (con el producto seleccionado) y desde Movimientos (el producto se elige en el
  formulario); no desde Productos.
- Q: ¿La fecha del movimiento la pone el sistema o puede capturarse una fecha anterior? → A:
  Siempre la fecha y hora de captura, asignada por el sistema y no modificable.
- Q: ¿Las entradas guardan una referencia del documento del proveedor? → A: Sí, un campo
  "referencia" opcional en todos los movimientos, visible en el kárdex, sin búsqueda por él.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configuración de inventario en el producto (Priority: P1)

Al dar de alta o editar un producto, el operador indica si controla inventario (los servicios no
lo controlan) y, opcionalmente, una existencia mínima para alertas. La unidad de medida ya es
obligatoria en todo producto desde 003 (catálogo fijo con claves del SAT) y ahora determina además
cuántos decimales admiten sus cantidades. La existencia actual se muestra en el
formulario y en el listado de productos, pero no se puede escribir: solo cambia mediante
movimientos.

**Why this priority**: sin esta configuración no se sabe qué productos tienen existencia ni en qué
unidad se cuentan; es la base de todo lo demás.

**Independent Test**: crear un producto que controla inventario con unidad "kilogramo" y mínimo 5;
verificar que se guarda, que la existencia aparece en 0 y que el campo de existencia no es
editable. Crear un servicio y verificar que no muestra datos de inventario.

**Acceptance Scenarios**:

1. **Given** un producto nuevo, **When** el operador marca que controla inventario y elige unidad,
   **Then** el producto se guarda con existencia 0 y esa configuración.
2. **Given** un producto que no controla inventario, **When** se abre su formulario, **Then** no se
   pide existencia mínima, y su existencia se muestra vacía o no aplicable en el listado.
3. **Given** un producto con existencia mayor que cero, **When** el operador intenta modificar la
   existencia desde el formulario, **Then** no es posible: el valor se muestra solo de lectura.
4. **Given** un producto con movimientos registrados, **When** el operador intenta cambiar su
   unidad de medida o desactivar el control de inventario, **Then** el sistema lo impide con un
   mensaje claro.
5. **Given** un producto cuya unidad es de cantidades enteras (por ejemplo pieza o caja), **When**
   el operador captura una existencia mínima con decimales, **Then** se rechaza; con kilogramo,
   litro o metro se aceptan hasta 3 decimales.

---

### User Story 2 - Movimientos de inventario (Priority: P1)

El operador registra inventario inicial, entradas por recepción de mercancía y ajustes positivos o
negativos, para que la existencia refleje la realidad. Cada movimiento queda guardado de forma
permanente con producto, tipo, cantidad, fecha, usuario, existencia resultante y motivo.

**Why this priority**: es la única vía para cambiar la existencia; sin movimientos no hay
inventario.

**Independent Test**: registrar inventario inicial de 10, una entrada de 5 y un ajuste negativo de
3 sobre un producto; verificar existencia 12 y tres movimientos con existencia resultante 10, 15 y
12.

**Acceptance Scenarios**:

1. **Given** un producto que controla inventario, **When** el operador registra una entrada de 5
   unidades, **Then** la existencia aumenta en 5 y el movimiento guarda la existencia resultante.
2. **Given** un producto con existencia 3, **When** el operador registra un ajuste negativo de 5,
   **Then** el movimiento se rechaza con un mensaje claro y ni la existencia ni el historial
   cambian.
3. **Given** un ajuste (positivo o negativo), **When** el operador no captura motivo, **Then** se
   rechaza indicando que el motivo es obligatorio; en inventario inicial y entrada el motivo es
   opcional.
4. **Given** un producto que no controla inventario, **When** se intenta registrar un movimiento,
   **Then** se rechaza con un mensaje claro.
5. **Given** un movimiento registrado con error, **When** el operador lo consulta, **Then** no
   existe opción para editarlo ni borrarlo; corrige registrando un movimiento contrario.
6. **Given** un producto en piezas, **When** el operador captura una cantidad con decimales,
   **Then** se rechaza; con kilogramos se acepta hasta 3 decimales.
7. **Given** una cantidad de cero o negativa, **When** el operador intenta registrarla, **Then** se
   rechaza (el sentido del movimiento lo da su tipo).
8. **Given** un producto que ya tiene cualquier movimiento, **When** el operador intenta registrar
   un inventario inicial, **Then** se rechaza indicando que debe usar un ajuste.

---

### User Story 3 - Consulta de existencias (Priority: P1)

La pantalla "Existencias" lista los productos que controlan inventario con su existencia, unidad,
existencia mínima y estado (normal, baja, sin existencia), con filtro por estado y búsqueda por
nombre, SKU o código de barras, paginada de 100 en 100. Desde ella el operador puede registrar un
movimiento para el producto elegido.

**Why this priority**: es la vista diaria del operador para saber qué hay y qué falta.

**Independent Test**: con productos en distintos estados, filtrar por "baja" y verificar que solo
aparecen los que cumplen; buscar por SKU y por código de barras.

**Acceptance Scenarios**:

1. **Given** productos con y sin control de inventario, **When** se abre Existencias, **Then** solo
   aparecen los que controlan inventario.
2. **Given** más de 100 productos, **When** se abre Existencias, **Then** se muestran 100 por
   página con navegación entre páginas.
3. **Given** un texto de búsqueda, **When** coincide con nombre, SKU o código de barras, **Then**
   se listan solo los productos coincidentes.
4. **Given** el filtro "sin existencia", **When** se aplica, **Then** solo aparecen productos con
   existencia cero.
5. **Given** un producto inactivo que controla inventario, **When** se abre Existencias, **Then**
   no aparece; al activar el filtro "incluir inactivos" sí aparece, identificado como inactivo.

---

### User Story 4 - Historial de movimientos (kárdex) (Priority: P2)

La pantalla "Movimientos" muestra el historial de todos los movimientos, filtrable por producto,
tipo y rango de fechas, paginado de 100 en 100. Desde un producto se puede abrir su historial ya
filtrado.

**Why this priority**: da trazabilidad y permite auditar y corregir errores, pero el inventario
funciona sin ella.

**Independent Test**: registrar movimientos de dos productos; filtrar por uno y por un rango de
fechas, y verificar que solo aparecen los esperados en orden del más reciente al más antiguo.

**Acceptance Scenarios**:

1. **Given** movimientos de varios productos, **When** se filtra por producto, tipo y rango de
   fechas, **Then** solo se listan los que cumplen todos los filtros.
2. **Given** un producto en el listado de Productos, **When** el operador abre su historial,
   **Then** se muestra Movimientos filtrado por ese producto.
3. **Given** más de 100 movimientos, **When** se abre el historial, **Then** se pagina de 100 en
   100.
4. **Given** la pantalla Movimientos, **When** el operador elige registrar un movimiento, **Then**
   se abre el formulario donde elige el producto (solo activos que controlan inventario) y, al
   guardar, el historial muestra el nuevo movimiento.

---

### User Story 5 - Alertas de existencia baja (Priority: P2)

Un producto está en existencia baja cuando su existencia es menor o igual a su mínimo (y mayor que
cero), y sin existencia cuando es cero. La pantalla de inicio muestra tarjetas con el número de
productos en cada estado, que llevan al listado de Existencias ya filtrado.

**Why this priority**: convierte el inventario en una acción preventiva, pero depende de las
historias anteriores.

**Independent Test**: con un producto de mínimo 5 y existencia 5, y otro con existencia 0, verificar
que las tarjetas muestran 1 y 1 y que al abrirlas se listan los productos correctos.

**Acceptance Scenarios**:

1. **Given** un producto con existencia igual a su mínimo, **When** se consulta su estado,
   **Then** es "baja".
2. **Given** un producto con existencia cero, **When** se consulta su estado, **Then** es "sin
   existencia" (no se cuenta además como "baja").
3. **Given** un producto sin existencia mínima definida y con existencia mayor que cero, **When**
   se consulta su estado, **Then** es "normal".
4. **Given** productos en alerta, **When** se abre Inicio, **Then** las tarjetas muestran los
   conteos correctos y al abrir cada una se llega a Existencias con el filtro aplicado.
5. **Given** un movimiento registrado, **When** cambia el estado de un producto, **Then** las
   tarjetas reflejan el cambio sin reiniciar la aplicación.
6. **Given** un producto inactivo con existencia cero, **When** se abre Inicio, **Then** no se
   cuenta en la tarjeta "Sin existencia".

---

### Edge Cases

- Dos movimientos casi simultáneos sobre el mismo producto no pueden dejar la existencia
  inconsistente ni permitir un ajuste negativo que la deje bajo cero.
- Fallo a mitad de un registro (error inesperado): no se guarda ni el movimiento ni el cambio de
  existencia.
- Producto existente, con control de inventario ya activado, sin movimientos: puede cambiar de
  unidad; con movimientos, no.
- Productos existentes antes de esta funcionalidad: quedan como "no controla inventario" hasta que
  el operador lo active.
- Producto desactivado con existencia: conserva su existencia e historial; no admite nuevos
  movimientos mientras esté inactivo, no genera alertas y solo se ve en Existencias con el filtro
  "incluir inactivos".
- Producto sin existencia mínima definida: nunca está en existencia baja, pero sí puede estar sin
  existencia.
- Cantidades con más decimales de los permitidos, vacías o no numéricas: se rechazan con mensaje.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Cada producto MUST indicar si controla inventario; por omisión no lo controla.
- **FR-002**: La unidad de medida del producto MUST seguir siendo la del catálogo fijo de 003 (pieza,
  kilogramo, gramo, litro, mililitro, metro, caja, paquete), obligatoria en todo producto.
- **FR-003**: Las cantidades de productos en kilogramo, litro y metro MUST admitir hasta 3
  decimales; las de pieza, gramo, mililitro, caja y paquete MUST ser enteras. Esta regla aplica a
  movimientos y a la existencia mínima.
- **FR-004**: El producto MUST admitir una existencia mínima opcional, no negativa.
- **FR-005**: La existencia actual MUST mostrarse en el formulario del producto y en su listado, y
  MUST NOT poder editarse directamente.
- **FR-006**: Un producto con movimientos registrados MUST NOT poder cambiar de unidad de medida ni
  dejar de controlar inventario.
- **FR-007**: El sistema MUST permitir registrar cuatro tipos de movimiento: inventario inicial,
  entrada, ajuste positivo y ajuste negativo.
- **FR-008**: Cada movimiento MUST guardar producto, tipo, cantidad (mayor que cero), fecha y hora,
  usuario que lo registró, existencia resultante y motivo. La fecha y hora MUST asignarla el
  sistema al momento de guardar; el operador MUST NOT poder capturarla ni modificarla.
- **FR-009**: El motivo MUST ser obligatorio en ajustes positivos y negativos, y opcional en los
  demás tipos.
- **FR-009a**: Todo movimiento MUST admitir una "referencia" opcional (por ejemplo, folio de
  factura o remisión del proveedor), visible en el historial de Movimientos; no es criterio de
  búsqueda ni de filtro.
- **FR-010**: Los movimientos MUST ser inmutables: la aplicación MUST NOT ofrecer editarlos ni
  borrarlos; los errores se corrigen con un movimiento contrario.
- **FR-011**: El sistema MUST rechazar, con mensaje claro, un ajuste negativo que deje la
  existencia por debajo de cero.
- **FR-012**: La existencia actual de un producto MUST ser siempre igual a la suma algebraica de
  sus movimientos (entradas positivas, ajuste negativo resta).
- **FR-013**: Registrar un movimiento y actualizar la existencia MUST ocurrir como una sola
  operación indivisible: si falla, no cambia ninguna de las dos cosas.
- **FR-014**: El sistema MUST rechazar movimientos sobre productos que no controlan inventario o
  que están inactivos.
- **FR-015**: El inventario inicial MUST poder registrarse una sola vez por producto y solo cuando
  el producto no tiene movimientos previos; en cualquier otro caso se rechaza indicando que se use
  un ajuste.
- **FR-015a**: El formulario de registro de movimiento MUST abrirse desde Existencias, con el
  producto seleccionado ya elegido, y desde Movimientos, donde el operador elige el producto entre
  los activos que controlan inventario. Productos MUST NOT ofrecer registrar movimientos.
- **FR-016**: La pantalla "Existencias" MUST listar solo los productos que controlan inventario,
  con nombre, SKU, existencia, unidad, existencia mínima y estado (normal, baja, sin existencia).
- **FR-017**: "Existencias" MUST permitir filtrar por estado, buscar por nombre, SKU o código de
  barras, y paginar de 100 en 100. Por omisión MUST mostrar solo productos activos; un filtro
  "incluir inactivos" MUST mostrar también los inactivos, identificados como tales.
- **FR-018**: La pantalla "Movimientos" MUST listar el historial ordenado del más reciente al más
  antiguo, filtrable por producto, tipo y rango de fechas, paginado de 100 en 100.
- **FR-019**: Desde un producto MUST poder abrirse su historial de movimientos.
- **FR-020**: Un producto MUST estar "sin existencia" cuando su existencia es cero; "baja" cuando
  es mayor que cero y menor o igual a su mínimo definido; y "normal" en otro caso.
- **FR-021**: Las tarjetas "Existencia baja" y "Sin existencia" de Inicio MUST mostrar los conteos
  reales de productos activos (los inactivos no cuentan) y MUST llevar a Existencias con el filtro correspondiente, sustituyendo el estado vacío
  actual.
- **FR-022**: Los mensajes de error MUST estar en español, ser comprensibles para el operador y no
  mostrar detalles técnicos.
- **FR-023**: Los productos existentes MUST conservarse íntegros al introducir esta funcionalidad y
  quedar como "no controla inventario".

### Key Entities

- **Producto (ampliado)**: indica si controla inventario, su unidad de medida, existencia mínima
  opcional y existencia actual (derivada de sus movimientos, solo lectura).
- **Unidad de medida**: catálogo fijo existente de 003 (claves SAT); ahora define además si la
  cantidad es entera o admite hasta 3 decimales.
- **Movimiento de inventario**: registro inmutable de un cambio de existencia: producto, tipo,
  cantidad, fecha y hora, usuario, existencia resultante, motivo y referencia opcional.
- **Tipo de movimiento**: inventario inicial, entrada, ajuste positivo, ajuste negativo; los dos
  primeros y el ajuste positivo suman, el ajuste negativo resta.
- **Estado de existencia**: normal, baja o sin existencia; se deduce de la existencia y el mínimo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tras cualquier secuencia de movimientos, la existencia de cada producto coincide en el
  100 % de los casos con la suma de sus movimientos.
- **SC-002**: El operador registra un movimiento en menos de 30 segundos desde la pantalla de
  Existencias.
- **SC-003**: Ningún movimiento puede modificarse ni eliminarse desde la aplicación.
- **SC-004**: El 100 % de los ajustes negativos que dejarían existencia negativa se rechazan con un
  mensaje claro y sin cambios en los datos.
- **SC-005**: Existencias y Movimientos muestran cada página de 100 registros en menos de 2
  segundos con un catálogo de 10 000 productos y 100 000 movimientos.
- **SC-006**: Los conteos de las tarjetas de Inicio coinciden con el número de filas del listado
  filtrado al que llevan.
- **SC-007**: Ninguna cantidad guardada excede los decimales permitidos por la unidad de su
  producto.

## Assumptions

- Un solo almacén por instalación; no hay ubicaciones ni traspasos.
- El catálogo de unidades no cambia: se reutiliza el de 003 (8 unidades, obligatorio en todo
  producto). Gramo y mililitro se tratan como cantidades enteras, igual que paquete; la
  descripción original solo mencionaba pieza, kilogramo, litro, metro y caja.
- Salidas por venta, costos y valuación, compras a proveedores, lotes, caducidades y números de
  serie están fuera de alcance; las salidas por venta se integrarán con el módulo de ventas.
- Se reutiliza la identificación del usuario actual ya existente para registrar quién hizo cada
  movimiento.
- Cualquier usuario con acceso a la aplicación puede registrar movimientos; los roles y permisos
  por acción quedan fuera de este alcance.
- Cambiar la unidad o desactivar el control de inventario en un producto con movimientos no se
  permite; si el operador se equivocó, corrige con movimientos.
- El "estado baja" excluye a los productos sin existencia para que ambas tarjetas de Inicio no
  cuenten dos veces al mismo producto.
- Se reutiliza el patrón de pantallas, paginación y filtros ya establecido en el catálogo de
  Productos y las opciones de menú Existencias y Movimientos ya definidas en la navegación.
