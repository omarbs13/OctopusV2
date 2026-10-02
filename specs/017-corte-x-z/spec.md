# Especificación de funcionalidad: Corte X y Corte Z

**Rama de funcionalidad**: `017-corte-x-z`

**Creado**: 2026-10-01

**Estado**: Borrador

**Entrada**: Corte X y Z: cierre parcial y definitivo de caja. Corte X (lectura sin afectar caja) y Corte Z (cierre definitivo que resetea contadores), con histórico de ambos.

## Objetivo

Dar a la caja los dos cortes habituales en México: el **Corte X**, una lectura parcial de las ventas y pagos del turno que no modifica nada, y el **Corte Z**, el cierre definitivo del turno con un folio consecutivo propio que queda para auditoría. Ambos se imprimen y se consultan en un histórico.

## Conceptos

- **Turno** (spec 008): periodo de trabajo de un usuario en la caja, desde la apertura hasta el cierre.
- **Corte X**: lectura de las cifras acumuladas del turno abierto en un momento dado. No cierra el turno, no pide conteo de efectivo y no cambia ningún dato del turno ni de las ventas. Se pueden hacer varios por turno.
- **Corte Z**: el cierre de turno con arqueo de la spec 008, ahora identificado como "Corte Z" y con un folio consecutivo propio. Es definitivo: después de él el turno ya no acepta operaciones y el turno siguiente empieza en cero.
- **Folio de corte**: número consecutivo legible, independiente para cada tipo (`X-000001`, `Z-000001`), que nunca se reinicia ni se reutiliza.

## Clarifications

### Session 2026-10-01

- Q: ¿Quién puede generar un Corte X? → A: El Administrador directamente; el Cajero solo con autorización de Administrador.
- Q: ¿El Corte X incluye conteo de efectivo y diferencia? → A: No; es solo un reporte de lectura, sin conteo.
- Q: ¿Los turnos cerrados antes de esta funcionalidad reciben folio Z retroactivo? → A: No; siguen solo en "Turnos" y no aparecen en el histórico de cortes.
- Q: ¿El Corte Z muestra un gran total acumulado entre cortes? → A: No; el folio Z consecutivo basta para detectar cortes faltantes.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Corte X: lectura parcial del turno (Prioridad: P2)

Desde "Caja > Corte X" se genera el reporte de ventas y pagos del turno abierto hasta ese momento, sin cerrarlo. Se usa para cuadres de caja a mitad del día o auditorías internas. El reporte se puede imprimir.

**Por qué esta prioridad**: da control intermedio del efectivo sin interrumpir la venta; la caja opera sin él.

**Prueba independiente**: con un turno abierto con ventas, generar un Corte X, imprimirlo, comprobar que el turno sigue abierto con las mismas cifras, realizar otra venta y generar un segundo Corte X que la incluya.

**Escenarios de aceptación**:

1. **Dado** un turno abierto con ventas, **cuando** un Administrador genera un Corte X, **entonces** se muestra el reporte con las cifras del turno hasta ese momento y un folio X nuevo.
2. **Dado** un Corte X generado, **cuando** termina, **entonces** el turno sigue abierto, se puede seguir vendiendo y ninguna cifra del turno cambió (ventas, movimientos, efectivo esperado).
3. **Dado** un Corte X generado, **cuando** el usuario elige imprimir, **entonces** se imprime con el título "Corte X", su folio, la fecha y hora de generación y la leyenda de que no es un cierre.
4. **Dado** dos Cortes X del mismo turno con una venta entre ellos, **cuando** se comparan, **entonces** el segundo incluye la venta y el primero conserva sus cifras originales.
5. **Dado** que no hay turno abierto, **cuando** se elige "Caja > Corte X", **entonces** se indica que no hay turno abierto y no se genera ningún corte.
6. **Dado** un Cajero, **cuando** elige "Caja > Corte X", **entonces** se requiere autorización de Administrador por el mecanismo existente; sin ella no se genera.

---

### Historia 2 - Corte Z: cierre definitivo del turno (Prioridad: P2)

El cierre de turno con arqueo (spec 008) se presenta como "Corte Z" para conformidad fiscal. Al confirmarlo, el turno queda cerrado, se le asigna un folio Z consecutivo y no se pueden registrar más ventas hasta abrir un turno nuevo, que empieza con sus cifras en cero.

**Por qué esta prioridad**: el cierre ya existe; esta historia le da el nombre, el folio y el reporte que exige la práctica fiscal.

**Prueba independiente**: cerrar un turno mediante "Caja > Corte Z", verificar el folio Z asignado, intentar vender y comprobar el rechazo, abrir un turno nuevo y verificar que sus cifras empiezan en cero.

**Escenarios de aceptación**:

1. **Dado** un turno abierto, **cuando** se elige "Caja > Corte Z", **entonces** se sigue el mismo flujo de cierre con arqueo ciego, diferencia y comentario obligatorio de la spec 008.
2. **Dado** el Corte Z confirmado, **cuando** termina, **entonces** el turno queda cerrado e inmutable y recibe el siguiente folio Z consecutivo.
3. **Dado** un turno cerrado con Corte Z, **cuando** alguien intenta vender, **entonces** se rechaza y se le pide abrir un turno nuevo.
4. **Dado** un turno nuevo abierto después de un Corte Z, **cuando** se consulta un Corte X, **entonces** sus cifras incluyen solo las operaciones del turno nuevo.
5. **Dado** el Corte Z confirmado, **cuando** se imprime, **entonces** el reporte lleva el título "Corte Z", su folio Z, el folio del turno y todos los datos del corte de la spec 008.
6. **Dado** cualquier forma de cerrar un turno (incluido el cierre de un turno ajeno por un Administrador), **cuando** se confirma, **entonces** se trata como Corte Z y recibe folio Z.
7. **Dado** un Corte Z, **cuando** se intenta deshacer o modificar, **entonces** no hay forma de hacerlo.

---

### Historia 3 - Histórico de cortes (Prioridad: P2)

Un Administrador consulta el listado de todos los Cortes X y Z realizados, abre cualquiera y lo reimprime con las mismas cifras con que se generó.

**Por qué esta prioridad**: da trazabilidad para auditoría, pero no bloquea la operación diaria.

**Prueba independiente**: con varios turnos con Cortes X y Z, filtrar el listado por tipo y fechas, abrir un corte y reimprimirlo, y comprobar que coincide con el original.

**Escenarios de aceptación**:

1. **Dado** un Administrador, **cuando** abre "Caja > Histórico de cortes", **entonces** ve los cortes del día (filtro de fechas en hoy por omisión, como en "Turnos") con tipo (X o Z), folio, fecha y hora, usuario que lo generó, folio del turno, total vendido y, en los Z, la diferencia del arqueo, del más reciente al más antiguo y en páginas de 100 registros; puede ampliar o quitar el rango de fechas para ver cortes anteriores.
2. **Dado** el listado, **cuando** filtra por tipo, rango de fechas o usuario, **entonces** solo ve los cortes que cumplen el filtro.
3. **Dado** un corte del listado, **cuando** lo abre, **entonces** ve el mismo reporte que se generó y puede reimprimirlo.
4. **Dado** un Corte X reimpreso después de que el turno tuvo más ventas, **cuando** se compara con el original, **entonces** las cifras son idénticas a las del momento en que se generó.
5. **Dado** un Cajero, **cuando** busca el histórico de cortes, **entonces** no tiene acceso.

---

### Casos límite

- Corte X de un turno sin ventas: se genera con cifras en cero.
- Corte X con una venta en curso sin completar: la venta en curso no se incluye y no se interrumpe.
- Corte X de un turno abierto por otro usuario: un Administrador puede generarlo; queda registrado quién lo generó y de qué turno.
- Varios Cortes X seguidos sin operaciones entre ellos: cada uno recibe su propio folio X, con cifras iguales.
- Falla al generar un Corte X: no se registra el corte, no se consume folio y el turno no se ve afectado.
- Falla durante el Corte Z: no queda información parcial; el turno sigue abierto y no se consume folio Z.
- Dos intentos simultáneos de Corte Z sobre el mismo turno: solo uno se completa; los folios Z no se repiten ni quedan huecos.
- Cancelaciones, devoluciones y abonos de otros turnos que afectan el efectivo del turno abierto (specs 013 y 014): aparecen en el Corte X y en el Corte Z del turno en que se registraron, igual que en el corte actual.
- Turnos cerrados antes de esta funcionalidad: no reciben folio Z retroactivo; siguen consultables en "Turnos" con su corte y no aparecen en el histórico de cortes.
- Módulo "Turnos y arqueo" sin licencia (spec 012): sin turnos no hay cortes; las opciones de Corte X, Corte Z e histórico no aparecen. Los cortes ya registrados se conservan y vuelven a estar disponibles al reactivar el módulo.
- Impresora no disponible al generar un corte: el corte queda registrado igualmente y se puede reimprimir desde el histórico.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema DEBE ofrecer en el menú "Caja" las opciones "Corte X", "Corte Z" e "Histórico de cortes".
- **FR-002**: El sistema DEBE generar un Corte X solo sobre el turno abierto de la caja y rechazarlo si no hay turno abierto.
- **FR-003**: Generar un Corte X NO DEBE modificar el turno, sus ventas, sus movimientos ni el efectivo esperado, ni impedir seguir vendiendo.
- **FR-004**: El Corte X DEBE contener las cifras del turno hasta el momento de generarlo, con el mismo contenido que el corte de turno de la spec 008 (caja, usuario del turno, fecha de apertura, fondo inicial, número de ventas y canceladas, totales por forma de pago, devoluciones y reintegros, abonos, ingresos, retiros y efectivo esperado), excepto efectivo contado y diferencia, que no aplican.
- **FR-004a**: El Corte X NO DEBE pedir ni registrar conteo de efectivo, efectivo contado, diferencia ni comentario de arqueo; es solo lectura.
- **FR-005**: El sistema DEBE permitir generar varios Cortes X por turno.
- **FR-006**: El sistema DEBE guardar cada Corte X con su folio, fecha y hora, usuario que lo generó, turno y una copia fija de sus cifras, de modo que su reimpresión muestre siempre las mismas cifras.
- **FR-007**: Un Administrador DEBE poder generar un Corte X directamente; un Cajero solo DEBE poder generarlo con autorización de Administrador mediante el mecanismo existente, y la bitácora registra quién autorizó. El reporte es el mismo en ambos casos.
- **FR-008**: El cierre de turno con arqueo de la spec 008 DEBE presentarse y registrarse como "Corte Z", conservando todas sus reglas (arqueo ciego, diferencia, comentario obligatorio, venta en curso, cierre de turno ajeno, inmutabilidad).
- **FR-009**: Al confirmar un Corte Z, el sistema DEBE asignarle el siguiente folio Z consecutivo dentro de la misma operación atómica del cierre.
- **FR-010**: Los folios X y Z DEBEN ser consecutivos por tipo, únicos, sin huecos ni repeticiones, y nunca reiniciarse ni reutilizarse.
- **FR-010a**: Los turnos cerrados antes de esta funcionalidad NO DEBEN recibir folio Z ni aparecer en el histórico de cortes; se siguen consultando y reimprimiendo en "Turnos" como en la spec 008. La numeración Z empieza en `Z-000001` con el primer Corte Z posterior a la actualización.
- **FR-011**: Después de un Corte Z, el sistema DEBE rechazar ventas, cancelaciones con reintegro en efectivo, abonos y movimientos de efectivo sobre ese turno y pedir abrir un turno nuevo.
- **FR-012**: Las cifras de un turno nuevo DEBEN empezar en cero; ningún Corte X ni Corte Z incluye operaciones de turnos anteriores (salvo las de specs 013 y 014 registradas en el turno actual).
- **FR-012a**: Ningún corte DEBE mostrar ni guardar un gran total acumulado entre turnos; la continuidad entre Cortes Z se verifica únicamente con su folio consecutivo.
- **FR-013**: Un Corte Z NO DEBE poder deshacerse, modificarse ni eliminarse.
- **FR-014**: Ambos cortes DEBEN poder imprimirse al generarse y reimprimirse desde el histórico cualquier número de veces. El reporte impreso indica el tipo ("Corte X" o "Corte Z"), su folio, el folio del turno y la fecha y hora de generación; el Corte X indica además que no es un cierre de caja.
- **FR-015**: El sistema DEBE ofrecer a los Administradores el "Histórico de cortes" con tipo, folio, fecha y hora, usuario, folio del turno, total vendido y diferencia (solo Z); filtros por tipo, rango de fechas y usuario; orden del más reciente al más antiguo y paginación de 100 registros.
- **FR-016**: El "total vendido" de un corte DEBE calcularse con la misma regla de las specs 008 y 013, de modo que coincida con el de "Turnos", Inicio y el corte de la spec 008.
- **FR-017**: La generación de cada Corte X y de cada Corte Z, así como sus reimpresiones, DEBEN registrarse en la bitácora de auditoría con usuario, tipo, folio y turno. Las autorizaciones de Administrador rechazadas para un Corte X (credenciales incorrectas o usuario que no es Administrador) quedan en la bitácora mediante el mecanismo existente de autorización, con el contexto del Corte X. Si el Cajero cancela la solicitud de autorización sin intentarla, no hay rechazo que registrar en la bitácora; queda solo en el log de diagnóstico.
- **FR-018**: Todos los importes DEBEN manejarse con el value object de dinero en centavos.
- **FR-019**: El registro de un Corte X y el cierre con Corte Z DEBEN ejecutarse cada uno en una única transacción.
- **FR-020**: Las opciones de esta funcionalidad pertenecen al módulo licenciado "Turnos y arqueo" (spec 012) y siguen sus reglas de bloqueo.

### Entidades clave

- **Corte**: registro de un Corte X o Z; tiene tipo, folio consecutivo por tipo, fecha y hora, usuario que lo generó, turno al que pertenece y la copia fija de sus cifras (ventas, canceladas, totales por forma de pago, devoluciones, abonos, ingresos, retiros, efectivo esperado y, en los Z, contado, diferencia y comentario).
- **Turno** (existente, spec 008): cada turno puede tener cero o más Cortes X y, si está cerrado por esta funcionalidad, exactamente un Corte Z.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: En el 100 % de los casos, las cifras del turno y el efectivo esperado son idénticos antes y después de generar un Corte X.
- **SC-002**: El 100 % de los turnos cerrados a partir de esta funcionalidad tienen exactamente un Corte Z con folio único, y la secuencia de folios Z no tiene huecos ni repeticiones.
- **SC-003**: Ninguna venta se registra en un turno con Corte Z.
- **SC-004**: Las cifras de un Corte X coinciden al 100 % con el cálculo manual de las operaciones del turno hasta ese momento, en escenarios con cambio, pagos mixtos, cancelaciones, devoluciones, abonos, ingresos y retiros.
- **SC-005**: Un Administrador genera e imprime un Corte X en menos de 30 segundos.
- **SC-006**: Un Administrador localiza un corte en el histórico y lo reimprime en menos de 1 minuto, con cifras idénticas a la impresión original.

## Supuestos

- Hay una sola caja por instalación (spec 008); los folios X y Z son consecutivos por instalación.
- Un cuadre a mitad del día se hace comparando el efectivo físico con el efectivo esperado del Corte X; el sistema no captura ese conteo (ver FR-004a).
- "Resetear contadores" significa que el turno siguiente empieza con cifras en cero (FR-012), sin gran total acumulado (FR-012a). La integración con facturación electrónica (CFDI) queda fuera de alcance.
- El Corte Z reemplaza la denominación del cierre de turno; no se crea un flujo de cierre distinto. La pantalla "Turnos" y la reimpresión de cortes de la spec 008 siguen funcionando y muestran el folio Z cuando existe.
- Se reutilizan usuarios, roles, autorización de Administrador, bitácora de auditoría, impresión de tickets, turnos y licenciamiento por módulos ya existentes (specs 006, 007, 008, 012, 013 y 014).
