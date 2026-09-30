# Feature Specification: Estructura de navegación y formularios del POS

**Feature Branch**: `002-navigation-forms`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Estructura de navegación y formularios del POS: pantalla de carga,
inicio, menú lateral colapsable con submenús y patrón estándar de formularios."

## Contexto y objetivo

Definir la estructura visual y de navegación que usarán todas las funcionalidades, para que cada
módulo nuevo se integre sin rediseñar la aplicación. Parte de la fundación (`001-pos-foundation`):
arranque seguro, gestión de Productos y pantalla Acerca de.

**Usuarios**:

- **Operador del POS**: navega entre pantallas y captura datos en formularios.
- **Desarrollador del equipo**: agrega módulos, opciones de menú, tarjetas de inicio y
  formularios siguiendo un patrón único.

## Clarifications

### Session 2026-09-29

- Q: ¿Qué deben mostrar las tarjetas "Existencia baja" y "Sin existencia" y las opciones
  Existencias y Movimientos, si los productos aún no registran existencias? → A: Estado vacío
  "disponible más adelante", igual que las gráficas de ventas; Existencias y Movimientos abren una
  pantalla "disponible más adelante".
- Q: Cuando el operador sale de un campo, ¿la validación inmediata debe revisar también si el valor
  ya existe en la base, o solo formato y obligatoriedad? → A: No hay validación al salir del
  campo; todas las validaciones (formato, obligatoriedad y duplicados) se muestran solo al intentar
  guardar.
- Q: Cuando el operador sale de una pantalla y después regresa, ¿la pantalla conserva cómo la dejó
  o se abre desde cero? → A: Conserva búsqueda, filtros y selección durante la sesión y refresca
  los datos al regresar; no se conserva entre reinicios.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pantalla de carga (Priority: P1)

Como operador, quiero ver una pantalla con el logotipo mientras la aplicación arranca, para saber
que está cargando.

**Why this priority**: es lo primero que ve el operador en cada arranque, y durante una migración
el arranque puede tardar; sin indicación, parece que la aplicación no respondió.

**Independent Test**: se abre la aplicación con una base existente, con una migración pendiente y
con una base que provoca error; se verifica la pantalla de carga, los textos de cada paso y el
resultado final.

**Acceptance Scenarios**:

1. **Given** la aplicación cerrada, **When** el operador la abre, **Then** antes de cualquier otra
   pantalla aparece la pantalla de carga con el logotipo, el nombre y la versión.
2. **Given** la pantalla de carga visible, **When** se ejecutan las tareas de arranque, **Then** se
   muestra un texto breve con el paso en curso (por ejemplo "Verificando la base de datos…",
   "Respaldando…", "Actualizando la base de datos…").
3. **Given** un arranque exitoso, **When** terminan las tareas, **Then** la pantalla de carga se
   cierra y se abre la ventana principal en la pantalla de inicio.
4. **Given** un arranque que falla (base más nueva, sin permisos, migración fallida, base
   dañada…), **When** termina el paso fallido, **Then** se muestra el mensaje comprensible definido
   en la fundación y la ventana principal no se abre.
5. **Given** un archivo de logotipo reemplazado por el cliente, **When** se abre la aplicación,
   **Then** la pantalla de carga muestra el nuevo logotipo sin haber cambiado ni recompilado la
   aplicación.

---

### User Story 2 - Pantalla de inicio (Priority: P1)

Como operador, quiero una pantalla de inicio con un resumen del negocio, para ver de un vistazo
cómo va todo.

**Why this priority**: es la pantalla de entrada diaria y el lugar donde los módulos futuros
mostrarán sus indicadores.

**Independent Test**: con un catálogo de muestra, se abre la aplicación y se verifica que la
pantalla de inicio muestra los indicadores reales de productos, los estados vacíos de ventas y la
navegación desde cada tarjeta.

**Acceptance Scenarios**:

1. **Given** un arranque exitoso, **When** se abre la ventana principal, **Then** la primera
   pantalla es Inicio y la opción "Inicio" del menú aparece seleccionada.
2. **Given** 12 productos activos y 3 inactivos, **When** se muestra Inicio, **Then** la tarjeta
   "Productos activos" indica 12.
3. **Given** que no existe el módulo de ventas, **When** se muestra Inicio, **Then** las gráficas
   "Ventas del día", "Ventas de los últimos 7 días" y "Productos más vendidos" muestran un estado
   vacío que indica que estarán disponibles más adelante, sin ningún dato de ejemplo.
4. **Given** la tarjeta de productos activos, **When** el operador la selecciona, **Then** se abre
   la pantalla de Productos.
5. **Given** que los productos aún no registran existencias, **When** se muestra Inicio, **Then**
   las tarjetas "Existencia baja" y "Sin existencia" muestran un estado vacío que indica que
   estarán disponibles con el módulo de Inventario, sin ningún número inventado.
6. **Given** que el operador regresa a Inicio después de dar de alta un producto, **When** se
   muestra Inicio, **Then** los indicadores reflejan el cambio.

---

### User Story 3 - Menú lateral colapsable con submenús (Priority: P1)

Como operador, quiero un menú lateral organizado por grupos que pueda contraer, para navegar rápido
y tener más espacio de trabajo.

**Why this priority**: es la columna vertebral de la navegación; cada módulo futuro se integra aquí.

**Independent Test**: se expande y contrae el menú, se abren y cierran grupos, se navega con el
menú contraído mediante el menú flotante, se reinicia la aplicación y se verifica que el estado se
conserva; se reduce el ancho de la ventana y el menú se contrae solo.

**Acceptance Scenarios**:

1. **Given** la ventana principal, **When** se muestra el menú, **Then** su estructura es: Inicio;
   Catálogos > Productos; Inventario > Existencias, Movimientos; Ayuda > Acerca de.
2. **Given** el menú expandido, **When** el operador selecciona un grupo, **Then** el grupo se abre o
   se cierra mostrando u ocultando sus opciones.
3. **Given** el menú expandido, **When** el operador usa el botón o el atajo de teclado para
   alternarlo, **Then** el menú se contrae y muestra solo íconos; al repetirlo se expande y muestra
   íconos y texto.
4. **Given** el menú contraído, **When** el operador selecciona un grupo, **Then** sus opciones
   aparecen en un menú flotante y al elegir una se navega a ella.
5. **Given** el menú contraído, **When** el operador coloca el puntero sobre un ícono, **Then** un
   tooltip muestra su nombre.
6. **Given** que se está en Productos, **When** se observa el menú expandido o contraído, **Then**
   la opción Productos y su grupo Catálogos se distinguen visualmente.
7. **Given** el menú contraído y el grupo Inventario abierto, **When** el operador cierra y vuelve a
   abrir la aplicación, **Then** el menú aparece contraído y, al expandirlo, Inventario sigue
   abierto.
8. **Given** una ventana más ancha que el umbral, **When** el operador la reduce por debajo del
   umbral, **Then** el menú se contrae automáticamente.
9. **Given** un módulo nuevo, **When** el desarrollador registra su grupo y sus opciones, **Then**
   aparecen en el menú sin modificar la vista del menú.
10. **Given** Productos con la búsqueda "leche", el filtro de inactivos activado y un producto
    seleccionado, **When** el operador va a Inicio y regresa a Productos, **Then** la búsqueda, el
    filtro y la selección se conservan y la lista muestra los datos actuales.

---

### User Story 4 - Patrón estándar de formularios (Priority: P1)

Como desarrollador, quiero un patrón único de formularios, para que todas las altas y ediciones se
comporten igual.

**Why this priority**: todas las funcionalidades futuras (ventas, inventario, clientes) dependen de
formularios; definirlo ahora evita comportamientos distintos en cada módulo.

**Independent Test**: se verifica que Productos se abre como formulario corto en panel lateral, y
que un formulario grande de prueba se abre a pantalla completa dentro del área de contenido con el
menú visible; en ambos se prueban Guardar, Cancelar, campos obligatorios, validación al guardar y
foco en el primer error.

**Acceptance Scenarios**:

1. **Given** el listado de Productos, **When** el operador crea o edita un producto, **Then** el
   formulario se abre en un panel lateral sobre el listado.
2. **Given** un formulario grande, **When** se abre, **Then** ocupa el área de contenido, el menú
   permanece visible y hay una forma clara de regresar al listado.
3. **Given** un formulario, **When** se decide mostrarlo como corto o como grande, **Then** su
   comportamiento (campos, validaciones, guardado, confirmación de salida) es el mismo sin
   reescribirlo.
4. **Given** cualquier formulario, **When** se muestra, **Then** tiene Guardar y Cancelar, y los
   campos obligatorios se distinguen visualmente.
5. **Given** un campo con un valor inválido, **When** el operador sale del campo, **Then** no se
   muestra ningún error todavía; la validación ocurre al intentar guardar.
6. **Given** varios campos inválidos, **When** el operador intenta guardar, **Then** aparecen todos
   los errores y el foco va al primer campo inválido.
7. **Given** un formulario válido, **When** el operador hace doble clic en Guardar, **Then** se
   registra una sola vez.

---

### User Story 5 - Confirmación al salir con cambios sin guardar (Priority: P1)

Como operador, quiero que se me avise si intento salir de un formulario con cambios sin guardar,
para no perder información por error.

**Why this priority**: perder datos capturados por un clic accidental afecta directamente al
trabajo del operador.

**Independent Test**: se modifica un formulario y se intenta cancelar, navegar a otra opción del
menú, cerrar el panel y cerrar la aplicación; en cada caso se prueban las tres opciones de la
confirmación. Se repite sin cambios y con cambios revertidos.

**Acceptance Scenarios**:

1. **Given** un formulario con cambios, **When** el operador cancela, navega a otra opción del
   menú, cierra el panel o cierra la aplicación, **Then** se pregunta con las opciones Guardar,
   Descartar y Seguir editando.
2. **Given** la confirmación, **When** el operador elige Guardar y los datos son válidos, **Then** se
   guarda y se completa la acción que había iniciado (navegar, cerrar…).
3. **Given** la confirmación, **When** el operador elige Guardar y hay errores de validación,
   **Then** el formulario permanece abierto mostrando los errores y la acción no se completa.
4. **Given** la confirmación, **When** el operador elige Descartar, **Then** se pierden los cambios y
   se completa la acción.
5. **Given** la confirmación, **When** el operador elige Seguir editando, **Then** el formulario
   permanece abierto con lo capturado y la acción no se completa.
6. **Given** un formulario sin cambios, o con cambios que se revirtieron a los valores originales,
   **When** el operador sale, **Then** no se pregunta nada.

---

### Edge Cases

- Falta el archivo de logotipo o está dañado: la pantalla de carga muestra el logotipo
  predeterminado incluido en la aplicación, sin error.
- El arranque tarda muy poco: la pantalla de carga no parpadea (se muestra un tiempo mínimo breve).
- Falla el cálculo de un indicador de inicio: esa tarjeta muestra un estado de error discreto y el
  resto de la pantalla sigue funcionando; el error queda registrado.
- No hay productos: la tarjeta "Productos activos" muestra 0.
- La preferencia del menú guardada está dañada o no existe: se usa el menú expandido con todos los
  grupos cerrados salvo el de la opción actual.
- La preferencia recuerda un grupo que ya no existe: se ignora.
- La ventana se agranda por encima del umbral después de una contracción automática: el menú
  vuelve al estado que el operador había elegido.
- Opciones de menú de módulos que aún no existen (Existencias, Movimientos): abren una pantalla
  "disponible más adelante" con el nombre de la opción; se pueden seleccionar y se distinguen como
  opción actual igual que las demás.
- Navegar a la opción que ya está abierta no pide confirmación ni reinicia el formulario.
- Al regresar a una pantalla, el elemento que estaba seleccionado ya no existe (por ejemplo, se
  borró): la selección queda vacía, sin error.
- Cerrar la aplicación con un formulario con cambios y elegir Seguir editando cancela el cierre,
  incluido el respaldo al cerrar.
- Guardar desde la confirmación falla por un error inesperado: el formulario permanece abierto con
  lo capturado y se muestra el mensaje genérico de la fundación.
- Un formulario corto abierto y el operador abre otro producto desde el listado: se aplica la misma
  confirmación que al cerrar el panel.

## Requirements *(mandatory)*

### Functional Requirements

**Pantalla de carga**

- **FR-001**: El sistema MUST mostrar una pantalla de carga con logotipo, nombre y versión antes de
  cualquier otra pantalla.
- **FR-002**: La pantalla de carga MUST permanecer visible durante todas las tareas de arranque de
  la fundación e indicar con un texto breve el paso en curso.
- **FR-003**: Si el arranque falla, el sistema MUST mostrar el mensaje comprensible de la fundación
  y no abrir la ventana principal. Los flujos de la fundación (restaurar una base dañada, segunda
  instancia) se conservan.
- **FR-004**: El logotipo MUST poder reemplazarse colocando un archivo de imagen en una ubicación
  documentada, sin cambiar ni recompilar la aplicación; si falta o es inválido, se usa el
  logotipo predeterminado.

**Inicio**

- **FR-005**: Inicio MUST ser la primera pantalla tras la carga y MUST estar accesible desde la
  opción "Inicio" del menú.
- **FR-006**: Inicio MUST mostrar la tarjeta "Productos activos" con el número real de productos
  activos no borrados.
- **FR-007**: Inicio MUST mostrar las tarjetas "Existencia baja" y "Sin existencia" con un estado
  vacío que indique que estarán disponibles con el módulo de Inventario. Cuando exista ese módulo,
  sus tarjetas mostrarán datos reales y llevarán al listado filtrado, sin modificar Inicio (FR-011).
- **FR-008**: Inicio MUST mostrar los espacios de gráficas "Ventas del día", "Ventas de los últimos
  7 días" y "Productos más vendidos" con un estado vacío que indique que estarán disponibles más
  adelante. MUST NOT mostrar datos de ejemplo como si fueran reales.
- **FR-009**: Cada tarjeta con datos MUST poder llevar a su pantalla relacionada.
- **FR-010**: Los indicadores MUST actualizarse cada vez que se muestra Inicio.
- **FR-011**: Un módulo MUST poder agregar tarjetas o gráficas a Inicio registrándolas, sin
  modificar la pantalla de inicio.

**Menú**

- **FR-012**: El menú MUST tener la estructura inicial: Inicio; Catálogos > Productos; Inventario >
  Existencias, Movimientos; Ayuda > Acerca de. Máximo dos niveles (grupos y opciones); una opción
  puede estar en el primer nivel sin grupo (Inicio).
- **FR-013**: Un botón y un atajo de teclado MUST alternar el menú entre expandido (ícono y texto) y
  contraído (solo ícono).
- **FR-014**: Expandido, cada grupo MUST abrirse y cerrarse mostrando u ocultando sus opciones.
- **FR-015**: Contraído, seleccionar un grupo MUST mostrar sus opciones en un menú flotante, y cada
  ícono MUST mostrar su nombre en un tooltip.
- **FR-016**: La opción actual y su grupo MUST distinguirse visualmente en ambos estados.
- **FR-017**: El estado expandido o contraído y los grupos abiertos MUST recordarse entre sesiones
  como preferencia local de la máquina.
- **FR-018**: Si el ancho de la ventana es menor que el umbral, el menú MUST contraerse
  automáticamente; al superar de nuevo el umbral, MUST volver al estado elegido por el operador.
- **FR-019**: Los elementos del menú MUST tener áreas de toque de al menos 44 × 44 píxeles
  independientes de la escala.
- **FR-020**: Un módulo MUST poder registrar sus grupos y opciones, con ícono, texto y orden, sin
  modificar la vista del menú.
- **FR-020a**: Las opciones de módulos aún no disponibles (Existencias y Movimientos) MUST abrir
  una pantalla "disponible más adelante" con el nombre de la opción.
- **FR-020b**: Al regresar a una pantalla durante la misma sesión, MUST conservarse su búsqueda,
  filtros y selección, y sus datos MUST refrescarse. Un formulario abierto no se conserva: salir de
  él sigue las reglas de cambios sin guardar. Este estado no se conserva entre reinicios.

**Formularios**

- **FR-021**: Los formularios cortos (hasta 8 campos, sin pestañas ni listas internas) MUST abrirse
  en un panel lateral sobre el listado.
- **FR-022**: Los formularios grandes MUST abrirse a pantalla completa dentro del área de
  contenido, con el menú visible y una forma clara de regresar al listado.
- **FR-023**: El comportamiento de un formulario MUST ser independiente de cómo se muestra; cambiar
  de corto a grande MUST NOT requerir reescribir su comportamiento.
- **FR-024**: Todo formulario MUST tener Guardar y Cancelar; Guardar MUST evitar registros
  duplicados por doble clic.
- **FR-025**: Los campos obligatorios MUST marcarse visualmente.
- **FR-026**: Las validaciones (formato, obligatoriedad y duplicados) MUST ejecutarse al intentar
  guardar y mostrarse todas juntas, cada una junto a su campo; al guardar con errores, el foco MUST
  ir al primer campo inválido. Salir de un campo MUST NOT mostrar errores. Un error mostrado
  permanece hasta el siguiente intento de guardar.
- **FR-027**: Productos MUST usar el patrón de formulario corto.

**Cambios sin guardar**

- **FR-028**: Al cancelar, navegar a otra opción del menú, cerrar el panel o cerrar la aplicación
  con cambios sin guardar, el sistema MUST preguntar con las opciones Guardar, Descartar y Seguir
  editando.
- **FR-029**: Si al elegir Guardar hay errores de validación o el guardado es rechazado (duplicado,
  conflicto), el formulario MUST permanecer abierto mostrando el problema y la acción iniciada MUST
  NOT completarse.
- **FR-030**: Si no hubo cambios, o los valores volvieron a ser los originales, el sistema MUST NOT
  preguntar.

### Key Entities *(include if feature involves data)*

- **Opción de menú**: destino navegable con identificador, texto, ícono, orden y grupo opcional.
- **Grupo de menú**: agrupa opciones; tiene identificador, texto, ícono y orden.
- **Preferencias de navegación**: estado expandido o contraído y grupos abiertos; se guardan por
  máquina, fuera de la base de datos del negocio.
- **Tarjeta de inicio**: indicador o gráfica con título, estado (cargando, con datos, vacío o
  error), destino opcional y orden; la registra cada módulo.
- **Formulario**: conjunto de campos con valores originales y actuales, obligatoriedad, errores por
  campo y estado "con cambios"; se muestra como corto o grande.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Al abrir la aplicación se ve la pantalla de carga con logotipo y después Inicio, en
  Windows y en Linux.
- **SC-002**: La pantalla de carga aparece en menos de 1 segundo desde que el operador abre la
  aplicación, en el equipo de referencia de la fundación.
- **SC-003**: El menú se expande, se contrae, abre grupos, muestra menús flotantes contraído y
  recuerda su estado tras reiniciar; las pruebas automáticas lo verifican.
- **SC-004**: Productos usa el formulario corto, y al menos una prueba automática verifica el patrón
  de formulario grande.
- **SC-005**: Pruebas automáticas verifican la confirmación de cambios sin guardar al cancelar,
  navegar, cerrar el panel y cerrar la aplicación, con las tres opciones, y que no se pregunta si
  no hubo cambios o si se revirtieron.
- **SC-006**: Las gráficas de ventas y las tarjetas de existencias muestran estado vacío, y la
  tarjeta de productos activos muestra el número real; ningún indicador muestra datos inventados.
- **SC-007**: Agregar una opción de menú o una tarjeta de inicio para un módulo nuevo no requiere
  modificar ninguna vista existente; una prueba lo demuestra registrando una opción y una tarjeta
  de prueba.
- **SC-008**: Un operador puede llegar a cualquier opción del menú en 2 selecciones o menos desde
  cualquier pantalla, con el menú expandido o contraído.

## Assumptions

- Umbral de contracción automática del menú: ventana de menos de 1000 píxeles independientes de la
  escala de ancho.
- Atajo para alternar el menú: Ctrl+B.
- Tiempo mínimo de la pantalla de carga: 800 ms, para evitar un parpadeo; no retrasa un arranque
  que de por sí tarda más.
- Logotipo reemplazable: un archivo `logo.png` en la carpeta de datos de la aplicación (ver
  `docs/carpeta-de-datos.md`); si no existe, se usa el incluido en la aplicación.
- Las preferencias del menú se guardan en un archivo dentro de la carpeta de datos de la
  aplicación, por usuario del sistema operativo en esa máquina.
- Al elegir una opción del menú flotante (contraído), el menú flotante se cierra.
- Un formulario tiene "cambios" si algún campo difiere de su valor original, comparado tal como se
  guardaría (por ejemplo, un SKU en minúsculas que se guardará igual en mayúsculas no cuenta como
  cambio).
- La pantalla Acerca de de la fundación se mueve al grupo Ayuda; la de Productos, al grupo
  Catálogos.
- Fuera de alcance: el módulo de ventas y sus datos reales; el módulo de inventario y cualquier
  registro de existencias en el producto; personalización del menú por usuario o
  rol; temas o personalización visual.
- Dependencias: fundación `001-pos-foundation` (arranque, Productos, OperationRunner, mensajes de
  error) y constitución v1.1.0.
