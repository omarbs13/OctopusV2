# Feature Specification: Fundación del POS con flujo de referencia de Productos

**Feature Branch**: `001-pos-foundation`

**Created**: 2026-09-29

**Status**: Draft

**Input**: User description: "Fundación del POS: base de la aplicación de escritorio con un flujo de
referencia de Productos. Primera funcionalidad del POS; deja una base estable, probada y documentada
y demuestra de punta a punta el camino interfaz → caso de uso → dominio → persistencia local,
usando la gestión básica de Productos como flujo de referencia."

## Contexto y objetivo

Es la primera funcionalidad del POS. Su propósito es dejar una base estable, probada y documentada
sobre la que se construirán todas las funcionalidades futuras (ventas, cobros, cortes,
inventario). Debe demostrar de punta a punta el camino que seguirá cualquier funcionalidad, de la
interfaz a la persistencia local, respetando la constitución del proyecto.

Como flujo de referencia se implementa la gestión básica de Productos, porque es el catálogo del
que dependerán las ventas.

**Usuarios**:

- **Operador del POS**: registra y consulta productos en la caja, sin internet.
- **Desarrollador del equipo**: usa esta base y su documentación para agregar funcionalidades
  nuevas.
- **Soporte técnico**: necesita diagnosticar problemas en la máquina de un cliente.

## Clarifications

### Session 2026-09-29

- Q: Cuando la base de datos está dañada al arrancar, ¿qué debe hacer la aplicación? → A: Mostrar
  el mensaje y ofrecer restaurar el respaldo automático más reciente, indicando su fecha; restaurar
  solo si el operador confirma y conservar aparte la base dañada para soporte.
- Q: ¿Con qué frecuencia debe la aplicación hacer respaldos automáticos de la base y cuántos
  debe conservar, aparte de los que hace antes de cada migración? → A: Al arrancar y al cerrar la
  aplicación, si el último respaldo automático tiene más de 24 h; conservar los 7 más recientes.
- Q: ¿Los productos inactivos deben aparecer en el listado y en la búsqueda? → A: Por defecto solo
  se muestran los activos; un filtro "Mostrar inactivos" permite verlos también.
- Q: Cuando el operador busca por código de barras, ¿la coincidencia debe ser exacta o parcial?
  → A: Exacta si el texto buscado parece un código completo (solo dígitos, entre 8 y 14);
  parcial en los demás casos.
- Q: Al capturar un precio, ¿qué formatos debe aceptar la aplicación? → A: Solo dígitos y punto
  decimal ("1234.50"); cualquier coma se rechaza.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Arranque confiable de la aplicación (Priority: P1)

Como operador, quiero que la aplicación abra de forma segura cada vez, para poder empezar a vender
sin preocuparme por el estado de la base de datos.

**Why this priority**: sin un arranque confiable no existe ninguna otra funcionalidad. Una
actualización que dañe la base de un cliente en producción detiene su negocio.

**Independent Test**: se prueba instalando la aplicación en un equipo limpio, en uno con una base
de una versión anterior, en uno con una base de una versión más nueva y provocando una falla de
actualización. En cada caso se verifica el resultado esperado sin usar ninguna otra historia.

**Acceptance Scenarios**:

1. **Given** un equipo sin datos previos de la aplicación, **When** el operador abre la
   aplicación, **Then** se crea la base de datos en la carpeta de datos del usuario del sistema
   operativo y se muestra la ventana principal.
2. **Given** una base creada por una versión anterior con cambios de esquema pendientes, **When**
   el operador abre la versión nueva, **Then** la aplicación respalda la base, aplica los cambios
   y abre normalmente, sin intervención del operador y sin perder datos.
3. **Given** la aplicación ya abierta, **When** el operador intenta abrirla otra vez, **Then** no
   se abre una segunda instancia, se informa al operador y, si es posible, se trae al frente la
   ventana existente.
4. **Given** una base creada por una versión más nueva de la aplicación, **When** el operador abre
   la aplicación, **Then** la base no se abre ni se modifica y se muestra un mensaje claro
   indicando que debe instalarse la versión correcta.
5. **Given** una actualización de esquema que falla a mitad del proceso, **When** el operador abre
   la aplicación, **Then** se restaura el respaldo, se registra el error, se muestra un mensaje
   comprensible y la aplicación no continúa con una base a medias.
6. **Given** una base dañada y al menos un respaldo automático, **When** el operador abre la
   aplicación, **Then** se muestra un mensaje claro con la fecha del respaldo más reciente y se
   ofrece restaurarlo. Si el operador confirma, la base dañada se conserva aparte y la aplicación
   abre con el respaldo restaurado; si no confirma, la aplicación no abre la base.

---

### User Story 2 - Registrar productos (Priority: P1)

Como operador, quiero dar de alta productos con su nombre, código y precio, para tenerlos
disponibles al vender.

**Why this priority**: el catálogo es el requisito previo de las ventas y es el flujo que
demuestra el camino completo de punta a punta.

**Independent Test**: se registra un producto válido y se verifica que aparece en el listado y
que persiste tras cerrar y reabrir la aplicación. Se intenta registrar datos inválidos y se
verifica que se indican los errores sin perder lo capturado.

**Acceptance Scenarios**:

1. **Given** el formulario de nuevo producto, **When** el operador captura nombre, SKU, código de
   barras opcional y precio válidos y guarda, **Then** el producto se registra activo y aparece
   de inmediato en el listado.
2. **Given** el formulario con uno o más datos inválidos, **When** el operador guarda, **Then** se
   indica junto a cada campo qué debe corregirse, no se registra nada y los datos capturados se
   conservan.
3. **Given** un producto no borrado con SKU "ABC-1", **When** el operador registra otro producto
   con SKU "abc-1", **Then** se rechaza indicando que el SKU ya existe.
4. **Given** el formulario válido, **When** el operador hace doble clic en "Guardar", **Then** el
   producto se registra una sola vez.

---

### User Story 3 - Consultar y buscar productos (Priority: P1)

Como operador, quiero ver el listado de productos y buscar por nombre, SKU o código de barras,
para encontrar rápido lo que necesito.

**Why this priority**: registrar sin poder consultar no aporta valor; la búsqueda rápida es la
base de la futura captura de ventas.

**Independent Test**: con un catálogo de muestra, se buscan productos por fragmentos de nombre
con y sin acentos, por SKU y por código de barras, y se verifican los resultados y el formato
del precio.

**Acceptance Scenarios**:

1. **Given** productos activos, inactivos y borrados, **When** el operador abre el listado,
   **Then** se muestran solo los activos; los inactivos y los borrados no se muestran.
2. **Given** el listado con productos inactivos, **When** el operador activa el filtro "Mostrar
   inactivos", **Then** se muestran también los inactivos, con su estado visible, y los borrados
   siguen sin mostrarse.
3. **Given** un producto llamado "Café Molido", **When** el operador busca "cafe molido",
   **Then** el producto aparece en los resultados.
4. **Given** un producto con SKU "LEC-001" y código de barras "7501234567890", **When** el
   operador busca "lec-001" o "7501234567890", **Then** el producto aparece en los resultados.
5. **Given** productos con códigos de barras "7501234567890" y "7501234567891", **When** el
   operador busca "7501234567890", **Then** solo aparece el primero; **When** busca "7501",
   **Then** aparecen ambos.
6. **Given** una búsqueda sin coincidencias, **When** se ejecuta, **Then** se indica
   explícitamente que no hay resultados.
7. **Given** un producto con precio 1234.5, **When** se muestra en el listado, **Then** el precio
   aparece como "$1,234.50".

---

### User Story 4 - Editar productos (Priority: P2)

Como operador, quiero modificar los datos de un producto, para mantener el catálogo al día.

**Why this priority**: es necesario para mantener el catálogo, pero la base puede operar sin
edición durante las primeras pruebas.

**Independent Test**: se edita un producto y se verifica el cambio. Se simulan dos ediciones
concurrentes del mismo producto y se verifica que la segunda se rechaza.

**Acceptance Scenarios**:

1. **Given** un producto existente, **When** el operador modifica sus datos con valores válidos y
   guarda, **Then** los cambios se reflejan en el listado y se registran la fecha y el usuario de
   la modificación.
2. **Given** un producto existente, **When** el operador guarda datos inválidos, **Then** se
   aplican las mismas validaciones que al registrar.
3. **Given** un producto abierto para edición que otra operación modificó después de abrirlo,
   **When** el operador guarda, **Then** el guardado se rechaza con un mensaje claro y se ofrece
   recargar los datos actuales.
4. **Given** un producto activo, **When** el operador lo marca como inactivo y guarda, **Then** el
   producto deja de aparecer en el listado por defecto y aparece, con estado inactivo, al activar
   el filtro "Mostrar inactivos".

---

### User Story 5 - Borrar productos (Priority: P2)

Como operador, quiero borrar productos que ya no se venden, para mantener limpio el catálogo.

**Why this priority**: mejora la limpieza del catálogo, pero no es necesaria para demostrar el
flujo base.

**Independent Test**: se borra un producto, se verifica que desaparece del listado y que el
registro se conserva, y se registra un producto nuevo con su mismo SKU y código de barras.

**Acceptance Scenarios**:

1. **Given** un producto en el listado, **When** el operador elige borrarlo, **Then** se pide
   confirmación antes de borrar.
2. **Given** la confirmación, **When** el operador confirma, **Then** el producto deja de aparecer
   en el listado y en la búsqueda, y su registro se conserva con la fecha de borrado.
3. **Given** la confirmación, **When** el operador cancela, **Then** el producto no cambia.
4. **Given** un producto borrado con SKU "LEC-001", **When** el operador registra un producto nuevo
   con SKU "LEC-001", **Then** el registro se acepta.

---

### User Story 6 - Diagnóstico para soporte (Priority: P3)

Como soporte técnico, quiero conocer la versión instalada y obtener los logs y un respaldo de la
base, para diagnosticar problemas sin acceso remoto complejo.

**Why this priority**: es indispensable para dar soporte en producción, pero no bloquea la
operación del operador.

**Independent Test**: se abre "Acerca de" y se verifican la versión y la ubicación de los datos.
Se exporta el diagnóstico y se verifica que el archivo contiene los logs y un respaldo que se puede
abrir.

**Acceptance Scenarios**:

1. **Given** la aplicación abierta, **When** el usuario abre "Acerca de", **Then** se muestran la
   versión de la aplicación y la ubicación de los datos.
2. **Given** la aplicación abierta, **When** el usuario elige exportar el diagnóstico y una
   ubicación, **Then** se genera en esa ubicación un único archivo comprimido con los logs
   recientes y un respaldo consistente de la base.
3. **Given** el archivo exportado, **When** soporte abre el respaldo que contiene, **Then** la base
   se abre correctamente y contiene los datos del momento de la exportación.

---

### Edge Cases

- La base está bloqueada o sin permisos de escritura: se muestra un mensaje claro al operador,
  el detalle queda en el log y la aplicación no se cierra abruptamente.
- La base está dañada: además del mensaje y el log, se ofrece restaurar el respaldo automático
  más reciente (FR-008a). Si no existe ningún respaldo, solo se muestra el mensaje y se indica
  contactar a soporte.
- El disco está lleno al respaldar o migrar: no se aplica la migración y se informa al operador.
- Doble clic en "Guardar": el producto no se registra dos veces.
- Búsqueda sin resultados: se indica explícitamente.
- Nombre con caracteres especiales o acentos: se guarda sin alterarse y se encuentra al buscar.
- Precio con más de 2 decimales: se rechaza con un mensaje; nunca se redondea en silencio.
- Precio con coma, ya sea como separador de miles ("1,234.50") o decimal ("12,50"): se rechaza
  con un mensaje que indica el formato válido ("1234.50").
- Precio negativo o mayor que 999,999.99: se rechaza.
- Código de barras con letras, espacios o una longitud fuera de 8 a 14 dígitos: se rechaza.
- SKU con espacios: se rechaza; SKU en minúsculas: se guarda en mayúsculas.
- Nombre con espacios al inicio o al final: se recortan antes de validar y guardar.
- La exportación de diagnóstico falla (ubicación sin permisos o disco lleno): se informa al usuario
  y no queda un archivo incompleto que parezca válido.
- Un error inesperado en cualquier operación: se registra con su contexto, el operador ve un
  mensaje comprensible y la aplicación sigue abierta sin perder lo capturado.

## Requirements *(mandatory)*

### Functional Requirements

**Arranque y base de datos**

- **FR-001**: El sistema MUST crear su base de datos en la carpeta de datos del usuario del
  sistema operativo en el primer arranque.
- **FR-002**: El sistema MUST impedir una segunda instancia simultánea, informar al operador y,
  si es posible, traer al frente la ventana existente.
- **FR-003**: El sistema MUST detectar si la base fue creada por una versión más nueva de la
  aplicación y, en ese caso, no abrirla ni modificarla, mostrando un mensaje que indique instalar
  la versión correcta.
- **FR-004**: Antes de aplicar cualquier cambio de esquema, el sistema MUST respaldar la base de
  forma consistente. Si el respaldo falla (por ejemplo, disco lleno), no se aplica el cambio y se
  informa al operador.
- **FR-005**: El sistema MUST aplicar automáticamente los cambios de esquema pendientes al
  arrancar, sin intervención del operador.
- **FR-006**: Si un cambio de esquema falla, el sistema MUST restaurar el respaldo previo,
  registrar el error, informar al operador y no continuar.
- **FR-007**: Además del respaldo previo a los cambios de esquema, el sistema MUST realizar un
  respaldo automático de la base al arrancar y al cerrar la aplicación, siempre que el último
  respaldo automático tenga más de 24 horas. MUST conservar los 7 respaldos automáticos más
  recientes y eliminar los más antiguos. Un respaldo fallido se registra en el log y MUST NOT
  impedir el uso de la aplicación ni retrasar su cierre de forma indefinida.
- **FR-008**: Si la base está bloqueada, dañada o sin permisos de escritura, el sistema MUST
  informar al operador con un mensaje claro, registrar el detalle y no cerrarse abruptamente.
- **FR-008a**: Si la base está dañada y existe al menos un respaldo automático, el sistema MUST
  ofrecer restaurar el más reciente, mostrando su fecha. Solo MUST restaurarlo si el operador
  confirma, y MUST conservar aparte la base dañada para que soporte la analice. Sin confirmación,
  la aplicación no abre la base.

**Productos**

- **FR-009**: Los operadores MUST poder registrar productos con nombre, SKU, código de barras
  opcional, precio de venta y estado activo/inactivo. Un producto nuevo se crea activo.
- **FR-010**: El sistema MUST validar el nombre: obligatorio, sin espacios al inicio ni al final
  (se recortan), máximo 200 caracteres.
- **FR-011**: El sistema MUST validar el SKU: obligatorio, máximo 50 caracteres, sin espacios,
  guardado en mayúsculas y único entre productos no borrados, sin distinguir mayúsculas y
  minúsculas.
- **FR-012**: El sistema MUST validar el código de barras: opcional; si se captura, solo dígitos,
  entre 8 y 14 caracteres y único entre productos no borrados.
- **FR-013**: El sistema MUST validar el precio de venta: obligatorio, mayor o igual a 0, máximo
  2 decimales y máximo 999,999.99, en pesos mexicanos. Al capturarlo solo se aceptan dígitos y
  un punto decimal opcional (por ejemplo "1234.50"); cualquier coma, símbolo de moneda u otro
  carácter se rechaza. Los valores con más de 2 decimales se rechazan; nunca se redondean en
  silencio.
  > **Nota (003)**: reemplazada por FR-015 y FR-016 de
  > `specs/003-product-catalog-improvements/spec.md`: el precio debe ser mayor que 0 y la captura
  > acepta el separador de miles ("1,234.50").
- **FR-014**: Ante datos inválidos, el sistema MUST indicar el error junto a cada campo afectado
  y conservar lo capturado.
- **FR-015**: El sistema MUST evitar registros duplicados cuando se envía el mismo guardado más de
  una vez, por ejemplo con un doble clic.
- **FR-016**: El sistema MUST mostrar un listado de productos que nunca incluya los borrados y
  que, por defecto, muestre solo los activos. Un filtro "Mostrar inactivos" MUST permitir incluir
  también los inactivos, con su estado visible. El filtro aplica igual al listado y a la búsqueda.
- **FR-017**: Los operadores MUST poder buscar productos por nombre, SKU o código de barras. El
  nombre y el SKU se buscan por coincidencia parcial. El código de barras se busca por
  coincidencia exacta cuando el texto buscado tiene solo dígitos y entre 8 y 14 caracteres, y por
  coincidencia parcial en los demás casos. La búsqueda no distingue mayúsculas y minúsculas y, en
  el nombre, tampoco acentos. Cuando no hay resultados, se indica explícitamente.
- **FR-018**: El sistema MUST mostrar los precios con formato de moneda de pesos mexicanos (por
  ejemplo "$1,234.50").
- **FR-019**: Los operadores MUST poder editar cualquier dato de un producto, con las mismas
  validaciones que al registrar.
- **FR-020**: El sistema MUST rechazar un guardado si el producto fue modificado por otra
  operación después de abrirse para edición, informar al operador y ofrecer recargar los datos
  actuales.
- **FR-021**: Los operadores MUST poder borrar productos previa confirmación. El borrado es
  lógico: el producto desaparece del listado y la búsqueda, pero el registro se conserva.
- **FR-022**: El sistema MUST permitir reutilizar el SKU y el código de barras de un producto
  borrado en un producto nuevo.
- **FR-023**: Todo producto MUST registrar fecha y usuario de creación, fecha y usuario de última
  modificación, fecha de borrado y una versión para detectar modificaciones concurrentes.
- **FR-024**: Mientras no exista autenticación, el usuario registrado en la auditoría MUST ser un
  usuario de sistema fijo, sustituible por el usuario real sin cambiar las operaciones de negocio.
- **FR-025**: Todas las operaciones de productos MUST funcionar sin conexión a internet.

**Errores y diagnóstico**

- **FR-026**: Ante un error inesperado en cualquier operación, el sistema MUST registrarlo con su
  contexto (operación, usuario, identificadores, sin datos sensibles), mostrar al operador un
  mensaje comprensible sin detalles técnicos y seguir abierto sin perder lo capturado.
- **FR-027**: El sistema MUST ofrecer una pantalla "Acerca de" con la versión de la aplicación y
  la ubicación de los datos.
- **FR-028**: El sistema MUST permitir exportar, a la ubicación que elija el usuario, un único
  archivo comprimido con los logs recientes y un respaldo consistente de la base. Si la
  exportación falla, se informa y no queda un archivo incompleto.

**Base para el desarrollo**

- **FR-029**: La solución MUST poder compilarse con un solo comando y probarse con un solo
  comando desde la raíz del repositorio, en Windows y en Linux.
- **FR-030**: MUST existir documentación en español que explique los prerrequisitos; cómo
  compilar, ejecutar y probar en Windows y Linux; dónde guarda la aplicación sus datos; cómo crear
  y revisar un cambio de esquema; y una guía paso a paso para agregar una funcionalidad nueva
  usando Productos como ejemplo.
- **FR-031**: MUST existir integración continua que compile y pruebe en Windows y en Linux.
- **FR-032**: MUST verificarse automáticamente que se cumplen las reglas de dependencia entre
  capas de la constitución, incluido que ninguna vista ni lógica de presentación accede
  directamente a la base de datos.
- **FR-033**: MUST conservarse una base de ejemplo de esta versión, con productos de muestra, para
  las pruebas de actualización de versiones futuras.

### Key Entities *(include if feature involves data)*

- **Producto**: artículo del catálogo que se venderá. Atributos: identificador, nombre, SKU
  (código interno, único entre no borrados), código de barras opcional (único entre no borrados),
  precio de venta en pesos mexicanos, estado activo/inactivo, fecha y usuario de creación, fecha y
  usuario de última modificación, fecha de borrado y versión de concurrencia.
- **Usuario de auditoría**: identidad que se registra como autor de cada cambio. En esta
  funcionalidad es un usuario de sistema fijo; en el futuro será el operador autenticado.
- **Respaldo de base de datos**: copia consistente de la base. Tiene fecha y origen: automático
  (al arrancar o cerrar, conservando 7), previo a un cambio de esquema (conservando 5) o de
  diagnóstico (incluido en el paquete exportado).
- **Paquete de diagnóstico**: archivo comprimido con los logs recientes, un respaldo de la base y
  la versión de la aplicación.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: La compilación y el 100 % de las pruebas pasan desde la raíz sin errores ni
  advertencias, en Windows y en Linux.
- **SC-002**: La verificación automática de reglas de dependencia pasa, incluida la regla de que
  ninguna vista ni lógica de presentación accede a la base de datos.
- **SC-003**: Con la red desconectada, el operador completa el registro, la búsqueda, la edición y
  el borrado de productos en Windows y en Linux.
- **SC-004**: Cada regla de negocio de Producto (FR-010 a FR-013, FR-020 a FR-023) tiene pruebas
  automáticas que incluyen sus casos límite.
- **SC-005**: Una prueba automática demuestra que se rechaza una edición con versión
  desactualizada.
- **SC-006**: Pruebas automáticas cubren los cinco escenarios de arranque: base nueva, cambio de
  esquema pendiente con respaldo previo, base más nueva que la aplicación, falla con restauración
  del respaldo, y base dañada con restauración confirmada del respaldo más reciente. En la falla y
  en la restauración, el 100 % de los datos del respaldo se recupera.
- **SC-007**: Existe una base de ejemplo de esta versión con productos de muestra, lista para las
  pruebas de actualización futuras.
- **SC-008**: En el 100 % de los errores inesperados provocados en pruebas, el error queda
  registrado con su contexto, el operador ve un mensaje comprensible y la aplicación sigue
  abierta.
- **SC-009**: El archivo de diagnóstico exportado contiene los logs y un respaldo que se abre
  correctamente.
- **SC-010**: Un desarrollador que no conoce el proyecto compila, ejecuta y prueba la solución
  siguiendo solo la documentación, sin ayuda adicional.
- **SC-011**: Con un catálogo de 10,000 productos, los resultados de búsqueda aparecen en menos de
  1 segundo y un producto registrado aparece en el listado en menos de 1 segundo.
- **SC-012**: En el equipo de referencia (ver Assumptions), la aplicación queda lista para usarse en menos de 5
  segundos cuando no hay cambios de esquema pendientes.

## Assumptions

- Plataformas objetivo: Windows y Linux de escritorio. macOS está fuera de alcance.
- Un solo puesto por instalación; no hay acceso concurrente desde otros equipos. Las
  modificaciones concurrentes provienen de otras pantallas u operaciones de la misma aplicación.
- En esta funcionalidad, "inactivo" solo afecta la visibilidad en el listado y la búsqueda
  (FR-016); su efecto en ventas se definirá con Ventas.
- Un producto borrado no puede restaurarse desde la interfaz en esta funcionalidad.
- Los respaldos previos a cambios de esquema se conservan aparte de los automáticos (FR-007), los
  5 más recientes.
- "Logs recientes" en el diagnóstico son los de los últimos 7 días.
- Equipo de referencia para las metas de rendimiento (SC-011 y SC-012): CPU x64 de 4 núcleos a
  2 GHz o más, 8 GB de RAM y disco SSD, con Windows 10 o posterior o una distribución Linux de
  escritorio actual. En equipos más lentos, las metas son orientativas.
- Los mensajes al operador y la documentación están en español; la moneda es única: pesos
  mexicanos.
- Fuera de alcance: ventas, cobros, cortes de caja, inventario y reportes; autenticación,
  usuarios y roles; impresión de tickets, cajón de dinero, lector de códigos y cualquier otro
  hardware; categorías, impuestos, costos y múltiples precios por producto; facturación
  electrónica, API, versión web y sincronización.
- Dependencias: la constitución del proyecto v1.1.0, en particular los Principios I (estabilidad),
  II (capas), IV (integridad de datos y orden de migración), V (multiplataforma), VI (calidad) y
  VIII (diagnóstico).
