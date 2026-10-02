# Feature Specification: Alertas inteligentes de bajo stock

**Feature Branch**: `022-low-stock-alerts`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Alertas inteligentes de bajo stock: notificaciones automáticas y
gestión de puntos de reorden. Objetivo: notificar automáticamente cuando un producto alcanza un
nivel crítico de existencia. H1 (P3) Configuración por producto: cada producto que controla
inventario tiene existencia mínima (alerta) y punto de reorden (urgente); existencia mínima <
punto de reorden. H2 (P3) Alertas: al abrir la aplicación o cada hora, si hay productos con
existencia ≤ punto de reorden, notificación urgente; si existencia ≤ mínimo pero > punto de
reorden, notificación normal; notificación clickeable que lleva a 'Reportes > Inventario'
filtrado. H3 (P3) Dashboard: tarjeta en Inicio con la cantidad de productos en alerta y urgentes,
con colores. Criterios: la alerta se dispara cuando se alcanza el nivel; no dispara
constantemente (una por día máximo); el usuario puede descartar alertas manualmente."

## Contexto

El inventario (004) ya permite que un producto controle inventario y defina una existencia mínima
opcional; con ella se calcula el estado de existencia (normal, baja, sin existencia). Inicio
muestra las tarjetas "Existencia baja" y "Sin existencia", y "Reportes > Inventario" (009) muestra
el estado del inventario con filtro por estado. Hoy nada avisa al operador si no abre esas
pantallas. Esta funcionalidad:

- Agrega un segundo umbral por producto, el punto de reorden, que marca el nivel urgente.
- Avisa automáticamente, sin que el operador busque, cuando hay productos en nivel de alerta o
  urgente, sin repetir el aviso más de una vez al día.
- Resume en Inicio cuántos productos están en alerta y cuántos son urgentes.

**Nota sobre los umbrales**: la descripción original decía "existencia mínima < punto de reorden",
pero las reglas de H2 solo pueden cumplirse si el punto de reorden es **menor** que la existencia
mínima; se aclaró así (ver Clarifications y FR-002). Ejemplo: mínimo 20 (alerta), punto de
reorden 5 (urgente).

## Clarifications

### Session 2026-10-02

- Q: ¿Cuál de los dos umbrales debe ser el más bajo: el punto de reorden o la existencia mínima? →
  A: El punto de reorden es menor que la existencia mínima; mínimo = alerta normal, punto de
  reorden = urgente.
- Q: Si el Cajero descarta o ya vio la notificación del día, ¿el Administrador debe recibirla igual
  cuando inicie sesión ese mismo día? → A: Sí. El control es por usuario: cada usuario recibe como
  máximo una notificación por producto y nivel al día, y lo que descarta solo le afecta a él.
- Q: ¿Qué debe pasar en Inicio con la tarjeta "Existencia baja" que ya existe cuando se agregue la
  nueva tarjeta "Alertas de existencia"? → A: La nueva tarjeta la reemplaza; "Sin existencia" y
  "Alertas" (productos críticos y arqueos) no cambian.
- Q: ¿Un producto con existencia en 0 debe contarse como "urgente" en las notificaciones y en la
  tarjeta, o solo como "Sin existencia"? → A: Los niveles son independientes del estado del
  reporte: con existencia ≤ punto de reorden es urgente aunque esté en 0, y el filtro "Urgente"
  del reporte incluye los agotados; "Sin existencia" se sigue mostrando como estado aparte.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configuración de umbrales por producto (Priority: P3, primera de la funcionalidad)

Quien administra productos abre un producto que controla inventario y, además de la existencia
mínima que ya existe, captura un punto de reorden. Ambos valores son opcionales y respetan los
decimales de la unidad de medida del producto. El sistema valida que la relación entre ambos sea
la correcta y guarda los dos.

**Why this priority**: sin el punto de reorden no existe el nivel urgente; es la base de las
alertas y de la tarjeta de Inicio.

**Independent Test**: editar un producto que controla inventario, capturar mínimo y punto de
reorden válidos, guardar y volver a abrir; luego capturar una relación inválida y comprobar que se
rechaza con un mensaje claro.

**Acceptance Scenarios**:

1. **Given** un producto que controla inventario, **When** se captura una existencia mínima y un
   punto de reorden que cumplen FR-002 y se guarda, **Then** ambos valores quedan guardados y se
   muestran al reabrir el producto.
2. **Given** un producto que controla inventario, **When** se captura un punto de reorden que no
   cumple la relación de FR-002 con la existencia mínima, **Then** se rechaza el guardado con un
   mensaje junto al campo que indica la relación correcta.
3. **Given** un producto con unidad sin decimales (pieza), **When** se captura un punto de reorden
   con decimales, **Then** se rechaza igual que la existencia mínima.
4. **Given** un producto que no controla inventario, **When** se edita, **Then** no se pide punto
   de reorden y no se guarda ninguno.
5. **Given** un producto con existencia mínima y sin punto de reorden, **When** se guarda,
   **Then** se acepta; el producto solo podrá generar alertas normales.

---

### User Story 2 - Notificaciones automáticas (Priority: P3, segunda de la funcionalidad)

Al iniciar sesión, y después cada hora mientras la aplicación siga abierta, el sistema revisa la
existencia de los productos activos que controlan inventario. Si hay productos en nivel urgente
muestra una notificación urgente; si hay productos en nivel de alerta muestra una notificación
normal. Cada notificación indica cuántos productos están en ese nivel y, al pulsarla, abre
"Reportes > Inventario" filtrado por ese nivel. El operador puede descartar una notificación; un
producto ya notificado en un nivel no vuelve a notificarse en ese nivel el mismo día.

**Why this priority**: es el objetivo de la funcionalidad: que el operador se entere sin buscar.

**Independent Test**: dejar un producto en nivel urgente y otro en nivel de alerta, iniciar
sesión y comprobar las dos notificaciones, su conteo y su navegación; descartarlas, forzar una
nueva revisión el mismo día y comprobar que no reaparecen.

**Acceptance Scenarios**:

1. **Given** un producto cuya existencia es igual o menor a su punto de reorden, **When** inicia
   sesión un usuario que puede ver el inventario, **Then** aparece una notificación urgente que
   indica la cantidad de productos en nivel urgente.
2. **Given** un producto en nivel de alerta (existencia ≤ mínimo y > punto de reorden), **When**
   se realiza una revisión, **Then** aparece una notificación normal, visualmente distinta de la
   urgente.
3. **Given** una notificación visible, **When** el operador la pulsa, **Then** se abre
   "Reportes > Inventario" con el filtro del nivel correspondiente aplicado y la notificación se
   cierra.
4. **Given** una notificación visible, **When** el operador la descarta, **Then** desaparece y los
   productos que incluía no vuelven a notificarse en ese mismo nivel durante el día.
5. **Given** un producto ya notificado hoy en nivel de alerta, **When** una revisión posterior del
   mismo día lo encuentra en el mismo nivel, **Then** no se genera una nueva notificación por él.
6. **Given** un producto notificado hoy en nivel de alerta, **When** una venta lo baja a nivel
   urgente y llega la siguiente revisión, **Then** se genera la notificación urgente ese mismo día.
7. **Given** un producto notificado ayer y que sigue en el mismo nivel, **When** se realiza la
   primera revisión del día siguiente, **Then** vuelve a notificarse.
8. **Given** que no hay productos en alerta ni urgentes, **When** se realiza una revisión,
   **Then** no aparece ninguna notificación.
9. **Given** un producto notificado que se reabastece por encima de su existencia mínima, **When**
   vuelve a bajar a un nivel de alerta otro día, **Then** se notifica normalmente.

---

### User Story 3 - Tarjeta de alertas en Inicio (Priority: P3, tercera de la funcionalidad)

En Inicio, el usuario que puede ver el inventario encuentra una tarjeta "Alertas de existencia"
con dos cifras: productos en alerta (color de advertencia) y productos urgentes (color de
peligro). Pulsar cada cifra abre "Reportes > Inventario" filtrado por ese nivel.

**Why this priority**: da una vista permanente del problema aunque la notificación se haya
descartado.

**Independent Test**: preparar productos en ambos niveles, abrir Inicio y comprobar las cifras,
los colores y la navegación; registrar una entrada que saque un producto del nivel y comprobar que
la cifra baja al volver a Inicio.

**Acceptance Scenarios**:

1. **Given** 3 productos urgentes y 5 en alerta, **When** se abre Inicio, **Then** la tarjeta
   muestra "Urgentes: 3" en color de peligro y "En alerta: 5" en color de advertencia.
2. **Given** que no hay productos en ningún nivel, **When** se abre Inicio, **Then** la tarjeta
   muestra ambas cifras en 0 con un color neutro.
3. **Given** la tarjeta visible, **When** se pulsa la cifra de urgentes, **Then** se abre
   "Reportes > Inventario" filtrado por nivel urgente.
4. **Given** notificaciones descartadas, **When** se abre Inicio, **Then** la tarjeta sigue
   contando esos productos (descartar no los saca de la tarjeta).

---

### Edge Cases

- Producto sin existencia mínima ni punto de reorden: nunca entra en alerta ni en urgente.
- Producto con punto de reorden y sin existencia mínima: se acepta; solo puede llegar a urgente.
- Producto con existencia 0 o negativa y con umbrales: es urgente si tiene punto de reorden; si
  solo tiene mínimo, es de alerta.
- Producto notificado hoy que se reabastece por encima de su existencia mínima y vuelve a bajar
  al mismo nivel ese mismo día: no se notifica de nuevo hasta el día siguiente (lo notificado del
  día se conserva). Si vuelve a bajar a un nivel más alto que el notificado (alerta → urgente), sí
  se notifica.
- La aplicación se cierra o reinicia con una notificación visible sin pulsar ni descartar: ya
  cuenta como notificada y no reaparece ese día; la tarjeta "Alertas de existencia" de Inicio
  sigue mostrando los conteos.
- Producto inactivo, borrado o que dejó de controlar inventario: no se notifica ni se cuenta.
- La aplicación se mantiene abierta al pasar la medianoche: la primera revisión del nuevo día
  vuelve a notificar los productos que sigan en nivel.
- Cambio de usuario (cierre de sesión e inicio de otro usuario): se hace una revisión al iniciar
  sesión; el nuevo usuario recibe las notificaciones del día aunque el anterior ya las haya visto
  o descartado. Si el mismo usuario vuelve a iniciar sesión ese día, no se le repiten.
- El usuario no tiene permiso para ver el inventario: no recibe notificaciones ni ve la tarjeta.
- El operador está a mitad de una venta cuando llega una revisión: la notificación no toma el foco
  ni interrumpe la captura; se muestra de forma no bloqueante.
- Se cambian los umbrales de un producto ya notificado hoy: el nuevo nivel se evalúa en la
  siguiente revisión; si sube de nivel (alerta → urgente) se notifica, si no, no se repite.
- La revisión falla por un error inesperado: se registra en el log, no se muestra error al
  operador y se reintenta en la siguiente revisión programada.
- Muchos productos en nivel (cientos): se muestra una sola notificación por nivel con el conteo,
  no una por producto.

## Requirements *(mandatory)*

### Functional Requirements

**Configuración (H1)**

- **FR-001**: Un producto que controla inventario MUST admitir un punto de reorden opcional, no
  negativo y con los decimales permitidos por su unidad de medida, además de la existencia mínima
  existente.
- **FR-002**: Cuando un producto tiene ambos umbrales, el sistema MUST exigir que el punto de
  reorden sea estrictamente menor que la existencia mínima y rechazar el guardado si no se cumple,
  con el mensaje "El punto de reorden debe ser menor que la existencia mínima." junto al campo.
- **FR-003**: Si el producto no controla inventario, el punto de reorden MUST quedar vacío.
- **FR-004**: Los cambios del punto de reorden MUST registrarse en la bitácora de auditoría junto
  con los demás campos auditados del producto (018).

**Niveles y revisiones (H2)**

- **FR-005**: El sistema MUST clasificar cada producto activo que controla inventario en uno de
  tres niveles:
  - **Urgente**: tiene punto de reorden y su existencia es menor o igual a él.
  - **Alerta**: tiene existencia mínima, su existencia es menor o igual a ella y no es urgente.
  - **Sin alerta**: cualquier otro caso.

  El nivel es independiente del estado de existencia del reporte (normal, baja, sin existencia):
  un producto con existencia 0 que cumple la regla es urgente (o de alerta, si solo tiene mínimo)
  y además conserva su estado "sin existencia".
- **FR-006**: El sistema MUST realizar una revisión de niveles al iniciar sesión y después cada
  hora mientras la aplicación esté abierta con una sesión iniciada.
- **FR-007**: En cada revisión, si hay productos urgentes que no se notificaron hoy como urgentes,
  el sistema MUST mostrar una notificación urgente; si hay productos en alerta que no se
  notificaron hoy en ningún nivel, MUST mostrar una notificación normal.
- **FR-008**: Cada notificación MUST indicar el nivel y el total de productos que están hoy en ese
  nivel, y distinguirse visualmente: la urgente con color de peligro, la normal con color de
  advertencia.
- **FR-009**: Un producto MUST notificarse a cada usuario como máximo una vez por día por nivel;
  lo notificado o descartado por un usuario no afecta a los demás. El paso de
  alerta a urgente el mismo día sí genera notificación urgente; el paso de urgente a alerta no
  genera notificación. El "día" es el día calendario local del equipo.
- **FR-010**: Pulsar una notificación MUST abrir "Reportes > Inventario" con la fecha de hoy y el
  filtro del nivel de la notificación aplicado, y cerrar la notificación.
- **FR-011**: El operador MUST poder descartar una notificación; descartarla cierra la
  notificación sin navegar y cuenta como notificada, solo para ese usuario, para los productos que
  incluía.
- **FR-012**: Las notificaciones MUST ser no bloqueantes: no toman el foco, no interrumpen una
  venta en curso y permanecen visibles hasta que el operador las pulsa o descarta.
- **FR-013**: Solo los usuarios con permiso para ver el inventario MUST recibir notificaciones.
- **FR-014**: El registro de lo notificado por usuario y por día MUST persistir entre reinicios de
  la aplicación, para que reiniciar o volver a iniciar sesión no repita las notificaciones del día.
- **FR-015**: Una falla durante la revisión MUST registrarse en el log sin mostrar error al
  operador ni afectar la operación en curso.

**Reporte e Inicio (H2, H3)**

- **FR-016**: "Reportes > Inventario" MUST permitir filtrar por nivel urgente y por nivel de
  alerta, aplicando la regla de FR-005 (los productos sin existencia se incluyen si les
  corresponde ese nivel), y mostrar el punto de reorden en la tabla junto a la existencia mínima.
  El filtro por estado existente (normal, baja, sin existencia) se conserva.
- **FR-017**: Inicio MUST mostrar a los usuarios con permiso para ver el inventario una tarjeta
  "Alertas de existencia" con el número de productos urgentes (color de peligro) y en alerta
  (color de advertencia); con 0 en ambos, color neutro.
- **FR-018**: Cada cifra de la tarjeta MUST navegar a "Reportes > Inventario" filtrado por su
  nivel.
- **FR-018a**: La tarjeta "Alertas de existencia" MUST reemplazar a la tarjeta "Existencia baja"
  de Inicio (004); las tarjetas "Sin existencia" y "Alertas" (009) se conservan sin cambios.
- **FR-019**: La tarjeta MUST calcularse con la existencia actual cada vez que se muestra Inicio y
  no depender de si las notificaciones se descartaron.

### Key Entities

- **Producto (ampliado)**: agrega el punto de reorden (opcional, con los decimales de su unidad)
  a la existencia mínima existente.
- **Nivel de existencia**: clasificación calculada (urgente, alerta, sin alerta) a partir de la
  existencia actual y los dos umbrales; no se guarda.
- **Registro de notificación**: por usuario, producto, nivel y día: qué productos ya se le
  notificaron (o descartó) a cada usuario, en qué nivel y en qué día. Evita repetir avisos y
  sobrevive a reinicios.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un producto que llega a nivel urgente durante la jornada se notifica en un máximo de
  1 hora (o al siguiente inicio de sesión si la aplicación estaba cerrada).
- **SC-002**: Se cumple al 100 % la regla de FR-009 y FR-014: ningún usuario recibe más de una
  notificación por producto y nivel en el mismo día, aunque la aplicación se reinicie o vuelva a
  iniciar sesión.
- **SC-003**: Desde la notificación o desde la tarjeta, el operador llega a la lista filtrada de
  productos afectados en 1 clic.
- **SC-004**: La revisión de niveles no provoca ninguna pausa perceptible en el Punto de venta con
  un catálogo de 10 000 productos.
- **SC-005**: Las cifras de la tarjeta de Inicio coinciden al 100 % con el número de filas del
  reporte "Reportes > Inventario" filtrado por el mismo nivel.

## Assumptions

- La existencia mínima existente (004) es el umbral de "alerta"; no se crea un campo nuevo para
  ella. El punto de reorden es el campo nuevo.
- Los niveles se evalúan con la existencia actual, no con la existencia al final de un periodo.
- Las notificaciones agrupan productos por nivel (una notificación por nivel), no una por
  producto.
- "Una por día máximo" se interpreta por producto y por nivel, con escalamiento de alerta a
  urgente permitido el mismo día.
- El permiso usado es el existente para ver el inventario (hoy lo tienen Administrador y Cajero);
  no se crea un permiso nuevo.
- Las notificaciones y la tarjeta solo existen si el módulo de inventario está habilitado por la
  licencia (012).
- La revisión por hora solo ocurre mientras la aplicación está abierta; no hay notificaciones del
  sistema operativo con la aplicación cerrada.
- No se generan órdenes de compra automáticas al llegar al punto de reorden; eso queda fuera del
  alcance.
- Los umbrales se capturan producto por producto; la edición masiva queda fuera del alcance.
