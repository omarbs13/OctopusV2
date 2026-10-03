# Feature Specification: Mejoras de UX/UI y comportamiento de la aplicación

**Feature Branch**: `023-ux-ui-polish`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Mejoras de UX/UI y comportamiento de la aplicación POS. Objetivo:
corregir defectos visuales, de alineación y de comportamiento detectados en la aplicación de
escritorio. H1 ventana maximizada por defecto y estado recordado; H2 tamaño mínimo de ventana
(ej. 1024×768) con scroll en pantallas pequeñas; H3 pantalla de carga visible mínimo 2 segundos;
H4 grupos del menú colapsados al cargar y estado recordado; H5 botón hamburguesa alineado a la
izquierda; H6 iconos distintos por opción del menú; H7 texto centrado en botones grandes; H8
encabezado con datos del negocio en reportes y tickets; H9 tarjeta 'Período de evaluación' con
proporciones correctas; H10 'Probar escáner' se mueve de 'Acerca de' a 'Configuración'."

## Contexto

La aplicación ya cuenta con pantalla de carga, ventana principal con menú lateral agrupado
(002), datos del negocio capturados en Configuración (006), reportes exportables a PDF y hoja de
cálculo (009), tickets impresos (006, 008, 013, 014), tarjeta de licencia de evaluación (011) y
la pantalla "Probar escáner" (021) dentro del grupo Ayuda. Esta funcionalidad no agrega
capacidades de negocio: corrige la presentación y el comportamiento de esas piezas.

Estado actual relevante detectado:

- La ventana abre en tamaño normal y permite reducirse hasta un tamaño menor al útil.
- La pantalla de carga se cierra en cuanto termina el arranque, a veces tan rápido que parpadea.
- Varias opciones del menú repiten icono (por ejemplo, el mismo icono se usa en Movimientos,
  Historial de ventas, Cortes, Turnos, Bitácora y Entrada de compra).
- El encabezado de los reportes PDF muestra nombre, dirección y teléfono, pero no RFC ni logo.
- "Probar escáner" está en el grupo Ayuda, junto a "Acerca de".

## Clarifications

### Session 2026-10-02

- Q: ¿El encabezado completo con logo se repite en cada página del PDF? → A: No; el encabezado
  de negocio aparece solo en la primera página.
- Q: Al mover "Probar escáner" a Configuración, ¿quién puede verla? → A: Todos los roles, aunque
  esté dentro de Configuración; un usuario sin permiso de configuración ve el grupo solo con esa
  opción.

## User Scenarios & Testing *(mandatory)*

Todas las historias tienen prioridad P1 según la descripción. Se ordenan por impacto en el uso
diario; cada una puede implementarse, probarse y entregarse por separado.

### User Story 1 - Ventana maximizada y estado recordado (Priority: P1)

El operador abre la aplicación y la ventana principal ocupa toda la pantalla sin que tenga que
maximizarla. Si la restaura a tamaño normal y cierra la aplicación, la próxima vez abre en tamaño
normal; si la deja maximizada, abre maximizada.

**Why this priority**: es lo primero que ve el operador en cada turno; una ventana pequeña
oculta información del punto de venta.

**Independent Test**: borrar las preferencias guardadas, abrir la aplicación (abre maximizada),
restaurarla, cerrarla y volver a abrir (abre en tamaño normal).

**Acceptance Scenarios**:

1. **Given** una instalación sin preferencias de ventana guardadas, **When** la aplicación
   arranca, **Then** la ventana principal aparece maximizada.
2. **Given** el operador dejó la ventana en tamaño normal al cerrar, **When** vuelve a abrir la
   aplicación, **Then** la ventana aparece en tamaño normal con el último tamaño usado.
3. **Given** el operador dejó la ventana maximizada al cerrar, **When** vuelve a abrir la
   aplicación, **Then** la ventana aparece maximizada.
4. **Given** el operador minimizó la ventana antes de cerrarla, **When** vuelve a abrir la
   aplicación, **Then** la ventana aparece en el último estado no minimizado (maximizada o normal).

---

### User Story 2 - Tamaño mínimo de ventana (Priority: P1)

El operador no puede reducir la ventana a un tamaño en el que las pantallas se deformen. En
equipos con pantalla más pequeña que el mínimo, el contenido sigue siendo accesible mediante
desplazamiento.

**Why this priority**: por debajo de cierto tamaño las pantallas de venta y formularios se
cortan y el operador pierde acceso a botones.

**Independent Test**: con la ventana en tamaño normal, intentar reducirla por debajo de
1024×768 y comprobar que se detiene en ese tamaño.

**Acceptance Scenarios**:

1. **Given** la ventana en tamaño normal, **When** el operador la arrastra para hacerla más
   pequeña, **Then** no puede reducirla por debajo de 1024 de ancho por 768 de alto.
2. **Given** una pantalla cuya área útil es menor que 1024×768, **When** la aplicación muestra
   cualquier pantalla, **Then** todo el contenido sigue alcanzable mediante barras de
   desplazamiento y ningún botón queda inaccesible.

---

### User Story 3 - Pantalla de carga con duración mínima (Priority: P1)

Al abrir la aplicación, la pantalla de carga permanece visible al menos 2 segundos, aunque el
arranque termine antes; si el arranque tarda más, permanece hasta que termina, sin retraso extra.

**Why this priority**: evita el parpadeo de una pantalla que aparece y desaparece y da una
percepción de arranque estable.

**Independent Test**: medir el tiempo visible de la pantalla de carga en un arranque rápido
(≈ 2 s) y en uno lento simulado (igual a la duración del arranque).

**Acceptance Scenarios**:

1. **Given** un arranque que termina en 0.5 segundos, **When** la aplicación inicia, **Then** la
   pantalla de carga se muestra 2 segundos y luego aparece la ventana principal.
2. **Given** un arranque que tarda 3 segundos, **When** la aplicación inicia, **Then** la pantalla
   de carga se muestra 3 segundos (no 5) y luego aparece la ventana principal.
3. **Given** un error durante el arranque, **When** se debe mostrar el mensaje de error, **Then**
   el mensaje se muestra de inmediato, sin esperar a cumplir los 2 segundos.

---

### User Story 4 - Menú con grupos colapsados y estado recordado (Priority: P1)

Al iniciar sesión por primera vez, todos los grupos del menú lateral aparecen colapsados, de
modo que el operador ve una lista corta de grupos. Al expandir un grupo se muestran sus
opciones. Lo que el operador expande o colapsa se conserva para la próxima sesión.

**Why this priority**: con todos los grupos abiertos el menú es largo y obliga a desplazarse
para encontrar opciones.

**Independent Test**: borrar las preferencias, iniciar sesión (todo colapsado), expandir
"Ventas", cerrar y volver a iniciar sesión ("Ventas" sigue expandido, el resto colapsado).

**Acceptance Scenarios**:

1. **Given** un usuario sin estado de menú guardado, **When** inicia sesión, **Then** todos los
   grupos del menú se muestran colapsados.
2. **Given** un grupo colapsado, **When** el usuario lo expande, **Then** se muestran todas las
   opciones del grupo a las que tiene acceso.
3. **Given** el usuario dejó expandidos "Ventas" e "Inventario", **When** cierra la aplicación y
   vuelve a iniciar sesión, **Then** esos dos grupos aparecen expandidos y el resto colapsados.
4. **Given** el menú en modo contraído (solo iconos), **When** el usuario lo vuelve a expandir,
   **Then** cada grupo conserva el estado expandido o colapsado que tenía.

---

### User Story 5 - Botón hamburguesa alineado a la izquierda (Priority: P1)

El botón que expande o contrae el menú lateral aparece en el borde izquierdo de la parte
superior del menú, alineado con los iconos de las opciones, tanto con el menú expandido como
contraído.

**Why this priority**: un botón centrado se desplaza al cambiar el ancho del menú y el operador
tiene que buscarlo.

**Independent Test**: alternar el menú entre expandido y contraído y verificar que el botón no
cambia de posición horizontal.

**Acceptance Scenarios**:

1. **Given** el menú expandido, **When** se observa su parte superior, **Then** el botón
   hamburguesa está alineado a la izquierda, en la misma columna que los iconos de las opciones.
2. **Given** el menú contraído, **When** se observa su parte superior, **Then** el botón ocupa la
   misma posición horizontal que con el menú expandido.

---

### User Story 6 - Iconos distintos por opción del menú (Priority: P1)

Cada grupo y cada opción del menú tiene un icono propio que sugiere su función, de modo que con
el menú contraído el operador distingue las opciones solo por el icono.

**Why this priority**: con el menú contraído, los iconos repetidos hacen imposible distinguir
opciones como Movimientos, Cortes, Turnos o Bitácora.

**Independent Test**: contraer el menú, iniciar sesión como Administrador (que ve todas las
opciones) y comprobar que no hay dos opciones con el mismo icono.

**Acceptance Scenarios**:

1. **Given** el menú completo visible para un Administrador, **When** se revisan todos los grupos
   y opciones, **Then** ningún icono se repite.
2. **Given** el menú contraído, **When** el operador pasa el puntero sobre un icono, **Then** se
   muestra el nombre de la opción.
3. **Given** el conjunto de iconos, **When** se comparan entre sí, **Then** todos comparten el
   mismo estilo visual (trazo, grosor y tamaño).

---

### User Story 7 - Texto centrado en botones grandes (Priority: P1)

En los botones grandes de acción principal (por ejemplo "Ingreso", "Retiro", "Punto de venta",
"Cobrar", accesos rápidos de Inicio), el texto y el icono aparecen centrados horizontal y
verticalmente dentro del botón.

**Why this priority**: el texto desalineado se percibe como un defecto y dificulta la lectura
rápida durante la venta.

**Independent Test**: recorrer las pantallas con botones de acción principal y verificar que el
contenido está centrado en ambos ejes, también con la ventana en el tamaño mínimo.

**Acceptance Scenarios**:

1. **Given** cualquier botón de acción principal, **When** se muestra en pantalla, **Then** su
   texto está centrado horizontal y verticalmente.
2. **Given** un botón cuyo texto ocupa dos líneas, **When** se muestra, **Then** ambas líneas
   aparecen centradas y el texto no sale del botón.

---

### User Story 8 - Encabezado con datos del negocio en reportes y tickets (Priority: P1)

Todo reporte exportado o impreso y todo ticket inician con el mismo encabezado con los datos del
negocio: nombre comercial, dirección, teléfono, RFC (si existe) y logo (si existe).

**Why this priority**: los reportes se entregan a contadores y dueños; sin los datos del negocio
no se identifica a quién corresponden.

**Independent Test**: capturar los datos del negocio con logo y RFC, exportar cada reporte e
imprimir cada tipo de ticket, y verificar el encabezado; repetir sin logo ni RFC.

**Acceptance Scenarios**:

1. **Given** datos del negocio completos con RFC y logo, **When** se exporta cualquier reporte a
   PDF, **Then** la primera página inicia con logo, nombre comercial, dirección, teléfono y RFC.
2. **Given** datos del negocio sin RFC ni logo, **When** se exporta un reporte, **Then** el
   encabezado muestra nombre, dirección y teléfono, sin espacios vacíos ni etiquetas sin valor.
3. **Given** datos del negocio completos, **When** se imprime cualquier tipo de ticket (venta,
   nota de crédito, abono, corte de caja), **Then** el ticket inicia con el mismo encabezado,
   adaptado al ancho del papel.
4. **Given** que no se han capturado datos del negocio, **When** se exporta un reporte, **Then** el
   encabezado indica "Datos del negocio no capturados" y el reporte se genera igualmente.
5. **Given** dos reportes diferentes, **When** se comparan sus encabezados, **Then** tienen el mismo
   orden de datos, tipografía y disposición.
6. **Given** un reporte de varias páginas, **When** se exporta a PDF, **Then** el encabezado
   aparece solo en la primera página; las páginas siguientes no llevan encabezado de negocio.

---

### User Story 9 - Tarjeta "Período de evaluación" con proporciones correctas (Priority: P1)

La tarjeta "Período de evaluación" tiene la misma altura que las demás tarjetas de su fila y el
texto "X días restantes" se ve completo y proporcionado, sin salirse de la tarjeta.

**Why this priority**: la tarjeta deformada es lo primero que ve un cliente en evaluación y da
una imagen de producto inacabado.

**Independent Test**: mostrar la tarjeta con 30, 1 y 0 días restantes, y con la ventana en tamaño
mínimo y maximizada; verificar altura y texto.

**Acceptance Scenarios**:

1. **Given** la tarjeta visible, **When** cambia el tamaño de la ventana, **Then** la altura de la
   tarjeta no cambia.
2. **Given** cualquier cantidad de días restantes, **When** se muestra la tarjeta, **Then** el texto
   cabe dentro de la tarjeta y su tamaño es proporcional al título.
3. **Given** un texto más largo que el ancho disponible, **When** se muestra la tarjeta, **Then** el
   texto se ajusta en líneas o se recorta con "..." y la tarjeta no crece ni se rompe; el texto
   completo se puede ver al pasar el puntero.

---

### User Story 10 - "Probar escáner" en Configuración (Priority: P1)

"Probar escáner" deja el grupo de Ayuda y pasa al grupo Configuración, junto a "Datos del
negocio", "Impresora" y "Seguridad". "Acerca de" queda solo como pantalla de información y
licencia.

**Why this priority**: probar un dispositivo es una tarea de configuración; mezclarlo con la
información de la aplicación confunde a quien busca dónde configurar el lector.

**Independent Test**: abrir el menú, verificar que "Probar escáner" aparece en Configuración y ya
no en Ayuda, y que "Acerca de" muestra solo lo indicado.

**Acceptance Scenarios**:

1. **Given** un usuario con acceso a Configuración, **When** abre el grupo Configuración, **Then**
   ve "Probar escáner" junto a "Datos del negocio" y las demás opciones de configuración.
2. **Given** cualquier usuario, **When** abre el grupo Ayuda, **Then** ya no aparece "Probar
   escáner".
3. **Given** la pantalla "Acerca de", **When** se abre, **Then** muestra solo: versión, ID de
   máquina (licencia), exportar diagnóstico y administración de licencia.
4. **Given** un Cajero sin acceso a Configuración, **When** necesita probar el lector, **Then**
   ve el grupo Configuración con la opción "Probar escáner" (y solo las opciones de configuración
   a las que tiene acceso) y puede usarla.

---

### Edge Cases

- La pantalla donde se guardó la ventana ya no está conectada (se desconectó un monitor): la
  ventana abre en la pantalla principal, dentro de su área visible.
- La preferencia guardada está dañada o no se puede leer: se usa el comportamiento por defecto
  (maximizada, grupos colapsados) sin mostrar error al operador y se registra en el log.
- La pantalla de carga no se puede cerrar porque el arranque falló: se muestra el mensaje de error
  sin esperar a los 2 segundos (escenario 3 de la historia 3).
- Se agrega un grupo nuevo al menú en una versión futura: aparece colapsado para todos los
  usuarios aunque tengan estado guardado de otros grupos.
- Un usuario no tiene acceso a ninguna opción de un grupo: el grupo no se muestra, igual que hoy.
- El logo del negocio es muy ancho o muy alto: se escala para caber en el espacio del encabezado
  sin deformarse.
- Nombre comercial o dirección en el máximo de caracteres permitido: el encabezado ajusta en
  varias líneas sin superponerse con el contenido del reporte.
- El reporte se exporta a hoja de cálculo: el encabezado aparece en las primeras filas con los
  mismos datos, sin logo.
- La impresora de tickets no admite imágenes: el ticket se imprime con el encabezado de texto sin
  logo.

## Requirements *(mandatory)*

### Functional Requirements

**Ventana (H1, H2)**

- **FR-001**: La ventana principal MUST abrir maximizada cuando no exista una preferencia de
  estado guardada.
- **FR-002**: El sistema MUST guardar al cerrar el estado de la ventana (maximizada o normal) y,
  en estado normal, su tamaño y posición; MUST restaurarlos en el siguiente arranque.
- **FR-003**: Una ventana minimizada al cerrar MUST restaurarse en su último estado no minimizado.
- **FR-004**: Si la posición guardada queda fuera de las pantallas conectadas, la ventana MUST
  abrir dentro del área visible de la pantalla principal.
- **FR-005**: La ventana principal MUST tener un tamaño mínimo de 1024×768 y no permitir
  redimensionarse por debajo de él.
- **FR-006**: Si el área útil de la pantalla es menor que el tamaño mínimo, la ventana MUST
  limitarse al área disponible y todas las pantallas MUST permitir desplazamiento para alcanzar
  todo su contenido.

**Pantalla de carga (H3)**

- **FR-007**: La pantalla de carga MUST permanecer visible al menos 2 segundos desde que aparece.
- **FR-008**: Si el arranque tarda más de 2 segundos, la pantalla de carga MUST cerrarse en cuanto
  el arranque termine, sin espera adicional.
- **FR-009**: La espera mínima MUST NOT retrasar el trabajo de arranque (migraciones, respaldos,
  verificación de licencia); el arranque se ejecuta en paralelo con la espera.
- **FR-010**: Si el arranque falla, el mensaje de error MUST mostrarse sin esperar la duración
  mínima.

**Menú lateral (H4, H5, H6)**

- **FR-011**: Todos los grupos del menú MUST mostrarse colapsados cuando el usuario no tiene un
  estado guardado para ellos.
- **FR-012**: El usuario MUST poder expandir y colapsar cada grupo de forma independiente.
- **FR-013**: El sistema MUST guardar el estado expandido o colapsado de cada grupo por usuario y
  restaurarlo en su siguiente sesión.
- **FR-014**: El botón para expandir o contraer el menú MUST estar alineado a la izquierda, en la
  misma columna que los iconos de las opciones, con el menú expandido o contraído.
- **FR-015**: Cada grupo y cada opción del menú MUST tener un icono distinto de todos los demás.
- **FR-016**: Todos los iconos del menú MUST compartir el mismo estilo visual y tamaño.
- **FR-017**: Con el menú contraído, cada icono MUST mostrar el nombre de su opción al pasar el
  puntero.

**Botones (H7)**

- **FR-018**: Los botones de acción principal MUST mostrar su texto (y su icono, si lo tienen)
  centrado horizontal y verticalmente.
- **FR-019**: Un texto que no cabe en una línea dentro de un botón de acción principal MUST
  ajustarse en varias líneas centradas sin salir del botón.

**Encabezado de reportes y tickets (H8)**

- **FR-020**: Todo reporte exportado a PDF MUST iniciar con un encabezado con logo (si existe),
  nombre comercial, dirección, teléfono y RFC (si existe). El encabezado MUST aparecer solo en la
  primera página; las páginas siguientes MUST NOT repetirlo.
- **FR-021**: Todo reporte exportado a hoja de cálculo MUST iniciar con filas de encabezado con
  nombre comercial, dirección, teléfono y RFC (si existe).
- **FR-022**: Todo ticket impreso (venta, nota de crédito, abono de cliente, corte de caja y
  cualquier otro comprobante) MUST iniciar con el mismo conjunto de datos del negocio, adaptado al
  ancho del papel.
- **FR-023**: Los datos opcionales que no existan (RFC, logo) MUST omitirse sin dejar espacios ni
  etiquetas vacías.
- **FR-024**: El encabezado MUST tener el mismo orden de datos y disposición en todos los
  reportes, y el mismo orden de datos en todos los tickets.
- **FR-025**: Si no hay datos del negocio capturados, los reportes MUST generarse con el aviso
  "Datos del negocio no capturados" en lugar del encabezado.

**Tarjeta de evaluación (H9)**

- **FR-026**: La tarjeta "Período de evaluación" MUST tener altura fija, igual a la de las demás
  tarjetas de su fila.
- **FR-027**: El texto de días restantes MUST tener un tamaño proporcional a la tarjeta y caber
  dentro de ella.
- **FR-028**: Un texto que no cabe MUST ajustarse o recortarse con "..." sin cambiar la altura de
  la tarjeta, y el texto completo MUST poder verse al pasar el puntero.

**Configuración y Acerca de (H10)**

- **FR-029**: "Probar escáner" MUST aparecer en el grupo Configuración, junto a "Datos del
  negocio", y MUST NOT aparecer en el grupo Ayuda.
- **FR-030**: La pantalla "Acerca de" MUST mostrar únicamente: producto ("Octopus punto de venta"),
  desarrollador ("Omar Ceron Ochoa") y email de contacto, versión, ID de máquina (licencia), carpeta
  de datos con "Copiar ruta", exportar diagnóstico y administración de licencia.
- **FR-031**: "Probar escáner" MUST seguir visible para todos los roles. El grupo Configuración
  MUST mostrarse a cualquier usuario con al menos una opción visible, y cada usuario MUST ver
  solo las opciones de configuración a las que tiene acceso.

**General**

- **FR-032**: Las preferencias de ventana y de menú MUST guardarse localmente en el equipo; si no
  se pueden leer, el sistema MUST usar los valores por defecto y registrar el problema en el log
  sin mostrar error al operador.

### Key Entities

- **Preferencias de ventana**: estado (maximizada o normal), tamaño y posición en estado normal.
  Una por equipo.
- **Preferencias de menú**: lista de grupos expandidos por usuario. Los grupos que no aparecen se
  consideran colapsados.
- **Datos del negocio**: ya existen (006); nombre comercial, dirección, teléfono, RFC y logo. Esta
  funcionalidad solo los consume en encabezados.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En el 100 % de los arranques sin preferencias guardadas, la ventana principal aparece
  maximizada.
- **SC-002**: En el 100 % de los arranques, la ventana y los grupos del menú aparecen en el mismo
  estado en que el usuario los dejó en su sesión anterior.
- **SC-003**: La ventana no puede reducirse por debajo de 1024×768 en ninguna prueba manual.
- **SC-004**: La pantalla de carga permanece visible entre 2.0 y 2.3 segundos cuando el arranque
  dura menos de 2 segundos, y no añade más de 0.3 segundos cuando dura más.
- **SC-005**: El menú visible para un Administrador tiene 0 iconos repetidos.
- **SC-006**: Con el menú contraído, un operador identifica correctamente al menos el 90 % de las
  opciones solo por su icono y su nombre emergente.
- **SC-007**: El 100 % de los reportes exportables y de los tipos de ticket muestran el encabezado
  con los datos del negocio capturados.
- **SC-008**: 0 botones de acción principal con texto descentrado o cortado en una revisión de
  todas las pantallas, con la ventana maximizada y en tamaño mínimo.
- **SC-009**: La tarjeta "Período de evaluación" conserva su altura y su texto queda dentro de
  ella con 0, 1, 9 y 30 días restantes, en tamaño mínimo y maximizado.
- **SC-010**: "Probar escáner" se encuentra en Configuración en menos de 10 segundos por un
  usuario que la busca por primera vez.

## Assumptions

- "Arrancan colapsados" aplica a la primera sesión de cada usuario y a grupos nuevos; después
  prevalece el estado recordado, que es por usuario porque varios operadores comparten el equipo.
- El estado de la ventana es por equipo, no por usuario, porque depende del monitor.
- El tamaño mínimo es 1024×768, el valor de ejemplo de la descripción.
- Los 2 segundos se cuentan desde que aparece la pantalla de carga.
- "Botones de acción principal" incluye los botones grandes de Inicio, Punto de venta, cobro,
  ingresos y retiros de caja, apertura y cierre de turno, y los accesos rápidos con icono y texto.
- "Reportes en pantalla" se interpreta como los reportes exportados o impresos; las pantallas de
  consulta de reportes dentro de la aplicación no agregan el encabezado, porque el operador ya
  conoce su negocio y el espacio se usa para los datos.
- En hojas de cálculo no se incluye el logo; solo los datos de texto.
- Los tickets ya incluyen datos del negocio (006); esta funcionalidad unifica su orden y agrega
  los que falten (RFC, logo) en los tipos de ticket que no los muestren.
- Los iconos nuevos se toman de la misma colección de iconos que ya usa la aplicación; no se
  agregan dependencias externas.
- No se requieren cambios en la base de datos: las preferencias se guardan en la configuración
  local de la aplicación.
- Toda la interfaz y los textos nuevos están en español.
