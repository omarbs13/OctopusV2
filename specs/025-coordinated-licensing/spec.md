# Feature Specification: Licenciamiento coordinado con OctopusAdmin

**Feature Branch**: `025-coordinated-licensing`

**Created**: 2026-10-03

**Status**: Draft

**Input**: User description: "Licenciamiento coordinado con OctopusAdmin: catálogo compartido de 9 módulos, solicitud de licencia, licencia firmada con vencimiento por módulo y bloqueo total sin módulo POS."

## Clarifications

### Session 2026-10-03

- Q: ¿Qué se hace con las licencias de formato 2 ya emitidas? → A: Se elimina el soporte del formato 2, porque no hay clientes con ellas.
- Q: ¿Cómo se decide si una licencia importada es más antigua que la vigente? → A: La fecha de emisión incluye fecha y hora en UTC. Se acepta una licencia con el mismo id de licencia que la vigente (reimportación) o con fecha y hora de emisión posteriores; cualquier otra se rechaza.
- Q: ¿Qué pasa con una venta en curso cuando vence la licencia? → A: El vencimiento nunca interrumpe una venta en curso: se permite terminarla, cobrarla y cerrar el turno abierto; solo se bloquean las ventas nuevas.
- Q: Si la licencia guardada falla la verificación o desaparece, ¿vuelve la prueba de 30 días? → A: No. Al aceptar la primera licencia, la prueba termina definitivamente. Desde entonces, sin licencia válida el sistema queda bloqueado por "licencia no válida" hasta que se importe una válida. Así, alterar o borrar la licencia guardada no puede dar los 9 módulos de la prueba.
- Q: ¿Quién puede usar "Ayuda > Licencia" en bloqueo? → A: Cualquier usuario con sesión puede ver el estado, copiar el ID y generar la solicitud, porque la solicitud no es secreta. Importar la licencia y exportar el respaldo requieren el rol Administrador, porque el respaldo contiene todos los datos del negocio y siempre existe al menos un Administrador activo.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Catálogo compartido de módulos (Priority: P1)

El proveedor (OctopusAdmin) y el POS hablan el mismo idioma sobre qué módulos existen. Un catálogo único de 9 módulos —POS, Inventario, Reportes avanzados, Crédito y clientes, Turnos y arqueo, Devoluciones, Proveedores, Descuentos y promociones y Categorías de productos— vive como contrato idéntico en ambos repositorios. Cada módulo tiene un identificador permanente, una clave estable en inglés, nombre, descripción y orden; POS está marcado como módulo base. Los seis módulos que ya existían conservan su identificador, de modo que las licencias ya emitidas siguen siendo válidas. Proveedores y Categorías pasan a ser módulos controlados por licencia, igual que los demás.

**Why this priority**: Es la base de todo lo demás: sin un catálogo común, una licencia emitida por OctopusAdmin no puede activar los módulos correctos en el POS.

**Independent Test**: Comparar el catálogo publicado con el catálogo interno de la aplicación y con los identificadores históricos; bloquear Proveedores o Categorías con una licencia que no los incluya y comprobar que su opción desaparece y sus operaciones se rechazan.

**Acceptance Scenarios**:

1. **Given** el catálogo compartido, **When** se compara con los identificadores de los 6 módulos existentes, **Then** los 6 identificadores son exactamente los mismos que antes de esta funcionalidad.
2. **Given** el catálogo compartido y el catálogo interno de la aplicación, **When** se ejecuta la prueba automática de consistencia, **Then** ambos coinciden en número de módulos, identificadores, claves, orden y módulo base; cualquier diferencia hace fallar la prueba.
3. **Given** la aplicación instalada, **When** se elimina o edita cualquier archivo suelto de la instalación, **Then** el catálogo que usa la aplicación no cambia, porque viaja dentro de la propia aplicación.
4. **Given** una licencia vigente sin el módulo Proveedores, **When** el operador busca la opción de Proveedores o intenta registrar una compra, **Then** la opción no aparece en el menú y la operación se rechaza con el mensaje de módulo no activo, sin modificar datos.
5. **Given** una licencia vigente sin el módulo Categorías de productos, **When** se intenta crear o editar una categoría, **Then** la operación se rechaza con el mensaje de módulo no activo y las categorías ya existentes se conservan.

---

### User Story 2 - Contrato del formato de licencia (Priority: P1)

Ambas aplicaciones comparten un documento que define el formato de licencia versión 3: qué datos contiene (versión de formato, id de licencia, fecha y hora de emisión en UTC, id de máquina, nombre del cliente y la lista de módulos con fecha de activación y fecha de vencimiento opcional), cómo se interpretan las fechas y exactamente qué contenido se firma, de modo que lo que OctopusAdmin firma es exactamente lo que el POS verifica.

**Why this priority**: Sin un contrato preciso, dos implementaciones independientes producirán firmas que no coinciden y ninguna licencia será aceptada.

**Independent Test**: Firmar una licencia de ejemplo siguiendo solo el documento y verificar que el POS la acepta; cambiar un solo carácter del contenido y verificar que la rechaza.

**Acceptance Scenarios**:

1. **Given** el documento del formato, **When** un tercero firma una licencia siguiéndolo al pie de la letra con la clave del proveedor, **Then** el POS la acepta.
2. **Given** una licencia válida, **When** se modifica cualquier dato firmado (un módulo, una fecha, el id de máquina, el cliente), **Then** la firma deja de ser válida y el POS la rechaza.
3. **Given** un módulo con vencimiento el día 15, **When** la fecha local del POS es el día 15, **Then** el módulo sigue activo todo ese día; el día 16 ya está vencido.
4. **Given** un módulo sin fecha de vencimiento, **When** pasa cualquier cantidad de tiempo, **Then** el módulo permanece activo indefinidamente.

---

### User Story 3 - Solicitud de licencia (Priority: P1)

Desde "Ayuda > Licencia", el operador pulsa "Generar solicitud" y guarda un archivo de solicitud que contiene el id de máquina, el nombre del negocio, la versión de la aplicación, la versión del catálogo y la fecha. Lo envía al proveedor por cualquier medio (correo, mensajería, USB). Como alternativa, puede copiar el id de máquina con un botón "Copiar" y dictarlo o pegarlo.

**Why this priority**: Es el primer paso para que el proveedor emita una licencia para esa máquina concreta.

**Independent Test**: Generar la solicitud y comprobar que el id de máquina del archivo es idéntico al mostrado en pantalla y al que usa la verificación de licencias.

**Acceptance Scenarios**:

1. **Given** la pantalla "Ayuda > Licencia", **When** el operador pulsa "Generar solicitud" y elige dónde guardarla, **Then** se crea un archivo de solicitud con el id de máquina correcto, el nombre del negocio, la versión de la aplicación, la versión del catálogo y la fecha.
2. **Given** la pantalla "Ayuda > Licencia", **When** el operador pulsa "Copiar" junto al id de máquina, **Then** el id queda en el portapapeles, idéntico al del archivo de solicitud.
3. **Given** un sistema bloqueado por falta del módulo POS, **When** el operador abre "Ayuda > Licencia", **Then** puede generar la solicitud y copiar el id de máquina.

---

### User Story 4 - Importar y verificar la licencia (Priority: P1)

En "Ayuda > Licencia", el operador pulsa "Importar licencia" y elige el archivo de licencia recibido. El sistema comprueba, en este orden, la firma del proveedor, que el id de máquina coincida y que la licencia sea la misma que la vigente (reimportación) o más reciente. Si todo es correcto, la nueva licencia reemplaza por completo a la anterior y los módulos se aplican al instante. Si algo falla, se muestra un mensaje claro que indica la causa y la licencia anterior sigue vigente. La licencia se guarda tal como llegó y su firma se vuelve a comprobar en cada arranque.

**Why this priority**: Es el único camino para convertir una prueba o una licencia anterior en los módulos contratados.

**Independent Test**: Importar licencias válidas, alteradas, de otra máquina y más antiguas, y comprobar en cada caso el resultado y la licencia que queda vigente; editar la licencia guardada y reiniciar.

**Acceptance Scenarios**:

1. **Given** una licencia firmada por el proveedor para esta máquina con Inventario y POS, **When** se importa, **Then** quedan activos exactamente POS e Inventario, sin reiniciar la aplicación, y el menú se actualiza de inmediato.
2. **Given** una licencia con la firma alterada, **When** se importa, **Then** se rechaza con un mensaje que indica que la licencia no es auténtica y se conserva la anterior.
3. **Given** una licencia válida emitida para otra máquina, **When** se importa, **Then** se rechaza con un mensaje que indica que la licencia pertenece a otro equipo y se conserva la anterior.
4. **Given** una licencia vigente emitida el día 10 a las 15:00 UTC, **When** se importa una licencia válida con otro id emitida el día 10 a las 15:00 UTC o antes, **Then** se rechaza con un mensaje que indica que no es más reciente que la vigente y se conserva la anterior.
5. **Given** una licencia vigente con Inventario y Devoluciones, **When** se importa una licencia más reciente que solo incluye POS e Inventario, **Then** Devoluciones deja de estar activo (la licencia es una fotografía completa, no se suma a la anterior).
6. **Given** una licencia guardada, **When** alguien edita su contenido directamente en la base de datos y reinicia, **Then** la licencia se rechaza al arrancar, no habilita ningún módulo (tampoco vuelve la prueba), el sistema queda bloqueado por licencia no válida y se avisa al operador.
7. **Given** una licencia que incluye un identificador de módulo desconocido, **When** se importa, **Then** se acepta, el identificador desconocido se ignora y los módulos conocidos se aplican.
8. **Given** un archivo de licencia de formato 2, **When** se importa, **Then** se rechaza con un mensaje que indica que el formato ya no es compatible y se conserva la licencia vigente.
9. **Given** una licencia vigente, **When** se importa de nuevo el mismo archivo (mismo id de licencia), **Then** se acepta como reimportación y el estado de los módulos no cambia.

---

### User Story 5 - Periodo de prueba (Priority: P1)

Una instalación que nunca ha tenido una licencia aceptada funciona con todos los módulos durante 30 días desde el primer arranque. El sistema avisa cuando quedan 5 días y cuando queda 1 día. La fecha de inicio está protegida: si alguien la altera, el periodo se considera vencido.

**Why this priority**: Permite al cliente evaluar el producto completo antes de comprar, y su protección evita que la prueba se extienda indefinidamente.

**Independent Test**: Simular el primer arranque y avanzar la fecha a los días 25, 29, 30 y 31; alterar la fecha de inicio guardada.

**Acceptance Scenarios**:

1. **Given** una instalación nueva, **When** arranca por primera vez, **Then** todos los módulos están activos y quedan 30 días de prueba.
2. **Given** una prueba en curso, **When** quedan 5 días, **Then** se muestra el aviso de 5 días; **When** queda 1 día, **Then** se muestra el aviso de último día.
3. **Given** una prueba con fecha de inicio alterada, **When** el sistema arranca, **Then** la prueba se considera vencida.
4. **Given** el día 31 sin licencia válida, **When** el sistema arranca, **Then** el sistema queda bloqueado por falta del módulo POS (Historia 6).

---

### User Story 6 - Bloqueo total sin módulo POS (Priority: P1)

POS es el módulo base. Si no está activo —prueba vencida sin licencia, licencia sin POS o POS vencido—, el sistema se bloquea por completo aunque otros módulos estén vigentes. En ese estado solo se puede iniciar sesión, abrir "Ayuda > Licencia" (generar solicitud e importar licencia) y exportar un respaldo de los datos, porque los datos pertenecen al cliente. Un mensaje claro explica cómo activar.

El bloqueo nunca interrumpe una venta en curso: si la licencia vence mientras hay una venta abierta, se permite terminarla, cobrarla y cerrar el turno abierto; solo se bloquean las ventas nuevas (constitución v1.3.0, Principio I).

**Why this priority**: Es el mecanismo que hace efectiva la licencia: sin él, el núcleo de venta quedaría siempre disponible.

**Independent Test**: Llevar el sistema a cada una de las causas de bloqueo (prueba vencida, licencia no válida, POS no contratado, POS pendiente y POS vencido) y comprobar que solo están disponibles inicio de sesión, licencia y respaldo, tanto desde el menú como al invocar directamente cualquier otra operación.

**Acceptance Scenarios**:

1. **Given** una prueba vencida sin licencia, **When** el operador inicia sesión, **Then** ve un mensaje que explica que el sistema no está activado y cómo activarlo, y solo tiene acceso a "Ayuda > Licencia" y a exportar respaldo.
2. **Given** una licencia válida con Inventario y Reportes pero sin POS, **When** el operador inicia sesión, **Then** el sistema está bloqueado igual que en el escenario anterior.
3. **Given** una licencia cuyo módulo POS venció ayer, **When** el sistema arranca, **Then** el sistema está bloqueado aunque otros módulos sigan vigentes.
4. **Given** un sistema bloqueado, **When** se intenta ejecutar una venta, un cobro o cualquier otra operación de negocio por cualquier vía, **Then** se rechaza sin modificar datos.
5. **Given** un sistema bloqueado, **When** se importa una licencia válida con POS, **Then** el bloqueo se levanta de inmediato sin reiniciar y todos los datos anteriores están disponibles.
6. **Given** un sistema bloqueado, **When** el operador exporta un respaldo, **Then** el respaldo se genera completo.
7. **Given** una venta con artículos capturados y un turno abierto, **When** el módulo POS vence (cambio de día con la aplicación abierta), **Then** el operador puede terminar la venta, cobrarla e imprimir el ticket; después puede cerrar el turno abierto, pero no puede iniciar una venta nueva.
8. **Given** un sistema bloqueado con un turno que quedó abierto, **When** el operador inicia sesión, **Then** puede cerrar ese turno además de las opciones permitidas en bloqueo.

---

### User Story 7 - Vencimiento y activación por módulo (Priority: P1)

Cada módulo de la licencia tiene su propia fecha de activación y, opcionalmente, de vencimiento. Un módulo con activación futura se activa solo al llegar ese día. Un módulo vencido se comporta como no contratado: su opción desaparece del menú y sus operaciones se rechazan, pero sus datos se conservan. El estado se recalcula al arrancar, al iniciar sesión y al cambiar de día con la aplicación abierta. En el Inicio se avisa de los módulos que vencen en los próximos 7 días.

**Why this priority**: Permite vender módulos por periodos (suscripciones, pruebas de un módulo, contratos con fecha de inicio) sin intervención manual.

**Independent Test**: Importar una licencia con un módulo de activación futura y otro con vencimiento cercano; avanzar el reloj atravesando la medianoche con la aplicación abierta.

**Acceptance Scenarios**:

1. **Given** una licencia con Crédito y clientes activable el día 20, **When** la fecha local llega al día 20, **Then** el módulo se activa sin necesidad de reimportar nada.
2. **Given** la aplicación abierta el día 19 a las 23:59 con un módulo activable el día 20, **When** pasa la medianoche, **Then** el módulo se activa sin reiniciar.
3. **Given** un módulo que vence el día 15, **When** llega el día 16, **Then** su opción desaparece del menú, sus operaciones se rechazan y sus datos se conservan intactos.
4. **Given** un módulo que vence dentro de 7 días o menos, **When** el operador abre el Inicio, **Then** ve un aviso con el nombre del módulo y su fecha de vencimiento.
5. **Given** un módulo vencido cuyos datos existen, **When** se importa una licencia nueva que lo renueva, **Then** sus datos anteriores están disponibles sin pérdida.

---

### User Story 8 - Protección contra reloj atrasado (Priority: P1)

El POS recuerda la última fecha que ha visto. Si la fecha del sistema es anterior a esa fecha en más de 1 día, no recalcula activaciones, mantiene el estado más restrictivo y avisa al operador de que el reloj parece atrasado.

**Why this priority**: Sin esta protección, atrasar el reloj reactivaría módulos vencidos o extendería la prueba.

**Independent Test**: Con un módulo vencido y una prueba vencida, atrasar el reloj del sistema varios días y comprobar que nada se reactiva.

**Acceptance Scenarios**:

1. **Given** un módulo vencido el día 15 y última fecha vista el día 20, **When** el reloj se atrasa al día 10, **Then** el módulo sigue vencido y se avisa al operador del reloj atrasado.
2. **Given** una prueba vencida, **When** el reloj se atrasa a una fecha dentro de los 30 días, **Then** la prueba sigue vencida.
3. **Given** última fecha vista el día 20, **When** el reloj marca el día 19 (atraso de 1 día o menos, por ejemplo un ajuste de zona horaria), **Then** no se muestra aviso y el estado no se reactiva.
4. **Given** un reloj atrasado, **When** se corrige la hora, **Then** el aviso desaparece y el estado se recalcula con normalidad.

---

### User Story 9 - Pantalla de licencia (Priority: P2)

"Ayuda > Licencia" muestra de un vistazo el id de máquina, el estado general (en prueba con días restantes, licenciado o bloqueado), el nombre del cliente y una tabla con los 9 módulos del catálogo, cada uno con su estado (activo, pendiente de activación, vencido o no contratado) y sus fechas de activación y vencimiento.

**Why this priority**: Facilita el soporte y la renovación, pero el licenciamiento funciona sin ella.

**Independent Test**: Importar una licencia que combine módulos en los cuatro estados y comprobar la tabla.

**Acceptance Scenarios**:

1. **Given** una prueba en curso con 12 días restantes, **When** se abre la pantalla, **Then** el estado general dice "En prueba" con 12 días restantes y los 9 módulos aparecen activos.
2. **Given** una licencia con módulos activos, pendientes, vencidos y no contratados, **When** se abre la pantalla, **Then** cada uno de los 9 módulos muestra el estado y las fechas correctas, en el orden del catálogo.
3. **Given** un sistema bloqueado, **When** se abre la pantalla, **Then** el estado general dice "Bloqueado" con la causa (prueba vencida, licencia no válida, POS no contratado, POS pendiente de activación o POS vencido).

---

### Edge Cases

- **Licencia con la misma fecha y hora de emisión que la vigente pero distinto id**: se rechaza. Solo se acepta la reimportación del mismo id o una licencia con fecha y hora de emisión posteriores.
- **Licencia guardada de formato 2** (instalación que tenía una): al arrancar no supera la verificación y se trata como licencia inválida.
- **Licencia válida pero ya caducada por completo** (todos sus módulos vencidos): se acepta si es más reciente; el resultado es el bloqueo, y el mensaje lo indica.
- **Licencia importada durante la prueba**: la reemplaza; a partir de ese momento solo rigen los módulos de la licencia. Si la licencia no incluye POS, el sistema se bloquea.
- **Licencia guardada que falla la verificación al arrancar, o que desapareció**: no habilita ningún módulo y la prueba no se reanuda, porque ya se había importado una licencia (FR-026a). El sistema se bloquea por licencia no válida y se informa al operador.
- **Archivo seleccionado que no es una licencia** (vacío, corrupto, otro formato o formato desconocido): se rechaza con un mensaje claro y se conserva la licencia vigente.
- **Fecha de vencimiento anterior a la de activación**: el módulo nunca está activo y se muestra como vencido, también antes de su fecha de activación (nunca como pendiente). Si es el módulo POS, la causa de bloqueo es POS vencido.
- **Operación en curso al cambiar el día**: una venta que ya inició se puede terminar y cobrar, aunque venza el módulo POS u otro módulo que participe en ella; las nuevas acciones del módulo que acaba de vencer se rechazan a partir de ese momento.
- **Venta abierta al reiniciar con el sistema bloqueado**: si al arrancar existe una venta sin terminar, se permite terminarla y cobrarla, o cancelarla, antes de aplicar el bloqueo.
- **Módulo con varias entradas en la misma licencia**: el módulo está activo si alguna de sus entradas está vigente ese día.
- **Cambio de hardware que altera el id de máquina**: la licencia guardada deja de coincidir y se rechaza; el sistema se bloquea por licencia no válida (no por prueba vencida) y el operador genera una nueva solicitud.
- **Instalación existente que se actualiza en plena prueba**: conserva su fecha de inicio original; la prueba no se reinicia.
- **Reloj adelantado y luego corregido**: la última fecha vista queda adelantada; al volver a la fecha real, se aplica la protección de reloj atrasado hasta que la fecha real alcanza la última vista.

## Requirements *(mandatory)*

### Functional Requirements

**Catálogo**

- **FR-001**: El sistema MUST definir un catálogo de exactamente 9 módulos: POS, Inventario, Reportes avanzados, Crédito y clientes, Turnos y arqueo, Devoluciones, Proveedores, Descuentos y promociones y Categorías de productos.
- **FR-002**: Cada módulo del catálogo MUST tener un identificador permanente (GUID), una clave estable en inglés, nombre, descripción y orden; exactamente un módulo, POS, MUST estar marcado como módulo base.
- **FR-003**: Inventario, Reportes avanzados, Crédito y clientes, Turnos y arqueo, Devoluciones y Descuentos y promociones MUST conservar exactamente el identificador que tienen hoy. POS, Proveedores y Categorías de productos MUST recibir un identificador nuevo, generado una sola vez y que nunca se modifica.
- **FR-004**: El catálogo MUST publicarse como contrato compartido en `contracts/module-catalog.json`, idéntico al de OctopusAdmin, e incluir una versión de catálogo.
- **FR-005**: La aplicación MUST llevar el catálogo integrado en sí misma, de forma que no pueda editarse ni faltar como archivo suelto de la instalación.
- **FR-006**: El catálogo interno de la aplicación MUST mantenerse como constantes del código, y una prueba automática MUST fallar si difiere en cualquier aspecto del catálogo compartido.
- **FR-007**: Proveedores (incluidas las compras a proveedores) y Categorías de productos MUST quedar controlados por licencia con el mismo mecanismo que los demás módulos: opción oculta en el menú y operaciones rechazadas cuando no están activos.

**Formato de licencia**

- **FR-008**: El formato de licencia versión 3 MUST documentarse en `contracts/license-format.md` como contrato compartido, idéntico al de OctopusAdmin.
- **FR-009**: Una licencia versión 3 MUST contener: versión de formato, id de licencia, fecha y hora de emisión en UTC, id de máquina, nombre del cliente y una lista de módulos, cada uno con su identificador, fecha de activación y fecha de vencimiento (vacía para indefinido).
- **FR-010**: Las fechas de activación y vencimiento de cada módulo MUST ser solo de día (sin hora) e interpretarse en la fecha local del POS. El vencimiento MUST ser inclusivo: el módulo funciona todo el día de vencimiento.
- **FR-011**: El contenido de la licencia MUST ir firmado con ECDSA P-256 / SHA-256, y el contrato MUST definir sin ambigüedad la secuencia exacta de bytes que se firma, de modo que ambas aplicaciones produzcan y verifiquen la misma firma.
- **FR-012**: Cada licencia MUST representar el conjunto completo de módulos vigentes del cliente; importar una licencia MUST reemplazar a la anterior, no sumarse a ella.

**Solicitud**

- **FR-013**: "Ayuda > Licencia" MUST ofrecer "Generar solicitud", que guarda un archivo `.octoreq` con id de máquina, nombre del negocio, versión de la aplicación, versión del catálogo y fecha.
- **FR-014**: El archivo de solicitud MUST ser legible y MUST NOT ir cifrado ni firmado.
- **FR-015**: "Ayuda > Licencia" MUST mostrar el id de máquina con un botón "Copiar" que lo deja en el portapapeles.
- **FR-016**: El id de máquina de la solicitud, el mostrado en pantalla y el usado para verificar licencias MUST ser el mismo.

**Importación y verificación**

- **FR-017**: "Ayuda > Licencia" MUST ofrecer "Importar licencia", que permite elegir un archivo `.lic`.
- **FR-018**: Al importar, el sistema MUST verificar en este orden: (1) firma válida con la clave pública del proveedor, (2) id de máquina coincidente, (3) mismo id de licencia que la vigente (reimportación) o fecha y hora de emisión en UTC estrictamente posteriores a las de la vigente; cualquier otra MUST rechazarse. El primer fallo MUST detener la verificación, mostrar un mensaje que indique la causa concreta y conservar la licencia vigente sin cambios.
- **FR-019**: Una licencia válida MUST reemplazar a la anterior y sus módulos MUST aplicarse de inmediato (menú y operaciones) sin reiniciar la aplicación.
- **FR-020**: El sistema MUST guardar la licencia exactamente como se recibió (contenido y firma) y MUST volver a verificar su firma y su id de máquina en cada arranque; una licencia guardada que no supere la verificación MUST NOT habilitar ningún módulo ni reanudar la prueba (FR-026a).
- **FR-021**: El sistema MUST aceptar únicamente licencias de formato 3. Un archivo de formato 2 o de cualquier otra versión MUST rechazarse con un mensaje que indique que el formato no es compatible; una licencia guardada de formato 2 MUST tratarse al arrancar como licencia inválida (FR-020).
- **FR-022**: Los identificadores de módulo que no estén en el catálogo MUST ignorarse sin error ni rechazo de la licencia.

**Periodo de prueba**

- **FR-023**: Sin licencia válida y sin haber aceptado nunca una licencia (FR-026a), el sistema MUST mantener activos los 9 módulos durante 30 días desde el primer arranque.
- **FR-024**: El sistema MUST avisar exactamente el día en que quedan 5 días de prueba y el día en que queda 1 día; los demás días no hay aviso (comportamiento heredado de 012).
- **FR-025**: La fecha de inicio de la prueba MUST estar protegida contra edición; si se detecta alterada, la prueba MUST considerarse vencida.
- **FR-026**: Una instalación existente MUST conservar su fecha de inicio de prueba original al actualizar; la prueba MUST NOT reiniciarse.
- **FR-026a**: Al aceptar la primera licencia, el sistema MUST registrar de forma protegida (junto a la fecha de inicio de la prueba) que la prueba terminó. Desde ese momento la prueba MUST NOT volver a aplicar, aunque la licencia guardada falte o no supere la verificación; sin licencia válida, el sistema MUST bloquearse por licencia no válida. Una licencia guardada que no supera la verificación MUST bloquear por licencia no válida aunque ese registro se haya perdido (por ejemplo, por un cambio de hardware). Al arrancar con una licencia guardada válida y sin el registro, el sistema MUST crearlo.

**Bloqueo por módulo base**

- **FR-027**: Si el módulo POS no está activo (prueba vencida sin licencia válida, licencia no válida tras haber licenciado (FR-026a), POS no incluido en la licencia, POS pendiente de activación o POS vencido), el sistema MUST bloquearse por completo, independientemente del estado de los demás módulos.
- **FR-028**: En estado bloqueado, solo MUST estar disponibles: inicio de sesión, "Ayuda > Licencia" (ver estado, copiar id y generar solicitud, para cualquier usuario con sesión; importar licencia, solo Administrador), la exportación de respaldo de datos (solo Administrador) y, mientras existan, terminar la venta en curso y cerrar el turno abierto (FR-030a).
- **FR-029**: En estado bloqueado, el sistema MUST mostrar un mensaje claro que explique la causa y los pasos para activar (generar solicitud, enviarla al proveedor e importar la licencia recibida).
- **FR-030**: El bloqueo MUST verificarse en las operaciones de negocio, no solo en el menú: cualquier operación fuera de las permitidas MUST rechazarse sin modificar datos.
- **FR-030a**: El vencimiento de la licencia (de la prueba, del módulo POS o de cualquier otro módulo) MUST NOT interrumpir una venta en curso: el sistema MUST permitir terminarla, cobrarla (incluido el ticket) o cancelarla, y MUST permitir cerrar el turno abierto. Solo MUST bloquearse el inicio de ventas nuevas y las demás operaciones no permitidas. Esta garantía también aplica a una venta o un turno que quedaron abiertos antes de un reinicio. Terminar la venta en curso, imprimir su ticket y contar y cerrar el turno abierto MUST funcionar aunque los módulos que participan (Turnos y arqueo, Crédito y clientes, Descuentos y promociones) no estén activos; lo ya capturado en la venta (descuentos aplicados, venta a crédito) se respeta al cobrar, pero agregar partes nuevas de un módulo no activo se rechaza.

**Vigencia por módulo**

- **FR-031**: Un módulo MUST estar activo exactamente en los días comprendidos entre su fecha de activación y su fecha de vencimiento (ambas inclusive), o desde su activación en adelante si no tiene vencimiento.
- **FR-032**: Un módulo no activo (vencido, pendiente o no contratado) MUST comportarse como no contratado: opción oculta en el menú y operaciones rechazadas con el mensaje de módulo no activo, sin modificar datos.
- **FR-033**: Los datos de un módulo no activo MUST conservarse íntegramente y volver a estar disponibles cuando el módulo se active.
- **FR-034**: El estado de licencia MUST recalcularse al arrancar, al iniciar sesión y al cambiar de día con la aplicación abierta.
- **FR-035**: El Inicio MUST avisar de cada módulo que vence en los próximos 7 días (incluido el día actual), con su nombre y fecha de vencimiento.
- **FR-036**: Cuando una operación de un módulo activo involucra a un módulo no activo, la operación básica MUST completarse y solo MUST omitirse, sin error, la parte del módulo no activo (comportamiento heredado de 012).

**Reloj atrasado**

- **FR-037**: El sistema MUST registrar, de forma protegida, la última fecha vista.
- **FR-038**: Si la fecha del sistema es anterior a la última fecha vista en más de 1 día, el sistema MUST NOT recalcular activaciones, MUST mantener el estado más restrictivo entre el último calculado y el que resultaría de la fecha actual, y MUST avisar al operador.
- **FR-039**: Ningún atraso del reloj, de cualquier magnitud, MUST reactivar un módulo vencido ni aumentar los días de prueba restantes.

**Pantalla de licencia**

- **FR-040**: "Ayuda > Licencia" MUST mostrar el id de máquina, el estado general (en prueba con días restantes, licenciado o bloqueado con su causa), el nombre del cliente y una tabla con los 9 módulos en el orden del catálogo, cada uno con su estado (activo, pendiente de activación, vencido o no contratado) y sus fechas de activación y vencimiento.

**Reglas generales**

- **FR-041**: La aplicación MUST contener únicamente la clave pública del proveedor; ninguna clave privada ni secreto compartido MUST estar en el repositorio ni en la aplicación.
- **FR-042**: Todo el control de licencias MUST funcionar sin conexión.

### Key Entities

- **Módulo del catálogo**: unidad licenciable. Atributos: identificador permanente, clave en inglés, nombre, descripción, orden, indicador de módulo base.
- **Catálogo de módulos**: conjunto versionado de los 9 módulos, compartido con OctopusAdmin.
- **Solicitud de licencia**: datos que el cliente envía al proveedor: id de máquina, nombre del negocio, versión de la aplicación, versión del catálogo, fecha. No es secreta.
- **Licencia**: documento firmado por el proveedor. Atributos: versión de formato, id de licencia, fecha y hora de emisión (UTC), id de máquina, cliente, lista de entradas de módulo y firma. Se guarda tal como se recibió.
- **Entrada de módulo**: dentro de una licencia, identificador de módulo, fecha de activación y fecha de vencimiento opcional.
- **Registro de prueba**: fecha de inicio de la prueba (protegida), última fecha vista (protegida) y fecha de la primera licencia aceptada (protegida; marca el fin definitivo de la prueba).
- **Estado de licencia** (calculado, no se guarda): estado general (prueba, licenciado, bloqueado y causa), días restantes de prueba, estado de cada módulo, avisos vigentes (prueba por vencer, módulos por vencer, reloj atrasado).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Los 6 identificadores de módulo existentes son idénticos antes y después de la funcionalidad, y la prueba de consistencia entre el catálogo interno y el compartido pasa en cada compilación.
- **SC-002**: El 100% de las licencias firmadas por OctopusAdmin para la máquina activan exactamente los módulos que contienen, ni uno más ni uno menos.
- **SC-003**: El 100% de las licencias alteradas, de otra máquina, de formato 2 o no más recientes que la vigente (salvo reimportación del mismo id) se rechazan, y en todos los casos la licencia anterior sigue vigente.
- **SC-004**: El 100% de las ediciones de la licencia guardada se detectan en el siguiente arranque.
- **SC-005**: Sin módulo POS activo, el 100% de las operaciones fuera de inicio de sesión, licencia, respaldo, la venta en curso y el cierre del turno abierto (FR-030a) se rechazan, por menú y por invocación directa.
- **SC-006**: Un módulo con activación futura queda activo en el primer recálculo de ese día, y uno vencido queda inactivo en el primer recálculo del día siguiente al vencimiento, sin pérdida de datos.
- **SC-007**: Ningún escenario de atraso de reloj reactiva un módulo vencido ni aumenta los días de prueba.
- **SC-008**: El id de máquina del archivo `.octoreq` coincide con el de la máquina en el 100% de los casos.
- **SC-009**: Un operador puede generar la solicitud en menos de 1 minuto y, con la licencia recibida, activar el sistema en menos de 2 minutos sin reiniciar.

## Assumptions

- Esta funcionalidad extiende la licencia modular de 012 y el verificador ECDSA existente: reutiliza el id de máquina, la copia protegida de la fecha de inicio, la última fecha vista y el mensaje "Este módulo no está activo en tu licencia.". No se usa cifrado simétrico compartido.
- **Licencias de formato 2**: se elimina su soporte porque no hay clientes con licencias de formato 2 emitidas; por eso no se necesita periodo de transición.
- La regla de antigüedad (fecha y hora de emisión en UTC, reimportación por mismo id) se documentará en `contracts/license-format.md`, que se crea durante el plan junto con la definición exacta de los bytes firmados.
- "Ayuda > Licencia" sustituye o renombra la pantalla actual de administración de licencia. Cualquier usuario con sesión puede verla, copiar el id y generar la solicitud, con o sin bloqueo. Importar licencia y exportar respaldo requieren el rol Administrador; siempre existe al menos un Administrador activo.
- La "exportación de respaldo" disponible en bloqueo es la exportación manual de la base de datos que ya ofrece la aplicación; los respaldos automáticos siguen ejecutándose.
- El bloqueo total por falta del módulo POS cumple la constitución v1.3.0 (Principio I), que permite el bloqueo por licencia con dos garantías: no interrumpir la venta en curso ni el cierre del turno abierto (FR-030a) y mantener disponibles inicio de sesión, pantalla de licencia y exportación de respaldo (FR-028).
- El nombre del negocio de la solicitud se toma de los datos del negocio ya configurados; si no existen, se deja vacío.
- La versión del catálogo es un número entero que se incrementa cuando cambia el catálogo compartido.
- Las fechas de licencia se comparan con la fecha local del equipo donde corre el POS, según su zona horaria configurada.
- Fuera de alcance: validación remota, envío automático de solicitudes, revocación de licencias y la herramienta de firma de OctopusAdmin.
