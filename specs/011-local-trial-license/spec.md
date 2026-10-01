# Feature Specification: Licencia local con período de evaluación

**Feature Branch**: `011-local-trial-license`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "Licencia local: período de evaluación de 30 días vinculado al ID de máquina, con bloqueo gradual."

## Clarifications

### Session 2026-09-30

- Q: ¿Qué otorga un archivo de licencia importado: activación permanente o extensión con vencimiento? → A: El archivo trae una fecha de fin o "sin vencimiento"; al llegar a esa fecha vuelve el modo lectura y los avisos de 5 y 1 día aplican igual.
- Q: ¿Quién puede generar un archivo de licencia válido? → A: Solo el proveedor: el archivo va firmado y la aplicación verifica la firma sin conexión.
- Q: ¿Cómo le comunica el cliente su ID de máquina al proveedor? → A: Exporta un archivo de solicitud con el ID que envía al proveedor.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Identificador único de máquina (Priority: P1)

En el primer arranque el sistema asigna a la máquina un identificador único y lo guarda de forma protegida en un archivo de licencia dentro de la carpeta de datos. Ese identificador vincula la instalación a la máquina: si la carpeta de datos se copia a otro equipo, el sistema lo detecta y rechaza la licencia. El archivo no es visible ni editable desde la interfaz.

**Why this priority**: Es la base de todo el control; sin un vínculo con la máquina el período de evaluación se puede evadir copiando los datos.

**Independent Test**: Arrancar en una máquina sin archivo de licencia y comprobar que se crea con un ID; copiar la carpeta de datos a otra máquina y comprobar que se rechaza.

**Acceptance Scenarios**:

1. **Given** una máquina sin archivo de licencia, **When** se inicia el sistema por primera vez, **Then** se genera un ID único de máquina y se guarda protegido en la carpeta de datos.
2. **Given** una carpeta de datos con licencia creada en otra máquina, **When** se inicia el sistema en la máquina actual, **Then** el ID no coincide y la licencia se rechaza, quedando el sistema en modo lectura con un mensaje claro.
3. **Given** un archivo de licencia existente, **When** el usuario recorre la interfaz o abre el archivo con un editor, **Then** la interfaz no lo muestra ni permite modificarlo y su contenido no es legible ni editable de forma válida.

---

### User Story 2 - Período de evaluación de 30 días (Priority: P1)

El archivo de licencia registra la fecha del primer arranque. En cada arranque el sistema calcula los días restantes (30 menos los días transcurridos). Mientras queden días, la aplicación funciona con normalidad. La pantalla de Inicio muestra una tarjeta "Período de evaluación" con los días restantes y los datos de contacto.

**Why this priority**: Define el modelo de evaluación y da visibilidad al usuario del tiempo disponible.

**Independent Test**: Con una fecha de inicio conocida, verificar que Inicio muestra los días restantes correctos y que todas las funciones están disponibles.

**Acceptance Scenarios**:

1. **Given** un primer arranque hoy, **When** se abre Inicio, **Then** la tarjeta muestra 30 días restantes y el contacto.
2. **Given** una fecha de inicio hace 12 días, **When** se abre Inicio, **Then** la tarjeta muestra 18 días restantes y todas las funciones operan normalmente.

---

### User Story 3 - Bloqueo gradual tras 30 días (Priority: P1)

Al vencer el período, el sistema pasa a modo lectura. Punto de venta, apertura de turnos, reportes y administración de usuarios quedan bloqueados con el mensaje "Período de evaluación vencido. Contacte a [teléfono/email]." Siguen permitidas la consulta de Productos, Inventario y Ventas registradas. Inicio muestra "Sistema en modo lectura. Contacte para activación."

**Why this priority**: Es el mecanismo que da valor comercial a la licencia; sin bloqueo no hay incentivo de activación.

**Independent Test**: Con una licencia vencida, intentar vender, abrir turno, abrir reportes y administrar usuarios (bloqueados) y consultar productos, inventario y ventas (permitido).

**Acceptance Scenarios**:

1. **Given** una licencia vencida, **When** el usuario intenta entrar al punto de venta, **Then** se bloquea y se muestra el mensaje de período vencido con el contacto.
2. **Given** una licencia vencida, **When** se intenta abrir un turno, abrir reportes o administrar usuarios, **Then** cada acción se bloquea con el mismo mensaje.
3. **Given** una licencia vencida, **When** se consultan Productos, Inventario o Ventas registradas, **Then** la información se muestra normalmente.
4. **Given** una licencia vencida, **When** se abre Inicio, **Then** se muestra "Sistema en modo lectura. Contacte para activación."
5. **Given** un turno abierto al momento del vencimiento, **When** el período vence, **Then** el turno abierto puede cerrarse para no dejar la caja sin corte.

---

### User Story 4 - Extensión de licencia (Priority: P2)

Un administrador abre "Acerca de > Administración de licencia" e importa un archivo de licencia emitido por el proveedor. El sistema verifica que sea válido y que su ID de máquina coincida con el de la máquina actual; si es así, la licencia se aplica de inmediato sin reiniciar y se levanta el bloqueo. Si no coincide o es inválido, se rechaza con un mensaje claro.

**Why this priority**: Permite activar o extender sin intervención técnica; importante pero el sistema ya cumple su control sin ella.

**Independent Test**: Importar un archivo válido con la licencia vencida y comprobar que las funciones se desbloquean sin reiniciar; importar uno de otra máquina y comprobar el rechazo.

**Acceptance Scenarios**:

1. **Given** una licencia vencida y un archivo válido para esta máquina, **When** un administrador lo importa, **Then** el sistema se desbloquea de inmediato sin reiniciar.
2. **Given** un archivo de licencia con un ID distinto, **When** se intenta importar, **Then** se rechaza indicando claramente que corresponde a otra máquina y la licencia actual no cambia.
3. **Given** un archivo alterado o ilegible, **When** se intenta importar, **Then** se rechaza indicando que no es válido y la licencia actual no cambia.
4. **Given** un usuario sin rol de administrador, **When** busca la opción, **Then** no puede importar licencias.

---

### User Story 5 - Avisos anticipados (Priority: P3)

Cuando quedan 5 días o menos, Inicio muestra una notificación de vencimiento próximo. Cuando queda 1 día o menos, la pantalla de inicio de sesión muestra un aviso en rojo.

**Why this priority**: Mejora la experiencia evitando sorpresas, pero no es necesaria para el control.

**Independent Test**: Simular 5 días y 1 día restantes y comprobar los avisos en Inicio y login.

**Acceptance Scenarios**:

1. **Given** 5 días o menos restantes, **When** se abre Inicio, **Then** aparece la notificación de vencimiento próximo.
2. **Given** 1 día o menos restante, **When** se muestra el login, **Then** aparece un aviso rojo de vencimiento.
3. **Given** más de 5 días restantes, **When** se usa el sistema, **Then** no aparece ningún aviso adicional.

---

### Edge Cases

- Archivo de licencia eliminado: se regenera conservando el ID si es recuperable desde el hardware; si no, se crea uno nuevo. Si el ID no es recuperable, se considera una instalación nueva (la pérdida de datos al reinstalar es el desincentivo aceptado).
- Archivo de licencia corrupto o manipulado: se trata como no válido y el sistema queda en modo lectura con mensaje claro.
- Reloj del sistema retrasado respecto a la última fecha registrada: no debe otorgar días adicionales; los días restantes nunca aumentan por retroceder el reloj.
- Cambio de hardware (por ejemplo, cambio de tarjeta de red): el ID puede dejar de coincidir; la activación por archivo de licencia es el camino de recuperación.
- Vencimiento ocurrido con la aplicación abierta (a medianoche): el bloqueo se aplica sin esperar a un nuevo arranque al intentar una acción bloqueada.
- Máquina sin red o con varios identificadores de hardware: el ID se obtiene de forma estable con un orden de preferencia definido.
- Cero días restantes: el día del vencimiento ya cuenta como vencido (0 días restantes = bloqueado).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST generar en el primer arranque un ID de máquina único derivado del hardware o del sistema operativo y guardarlo protegido en un archivo de licencia en la carpeta de datos.
- **FR-002**: El sistema MUST rechazar una licencia cuyo ID de máquina no coincida con el de la máquina actual.
- **FR-003**: El archivo de licencia MUST NOT ser visible ni editable desde la interfaz, y su contenido MUST estar protegido contra edición manual (una modificación lo invalida).
- **FR-004**: El sistema MUST registrar la fecha del primer arranque y, en cada arranque, calcular los días restantes como 30 menos los días transcurridos desde esa fecha.
- **FR-005**: Mientras queden días de evaluación (o exista licencia activa), el sistema MUST funcionar sin restricciones.
- **FR-006**: Inicio MUST mostrar una tarjeta "Período de evaluación" con los días restantes y los datos de contacto.
- **FR-007**: Al vencer el período, el sistema MUST bloquear el punto de venta, la apertura de turnos, los reportes y la administración de usuarios, mostrando "Período de evaluación vencido. Contacte a [teléfono/email]."
- **FR-008**: En modo lectura el sistema MUST permitir consultar Productos, Inventario y Ventas registradas.
- **FR-009**: En modo lectura Inicio MUST mostrar "Sistema en modo lectura. Contacte para activación."
- **FR-010**: El sistema MUST permitir cerrar un turno ya abierto aunque la licencia esté vencida.
- **FR-011**: Un administrador MUST poder importar un archivo de licencia desde "Acerca de > Administración de licencia"; la licencia se aplica sin reiniciar. El archivo indica una fecha de fin de vigencia o "sin vencimiento"; al llegar a la fecha de fin el sistema vuelve a modo lectura y los avisos anticipados (FR-013) aplican igual.
- **FR-011a**: Un administrador MUST poder exportar desde "Administración de licencia" un archivo de solicitud que contenga el ID de máquina, para enviarlo al proveedor y obtener la licencia.
- **FR-012**: Al importar, el sistema MUST verificar sin conexión la firma del proveedor del archivo (solo el proveedor puede emitir licencias válidas) y que su ID de máquina coincida; en caso contrario MUST rechazarlo con un mensaje claro y conservar la licencia actual.
- **FR-013**: Con 5 días o menos restantes, Inicio MUST mostrar una notificación; con 1 día o menos, el login MUST mostrar un aviso rojo.
- **FR-014**: La exportación de diagnóstico MUST excluir el archivo de licencia.
- **FR-015**: Si el archivo de licencia se elimina, el sistema MUST regenerarlo conservando el ID si es recuperable o creando uno nuevo.
- **FR-016**: Los días restantes MUST NOT aumentar si el reloj del sistema se retrasa.
- **FR-017**: El control MUST operar totalmente sin conexión; no hay validación, revocación ni descarga remota.

### Key Entities

- **Licencia local**: ID de máquina, fecha del primer arranque, vigencia vigente (período de evaluación o fecha de fin otorgada), última fecha vista y marca de integridad.
- **ID de máquina**: identificador estable que vincula la instalación al equipo.
- **Archivo de solicitud de licencia**: documento exportado que contiene el ID de máquina para enviarlo al proveedor.
- **Archivo de licencia importable**: documento emitido por el proveedor con el ID de máquina destino y la vigencia otorgada.
- **Estado de licencia**: en evaluación (con días restantes), activa o vencida (modo lectura), o inválida (ID distinto/corrupta).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En una máquina nueva, el 100% de los primeros arranques crea una licencia con ID único sin intervención del usuario.
- **SC-002**: El 100% de las licencias copiadas a otra máquina o alteradas manualmente se rechazan.
- **SC-003**: Con la licencia vencida, el 100% de los intentos de vender, abrir turno, abrir reportes o administrar usuarios se bloquea, y el 100% de las consultas de Productos, Inventario y Ventas registradas sigue disponible.
- **SC-004**: Un administrador activa una licencia válida en menos de 1 minuto y sin reiniciar.
- **SC-005**: Los días restantes mostrados en Inicio coinciden con el cálculo esperado en todos los casos de prueba (día 0, 1, 5, 29, 30 y posteriores).
- **SC-006**: Un archivo de licencia con ID distinto se rechaza con un mensaje que indica que pertenece a otra máquina.

## Assumptions

- Los datos de contacto (teléfono/email) provienen de una configuración del proveedor incluida en la aplicación; no los edita el usuario.
- Los archivos de licencia importables los emite y firma el proveedor fuera del sistema; la herramienta de emisión no forma parte de esta funcionalidad, pero la aplicación debe poder verificar esa firma sin conexión.
- Importar una licencia válida reemplaza la vigencia actual por la definida en el archivo (fecha de fin o sin vencimiento).
- La fecha local de la máquina es la fuente de tiempo; el control es deliberadamente local y no busca ser infalible, siendo la pérdida de datos al reinstalar el principal desincentivo.
- El rol de administrador existente (usuarios y roles) determina quién importa licencias.
- Fuera de alcance: validación remota, revocación remota y descarga de actualizaciones de licencia.
