# Especificación de funcionalidad: Auditoría detallada de cambios

**Rama de funcionalidad**: `018-audit-trail`

**Creado**: 2026-10-01

**Estado**: Borrador

**Entrada**: Auditoría detallada de cambios: registro de quién modificó qué y cuándo. Bitácora centralizada (solo Administrador) con productos, precios, costos, categorías, cancelaciones, devoluciones, descuentos, aperturas de cajón, usuarios y accesos; filtros por usuario, fecha, tipo de operación y entidad; exportación a PDF o Excel. Criterios: antes/después en todo cambio, usuario obligatorio, bitácora inmutable, búsquedas en menos de 1 segundo.

## Objetivo

Completar la bitácora de auditoría existente (spec 007) para que sirva de evidencia en revisiones de cumplimiento y en la detección de fraude. Cada cambio debe responder quién lo hizo, cuándo, sobre qué registro y qué valores tenía antes y después. Un Administrador debe poder localizar los eventos rápidamente y exportarlos.

## Clarifications

### Session 2026-10-01

- Q: El producto no tiene un campo de costo. ¿Se agrega en esta funcionalidad para poder auditarlo? → A: No; el costo queda fuera hasta que exista y entonces se auditará con la misma regla.
- Q: ¿La inmutabilidad debe cubrir alteraciones hechas directamente sobre el archivo de datos? → A: No; basta con impedir cambios y borrados desde la aplicación.

## Punto de partida

La bitácora ya existe y hoy registra accesos (inicio y cierre de sesión, intentos fallidos, bloqueos), cambios de usuarios, autorizaciones de Administrador, cancelaciones y devoluciones, autorizaciones de descuento, aperturas de cajón sin venta, turnos y cortes, clientes, cupones, categorías y licencia. Tiene una pantalla de consulta de solo lectura para el Administrador, con filtros por fechas, usuario y tipo de evento y páginas de 100 registros (spec 007, FR-027).

Esta funcionalidad agrega lo que falta:

- Los cambios de productos, que hoy no se registran.
- El detalle de antes y después, campo por campo, en todas las ediciones.
- El registro de todos los descuentos aplicados, no solo de los autorizados.
- El filtro por entidad.
- La exportación a PDF y Excel.
- Garantías explícitas de inmutabilidad y de tiempo de respuesta.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Bitácora centralizada con antes y después (Prioridad: P2)

Un Administrador abre la pantalla "Auditoría" y ve en un solo lugar todos los eventos relevantes del negocio. En cada edición ve qué campos cambiaron, con su valor anterior y su valor nuevo. En las operaciones sensibles ve quién las hizo, quién las autorizó y el motivo.

**Por qué esta prioridad**: es la evidencia que piden el cumplimiento y la detección de fraude. La venta funciona sin ella, por eso no es P1.

**Prueba independiente**: con un usuario Administrador, crear un producto, cambiarle el precio y la categoría, y borrarlo. Cancelar una venta, hacer una devolución, aplicar un descuento, abrir el cajón sin venta, editar un usuario, cerrar sesión y volver a entrar. Abrir "Auditoría" y comprobar que cada evento aparece con su autor, su fecha y hora y, en las ediciones, con sus valores anteriores y nuevos.

**Escenarios de aceptación**:

1. **Dado** un Administrador que crea un producto, **cuando** consulta la bitácora, **entonces** aparece "Producto creado" con su autor, su fecha y hora y los valores iniciales de los campos auditados.
2. **Dado** un producto cuyo precio cambia de $25.00 a $28.50, **cuando** se consulta la bitácora, **entonces** la entrada muestra el campo "Precio" con el valor anterior $25.00 y el valor nuevo $28.50.
3. **Dado** una edición que cambia el precio y la categoría a la vez, **cuando** se consulta la bitácora, **entonces** aparece una sola entrada con los dos campos y sus valores anteriores y nuevos. Los campos que no cambiaron no aparecen.
4. **Dado** una edición que se guarda sin cambiar ningún valor, **cuando** se consulta la bitácora, **entonces** no aparece ninguna entrada de modificación.
5. **Dado** un producto borrado, **cuando** se consulta la bitácora, **entonces** aparece "Producto eliminado" con su autor y los últimos valores que tenía el producto.
6. **Dado** una venta cancelada o devuelta por un Cajero con autorización de Administrador, **cuando** se consulta la bitácora, **entonces** la entrada muestra quién la hizo, quién la autorizó, cuándo, el folio de la venta, el importe y el motivo.
7. **Dado** una venta cobrada con descuentos, con o sin autorización, **cuando** se consulta la bitácora, **entonces** aparece una entrada con el cajero, el folio de la venta, cada descuento (sobre qué producto o sobre el total), su tipo, su monto y, si lo hubo, quién lo autorizó.
8. **Dado** una apertura del cajón sin venta, **cuando** se consulta la bitácora, **entonces** aparece quién lo abrió y cuándo.
9. **Dado** un usuario creado, editado o desactivado, **cuando** se consulta la bitácora, **entonces** la entrada muestra quién hizo el cambio y, en la edición, los campos que cambiaron (nombre, rol, estado) con sus valores anteriores y nuevos. Las contraseñas nunca aparecen.
10. **Dado** un inicio de sesión, un intento fallido o un cierre de sesión, **cuando** se consulta la bitácora, **entonces** aparece el evento con el usuario y la fecha y hora.
11. **Dado** un Cajero, **cuando** busca la opción "Auditoría", **entonces** no tiene acceso.
12. **Dado** cualquier entrada de la bitácora, **cuando** un Administrador intenta modificarla o borrarla, **entonces** la aplicación no ofrece ninguna forma de hacerlo.

---

### Historia 2 - Filtros de búsqueda (Prioridad: P2)

El Administrador acota la bitácora por usuario, rango de fechas, tipo de operación y entidad (producto, venta, usuario, etc.) para investigar un caso concreto. Por ejemplo: todos los cambios de precio de la última semana, o todo lo que hizo un cajero en un día.

**Por qué esta prioridad**: sin filtros, una bitácora con miles de entradas no sirve para investigar.

**Prueba independiente**: con una bitácora de miles de entradas, combinar filtros y comprobar que solo aparecen las entradas que cumplen todos y que cada búsqueda responde en menos de 1 segundo.

**Escenarios de aceptación**:

1. **Dado** la pantalla "Auditoría", **cuando** se abre, **entonces** muestra las entradas del día, de la más reciente a la más antigua, en páginas de 100 registros.
2. **Dado** un filtro por usuario, **cuando** se aplica, **entonces** solo aparecen las entradas en las que ese usuario fue el autor, el autorizador o el usuario afectado.
3. **Dado** un filtro por entidad "Producto", **cuando** se aplica, **entonces** solo aparecen eventos de productos (alta, modificación, eliminación).
4. **Dado** un filtro por tipo de operación "Modificación de producto" y un rango de fechas, **cuando** se aplican juntos, **entonces** solo aparecen las modificaciones de productos dentro del rango.
5. **Dado** una entrada de un producto, una venta o un usuario, **cuando** el Administrador pide ver el historial de ese registro, **entonces** la bitácora queda filtrada por ese registro y muestra su historia completa en orden cronológico.
6. **Dado** filtros sin coincidencias, **cuando** se aplican, **entonces** se indica que no hay resultados.
7. **Dado** un rango de fechas con la fecha final anterior a la inicial, **cuando** se aplica, **entonces** se rechaza con un mensaje claro.

---

### Historia 3 - Exportación a PDF o Excel (Prioridad: P3)

El Administrador exporta la bitácora de un rango de fechas a PDF (para archivar o entregar a un auditor) o a Excel (para analizarla).

**Por qué esta prioridad**: la consulta en pantalla cubre la investigación diaria. La exportación sirve para auditorías externas y archivo.

**Prueba independiente**: filtrar un rango de fechas con más de 100 entradas, exportar a PDF y a Excel, y comprobar que ambos archivos contienen todas las entradas filtradas (no solo la página visible), con su antes y después.

**Escenarios de aceptación**:

1. **Dado** filtros aplicados, **cuando** el Administrador exporta a PDF, **entonces** el archivo contiene todas las entradas que cumplen los filtros e indica el rango, los filtros, el negocio, la fecha de generación y quién exportó.
2. **Dado** filtros aplicados, **cuando** exporta a Excel, **entonces** el archivo tiene una fila por campo modificado, con fecha y hora, evento, entidad, registro, autor, autorizador, campo, valor anterior y valor nuevo, de modo que se puede filtrar y ordenar.
3. **Dado** una exportación realizada, **cuando** se consulta la bitácora, **entonces** aparece la exportación con quién la hizo, el formato, el rango y los filtros.
4. **Dado** que la exportación no se puede guardar en la ubicación elegida, **cuando** falla, **entonces** se muestra un mensaje claro y la exportación no queda registrada como exitosa.

---

### Casos límite

- Edición que cambia solo campos no auditados (por ejemplo, la imagen del producto): se registra "Producto modificado" e indica que cambió la imagen, sin guardar la imagen ni su contenido.
- Valores largos (nombres extensos, motivos): se guardan completos en la entrada. La pantalla los muestra recortados y permite ver el texto completo.
- Cambio de categoría: el antes y el después muestran el nombre de la categoría tal como se llamaba en ese momento, aunque luego se renombre o se elimine.
- Producto, usuario o categoría eliminados después: sus entradas en la bitácora se conservan y siguen mostrando los datos que tenían.
- Operación que falla o se revierte: no queda entrada de bitácora del cambio (la entrada viaja en la misma operación atómica).
- Intento de acceso con un nombre de usuario que no existe: se registra el nombre capturado como texto. El autor es "Sistema" porque no hay un usuario identificado.
- Eventos automáticos sin intervención de una persona (bloqueo por intentos fallidos, regeneración de licencia, descarte automático de una venta conservada): el autor es "Sistema" o el usuario cuya acción los provocó. Nunca queda vacío.
- Entradas anteriores a esta funcionalidad: se conservan tal como están, con su descripción en texto libre y sin antes y después estructurado. Aparecen en los filtros con su entidad cuando se puede deducir del evento.
- Venta con muchos descuentos: una sola entrada por venta con todos sus descuentos.
- Bitácora con años de operación: las búsquedas mantienen el tiempo de respuesta (SC-002).
- Exportación muy grande: se genera completa. Mientras se genera, se muestra el progreso o un indicador de espera y la venta no se ve afectada.
- Cambio de reloj del equipo: las entradas se guardan con la hora del momento en UTC. No se reordenan ni se modifican después.

## Requisitos *(obligatorio)*

### Requisitos funcionales

**Registro**

- **FR-001**: El sistema DEBE registrar en la bitácora el alta, la modificación y la eliminación de productos.
- **FR-002**: Cada modificación de un registro auditado DEBE guardar la lista de campos que cambiaron, cada uno con su valor anterior y su valor nuevo. Si ningún valor cambió, no se registra la modificación.
- **FR-003**: Los campos auditados de un producto son: SKU, código de barras, nombre, precio, categoría, unidad de medida, si maneja inventario, existencia mínima, si es crítico y estado (activo o inactivo). Los cambios de imagen se registran como "imagen cambiada", sin su contenido.
- **FR-003a**: El costo del producto queda fuera de esta funcionalidad porque el producto no tiene ese campo. Cuando se agregue, se auditará con la misma regla de antes y después (FR-002).
- **FR-004**: Las altas DEBEN guardar los valores iniciales de los campos auditados, y las eliminaciones o desactivaciones, los últimos valores que tenía el registro.
- **FR-005**: Las referencias a otros registros (categoría, usuario, cliente) DEBEN guardarse con el nombre que tenían en ese momento, además de su identificador, para que la entrada siga siendo legible si el registro cambia o se elimina.
- **FR-006**: Las ediciones de usuarios, categorías, clientes, cupones y configuraciones que ya se registran (specs 007, 013, 014, 015, 016) DEBEN incluir también el antes y después por campo. Las contraseñas, sus hashes y cualquier secreto NUNCA DEBEN guardarse en la bitácora.
- **FR-007**: Las cancelaciones y devoluciones DEBEN registrar quién las hizo, quién las autorizó (si aplica), cuándo, el folio de la venta, los productos y el importe afectados y el motivo capturado.
- **FR-008**: El sistema DEBE registrar una entrada por cada venta cobrada que tenga al menos un descuento, con o sin autorización. La entrada indica el cajero, el folio de la venta y, por cada descuento, sobre qué se aplicó (producto o total de la venta), su tipo (porcentaje, monto o cupón), su monto en dinero y quién lo autorizó, si lo hubo.
- **FR-009**: El sistema DEBE seguir registrando las aperturas del cajón sin venta (quién y cuándo), los cambios de usuarios (alta, edición, desactivación y activación) y los accesos (inicio de sesión, intento fallido, bloqueo y cierre de sesión), como en la spec 007.
- **FR-010**: Toda entrada DEBE tener un autor. Cuando no hay una persona identificada, el autor es el usuario especial "Sistema". Ninguna entrada se guarda sin autor.
- **FR-011**: Cada entrada DEBE guardar: fecha y hora (UTC, mostrada en hora local), tipo de operación, entidad, identificador y nombre legible del registro afectado, autor, autorizador (si lo hubo), motivo (si lo hubo) y el detalle de antes y después.
- **FR-012**: La entrada de bitácora DEBE guardarse en la misma operación atómica que el cambio que documenta. Si el cambio falla, no queda entrada. Si la entrada no se puede guardar, el cambio no se aplica.

**Inmutabilidad**

- **FR-013**: Las entradas de la bitácora NO DEBEN poder modificarse ni borrarse desde la aplicación, por ningún rol. Ningún proceso de la aplicación (incluido el borrado lógico, las migraciones de datos o la depuración de registros antiguos) DEBE alterarlas ni eliminarlas.
- **FR-014**: La inmutabilidad se garantiza dentro de la aplicación (FR-013). La protección o detección de alteraciones hechas directamente sobre el archivo de datos con otras herramientas queda fuera de esta funcionalidad.
- **FR-015**: La bitácora DEBE conservarse indefinidamente. No hay depuración automática.

**Consulta y filtros**

- **FR-016**: Solo un Administrador DEBE poder abrir la pantalla "Auditoría". Es de solo lectura.
- **FR-017**: La pantalla DEBE permitir filtrar por rango de fechas, usuario involucrado (autor, autorizador o afectado), tipo de operación y entidad, de forma combinable. Las entidades son, como mínimo: Producto, Venta, Usuario, Categoría, Cliente, Cupón, Caja/Turno, Configuración y Sesión.
- **FR-018**: La pantalla DEBE abrir con el filtro de fechas en el día actual, ordenar de la más reciente a la más antigua y paginar de 100 en 100 registros.
- **FR-019**: Desde una entrada, el Administrador DEBE poder ver el historial completo del registro afectado (todas las entradas de ese producto, venta o usuario).
- **FR-020**: El detalle de una entrada DEBE mostrar sus campos modificados como una tabla de campo, valor anterior y valor nuevo, con importes en formato de moneda y fechas en hora local.
- **FR-021**: Las entradas anteriores a esta funcionalidad DEBEN seguir visibles con su descripción original. El filtro por entidad las incluye cuando su entidad se puede deducir del tipo de evento.

**Exportación**

- **FR-022**: El Administrador DEBE poder exportar a PDF o a Excel las entradas que cumplen los filtros aplicados, y el rango de fechas es obligatorio. La exportación incluye todas las entradas, no solo la página visible.
- **FR-023**: El PDF DEBE incluir el encabezado del negocio, el rango y los filtros aplicados, la fecha de generación y el usuario que exportó, con el mismo formato de los reportes de la spec 009.
- **FR-024**: El archivo de Excel DEBE tener una fila por campo modificado (o una fila por entrada si no tiene campos), con columnas de fecha y hora, tipo de operación, entidad, registro, autor, autorizador, motivo, campo, valor anterior y valor nuevo.
- **FR-025**: Cada exportación DEBE registrarse en la bitácora con quién la hizo, el formato, el rango y los filtros.

**Generales**

- **FR-026**: Los importes DEBEN manejarse con el value object de dinero en centavos y mostrarse con formato de moneda.
- **FR-027**: La pantalla "Auditoría" y su exportación forman parte de la funcionalidad base (no pertenecen a un módulo licenciado de la spec 012). Los eventos de un módulo bloqueado se siguen mostrando en la bitácora.

### Entidades clave

- **Entrada de bitácora** (existente, spec 007, ampliada): hecho auditado e inmutable. Tiene fecha y hora, tipo de operación, entidad, identificador y nombre legible del registro afectado, autor (obligatorio), autorizador opcional, motivo opcional y una lista de cambios de campo.
- **Cambio de campo** (nuevo): parte de una entrada. Tiene el nombre legible del campo, el valor anterior y el valor nuevo, ya formateados para mostrarse (importes, nombres de categoría, estados).
- **Usuario "Sistema"** (nuevo): autor de los eventos que no tienen una persona identificada. No puede iniciar sesión.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: El 100 % de las altas, modificaciones y eliminaciones de productos, y el 100 % de los cambios de precio y categoría, tienen su entrada en la bitácora con su antes y después.
- **SC-002**: Con una bitácora de 1,000,000 de entradas (unos 5 años de operación intensiva), cualquier búsqueda con cualquier combinación de filtros muestra su primera página en menos de 1 segundo.
- **SC-003**: El 0 % de las entradas tiene el autor vacío.
- **SC-004**: Ninguna entrada puede modificarse ni borrarse desde la aplicación; una revisión de todas las pantallas y opciones lo confirma.
- **SC-005**: Un Administrador encuentra todos los cambios de precio de un producto en un mes en menos de 1 minuto.
- **SC-006**: La exportación de 10,000 entradas a PDF o a Excel termina en menos de 30 segundos y contiene el 100 % de las entradas filtradas.
- **SC-007**: El 100 % de las ventas con descuento cobradas a partir de esta funcionalidad tiene su entrada de descuento en la bitácora.

## Supuestos

- Se reutilizan la bitácora, la pantalla de consulta y el mecanismo de autorización de Administrador de la spec 007, así como los generadores de PDF y Excel de la spec 009.
- Los movimientos de inventario no se agregan a la bitácora: ya tienen su propio historial (kardex, spec 004), que registra quién, cuándo y por qué.
- Las ventas normales sin descuento no generan entrada de bitácora: ya están en "Consultar ventas".
- El costo del producto y la protección de la bitácora fuera de la aplicación quedan fuera de alcance (ver Clarifications).
- Hay una sola caja por instalación (spec 008) y la bitácora es local a la instalación.
- Las entradas anteriores a esta funcionalidad no se completan retroactivamente con antes y después.
- El volumen esperado es de unas 500 entradas diarias, lo que da unas 200,000 por año. El objetivo de rendimiento se fija con margen (SC-002).
