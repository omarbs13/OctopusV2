# Feature Specification: Impresión de ticket, cajón de dinero y datos del negocio

**Feature Branch**: `006-ticket-printing`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "Impresión de ticket de venta, apertura de cajón de dinero y datos del
negocio. Entregar al cliente un comprobante impreso de su compra y abrir el cajón al cobrar en
efectivo, en Windows y Linux, sin que una falla de impresión afecte la venta."

## Clarifications

### Session 2026-09-30

- Q: ¿Quién puede abrir el cajón sin venta? → A: Cualquier operador con sesión iniciada; queda en la bitácora con su usuario y motivo.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Datos del negocio (Priority: P1)

Una pantalla de configuración permite capturar los datos del negocio que aparecen en el ticket:
nombre comercial, dirección, teléfono, RFC (opcional), logotipo (opcional) y mensaje de pie de
ticket.

**Why this priority**: sin estos datos el ticket no identifica al negocio; es requisito de los
tickets y de su prueba.

**Independent Test**: capturar los datos, cerrar y reabrir la aplicación y verificar que se
conservan; generar un ticket de prueba y verificar que los muestra.

**Acceptance Scenarios**:

1. **Given** la pantalla de datos del negocio vacía, **When** el operador captura nombre comercial,
   dirección, teléfono y mensaje de pie y guarda, **Then** los datos quedan guardados y se
   muestran al reabrir la pantalla.
2. **Given** el nombre comercial, la dirección o el teléfono vacíos, **When** intenta guardar,
   **Then** se indica qué campo falta y no se guarda.
3. **Given** RFC y logotipo omitidos, **When** guarda, **Then** se acepta y el ticket simplemente
   no los muestra.
4. **Given** un logotipo que no es una imagen válida o excede el tamaño permitido, **When** intenta
   guardar, **Then** se rechaza con un mensaje claro y se conserva el logotipo anterior.
5. **Given** datos guardados, **When** se imprime cualquier ticket, **Then** usa los datos vigentes.

---

### User Story 2 - Configurar la impresora (Priority: P1)

Una pantalla de configuración permite elegir la impresora de tickets entre las instaladas en el
sistema, el ancho del papel (58 mm u 80 mm) y si el ticket se imprime automáticamente al cobrar.
Incluye un botón de impresión de prueba y una opción de impresora virtual que guarda el ticket como
archivo, para trabajar sin impresora física. La configuración es local de cada máquina.

**Why this priority**: sin impresora configurada no hay ticket; la impresora virtual permite operar
y probar sin hardware.

**Independent Test**: elegir la impresora virtual, 58 mm, y pulsar la prueba; verificar que se
genera un archivo con el ticket de prueba; repetir con 80 mm.

**Acceptance Scenarios**:

1. **Given** varias impresoras instaladas, **When** el operador abre la pantalla, **Then** ve la
   lista de impresoras del sistema y la opción de impresora virtual.
2. **Given** una impresora y ancho elegidos, **When** pulsa la impresión de prueba, **Then** se
   imprime un ticket de prueba con los datos del negocio y al ancho elegido.
3. **Given** la impresora virtual, **When** pulsa la prueba, **Then** el ticket se guarda como
   archivo y el sistema informa dónde quedó.
4. **Given** la impresora elegida desconectada o ya no instalada, **When** pulsa la prueba, **Then**
   se muestra un aviso comprensible sin cerrar la aplicación.
5. **Given** dos equipos con la misma base de datos o instalación, **When** se configura la
   impresora en uno, **Then** el otro no cambia.
6. **Given** la impresión automática desactivada, **When** se cobra, **Then** no se imprime solo.

---

### User Story 3 - Imprimir el ticket al cobrar (Priority: P1)

Al confirmar el cobro, si la impresión automática está activada, se imprime el ticket con: datos
del negocio, folio, fecha y hora, líneas (cantidad, descripción, importe), total, pagos por forma
de pago, cambio y mensaje de pie. El texto se ajusta al ancho del papel sin cortar información
importante. Si la impresión falla, la venta sigue registrada, se avisa al operador y se ofrece
reintentar.

**Why this priority**: es el objetivo central: entregar el comprobante sin poner en riesgo la venta.

**Independent Test**: con la impresora virtual, cobrar una venta con varias líneas, incluida una de
nombre muy largo, y pagos mixtos; revisar el archivo generado en 58 mm y en 80 mm.

**Acceptance Scenarios**:

1. **Given** impresión automática activa, **When** se confirma el cobro, **Then** se imprime un
   ticket con todo el contenido indicado y la venta queda registrada.
2. **Given** una descripción más larga que el ancho del papel, **When** se imprime, **Then** continúa
   en la línea siguiente sin perder texto y sin desalinear cantidad e importe.
3. **Given** un pago con varias formas de pago, **When** se imprime, **Then** cada forma de pago
   aparece con su monto, junto con el cambio entregado.
4. **Given** la impresora desconectada o con error, **When** se confirma el cobro, **Then** la venta
   queda registrada completa, se avisa al operador del fallo y se ofrece reintentar o continuar sin
   imprimir.
5. **Given** el aviso de fallo, **When** el operador elige reintentar y la impresora ya funciona,
   **Then** se imprime el ticket sin duplicar la venta.
6. **Given** que el operador elige continuar sin imprimir, **Then** el Punto de venta queda listo
   para la siguiente venta y el ticket puede reimprimirse después.
7. **Given** cualquier falla de impresión, **When** ocurre, **Then** nunca se deshace ni bloquea la
   venta, ni se cierra la aplicación.

---

### User Story 4 - Reimprimir y ticket de venta cancelada (Priority: P2)

Desde el detalle de una venta se puede reimprimir el ticket, que lleva la leyenda "REIMPRESIÓN".
Si la venta está cancelada, el ticket lleva la leyenda "CANCELADA".

**Why this priority**: recupera tickets no impresos o perdidos; no bloquea la operación diaria.

**Independent Test**: reimprimir una venta completada y una cancelada con la impresora virtual y
verificar las leyendas.

**Acceptance Scenarios**:

1. **Given** el detalle de una venta completada, **When** el operador pulsa reimprimir, **Then** se
   imprime el ticket con la leyenda "REIMPRESIÓN" y el mismo contenido de la venta original.
2. **Given** una venta cancelada, **When** se reimprime, **Then** el ticket muestra "CANCELADA"
   (además de "REIMPRESIÓN").
3. **Given** una venta cuyo ticket no pudo imprimirse al cobrar, **When** se reimprime, **Then**
   se imprime correctamente.
4. **Given** que la reimpresión falla, **Then** se avisa y se puede reintentar, sin afectar la venta.

---

### User Story 5 - Cajón de dinero (Priority: P2)

El cajón conectado a la impresora se abre automáticamente al cobrar con efectivo (configurable).
Un botón permite abrir el cajón sin venta, pidiendo un motivo; la apertura queda registrada en la
bitácora de auditoría.

**Why this priority**: agiliza el cobro en efectivo y controla el acceso al efectivo; la venta
funciona sin cajón.

**Independent Test**: cobrar en efectivo y verificar la apertura; cobrar solo con otra forma de
pago y verificar que no abre; abrir sin venta con motivo y revisar la bitácora.

**Acceptance Scenarios**:

1. **Given** apertura automática activada, **When** se cobra con efectivo (total o parcial),
   **Then** el cajón se abre.
2. **Given** un cobro sin efectivo, **When** se confirma, **Then** el cajón no se abre.
3. **Given** apertura automática desactivada, **When** se cobra en efectivo, **Then** no se abre.
4. **Given** el botón de apertura sin venta, **When** el operador captura un motivo y confirma,
   **Then** el cajón se abre y se registra en la bitácora quién, cuándo y el motivo.
5. **Given** un motivo vacío, **When** intenta abrir sin venta, **Then** no se abre y se pide el
   motivo.
6. **Given** que el cajón no responde, **When** se intenta abrir, **Then** se avisa al operador y la
   venta no se afecta; en la apertura sin venta el intento fallido también queda en la bitácora.

---

### Edge Cases

- Impresora sin configurar y impresión automática activa: se avisa una vez con acceso directo a la
  configuración, sin bloquear la venta.
- Impresora ocupada o sin papel: se trata como falla de impresión (aviso y reintento).
- Varios tickets seguidos en rápida sucesión: se imprimen todos en orden, sin mezclarse.
- Producto con nombre de una sola palabra más larga que el ancho del papel: se parte para no
  perder texto.
- Logotipo muy grande: se ajusta al ancho del papel.
- Caracteres con acentos y "ñ" se imprimen correctamente.
- Carpeta de la impresora virtual sin permisos o sin espacio: se trata como falla de impresión.
- Cambio de ancho de papel: los tickets siguientes usan el nuevo ancho.
- Aplicación cerrada justo tras cobrar: la venta existe y el ticket puede reimprimirse.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST permitir capturar y guardar nombre comercial, dirección, teléfono,
  RFC (opcional), logotipo (opcional) y mensaje de pie de ticket, validando los obligatorios y el
  formato y tamaño del logotipo.
- **FR-002**: El sistema MUST usar los datos vigentes del negocio en todos los tickets.
- **FR-003**: El sistema MUST listar las impresoras instaladas en el sistema y ofrecer además una
  impresora virtual que guarda el ticket como archivo.
- **FR-004**: El sistema MUST permitir elegir ancho de papel de 58 mm u 80 mm, y activar o
  desactivar la impresión automática al cobrar.
- **FR-005**: El sistema MUST ofrecer impresión de prueba con la configuración vigente.
- **FR-006**: La configuración de impresora, ancho, impresión automática y apertura de cajón MUST
  ser local de cada máquina.
- **FR-007**: Al confirmar el cobro con impresión automática activa, el sistema MUST imprimir un
  ticket con datos del negocio, folio, fecha y hora, líneas (cantidad, descripción, importe),
  total, pagos por forma de pago, cambio y mensaje de pie.
- **FR-008**: El sistema MUST ajustar el texto al ancho del papel sin perder información, con
  cantidad e importe alineados.
- **FR-009**: La impresión MUST NOT bloquear, retrasar de forma perceptible ni deshacer una venta;
  ante una falla, la venta permanece registrada, se avisa al operador con un mensaje comprensible y
  se ofrece reintentar. Los detalles técnicos se registran en el log.
- **FR-010**: El sistema MUST permitir reimprimir el ticket desde el detalle de cualquier venta,
  marcado con la leyenda "REIMPRESIÓN".
- **FR-011**: El ticket de una venta cancelada MUST llevar la leyenda "CANCELADA".
- **FR-012**: El sistema MUST abrir el cajón automáticamente al cobrar con efectivo, salvo que esa
  opción esté desactivada.
- **FR-013**: El sistema MUST permitir a cualquier operador con sesión iniciada (sin permiso especial) abrir el cajón sin venta, exigiendo un motivo, y registrar en
  la bitácora de auditoría usuario, fecha y hora, motivo y resultado.
- **FR-014**: Una falla del cajón MUST NOT afectar la venta y MUST avisarse al operador.
- **FR-015**: Impresión y cajón MUST funcionar en Windows y Linux y con impresoras térmicas de
  tickets, accediéndose mediante interfaces de la capa de aplicación con implementaciones por
  sistema operativo, según la constitución.
- **FR-016**: Todo el flujo MUST funcionar sin conexión a internet.

### Key Entities

- **Datos del negocio**: identidad que aparece en el ticket (nombre comercial, dirección,
  teléfono, RFC, logotipo, mensaje de pie). Uno por instalación.
- **Configuración de impresión (local)**: impresora elegida o virtual, ancho de papel, impresión
  automática, apertura automática de cajón. Una por máquina.
- **Ticket**: representación imprimible de una venta (o de una prueba), con leyendas opcionales
  "REIMPRESIÓN" y "CANCELADA". Se genera a partir de la venta, no se almacena.
- **Apertura de cajón sin venta**: registro de auditoría con usuario, fecha y hora, motivo y
  resultado.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100 % de las ventas cobradas con impresión automática activa producen un ticket
  con todos los campos requeridos, en 58 mm y en 80 mm.
- **SC-002**: Con la impresora desconectada, el 100 % de las ventas se registran y pueden
  reimprimirse después.
- **SC-003**: Ninguna información del ticket (descripciones, montos, folio) se pierde ni queda
  ilegible en ninguno de los dos anchos de papel.
- **SC-004**: El operador completa la configuración inicial (datos del negocio e impresora, con
  prueba) en menos de 5 minutos.
- **SC-005**: El ticket sale impreso a los pocos segundos de confirmar el cobro, y el Punto de
  venta queda disponible para la siguiente venta sin esperar a la impresora.
- **SC-006**: El 100 % de las aperturas de cajón sin venta quedan en la bitácora con su motivo.
- **SC-007**: El cajón se abre en el 100 % de los cobros con efectivo con la opción activa, y en
  ninguno de los cobros sin efectivo.

## Assumptions

- Los datos del negocio son de la instalación (base de datos), a diferencia de la configuración de
  impresora, que es por máquina.
- Se soportan impresoras térmicas de tickets compatibles con el estándar ESC/POS; el cajón se
  conecta a la impresora.
- El ticket refleja lo ya registrado en la venta (folio, líneas con precio y nombre al momento de la
  venta, pagos y cambio) y no recalcula nada.
- El operador autenticado se toma del sistema de usuarios existente; no se agregan permisos
  nuevos.
- La impresión de prueba usa datos de ejemplo y no consume folio ni genera venta.
- Fuera de alcance: impresoras de hojas tamaño carta, tickets con código QR, impresión de reportes
  y facturación.
- Depende del módulo de ventas (005), de la bitácora de auditoría y del detalle de ventas
  existentes.
