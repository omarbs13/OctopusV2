# Especificación de funcionalidad: Registro global de excepciones

**Rama de funcionalidad**: `010-global-exception-logging`

**Creado**: 2026-09-30

**Estado**: Borrador

**Entrada**: Logging global de excepciones: captura y registro estructurado de errores no controlados sin cambiar los casos de uso existentes.

## Objetivo

Que toda excepción no controlada quede registrada con contexto suficiente para diagnosticar problemas en campo sin acceso remoto, y que el operador nunca vea un cierre abrupto ni detalles técnicos. El comportamiento de negocio de los casos de uso existentes no cambia.

## Clarificaciones

### Sesión 2026-09-30

- Q: Después de un error no controlado, ¿a dónde debe llevar el sistema al operador al cerrar el mensaje? → A: Permanece en la pantalla actual con la venta en curso intacta; solo si la pantalla no puede continuar, regresa a la pantalla principal de venta (pantalla segura).
- Q: ¿Qué debe incluir el paquete de "Exportar diagnóstico" además de los registros? → A: Un resumen de versión y entorno (versión de la app, sistema operativo); el respaldo de la base se ofrece como opción aparte que el operador elige.
- Q: Cuando un mismo error se repite muchas veces seguidas, ¿cómo debe comportarse el sistema? → A: Se registra el primer error completo; los idénticos en una ventana corta se agrupan en una sola entrada con el conteo de repeticiones; se muestra un único mensaje al operador por episodio.

## Conceptos

- **Excepción no controlada**: error inesperado que ningún caso de uso manejó (incluye errores en tareas asincrónicas en segundo plano).
- **Excepción controlada**: error previsto y manejado por el negocio (validación fallida, base de datos inaccesible).
- **Contexto de diagnóstico**: usuario conectado, pantalla u operación en curso, venta en curso y valores relevantes (identificadores).
- **Pantalla segura**: pantalla principal de venta, a la que el operador regresa solo cuando la pantalla actual no puede continuar.
- **Diagnóstico exportable**: paquete con los registros disponibles y un resumen de versión y entorno que el operador entrega a soporte técnico; puede incluir, a elección del operador, un respaldo de la base.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia de usuario 1 - Captura global de excepciones (Prioridad: P1)

Cuando ocurre un error inesperado en cualquier parte de la aplicación, el sistema lo registra, informa al operador con un mensaje comprensible y sigue funcionando.

**Por qué esta prioridad**: protege la venta en curso y evita que un error aislado detenga la operación del negocio.

**Prueba independiente**: provocar una excepción en un caso de uso y otra en una tarea asincrónica; verificar que la aplicación sigue abierta, que se muestra el mensaje en español y que existe un registro de cada una.

**Escenarios de aceptación**:

1. **Dado** que un caso de uso lanza una excepción no controlada, **cuando** ocurre, **entonces** se registra con mensaje, traza de pila, tipo, fecha y hora, usuario y operación en curso, y el operador ve "Ocurrió un error inesperado. Los detalles se registraron para soporte técnico." sin detalles técnicos.
2. **Dado** que una tarea asincrónica en segundo plano falla, **cuando** la excepción no es observada por nadie, **entonces** se registra y la aplicación no se cierra.
3. **Dado** que ocurrió un error y se mostró el mensaje, **cuando** el operador lo cierra, **entonces** permanece en la misma pantalla con la venta en curso intacta; solo si esa pantalla no puede continuar, regresa a la pantalla segura (pantalla principal de venta).
4. **Dado** que no hay usuario conectado (por ejemplo, en la pantalla de acceso), **cuando** ocurre un error, **entonces** se registra indicando que no había usuario.

---

### Historia de usuario 2 - Almacenamiento en archivo y exportación (Prioridad: P1)

Los registros se guardan en archivos diarios dentro de la carpeta de datos de la aplicación, con limpieza automática, y se pueden exportar para soporte.

**Por qué esta prioridad**: sin registros persistentes y accesibles el diagnóstico en campo no es posible.

**Prueba independiente**: generar registros en distintos días, verificar un archivo por día, que los de más de 30 días se eliminan y que "Acerca de > Exportar diagnóstico" entrega los registros.

**Escenarios de aceptación**:

1. **Dado** que se registran eventos en un mismo día, **cuando** se revisa la carpeta de registros, **entonces** hay un único archivo para ese día con entradas legibles que incluyen fecha y hora, nivel, mensaje y contexto.
2. **Dado** que existen archivos de más de 30 días, **cuando** la aplicación inicia o cambia el día, **entonces** esos archivos se eliminan automáticamente.
3. **Dado** que el operador abre "Acerca de" y elige "Exportar diagnóstico", **cuando** elige un destino, **entonces** se genera un paquete con los registros disponibles y un resumen de versión y entorno, se ofrece incluir opcionalmente un respaldo de la base y se confirma el resultado.
4. **Dado** que la carpeta de registros no se puede escribir, **cuando** ocurre un evento, **entonces** la aplicación no falla por ello y continúa operando.

---

### Historia de usuario 3 - Contexto útil en cada registro (Prioridad: P2)

Cada entrada incluye lo necesario para reproducir el problema: operación, usuario, venta en curso e identificadores relevantes, sin datos sensibles.

**Por qué esta prioridad**: aumenta mucho el valor del registro, pero la captura básica ya aporta valor sin ella.

**Prueba independiente**: provocar un error con una venta sin guardar y comprobar que el registro incluye pantalla/operación, usuario, folio o cantidad de líneas e identificadores, y que no aparece ninguna contraseña ni dato de tarjeta.

**Escenarios de aceptación**:

1. **Dado** que hay una venta sin guardar, **cuando** ocurre un error, **entonces** el registro incluye su folio o la cantidad de líneas.
2. **Dado** que el error ocurre durante una operación con producto, venta o turno, **cuando** se registra, **entonces** se incluyen sus identificadores.
3. **Dado** que el contexto contiene contraseñas o datos de tarjeta, **cuando** se registra, **entonces** esos valores nunca se escriben.

---

### Historia de usuario 4 - Niveles de registro (Prioridad: P2)

Los eventos se clasifican por nivel para filtrar con rapidez lo importante.

**Por qué esta prioridad**: mejora la lectura del registro; no bloquea el diagnóstico básico.

**Prueba independiente**: ejecutar una venta, abrir un turno, desconectar la impresora y provocar errores controlados y no controlados; verificar el nivel de cada entrada.

**Escenarios de aceptación**:

1. **Dado** una excepción no controlada, **cuando** se registra, **entonces** su nivel es FATAL.
2. **Dado** una excepción controlada (validación fallida, base de datos inaccesible), **cuando** se registra, **entonces** su nivel es ERROR.
3. **Dado** una operación crítica de negocio (venta registrada, turno abierto, usuario conectado), **cuando** ocurre, **entonces** se registra con nivel INFO.
4. **Dado** una situación esperada pero anómala (impresora desconectada, existencia negativa), **cuando** ocurre, **entonces** se registra con nivel WARNING.

---

### Casos límite

- Falla el propio registro (disco lleno, permisos): la aplicación continúa y no entra en un ciclo de errores.
- Una misma falla se repite muchas veces en poco tiempo: se registra completa la primera vez y las repeticiones idénticas en una ventana corta se agrupan en una sola entrada con su conteo; el registro no crece sin control ni bloquea la interfaz.
- El error ocurre durante el arranque, antes de que exista usuario o pantalla: se registra con contexto vacío.
- El error ocurre a mitad de una venta: la venta en curso no se pierde.
- Varias excepciones simultáneas: se muestra un solo mensaje al operador por episodio, y todas se registran.
- Exportar diagnóstico sin registros disponibles: el paquete se genera igualmente con el resumen de versión y entorno, y se informa claramente al operador que no había registros.
- Los mensajes de excepción o los valores de contexto pueden contener datos sensibles: se filtran antes de escribirse.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema DEBE capturar toda excepción no controlada de la aplicación, incluidas las de tareas asincrónicas no observadas, sin cerrarse.
- **FR-002**: Cada excepción no controlada DEBE registrarse con mensaje, traza de pila, tipo, fecha y hora, usuario conectado (si lo hay) y pantalla u operación en curso.
- **FR-003**: Tras registrar, el sistema DEBE mostrar al operador el mensaje "Ocurrió un error inesperado. Los detalles se registraron para soporte técnico.", sin detalles técnicos.
- **FR-004**: Tras un error, el sistema DEBE mantener al operador en la pantalla actual con la venta en curso intacta y llevarlo a la pantalla segura (pantalla principal de venta) solo si la pantalla actual no puede continuar.
- **FR-005**: El sistema DEBE guardar los registros en archivos de texto, uno por día, en la carpeta de datos de la aplicación.
- **FR-006**: El sistema DEBE conservar como máximo 30 días de registros y eliminar automáticamente los anteriores.
- **FR-007**: Cada entrada DEBE ser legible y contener fecha y hora, nivel, mensaje y contexto.
- **FR-008**: "Acerca de" DEBE ofrecer "Exportar diagnóstico", que entrega al operador un paquete para soporte con los registros disponibles y un resumen de versión y entorno (versión de la aplicación, sistema operativo).
- **FR-009**: La exportación DEBE ofrecer, como opción separada que el operador elige, incluir un respaldo de la base; por omisión NO se incluye.
- **FR-010**: El registro DEBE incluir la pantalla o caso de uso en ejecución y el usuario conectado.
- **FR-011**: Si hay una venta sin guardar, el registro DEBE incluir su folio o cantidad de líneas.
- **FR-012**: El registro DEBE incluir identificadores relevantes (producto, venta, turno) y NUNCA contraseñas, datos de tarjeta ni otros datos sensibles.
- **FR-013**: El sistema DEBE usar los niveles INFO, WARNING, ERROR y FATAL según: INFO para operaciones críticas de negocio; WARNING para situaciones esperadas pero anómalas; ERROR para fallas controladas (excepciones controladas y resultados fallidos de un caso de uso, como una validación fallida o una base inaccesible), registrando solo el tipo de falla y los nombres de campo, nunca los valores ingresados; FATAL para excepciones no controladas.
- **FR-014**: Una falla del propio registro NO DEBE afectar la operación de la aplicación ni la venta en curso.
- **FR-015**: La funcionalidad NO DEBE cambiar el comportamiento observable de los casos de uso existentes.
- **FR-016**: Todos los mensajes mostrados al operador DEBEN estar en español.
- **FR-017**: Cuando un mismo error se repite en una ventana corta, el sistema DEBE registrar el primero completo, agrupar las repeticiones idénticas en una sola entrada con el conteo y mostrar un único mensaje al operador por episodio.

### Entidades clave

- **Entrada de registro**: evento con fecha y hora, nivel, mensaje, tipo y traza de pila (si es error) y contexto de diagnóstico.
- **Contexto de diagnóstico**: usuario, pantalla/operación, venta en curso (folio o cantidad de líneas) e identificadores relevantes.
- **Archivo diario de registro**: conjunto de entradas de un día; se elimina pasados 30 días.
- **Paquete de diagnóstico**: exportación para soporte técnico con registros, resumen de versión y entorno y, opcionalmente, respaldo de la base.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: El 100% de las excepciones no controladas provocadas en pruebas (en casos de uso y en tareas asincrónicas) se registran y la aplicación permanece abierta.
- **SC-002**: Soporte técnico puede identificar usuario, operación, venta en curso y causa de un error solo con el registro, sin acceso remoto, en el 100% de los casos de prueba.
- **SC-003**: Tras 31 días de registros simulados, solo quedan los últimos 30 días y hay un archivo por día.
- **SC-004**: El operador ve el mensaje en español, sin detalles técnicos, en el 100% de los errores no controlados.
- **SC-005**: Tras un error, el operador puede retomar su trabajo en menos de 10 segundos.
- **SC-006**: Ninguna contraseña ni dato de tarjeta aparece en los registros en las pruebas de revisión.
- **SC-007**: La exportación del diagnóstico se completa en menos de 30 segundos con 30 días de registros.

## Supuestos

- Los registros no se envían a ningún servidor ni generan alertas en tiempo real (fuera de alcance), ni se rotan por tamaño de archivo.
- Se reutiliza la información de sesión del usuario y de venta en curso ya existentes.
- La carpeta de datos de la aplicación ya existe y es la misma que usan la base de datos y los respaldos.
- Solo el Administrador puede exportar el diagnóstico (permiso ya existente en la aplicación); "operador" en las historias se refiere a quien tiene ese permiso en "Acerca de".
- La limpieza de archivos antiguos se hace al iniciar y cuando cambia el día.
- Los casos de uso existentes no se modifican; el contexto se obtiene de forma transversal.
- El formato de exportación es un único archivo comprimido.
