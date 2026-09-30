# Especificación de funcionalidad: Usuarios, inicio de sesión y roles

**Rama de funcionalidad**: `007-users-roles-auth`

**Creado**: 2026-09-30

**Estado**: Borrador

**Entrada**: Usuarios, inicio de sesión y roles: autenticación local, roles Cajero y Administrador, menú según rol, administración de usuarios y registro del usuario real en toda la auditoría.

## Objetivo

Que cada operación del POS quede ligada a la persona que la realizó y que cada usuario solo pueda hacer lo que su rol le permite. Sustituye al usuario de sistema usado hasta ahora.

## Clarifications

### Session 2026-09-30

- Q: ¿Qué ventas puede consultar un cajero: todas las del negocio o solo las que él mismo realizó? → A: Solo sus propias ventas (consulta y reimpresión); el administrador ve todas.
- Q: ¿La venta en curso que se conserva al cerrar sesión o cambiar de usuario debe seguir disponible si la aplicación se cierra o se reinicia? → A: Sí, se guarda en la base de datos y sobrevive al reinicio; máximo una venta conservada por usuario.
- Q: ¿Qué pasa con la venta conservada de un usuario si un administrador lo desactiva? → A: Se descarta automáticamente al desactivarlo y queda registro en la bitácora.
- Q: ¿La bitácora de auditoría debe tener una pantalla para consultarla dentro de esta funcionalidad? → A: Sí, pantalla de consulta solo para Administrador con filtro por fecha, usuario y tipo de evento.
- Q: ¿Los intentos fallidos al capturar credenciales de administrador en una autorización cuentan para el bloqueo de 5 intentos de ese administrador? → A: Sí, cuentan para el mismo contador y bloqueo (5 intentos / 5 minutos).
- Q: ¿Puede un Cajero exportar el diagnóstico (logs y respaldo de la base) desde Acerca de? → A: No, solo Administrador; el cajero ve Acerca de sin el botón de exportar.
- Q: ¿Qué se hace con una venta en curso guardada antes de esta funcionalidad al crear el primer administrador? → A: Se reasigna al primer administrador, que podrá recuperarla al iniciar sesión.
- Q: ¿Qué pasa si un administrador intenta restablecer su propia contraseña desde Usuarios? → A: Se rechaza; usa "Cambiar contraseña" (FR-025) y el botón se deshabilita sobre uno mismo.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Primer arranque: crear el administrador (Prioridad: P1)

En una instalación nueva, sin usuarios, después de la pantalla de carga se muestra un asistente para crear el primer usuario Administrador. Sin completarlo no se puede usar la aplicación.

**Por qué esta prioridad**: sin un administrador nadie puede configurar el sistema ni crear otros usuarios.

**Prueba independiente**: iniciar con una base vacía y verificar que solo se ofrece el asistente y que al terminarlo se puede entrar.

**Escenarios de aceptación**:

1. **Dado** que no existe ningún usuario, **cuando** termina la pantalla de carga, **entonces** se muestra el asistente de primer administrador y no hay forma de omitirlo ni de acceder al resto de la aplicación.
2. **Dado** el asistente, **cuando** se capturan nombre completo, usuario y una contraseña válida (mínimo 8 caracteres), **entonces** se crea el usuario con rol Administrador y se continúa a la aplicación.
3. **Dado** el asistente, **cuando** la contraseña tiene menos de 8 caracteres o no coincide con su confirmación, **entonces** se muestra el error y no se crea el usuario.

---

### Historia 2 - Iniciar sesión (Prioridad: P1)

Después de la pantalla de carga se muestra el inicio de sesión con usuario y contraseña; tras entrar se abre el Inicio.

**Por qué esta prioridad**: es la puerta de entrada y la base de la trazabilidad.

**Prueba independiente**: con un usuario existente, probar acceso correcto, incorrecto, bloqueo e inactivo.

**Escenarios de aceptación**:

1. **Dado** un usuario activo, **cuando** captura sus credenciales correctas, **entonces** se abre el Inicio.
2. **Dado** el usuario "Maria", **cuando** escribe "maria" o "MARIA", **entonces** el acceso funciona igual (el nombre de usuario no distingue mayúsculas/minúsculas).
3. **Dado** un usuario inexistente o una contraseña errónea, **cuando** intenta entrar, **entonces** se muestra "Usuario o contraseña incorrectos", sin indicar cuál dato falló.
4. **Dado** un usuario con 5 intentos fallidos consecutivos, **cuando** intenta un sexto acceso dentro de 5 minutos, **entonces** se rechaza aun con la contraseña correcta y se informa que está bloqueado temporalmente; pasados 5 minutos puede intentar de nuevo.
5. **Dado** un usuario inactivo, **cuando** intenta entrar, **entonces** se rechaza con el mismo mensaje genérico.
6. **Dado** un usuario con contraseña restablecida por un administrador, **cuando** inicia sesión, **entonces** debe cambiar la contraseña antes de continuar.

---

### Historia 3 - Menú según rol y usuario visible (Prioridad: P1)

El menú muestra solo las opciones permitidas al rol; los grupos sin opciones permitidas se ocultan. Muestra nombre y rol del usuario conectado (iniciales con tooltip cuando está contraído) y permite cerrar sesión, cambiar de usuario y cambiar la contraseña propia.

**Por qué esta prioridad**: hace visible y usable el control de acceso.

**Prueba independiente**: entrar con un cajero y con un administrador y comparar los menús.

**Escenarios de aceptación**:

1. **Dado** un cajero conectado, **cuando** ve el menú, **entonces** solo aparecen las opciones permitidas a su rol y no aparecen grupos vacíos.
2. **Dado** un administrador conectado, **cuando** ve el menú, **entonces** aparecen todas las opciones.
3. **Dado** el menú contraído, **cuando** se pasa el cursor sobre el usuario, **entonces** se ven sus iniciales y un tooltip con nombre y rol.
4. **Dado** cualquier usuario, **cuando** usa las acciones de la sección de usuario, **entonces** puede cerrar sesión, cambiar de usuario o cambiar su propia contraseña (pidiendo la contraseña actual).

---

### Historia 4 - Permisos por rol (Prioridad: P1)

Administrador: acceso total. Cajero: puede Inicio, Punto de venta, cobrar, consultar sus propias ventas realizadas, reimprimir los tickets de sus propias ventas, consultar productos y existencias (solo lectura) y cambiar su propia contraseña. No puede: crear, editar ni borrar productos; registrar movimientos de inventario; cancelar ventas; abrir el cajón sin venta; administrar usuarios; consultar la bitácora de auditoría; modificar la configuración (negocio, impresora); exportar el diagnóstico (logs y respaldo de la base).

**Por qué esta prioridad**: sin aplicar restricciones reales el rol no aporta seguridad.

**Prueba independiente**: invocar cada operación restringida con un cajero y verificar el rechazo, sin pasar por la interfaz.

**Escenarios de aceptación**:

1. **Dado** un cajero, **cuando** se invoca directamente cualquier operación restringida (aunque no sea desde la interfaz), **entonces** se rechaza sin efectos.
2. **Dado** un cajero, **cuando** realiza una operación permitida, **entonces** se ejecuta normalmente.
3. **Dado** que los permisos se definen en un solo lugar, **cuando** se agrega un rol o permiso futuro, **entonces** no se requiere modificar cada pantalla.
4. **Dado** un cajero, **cuando** consulta el listado o el detalle de ventas o reimprime un ticket, **entonces** solo tiene acceso a las ventas que él realizó; consultar o reimprimir una venta de otro usuario se rechaza aunque se invoque directamente. El administrador ve todas las ventas.

---

### Historia 5 - Administración de usuarios (Prioridad: P1)

Solo para Administrador: listado (nombre completo, usuario, rol, estado) con búsqueda, filtro de inactivos (misma regla que en productos) y paginación de 100 registros; alta y edición con formulario corto; restablecer contraseña de otro usuario; desactivación en lugar de borrado.

**Por qué esta prioridad**: sin ella no se pueden dar de alta cajeros.

**Prueba independiente**: crear, editar, desactivar y restablecer la contraseña de un usuario; intentar desactivar al último administrador.

**Escenarios de aceptación**:

1. **Dado** un administrador, **cuando** crea un usuario con nombre completo, usuario único, rol, estado y contraseña inicial válida, **entonces** el usuario aparece en el listado.
2. **Dado** un nombre de usuario ya existente (sin distinguir mayúsculas), **cuando** se intenta crear otro igual, **entonces** se rechaza.
3. **Dado** un administrador, **cuando** restablece la contraseña de otro usuario, **entonces** ese usuario debe cambiarla en su siguiente inicio de sesión.
4. **Dado** que los usuarios no se borran físicamente, **cuando** se desactiva uno, **entonces** conserva su historial y deja de poder iniciar sesión.
5. **Dado** el último administrador activo, o un administrador editándose a sí mismo, **cuando** se intenta desactivarlo o quitarle el rol de Administrador, **entonces** se rechaza.
6. **Dado** un cajero, **cuando** intenta acceder a la administración de usuarios, **entonces** no está disponible.

---

### Historia 6 - Usuario real en toda la auditoría (Prioridad: P1)

Todas las operaciones de alta, edición, borrado lógico, ventas, cancelaciones y movimientos de inventario registran el usuario conectado en los campos de auditoría existentes. Las ventas muestran al cajero en detalle, listado (con filtro por cajero) y ticket. Los registros previos quedan asociados al usuario "Sistema".

**Por qué esta prioridad**: es el objetivo central: trazabilidad por persona.

**Prueba independiente**: realizar operaciones con dos usuarios y verificar que cada registro queda con el suyo; migrar una base con datos previos.

**Escenarios de aceptación**:

1. **Dado** un usuario conectado, **cuando** crea, edita o elimina lógicamente un registro, vende, cancela o registra un movimiento de inventario, **entonces** queda registrado el identificador de ese usuario.
2. **Dado** el listado de ventas visto por un administrador, **cuando** se filtra por cajero, **entonces** solo aparecen las ventas de ese cajero.
3. **Dado** una venta, **cuando** se ve su detalle o se imprime su ticket, **entonces** se muestra el cajero que la realizó.
4. **Dado** datos creados antes de esta funcionalidad, **cuando** se actualiza la base, **entonces** se asocian al usuario "Sistema" sin pérdida de información; ese usuario no puede iniciar sesión ni aparece en la administración de usuarios.
5. **Dado** el punto único actual de obtención del usuario conectado, **cuando** se adopta el usuario real, **entonces** los casos de uso existentes no cambian salvo por las validaciones de permisos.

---

### Historia 7 - Autorización de administrador (Prioridad: P2)

Desde el punto de venta, ante una operación restringida (cancelar una venta, abrir el cajón sin venta), el cajero puede pedir autorización: un administrador captura su usuario y contraseña sin cerrar la sesión del cajero.

**Por qué esta prioridad**: evita que el cajero deba ceder su puesto, pero el sistema funciona sin ella.

**Prueba independiente**: con sesión de cajero, cancelar una venta con credenciales de administrador y revisar el registro.

**Escenarios de aceptación**:

1. **Dado** un cajero en el punto de venta, **cuando** intenta una operación restringida, **entonces** se le ofrece solicitar autorización de un administrador.
2. **Dado** credenciales válidas de un administrador activo, **cuando** las captura, **entonces** la operación se ejecuta y queda registrada con ambos usuarios (solicitante y autorizador), sin cambiar la sesión del cajero.
3. **Dado** credenciales inválidas o de un usuario que no es administrador, **cuando** se capturan, **entonces** la operación se rechaza y el intento queda en la bitácora.
4. **Dado** un administrador, **cuando** se capturan 5 veces seguidas contraseñas erróneas suyas en autorizaciones (o combinadas con inicios de sesión fallidos), **entonces** ese administrador queda bloqueado 5 minutos tanto para autorizar como para iniciar sesión.

---

### Historia 8 - Cambio de usuario y venta en curso (Prioridad: P2)

Al cerrar sesión o cambiar de usuario con una venta en curso se pide confirmación; la venta se conserva ligada a su usuario y se ofrece al volver a entrar con él.

**Por qué esta prioridad**: evita pérdida de ventas en cambios de turno.

**Prueba independiente**: armar una venta, cambiar de usuario y volver.

**Escenarios de aceptación**:

1. **Dado** una venta en curso, **cuando** se cierra sesión o se cambia de usuario, **entonces** se pide confirmación y, si se cancela la confirmación, nada cambia.
2. **Dado** una venta conservada, **cuando** ese mismo usuario vuelve a iniciar sesión, **entonces** se le ofrece recuperarla; otro usuario no la ve.
3. **Dado** una venta conservada, **cuando** la aplicación se cierra o se reinicia, **entonces** la venta sigue disponible para su usuario al volver a entrar.
4. **Dado** que cada usuario tiene como máximo una venta conservada, **cuando** la recupera, **entonces** vuelve a ser su venta en curso y deja de estar conservada.

---

### Historia 9 - Bloqueo por inactividad (Prioridad: P3)

Tras un tiempo configurable sin actividad (15 minutos por defecto, desactivable) la sesión se bloquea y pide la contraseña del mismo usuario, conservando la pantalla y la venta en curso.

**Por qué esta prioridad**: mejora la seguridad pero no es imprescindible.

**Prueba independiente**: configurar un tiempo corto, esperar y desbloquear.

**Escenarios de aceptación**:

1. **Dado** el tiempo configurado sin actividad, **cuando** se cumple, **entonces** la sesión se bloquea y oculta el contenido.
2. **Dado** una sesión bloqueada, **cuando** se captura la contraseña correcta del mismo usuario, **entonces** se regresa a la misma pantalla con la venta en curso intacta.
3. **Dado** el bloqueo desactivado, **cuando** pasa el tiempo, **entonces** la sesión nunca se bloquea.
4. **Dado** una sesión bloqueada, **cuando** se captura 5 veces seguidas una contraseña errónea, **entonces** el usuario queda bloqueado 5 minutos (mismo contador que el inicio de sesión).

---

### Casos límite

- Dos personas intentan crear el mismo nombre de usuario; o el asistente inicial se ejecuta dos veces: solo uno prevalece.
- Un usuario es desactivado mientras tiene la sesión abierta: no puede iniciar nuevas operaciones restringidas ni volver a entrar.
- Bloqueo por intentos fallidos: el contador se reinicia tras un acceso correcto; los intentos sobre usuarios inexistentes no revelan su inexistencia.
- El bloqueo de 5 minutos debe sobrevivir al cierre y reapertura de la aplicación.
- Un administrador intenta restablecer su propia contraseña: se rechaza y debe usar "Cambiar contraseña" (FR-025). Si intenta cambiarse el rol o desactivarse: se aplica la protección de la Historia 5.
- Una autorización de administrador con el mismo usuario que solicita (un administrador ya conectado): no requiere autorización.
- Una venta conservada de un usuario que luego es desactivado: se descarta automáticamente al desactivarlo y el descarte queda en la bitácora.
- Cambio de contraseña obligatorio interrumpido: no se accede hasta completarlo.
- Base migrada con una venta en curso guardada antes de esta funcionalidad (asociada a "Sistema"): al crear el primer administrador se le reasigna, y la puede recuperar al iniciar sesión.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: Si no existe ningún usuario, el sistema DEBE mostrar, tras la pantalla de carga, un asistente para crear el primer Administrador y no permitir el uso de la aplicación hasta completarlo.
- **FR-002**: El sistema DEBE mostrar tras la pantalla de carga el inicio de sesión (usuario y contraseña) y abrir el Inicio al autenticarse.
- **FR-003**: El nombre de usuario DEBE ser único y no distinguir mayúsculas/minúsculas.
- **FR-004**: Ante credenciales incorrectas el sistema DEBE mostrar un mensaje genérico ("Usuario o contraseña incorrectos") sin indicar cuál dato falló.
- **FR-005**: Tras 5 intentos fallidos consecutivos el sistema DEBE bloquear al usuario durante 5 minutos, incluso entre reinicios de la aplicación; un acceso correcto reinicia el contador. Los intentos fallidos de autorización de administrador (FR-013) cuentan para el mismo contador y bloqueo, y un usuario bloqueado tampoco puede autorizar. Los intentos fallidos de contraseña en el desbloqueo por inactividad (FR-023) también cuentan para el mismo contador y se registran en la bitácora.
- **FR-006**: Los usuarios inactivos y el usuario "Sistema" NO DEBEN poder iniciar sesión.
- **FR-007**: La contraseña DEBE tener mínimo 8 caracteres y almacenarse solo como hash robusto, nunca en claro.
- **FR-008**: El menú DEBE mostrar solo las opciones permitidas al rol del usuario y ocultar los grupos sin opciones permitidas.
- **FR-009**: El menú DEBE mostrar nombre y rol del usuario conectado (iniciales y tooltip con nombre y rol si está contraído) y ofrecer cerrar sesión, cambiar de usuario y cambiar la contraseña propia.
- **FR-010**: El rol Administrador DEBE tener acceso total; el rol Cajero DEBE tener exactamente los permisos descritos en la Historia 4.
- **FR-011**: Los permisos DEBEN verificarse en los casos de uso, rechazando toda operación no permitida sin efectos, aunque se invoque sin pasar por la interfaz.
- **FR-012**: La asignación de permisos por rol DEBE definirse en un solo lugar, de modo que agregar un rol o permiso no requiera modificar cada pantalla.
- **FR-013**: Un cajero DEBE poder solicitar autorización de administrador para cancelar una venta o abrir el cajón sin venta, mediante usuario y contraseña del administrador, sin cerrar la sesión del cajero.
- **FR-014**: Las operaciones autorizadas DEBEN registrar al solicitante y al autorizador.
- **FR-015**: Solo un Administrador DEBE poder administrar usuarios: listado (nombre completo, usuario, rol, estado) con búsqueda, filtro de inactivos (misma regla que productos) y paginación de 100 registros; alta y edición de nombre completo, usuario, rol y estado; contraseña inicial al crear.
- **FR-016**: Un administrador DEBE poder restablecer la contraseña de otro usuario, quien DEBE cambiarla en su siguiente inicio de sesión antes de continuar.
- **FR-017**: Los usuarios NO DEBEN borrarse físicamente; solo desactivarse.
- **FR-018**: El sistema NO DEBE permitir desactivar ni quitar el rol de Administrador al último administrador activo, ni a uno mismo.
- **FR-019**: Todas las operaciones de alta, edición, borrado lógico, ventas, cancelaciones y movimientos de inventario DEBEN registrar el identificador del usuario conectado en los campos de auditoría existentes, obtenido del mismo punto único usado hasta ahora.
- **FR-020**: Las ventas DEBEN mostrar el cajero en el detalle, en el listado (con filtro por cajero) y en el ticket.
- **FR-026**: Un cajero DEBE poder consultar y reimprimir únicamente las ventas que él realizó; el sistema DEBE rechazar en el caso de uso el acceso a ventas de otros usuarios. El filtro por cajero del listado solo está disponible para el Administrador.
- **FR-021**: Los registros anteriores DEBEN asociarse a un usuario "Sistema" que existe en la base, no puede iniciar sesión y no aparece en la administración de usuarios, sin pérdida de datos al migrar.
- **FR-022**: Al cerrar sesión o cambiar de usuario con una venta en curso, el sistema DEBE pedir confirmación, conservar la venta ligada a su usuario y ofrecerla al volver a entrar con él. La venta conservada DEBE persistirse en la base de datos y sobrevivir al cierre o reinicio de la aplicación; cada usuario tiene como máximo una venta conservada. Al desactivar un usuario, su venta conservada DEBE descartarse automáticamente.
- **FR-023**: El sistema DEBE bloquear la sesión tras un tiempo de inactividad configurable (15 minutos por defecto, desactivable), pedir la contraseña del mismo usuario y conservar la pantalla y la venta en curso.
- **FR-024**: El sistema DEBE registrar en una bitácora de auditoría: inicio y cierre de sesión, intentos fallidos, bloqueos, altas y cambios de usuarios, restablecimientos de contraseña, autorizaciones de administrador y descartes de ventas conservadas por desactivación del usuario.
- **FR-025**: Cualquier usuario DEBE poder cambiar su propia contraseña, cumpliendo la regla de mínimo 8 caracteres.
- **FR-027**: Solo un Administrador DEBE poder consultar la bitácora de auditoría en una pantalla de solo lectura con filtros por rango de fechas, usuario involucrado y tipo de evento, ordenada del más reciente al más antiguo y con paginación de 100 registros (misma regla que productos). Los eventos de la bitácora NO DEBEN poder editarse ni borrarse.

### Entidades clave

- **Usuario**: persona que opera el sistema; nombre completo, nombre de usuario único, rol, estado (activo/inactivo), credencial protegida, indicador de cambio obligatorio de contraseña, estado de bloqueo temporal. Incluye el usuario especial "Sistema".
- **Rol**: Administrador o Cajero; determina un conjunto de permisos.
- **Permiso**: capacidad puntual (p. ej. cancelar venta, gestionar productos) asociada a roles en un único punto.
- **Sesión**: usuario conectado actual, con estado de bloqueo por inactividad.
- **Evento de bitácora de auditoría**: hecho de seguridad o administración con fecha, tipo de evento, usuario(s) involucrados y resultado; inmutable y consultable solo por el Administrador.
- **Autorización**: vincula una operación restringida con el usuario solicitante y el administrador autorizador.
- **Venta en curso conservada**: venta no cobrada ligada a su usuario, persistida en la base de datos; como máximo una por usuario.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: En una instalación nueva, el 100 % de los intentos de usar la aplicación sin crear el primer administrador son imposibles, y completar el asistente toma menos de 2 minutos.
- **SC-002**: Un cajero ve únicamente sus opciones de menú, y una prueba automática demuestra que el 100 % de las operaciones restringidas rechazan su ejecución al invocarse directamente.
- **SC-003**: El 100 % de las operaciones nuevas registran al usuario conectado, y las ventas muestran al cajero en detalle, listado y ticket.
- **SC-004**: Al migrar una base existente, el 100 % de los registros previos conservan su información y quedan asociados al usuario "Sistema".
- **SC-005**: Tras 5 intentos fallidos consecutivos el acceso queda bloqueado 5 minutos en el 100 % de los casos, y el último administrador activo nunca puede quedar desactivado ni sin rol.
- **SC-006**: Una cancelación autorizada por un administrador desde la sesión de un cajero registra ambos usuarios en el 100 % de los casos.
- **SC-007**: Un cajero puede iniciar sesión y llegar al punto de venta en menos de 15 segundos.
- **SC-008**: Ninguna contraseña es recuperable en claro desde los datos almacenados.

## Supuestos

- Solo existen los roles Cajero y Administrador; roles personalizados y permisos editables quedan fuera de alcance.
- Quedan fuera del alcance el acceso con PIN, huella o tarjeta y la recuperación de contraseña por correo.
- La autenticación es local y sin conexión; no hay cuentas en servidores externos.
- La contraseña inicial de un usuario nuevo creada por el administrador se trata como temporal: se exige cambiarla en el primer inicio de sesión, igual que tras un restablecimiento.
- Un administrador que ya está conectado no necesita solicitar autorización para operaciones restringidas.
- El tiempo de inactividad se configura desde la configuración general (solo Administrador).
- Se reutiliza el punto único actual que entrega el usuario conectado a los casos de uso y los campos de auditoría existentes (Principio IV de la constitución); el almacenamiento de contraseñas sigue el Principio IX.
- El filtro de inactivos y la paginación de 100 registros siguen la misma regla ya usada en productos.
