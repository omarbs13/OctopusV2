# Feature Specification: Proveedores y compras básicas

**Feature Branch**: `020-suppliers-purchases`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Proveedores y compras básicas: catálogo de proveedores y registro de
entradas de mercancía. Registrar de dónde vienen los productos y mantener un histórico de compras
para auditoría e inventario. H1 (P3) Catálogo de proveedores: alta con nombre, RUC, teléfono,
email, dirección (opcional), condiciones de pago; listado, edición, desactivación. H2 (P3)
Registrar compra desde 'Inventario > Entrada de mercancía': número de factura del proveedor,
proveedor, fecha, líneas (producto, cantidad, costo unitario); genera automáticamente un
movimiento de inventario tipo 'Entrada de compra'; calcula el costo total. H3 (P3) Reporte de
compras: filtro por proveedor, fecha, total; costo total acumulado. Criterios: la entrada de
compra afecta inventario correctamente; el costo unitario se guarda por compra y no actualiza el
producto retroactivamente; el reporte de compras es consultable."

## Clarifications

### Session 2026-10-01

- Q: Si una compra se registró con un error, ¿cómo se corrige? → A: El Administrador anula la
  compra completa con motivo; se generan movimientos "Anulación de compra" que revierten la
  existencia (se rechaza si la existencia actual no alcanza), la compra queda "Anulada" y sale de
  los totales del reporte. No se anulan líneas sueltas ni se editan compras.
- Q: ¿Qué roles pueden registrar compras en "Entrada de mercancía"? → A: Un permiso nuevo,
  "Registrar compras", asignado por omisión solo al Administrador; es independiente de "Registrar
  movimientos de inventario".
- Q: ¿El costo unitario capturado incluye impuestos? → A: No. El costo unitario es antes de
  impuestos; se captura además el importe total de impuestos de la factura. La compra guarda
  subtotal (suma de líneas), impuestos y total (subtotal + impuestos), y el reporte acumula los
  tres.
- Q: ¿El RUC del proveedor es obligatorio? → A: No. Es opcional; cuando se captura debe ser único
  entre todos los proveedores. El nombre puede repetirse.
- Q: ¿Cómo se registra la mercancía bonificada (costo $0) de una factura? → A: Como línea de la
  misma compra con costo $0.00, marcada "Bonificación"; se rechaza la compra si su subtotal es
  $0.00.

## User Scenarios & Testing *(mandatory)*

Las tres historias tienen prioridad P3 en el conjunto del producto. Dentro de esta funcionalidad se
ordenan por dependencia: el catálogo de proveedores es requisito para registrar compras, y las
compras son requisito para el reporte.

### User Story 1 - Catálogo de proveedores (Priority: P3, primera de la funcionalidad)

El Administrador da de alta a los proveedores con nombre, RUC, teléfono, email, dirección
(opcional) y condiciones de pago. Puede consultarlos en un listado con búsqueda, editar sus datos
y desactivarlos cuando dejan de surtir. Un proveedor desactivado conserva su historial de compras,
pero ya no se puede elegir en compras nuevas.

**Why this priority**: sin proveedores no se puede registrar de dónde viene la mercancía; es la
base de las otras dos historias.

**Independent Test**: dar de alta un proveedor con todos los datos y otro solo con nombre; buscar
por nombre y por RUC; editar el teléfono; desactivar uno y verificar que desaparece del listado
por omisión y aparece con el filtro "incluir inactivos".

**Acceptance Scenarios**:

1. **Given** el catálogo vacío, **When** el Administrador captura un proveedor con nombre y
   condiciones de pago, **Then** el proveedor se guarda como activo y aparece en el listado.
2. **Given** un proveedor con RUC "X", **When** se intenta dar de alta otro proveedor con el mismo
   RUC, **Then** se rechaza con un mensaje que indica el proveedor existente.
3. **Given** un email con formato inválido, **When** se intenta guardar el proveedor, **Then** se
   rechaza indicando el campo con error.
4. **Given** un proveedor existente, **When** el Administrador edita su teléfono y condiciones de
   pago, **Then** los cambios se guardan y las compras ya registradas no se modifican.
5. **Given** un proveedor activo, **When** el Administrador lo desactiva, **Then** deja de
   aparecer en el listado por omisión y en el selector de proveedor de compras nuevas; sus compras
   siguen visibles en el reporte.
6. **Given** un proveedor inactivo, **When** el Administrador lo reactiva, **Then** vuelve a estar
   disponible para compras nuevas.
7. **Given** condiciones de pago "crédito", **When** no se capturan los días de crédito o son cero,
   **Then** se rechaza; con "contado" no se piden días.

---

### User Story 2 - Registrar compra (entrada de mercancía) (Priority: P3, segunda de la funcionalidad)

Desde "Inventario > Entrada de mercancía", un usuario con el permiso "Registrar compras" (por
omisión, el Administrador) registra una compra: elige el
proveedor, captura el número de factura del proveedor y la fecha de la factura, y agrega líneas con
producto, cantidad y costo unitario antes de impuestos, y al final el importe de impuestos que
indica la factura. El sistema muestra el importe de cada línea, el subtotal, los impuestos y el
total de la compra mientras se captura. Al guardar, la compra queda registrada y, en la misma operación,
cada línea genera un movimiento de inventario tipo "Entrada de compra" que aumenta la existencia
del producto y queda ligado a la compra.

**Why this priority**: es el núcleo de la funcionalidad: deja trazabilidad del origen y del costo
de la mercancía y mantiene el inventario al día.

**Independent Test**: con un producto en existencia 10, registrar una compra de 5 unidades a
$12.50 y otra línea de otro producto con 2.5 kg a $40.00, con impuestos de $26.00; verificar
subtotal $162.50, impuestos $26.00, total $188.50, existencia
15 del primer producto, dos movimientos "Entrada de compra" con la factura como referencia, y que
el precio y los datos del producto no cambiaron.

**Acceptance Scenarios**:

1. **Given** un proveedor activo y productos activos que controlan inventario, **When** el operador
   guarda una compra con factura, fecha, dos líneas e impuestos, **Then** la compra se registra con
   su subtotal, impuestos y total, y la existencia de cada producto aumenta exactamente en la cantidad de su línea.
2. **Given** una compra guardada, **When** se consulta el kárdex del producto, **Then** aparece un
   movimiento "Entrada de compra" con la cantidad, la existencia resultante, el usuario, el
   proveedor y el número de factura como referencia.
3. **Given** líneas de 3 unidades a $10.00 y 1.255 kg a $20.00, **When** se capturan, **Then** los
   importes de línea son $30.00 y $25.10 y el subtotal es $55.10; con impuestos de $8.82 el total
   es $63.92; con impuestos vacíos se toman como $0.00 y el total es igual al subtotal; con
   impuestos negativos, la compra se rechaza.
4. **Given** una compra registrada con costo unitario $12.50, **When** después se registra otra
   compra del mismo producto a $13.00, **Then** la primera compra conserva $12.50 y ningún dato del
   producto (precio de venta incluido) cambia por ninguna de las dos compras.
5. **Given** que ya existe una compra del proveedor "A" con factura "F-100", **When** se intenta
   registrar otra compra del proveedor "A" con factura "F-100", **Then** se rechaza indicando la
   compra existente; con el proveedor "B" y factura "F-100" se acepta.
6. **Given** una compra sin líneas, sin proveedor, sin número de factura o con fecha de factura
   posterior a hoy, **When** se intenta guardar, **Then** se rechaza indicando qué falta o qué es
   inválido, y no se crea ningún movimiento.
7. **Given** una línea con cantidad cero o negativa, con decimales en un producto de unidades
   enteras, o con costo unitario negativo, **When** se intenta guardar, **Then** se rechaza la
   compra completa con un mensaje que identifica la línea.
8. **Given** una compra con una línea de 10 unidades a $15.00 y otra de 2 unidades a $0.00,
   **When** se guarda, **Then** se acepta, la segunda línea aparece como "Bonificación" en el
   detalle, ambas suman a la existencia y el subtotal es $150.00; si todas las líneas tienen costo
   $0.00 (subtotal $0.00), la compra se rechaza.
9. **Given** un producto inactivo o que no controla inventario, **When** el operador busca productos
   para agregar a la compra, **Then** no aparece como opción; si llega a una línea por otra vía, la
   compra se rechaza.
10. **Given** un producto ya agregado a la compra, **When** el operador lo agrega otra vez, **Then**
   el sistema no crea una segunda línea: lleva al operador a la línea existente para ajustar su
   cantidad.
11. **Given** una compra guardada, **When** el operador la consulta, **Then** no existe opción para
    editarla, borrarla ni anular líneas sueltas; los errores se corrigen anulando la compra
    completa y registrándola de nuevo.
12. **Given** un fallo inesperado al guardar, **When** el operador intenta registrar la compra,
    **Then** no se guarda ni la compra ni ningún movimiento ni cambio de existencia, y la captura en
    pantalla no se pierde.
13. **Given** una compra vigente de 5 unidades de un producto con existencia 8, **When** el
    Administrador la anula capturando un motivo, **Then** se genera un movimiento "Anulación de
    compra" de 5 unidades, la existencia queda en 3, la compra queda "Anulada" con fecha, usuario y
    motivo, y su proveedor y factura pueden volver a usarse en una compra nueva.
14. **Given** una compra vigente de 5 unidades de un producto cuya existencia actual es 2, **When**
    el Administrador intenta anularla, **Then** se rechaza indicando qué producto no tiene
    existencia suficiente, y ni la compra ni el inventario cambian.
15. **Given** una anulación sin motivo, o una compra ya anulada, **When** se intenta anular,
    **Then** se rechaza con un mensaje claro.
16. **Given** un usuario con rol Cajero, **When** abre el menú Inventario, **Then** no ve "Entrada
    de mercancía" ni puede registrar ni anular compras; un Administrador sí.

---

### User Story 3 - Reporte de compras (Priority: P3, tercera de la funcionalidad)

En "Reportes > Compras", el Administrador consulta el histórico de compras filtrando por proveedor,
rango de fechas de factura y rango de total. El reporte lista cada compra (fecha, proveedor,
número de factura, número de líneas, subtotal, impuestos, total, usuario que la registró), muestra
el número de compras y el subtotal, impuestos y total acumulados de lo filtrado, y permite abrir el detalle de una compra con
sus líneas.

**Why this priority**: da el valor de auditoría y de control de gasto, pero depende de que existan
compras registradas.

**Independent Test**: registrar tres compras de dos proveedores en fechas distintas; filtrar por un
proveedor y un rango de fechas y verificar que solo aparecen las esperadas con el total acumulado
correcto; abrir una y verificar sus líneas y costos.

**Acceptance Scenarios**:

1. **Given** compras de varios proveedores, **When** se filtra por proveedor, rango de fechas y
   rango de total, **Then** solo se listan las que cumplen todos los filtros, de la más reciente a
   la más antigua.
2. **Given** un filtro aplicado, **When** se muestra el resultado, **Then** el subtotal, los
   impuestos y el total acumulados son exactamente la suma de los de las compras listadas en todas
   las páginas, no solo de la página visible.
3. **Given** más de 100 compras en el resultado, **When** se abre el reporte, **Then** se pagina de
   100 en 100.
4. **Given** una compra en el listado, **When** el Administrador abre su detalle, **Then** ve
   proveedor, factura, fecha, usuario, fecha de registro y cada línea con producto, cantidad, costo
   unitario e importe tal como se registraron.
5. **Given** un proveedor inactivo con compras, **When** se usa el filtro de proveedor, **Then** el
   proveedor inactivo está disponible en el filtro y sus compras se listan.
6. **Given** un rango de fechas sin compras, **When** se aplica, **Then** se muestra un mensaje de
   "sin resultados" con total acumulado $0.00.
7. **Given** un rango de total con mínimo mayor que el máximo, o una fecha inicial posterior a la
   final, **When** se aplica, **Then** se indica que el filtro es inválido.
8. **Given** una compra anulada, **When** se abre el reporte, **Then** no aparece ni suma al total
   acumulado; con el filtro "incluir anuladas" aparece marcada "Anulada", con su motivo visible en
   el detalle, y sigue sin sumar al total.

---

### Edge Cases

- Dos compras guardadas casi al mismo tiempo sobre el mismo producto: ambas suman su cantidad y la
  existencia final es la suma correcta; ninguna se pierde.
- Dos compras casi simultáneas con el mismo proveedor y la misma factura: solo una se guarda.
- Anulación al mismo tiempo que una venta del mismo producto: la existencia nunca queda bajo cero;
  si ya no alcanza, la anulación se rechaza.
- Anulación de una compra de un producto que después se desactivó o dejó de controlar inventario:
  se rechaza indicando el producto; primero hay que reactivarlo.
- Número de factura con espacios al inicio o al final, o con distinta capitalización ("f-100" y
  "F-100"): se considera el mismo número para la detección de duplicados.
- Producto desactivado después de registrar compras: las compras y sus líneas se siguen mostrando
  con el nombre del producto.
- Producto o proveedor renombrado después de una compra: el detalle de la compra muestra los datos
  guardados al momento de registrarla (nombre del proveedor, nombre y SKU del producto).
- Compra con muchas líneas (por ejemplo 200): se guarda completa en una sola operación.
- Importes de línea con fracciones de centavo (cantidad con decimales × costo): se redondean al
  centavo por línea y el total es la suma de los importes de línea redondeados.
- Fecha de factura anterior a hoy: se acepta (factura recibida antes de capturarse); el movimiento
  de inventario conserva la fecha y hora de captura del sistema.
- Proveedor sin RUC: se acepta; la regla de RUC único solo aplica cuando se captura.
- Licencia o permisos que no habilitan inventario: las pantallas de entrada de mercancía y de
  compras no están disponibles.

## Requirements *(mandatory)*

### Functional Requirements

**Catálogo de proveedores**

- **FR-001**: El sistema MUST permitir dar de alta proveedores con: nombre (obligatorio, hasta 150
  caracteres), RUC (opcional, hasta 20 caracteres), teléfono (opcional), email (opcional, con
  formato válido), dirección (opcional, hasta 300 caracteres) y condiciones de pago (obligatorio).
- **FR-002**: Las condiciones de pago MUST ser "contado" o "crédito"; en "crédito" el número de días
  de crédito es obligatorio y mayor que cero (hasta 365).
- **FR-003**: El RUC, cuando se captura, MUST ser único entre todos los proveedores (activos e
  inactivos), sin distinguir mayúsculas ni espacios al inicio o al final. El nombre del proveedor
  no tiene que ser único.
- **FR-004**: El sistema MUST listar proveedores con búsqueda por nombre o RUC, mostrando por
  omisión solo los activos y con un filtro "incluir inactivos".
- **FR-005**: El sistema MUST permitir editar todos los datos del proveedor; la edición no modifica
  las compras ya registradas.
- **FR-006**: El sistema MUST permitir desactivar y reactivar proveedores; no se eliminan
  proveedores con compras registradas.

**Registro de compras**

- **FR-007**: El sistema MUST ofrecer la pantalla "Inventario > Entrada de mercancía" para
  registrar una compra con: proveedor (solo activos), número de factura del proveedor
  (obligatorio, hasta 50 caracteres), fecha de factura (obligatoria, no posterior a la fecha
  actual) y una o más líneas.
- **FR-008**: Cada línea MUST tener producto (activo y que controla inventario), cantidad mayor que
  cero respetando los decimales que admite la unidad del producto, y costo unitario mayor o igual a
  cero expresado en moneda con centavos. Una línea con costo $0.00 es una bonificación: se muestra
  marcada como "Bonificación" en la captura y en el detalle, y aumenta la existencia como cualquier
  otra línea.
- **FR-008a**: El sistema MUST rechazar una compra cuyo subtotal sea $0.00 (todas sus líneas
  bonificadas).
- **FR-009**: Una compra MUST tener como máximo una línea por producto.
- **FR-010**: El costo unitario MUST ser el costo antes de impuestos. El sistema MUST calcular el
  importe de cada línea (cantidad × costo unitario, redondeado al centavo) y el subtotal de la
  compra (suma de importes de línea), y mostrarlos mientras se captura.
- **FR-010a**: La compra MUST incluir el importe total de impuestos de la factura, capturado por el
  operador como un solo importe (mayor o igual a cero; vacío equivale a cero). El sistema MUST
  calcular el total de la compra como subtotal + impuestos y guardar los tres importes. No se
  desglosan impuestos por línea ni por tasa.
- **FR-011**: El sistema MUST rechazar una compra cuyo proveedor y número de factura coincidan con
  los de una compra vigente (no anulada) (número de factura comparado sin distinguir mayúsculas ni espacios al
  inicio o al final).
- **FR-012**: Al guardar una compra, el sistema MUST registrar, en una única operación que se
  guarda completa o no se guarda, la compra, sus líneas y un movimiento de inventario tipo
  "Entrada de compra" por cada línea que aumenta la existencia del producto en la cantidad de la
  línea.
- **FR-013**: Cada movimiento "Entrada de compra" MUST quedar ligado a su compra y mostrar en el
  kárdex el número de factura como referencia, además de los datos que ya registra todo movimiento
  (producto, cantidad, fecha y hora de captura, usuario, existencia resultante).
- **FR-014**: El tipo "Entrada de compra" MUST distinguirse en el kárdex y en sus filtros de las
  entradas manuales existentes, y MUST poder generarse solo mediante el registro de una compra.
- **FR-015**: El costo unitario MUST guardarse en la línea de la compra; registrar una compra NO
  MUST modificar ningún dato del producto (precio de venta ni ningún otro) ni los costos de compras
  anteriores.
- **FR-016**: La compra MUST guardar, al momento de registrarla, el nombre del proveedor y el
  nombre y SKU de cada producto, para mostrarlos tal como estaban aunque después cambien.
- **FR-017**: Las compras registradas MUST ser de solo lectura: no se editan, no se eliminan y no se
  anulan líneas sueltas; la única corrección posible es anular la compra completa (FR-017a).
- **FR-017a**: El Administrador MUST poder anular una compra vigente capturando un motivo
  obligatorio. En una única operación que se guarda completa o no se guarda, el sistema genera por
  cada línea un movimiento de inventario tipo "Anulación de compra" que disminuye la existencia en
  la cantidad de la línea, y marca la compra como "Anulada" con fecha, usuario y motivo.
- **FR-017b**: El sistema MUST rechazar la anulación si la existencia actual de cualquier producto
  de la compra es menor que la cantidad de su línea, indicando los productos afectados, o si la
  compra ya está anulada. Una compra anulada no se puede reactivar.
- **FR-018**: El registro y la anulación de cada compra MUST quedar en la bitácora de auditoría con
  usuario, proveedor, número de factura, subtotal, impuestos, total y, en la anulación, el motivo; también el alta,
  edición, desactivación y reactivación de proveedores.

**Reporte de compras**

- **FR-019**: El sistema MUST ofrecer "Reportes > Compras" con filtros combinables por proveedor
  (incluidos inactivos), rango de fechas de factura y rango de total de la compra (mínimo y/o
  máximo, impuestos incluidos).
- **FR-020**: El reporte MUST listar, de la más reciente a la más antigua, fecha de factura,
  proveedor, número de factura, número de líneas, subtotal, impuestos, total y usuario, paginado de
  100 en 100.
- **FR-021**: El reporte MUST mostrar el número de compras y el subtotal, impuestos y total
  acumulados de todo el resultado filtrado (todas las páginas), considerando solo compras
  vigentes.
- **FR-021a**: Por omisión el reporte MUST excluir las compras anuladas; con el filtro "incluir
  anuladas" se listan identificadas como "Anuladas", sin sumarse al número de compras ni a los
  acumulados.
- **FR-022**: El sistema MUST permitir abrir el detalle de una compra con todas sus líneas
  (producto, cantidad, unidad, costo unitario, importe), el subtotal, los impuestos, el total y
  los datos de registro.
- **FR-023**: El sistema MUST rechazar filtros inválidos (fecha inicial posterior a la final,
  total mínimo mayor que el máximo, importes negativos) con un mensaje claro.

**Acceso**

- **FR-024**: La gestión de proveedores y el reporte de compras MUST estar disponibles solo para
  roles con el permiso correspondiente (por omisión, el Administrador).
- **FR-025**: Registrar compras MUST requerir un permiso nuevo, "Registrar compras", asignado por
  omisión solo al Administrador. Es independiente de "Registrar movimientos de inventario": tener
  uno no otorga el otro. Sin el permiso, la opción "Inventario > Entrada de mercancía" no está
  disponible. Como los permisos por rol son fijos (007), en esta versión solo el Administrador
  registra compras; el permiso propio permite asignarlo a otro rol en el futuro sin afectar los
  ajustes manuales.
- **FR-026**: Anular compras MUST estar reservado al Administrador.

### Key Entities *(include if feature involves data)*

- **Proveedor**: quien surte la mercancía. Nombre, RUC opcional, teléfono, email, dirección,
  condiciones de pago (contado o crédito con días) y estado activo/inactivo. Tiene muchas compras.
- **Compra**: una factura de proveedor registrada como entrada de mercancía. Proveedor (y su nombre
  al registrar), número de factura, fecha de factura, subtotal (suma de líneas), impuestos
  (importe único capturado), total (subtotal + impuestos), usuario y fecha de registro.
  Estado "Vigente" o "Anulada" (con fecha, usuario y motivo de anulación); la única transición es
  de Vigente a Anulada. Única por proveedor y número de factura entre las compras vigentes. Solo
  lectura una vez guardada.
- **Línea de compra**: producto (con nombre y SKU al registrar), cantidad, costo unitario e importe.
  Pertenece a una compra; genera exactamente un movimiento de inventario.
- **Movimiento de inventario "Entrada de compra"**: nuevo tipo del movimiento existente (004), que
  aumenta la existencia y referencia a su compra y línea.
- **Movimiento de inventario "Anulación de compra"**: nuevo tipo que disminuye la existencia al
  anular una compra; referencia a la compra y línea que revierte. Solo se genera al anular.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En el 100% de las compras registradas, la existencia de cada producto aumenta
  exactamente en la cantidad de su línea, y la suma de movimientos del kárdex coincide con la
  existencia actual.
- **SC-002**: En el 100% de los casos, registrar una compra no cambia ningún dato del producto ni el
  costo de compras anteriores.
- **SC-003**: El operador registra una compra de 10 líneas en menos de 3 minutos.
- **SC-004**: El Administrador obtiene el total comprado a un proveedor en un mes en menos de 30
  segundos, con un total acumulado que coincide al centavo con la suma de los totales de las
  facturas registradas.
- **SC-005**: El reporte de compras muestra resultados en menos de 2 segundos con 10,000 compras
  registradas.
- **SC-006**: Ningún fallo durante el guardado deja una compra sin sus movimientos o movimientos sin
  su compra (0 inconsistencias en pruebas de fallo).

## Assumptions

- "RUC" es el identificador fiscal del proveedor; se captura como texto libre (hasta 20
  caracteres) sin validar su estructura, porque el negocio ya maneja RFC en su perfil y el formato
  varía por país. Es opcional porque hay proveedores pequeños que no lo proporcionan.
- Las condiciones de pago son informativas: no se implementan cuentas por pagar, pagos a
  proveedores, saldos ni vencimientos (fuera de alcance).
- El costo unitario es antes de impuestos y los impuestos se capturan como un solo importe por
  factura; no se calculan a partir de las tasas de impuesto (019) ni se desglosan por tasa. No se
  capturan descuentos de factura ni fletes por separado. No se calcula costo promedio ni margen, y
  el producto no guarda un "último costo" (fuera de alcance).
- No se soportan órdenes de compra, recepciones parciales ni devoluciones parciales a proveedor; un
  error en una compra se corrige anulándola completa y registrándola de nuevo.
- La mercancía regalada o bonificada que llega con una factura se registra dentro de la misma
  compra como línea de costo $0.00; la que llega sin factura se registra como entrada manual
  existente (004).
- La fecha de la factura es un dato de la compra; el movimiento de inventario usa la fecha y hora
  de captura asignada por el sistema, como el resto de movimientos (004).
- Las entradas manuales existentes ("Entrada" de 004) se conservan sin cambios; la nueva
  "Entrada de compra" convive con ellas.
- Un solo almacén y una sola moneda por instalación, como en el resto del sistema.
- Proveedores, compras y el reporte forman parte del módulo de inventario de la licencia (012).
- Se reutilizan la bitácora de auditoría (018), los roles y permisos (007), el kárdex (004) y la
  sección de Reportes (009).
