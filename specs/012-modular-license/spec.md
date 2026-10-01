# Feature Specification: Licencia modular por módulos

**Feature Branch**: `012-modular-license`

**Created**: 2026-09-30

**Status**: Draft

**Input**: User description: "Licencia modular con GUIDs: período de evaluación de 30 días con todos los módulos, después se activan por compra individual."

## Clarifications

### Session 2026-09-30

- Q: Si el archivo de licencia se elimina, ¿dónde se conserva la fecha de inicio original para que la evaluación no se reinicie? → A: En el archivo y en una copia protegida dentro de la base de datos local; si falta el archivo, se recupera la fecha de esa copia.
- Q: ¿Cómo debe detectar el sistema que el reloj se retrasó para que no otorgue días extra de evaluación? → A: Se guarda cifrada la fecha más reciente vista; los días restantes se calculan con el máximo entre el reloj actual y esa fecha.
- Q: ¿Qué pasa con las instalaciones en producción con licencia 011 al actualizar? → A: Las que tienen licencia 011 activa conservan todos los módulos habilitados; las que no la tienen inician la evaluación de 30 días.
- Q: Cuando un módulo bloqueado participa en una operación de otro módulo activo, ¿qué debe pasar? → A: La función básica se completa normalmente y solo se omite, sin error, la parte que pertenece al módulo bloqueado.
- Q: ¿Qué ocurre si el archivo de licencia está dañado o editado al arrancar con la evaluación vigente? → A: Se trata como archivo eliminado: se recupera la fecha de inicio de la copia protegida, se regenera sin módulos comprados, se muestra el mensaje y la evaluación sigue su curso.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Evaluación de 30 días con todos los módulos (Priority: P1)

En el primer arranque el sistema registra la fecha de inicio en el archivo de licencia. Durante los 30 días siguientes todos los módulos (Inventario, Reportes avanzados, Crédito y clientes, Turnos y arqueo, Devoluciones) están activos. Al terminar, la aplicación entra en "modo modular": solo quedan disponibles los módulos que tengan licencia.

**Why this priority**: Es el modelo comercial: probar todo primero y comprar solo lo que se necesita después.

**Independent Test**: Con una fecha de inicio conocida, verificar que dentro de los 30 días todos los módulos son accesibles y que al día siguiente del vencimiento solo lo son los licenciados.

**Acceptance Scenarios**:

1. **Given** un primer arranque o una fecha de inicio dentro de los últimos 30 días, **When** el usuario recorre el menú, **Then** todos los módulos están visibles y operan.
2. **Given** un período de evaluación vencido y ningún módulo licenciado, **When** el usuario abre la aplicación, **Then** el modo modular está activo y ningún módulo licenciable aparece.
3. **Given** un período de evaluación vencido con algunos módulos licenciados, **When** el usuario abre la aplicación, **Then** solo aparecen esos módulos.

---

### User Story 2 - Archivo de licencia protegido con módulos por identificador opaco (Priority: P1)

El archivo de licencia contiene el ID de máquina, la fecha de inicio, los días de evaluación (30) y la lista de identificadores (GUID) de los módulos habilitados. El archivo está cifrado y se valida en cada arranque. Los identificadores son opacos: el cliente no puede deducir ni editar a mano qué módulo corresponde a cada uno, porque cualquier edición rompe la protección y lo invalida. La aplicación lleva internamente el mapeo fijo de identificador a módulo (Inventario, Reportes avanzados, Crédito y clientes, Turnos y arqueo, Devoluciones), que no es visible ni configurable desde la interfaz.

**Why this priority**: Sin protección ni identificación opaca, cualquiera podría activar módulos sin pagar.

**Independent Test**: Editar el archivo con un editor y comprobar que se invalida; copiarlo a otra máquina y comprobar que se rechaza.

**Acceptance Scenarios**:

1. **Given** un archivo de licencia válido, **When** el sistema arranca, **Then** se valida y se habilitan los módulos cuyos identificadores contiene.
2. **Given** un archivo de licencia editado manualmente, **When** el sistema arranca, **Then** se considera inválido y se trata como archivo eliminado (FR-018): se regenera sin módulos comprados, la evaluación sigue su curso según la fecha de inicio recuperada y se muestra un mensaje claro.
3. **Given** un archivo de otra máquina, **When** el sistema arranca, **Then** no se puede validar (el ID de máquina distinto impide descifrarlo), no habilita ningún módulo y se trata como archivo eliminado (FR-018): se regenera sin módulos comprados y la evaluación sigue según la fecha de inicio recuperada.
4. **Given** un identificador desconocido dentro de una licencia válida, **When** se interpreta, **Then** se ignora y no habilita ningún módulo.

---

### User Story 3 - Activación de módulos pagados con licencia extendida (Priority: P1)

Un administrador abre "Acerca de > Administración de licencia" e importa un archivo de licencia extendida emitido por el proveedor, que indica qué módulos se activan. El sistema verifica que el ID de máquina coincida con el de la máquina actual y que el archivo sea auténtico; si no, lo rechaza. Importar una licencia extendida suma módulos y no altera la fecha de inicio ni reinicia el período de evaluación.

**Why this priority**: Es el único camino para convertir una prueba en una compra por módulo.

**Independent Test**: Con la evaluación vencida, importar un archivo válido que active Inventario y comprobar que solo ese módulo se habilita; importar uno de otra máquina y comprobar el rechazo.

**Acceptance Scenarios**:

1. **Given** evaluación vencida y un archivo válido para esta máquina que activa Inventario, **When** un administrador lo importa, **Then** Inventario queda activo y los demás módulos siguen bloqueados.
2. **Given** un archivo con ID de máquina distinto, **When** se intenta importar, **Then** se rechaza indicando que corresponde a otra máquina y la licencia actual no cambia.
3. **Given** un archivo alterado o ilegible, **When** se intenta importar, **Then** se rechaza como no válido y la licencia actual no cambia.
4. **Given** un usuario sin rol de administrador, **When** busca la opción, **Then** no puede importar licencias.
5. **Given** módulos ya activos, **When** se importa una licencia extendida con otros módulos, **Then** se conservan los anteriores y se agregan los nuevos.

---

### User Story 4 - Bloqueo de módulos no licenciados (Priority: P1)

Tras vencer la evaluación, cada módulo cuyo identificador no esté en la licencia se oculta del menú y cualquier intento de usar sus funciones (incluido el acceso directo) se rechaza con el mensaje "Este módulo no está activo en tu licencia." Los datos del módulo bloqueado se conservan intactos y vuelven a estar disponibles al activarlo.

**Why this priority**: Es el mecanismo que da valor a la venta por módulo.

**Independent Test**: Con la evaluación vencida y un solo módulo licenciado, comprobar que los demás desaparecen del menú, que sus operaciones se rechazan y que sus datos siguen ahí al reactivarlos.

**Acceptance Scenarios**:

1. **Given** evaluación vencida y Devoluciones sin licencia, **When** el usuario ve el menú, **Then** la opción Devoluciones no aparece.
2. **Given** un módulo bloqueado, **When** se intenta ejecutar una de sus operaciones, **Then** se rechaza con "Este módulo no está activo en tu licencia." y no se modifica ningún dato.
3. **Given** un módulo bloqueado con datos previos, **When** se activa su licencia, **Then** todos sus datos anteriores están disponibles sin pérdida.
4. **Given** un módulo bloqueado, **When** otro módulo activo lee información que el módulo bloqueado generó, **Then** esa información permanece sin alterarse.

---

### User Story 5 - Avisos antes del vencimiento (Priority: P1)

Cuando quedan 5 días de evaluación el sistema avisa "Te quedan 5 días de acceso a todos los módulos." y cuando queda 1 día avisa "Mañana vence el período de evaluación."

**Why this priority**: Evita sorpresas y da tiempo para comprar.

**Independent Test**: Simular 5 y 1 día restantes y comprobar cada aviso.

**Acceptance Scenarios**:

1. **Given** 5 días restantes, **When** el usuario abre el sistema, **Then** ve el aviso de 5 días.
2. **Given** 1 día restante, **When** el usuario abre el sistema, **Then** ve el aviso de vencimiento mañana.
3. **Given** más de 5 días restantes o evaluación ya vencida, **When** se usa el sistema, **Then** no aparece ningún aviso de cuenta atrás.

---

### User Story 6 - Activación inmediata sin reiniciar (Priority: P2)

Al importar una licencia extendida válida, los módulos se activan y aparecen en el menú de inmediato, sin cerrar ni reiniciar la aplicación.

**Why this priority**: Mejora la experiencia de compra; el control funciona igual sin ella.

**Independent Test**: Importar una licencia con la aplicación abierta y comprobar que el menú se actualiza sin reiniciar.

**Acceptance Scenarios**:

1. **Given** la aplicación abierta con un módulo bloqueado, **When** se importa una licencia válida que lo activa, **Then** aparece en el menú y puede usarse sin reiniciar.

---

### Edge Cases

- Archivo de licencia eliminado: se regenera con todos los módulos bloqueados, recuperando la fecha de inicio de la copia protegida en la base de datos local; el período no vuelve a empezar. Si faltan el archivo y la copia a la vez (base de datos nueva), se trata como primer arranque.
- Módulo bloqueado con operación en curso en el momento del vencimiento: la operación no puede iniciar nuevas acciones del módulo; los datos ya guardados se conservan.
- Reloj del sistema retrasado: no otorga días adicionales de evaluación.
- Licencia extendida repetida o que incluye módulos ya activos: se acepta sin duplicar.
- Licencia extendida con identificadores desconocidos junto con válidos: se activan los válidos y los desconocidos se ignoran.
- Fecha de vencimiento exacta: el día 30 de la evaluación es el último con todos los módulos; el siguiente ya es modo modular.
- Funciones del sistema que no pertenecen a ningún módulo licenciable (venta básica, productos, ajustes, usuarios): permanecen disponibles siempre.
- Dependencias entre módulos (por ejemplo una devolución que toca inventario): el módulo bloqueado no ejecuta sus operaciones; la función básica se completa normalmente y solo se omite, sin error, la parte que pertenece al módulo bloqueado.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST registrar la fecha de inicio en el archivo de licencia en el primer arranque y mantenerla sin cambios en adelante.
- **FR-002**: Durante los 30 días desde la fecha de inicio, el sistema MUST mantener activos todos los módulos licenciables.
- **FR-003**: Tras 30 días, el sistema MUST operar en modo modular, habilitando solo los módulos cuyo identificador esté en la licencia.
- **FR-004**: El archivo de licencia MUST contener el ID de máquina, la fecha de inicio, los días de evaluación (30) y la lista de identificadores de módulos habilitados.
- **FR-005**: El archivo de licencia MUST estar cifrado y MUST validarse íntegramente en cada arranque; cualquier edición manual MUST invalidarlo.
- **FR-006**: El sistema MUST rechazar una licencia cuyo ID de máquina no coincida con el de la máquina actual.
- **FR-007**: El sistema MUST mantener un mapeo interno fijo entre identificadores y los módulos Inventario, Reportes avanzados, Crédito y clientes, Turnos y arqueo y Devoluciones; este mapeo MUST NOT ser visible ni modificable desde la interfaz ni por configuración.
- **FR-008**: Un identificador no presente en el mapeo MUST ignorarse sin habilitar ningún módulo.
- **FR-009**: Un administrador MUST poder importar un archivo de licencia extendida desde "Acerca de > Administración de licencia".
- **FR-010**: Al importar, el sistema MUST verificar la autenticidad del archivo y que el ID de máquina coincida; si falla, MUST rechazarlo con un mensaje claro y conservar la licencia actual.
- **FR-011**: La licencia extendida MUST sumar los módulos que indica a los ya habilitados sin modificar la fecha de inicio ni reiniciar la evaluación.
- **FR-012**: Tras el período de evaluación, el sistema MUST ocultar del menú toda opción de un módulo no licenciado.
- **FR-013**: Tras el período de evaluación, toda operación de un módulo no licenciado MUST rechazarse con el mensaje "Este módulo no está activo en tu licencia." sin modificar datos.
- **FR-014**: El sistema MUST conservar íntegramente los datos de un módulo bloqueado y volver a ofrecerlos al activarlo.
- **FR-015**: El sistema MUST mostrar el aviso "Te quedan 5 días de acceso a todos los módulos." cuando queden 5 días de evaluación.
- **FR-016**: El sistema MUST mostrar el aviso "Mañana vence el período de evaluación." cuando quede 1 día.
- **FR-017**: Al importar una licencia extendida válida, el sistema MUST activar los módulos y actualizar el menú de inmediato, sin reiniciar.
- **FR-018**: El sistema MUST guardar una copia protegida de la fecha de inicio en la base de datos local. Si el archivo de licencia se elimina, MUST regenerarlo con todos los módulos bloqueados, recuperando la fecha de inicio de esa copia, y MUST NOT reiniciar el período de evaluación.
- **FR-019**: El sistema MUST guardar cifrada la fecha más reciente vista y calcular los días restantes con el máximo entre el reloj actual y esa fecha, de modo que NO aumenten si el reloj del sistema se retrasa.
- **FR-020**: El control de licencia MUST operar totalmente sin conexión.
- **FR-021**: Las funciones que no pertenecen a un módulo licenciable MUST permanecer disponibles en todo momento.
- **FR-022**: Al actualizar una instalación existente, el sistema MUST habilitar todos los módulos licenciables si tiene una licencia 011 activa; si no la tiene, MUST iniciar la evaluación de 30 días.

### Key Entities

- **Licencia**: ID de máquina, fecha de inicio, días de evaluación, lista de identificadores de módulos habilitados y marca de integridad.
- **Módulo licenciable**: Inventario, Reportes avanzados, Crédito y clientes, Turnos y arqueo o Devoluciones; cada uno con un identificador opaco fijo.
- **Mapeo de identificadores**: relación interna e inmutable entre identificador y módulo.
- **Licencia extendida**: archivo emitido por el proveedor con el ID de máquina destino y los identificadores de módulos a activar.
- **Estado de evaluación**: días restantes, o vencida.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los módulos está disponible durante los 30 días de evaluación y el 0% de los no licenciados lo está a partir del día 31.
- **SC-002**: El 100% de las licencias editadas manualmente o de otra máquina se rechaza.
- **SC-003**: El 100% de los intentos de operar un módulo bloqueado se rechaza con el mensaje indicado, sin cambios en datos.
- **SC-004**: Un administrador activa un módulo con una licencia válida en menos de 1 minuto y sin reiniciar.
- **SC-005**: El 100% de los datos de un módulo bloqueado está disponible tras reactivarlo.
- **SC-006**: Los avisos aparecen exactamente con 5 días y con 1 día restantes en todos los casos de prueba.
- **SC-007**: Eliminar el archivo de licencia nunca reinicia ni extiende la evaluación.

## Assumptions

- Esta funcionalidad extiende la licencia local de la especificación 011: reutiliza el ID de máquina, el cifrado y la importación en "Administración de licencia". Reemplaza el bloqueo global de modo lectura por el bloqueo por módulo; las funciones fuera de los cinco módulos permanecen disponibles (ver FR-021).
- El archivo de licencia extendida lo emite y firma el proveedor con una herramienta externa (fuera de alcance); la aplicación solo verifica su autenticidad sin conexión.
- Los identificadores de ejemplo de la descripción no son GUID hexadecimales estándar (contienen letras fuera de a–f); se tratan como cadenas opacas y los valores definitivos se fijan en el mapeo interno.
- Para recuperar el ID de máquina cuando el archivo se elimina, se aplica la misma regla de 011; la fecha de inicio original se recupera de la copia protegida en la base de datos local (FR-018).
- Los avisos de 5 y 1 día se muestran en la pantalla de inicio al abrir el sistema.
- El rol de administrador existente determina quién importa licencias.
- Fuera de alcance: generador de licencias, vigencia por módulo, módulos temporales o por suscripción.
