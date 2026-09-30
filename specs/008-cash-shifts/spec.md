# Especificación de funcionalidad: Turnos de caja

**Rama de funcionalidad**: `008-cash-shifts`

**Creado**: 2026-09-30

**Estado**: Aprobada para implementación

**Entrada**: Turnos de caja: apertura con fondo inicial, movimientos de efectivo y cierre con arqueo.

## Objetivo

Controlar el dinero de la caja por turno: con cuánto efectivo se inicia, qué entra y sale durante el turno y si al cerrar el efectivo contado coincide con el esperado. Cada venta queda ligada a un turno y a su cajero.

## Conceptos

- **Caja**: la instalación del POS en una máquina. En esta fase hay una caja por instalación.
- **Turno**: periodo de trabajo de un usuario en la caja, desde la apertura hasta el cierre.
- **Fondo inicial**: efectivo con el que se abre el turno para dar cambio.

## Clarifications

### Session 2026-09-30

- Q: Si al cancelar una venta en efectivo el efectivo esperado quedaría negativo, ¿qué hace el sistema? → A: Rechaza la cancelación sin revelar el monto del efectivo esperado.
- Q: ¿Qué cuenta como "total vendido" de un turno (Inicio, listado, corte)? → A: Suma de los totales de las ventas completadas del turno, sin canceladas, con todas las formas de pago.
- Q: Si un Administrador cierra un turno ajeno con una venta en curso guardada del otro usuario, ¿qué pasa con ella? → A: Se avisa, se pide confirmación y se descarta; el descarte queda en la bitácora.
- Q: ¿Qué muestra el resumen del turno abierto que ve el Cajero? → A: Hora de apertura, número de ventas y total vendido; sin fondo inicial, desglose por forma de pago ni movimientos.
- Q: Al rechazar un retiro mayor que el efectivo esperado, ¿se muestra el monto disponible? → A: Al Cajero, rechazo genérico sin montos; al Administrador, se muestra el monto disponible.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Abrir turno con fondo inicial (Prioridad: P1)

Para vender se requiere un turno abierto. Al entrar al Punto de venta sin turno abierto se solicita la apertura, capturando el fondo inicial en efectivo.

**Por qué esta prioridad**: sin turno no se puede vender; es la base de todo el control de efectivo.

**Prueba independiente**: iniciar sesión sin turno abierto, entrar al Punto de venta, abrir el turno y comprobar que ya se puede vender.

**Escenarios de aceptación**:

1. **Dado** que no hay turno abierto, **cuando** el usuario entra al Punto de venta, **entonces** se le solicita abrir turno y no se puede vender hasta hacerlo.
2. **Dado** la apertura, **cuando** captura un fondo inicial mayor que 0 y confirma, **entonces** el turno queda abierto a su nombre.
3. **Dado** la apertura, **cuando** captura 0, **entonces** se pide una confirmación explícita antes de abrir.
4. **Dado** un turno abierto por otro usuario, **cuando** un Cajero entra al Punto de venta, **entonces** no puede vender ni abrir otro turno y se le indica que debe cerrarse el turno existente.
5. **Dado** un turno abierto por otro usuario, **cuando** un Administrador lo cierra, **entonces** se libera la caja para abrir un nuevo turno.
6. **Dado** un turno abierto, **cuando** la aplicación se cierra y el mismo usuario vuelve a iniciar sesión, **entonces** continúa en su turno sin perder información.

---

### Historia 2 - Ventas ligadas al turno (Prioridad: P1)

Toda venta queda registrada con el turno en que se realizó y con su cajero. Solo se cancelan ventas del turno abierto actual.

**Por qué esta prioridad**: el efectivo esperado depende de conocer qué ventas pertenecen a cada turno.

**Prueba independiente**: realizar una venta y verificar su turno y cajero; cancelarla y verificar que el efectivo esperado se descuenta.

**Escenarios de aceptación**:

1. **Dado** un turno abierto propio, **cuando** se completa una venta, **entonces** queda ligada a ese turno y a su cajero.
2. **Dado** un turno abierto de otro usuario, **cuando** un Cajero intenta vender, **entonces** se rechaza.
3. **Dado** una venta del turno abierto actual, **cuando** se cancela, **entonces** el efectivo de esa venta se descuenta del efectivo esperado del turno.
4. **Dado** una venta de un turno cerrado, **cuando** se intenta cancelar, **entonces** se rechaza indicando que pertenece a un turno cerrado.
5. **Dado** una venta anterior a esta funcionalidad (sin turno), **cuando** se intenta cancelar, **entonces** se rechaza igual que una de un turno cerrado; sigue visible en consulta.

---

### Historia 3 - Cerrar turno con arqueo (Prioridad: P1)

Al terminar, el usuario cierra el turno capturando el efectivo contado sin ver antes el esperado (arqueo ciego). Luego se muestran esperado, contado y diferencia, y se imprime el corte.

**Por qué esta prioridad**: es el objetivo central: saber si el efectivo coincide y dejar el turno cerrado y auditable.

**Prueba independiente**: con un turno con ventas, cerrar capturando un conteo y verificar cifras, diferencia, inmutabilidad y corte impreso.

**Escenarios de aceptación**:

1. **Dado** una venta en curso, **cuando** se intenta cerrar el turno, **entonces** se pide terminarla o cancelarla antes de continuar.
2. **Dado** el cierre, **cuando** el cajero está capturando el conteo, **entonces** no se muestra el efectivo esperado.
3. **Dado** un conteo capturado, **cuando** se confirma la captura, **entonces** se muestran efectivo esperado, contado y diferencia (sobrante o faltante), además de los totales de tarjeta y transferencia.
4. **Dado** un conteo distinto del esperado, **cuando** se intenta confirmar el cierre sin comentario, **entonces** se rechaza hasta capturar un comentario.
5. **Dado** el cierre confirmado, **cuando** termina, **entonces** el turno queda cerrado e inmutable y se imprime el corte con los datos requeridos.
6. **Dado** el efectivo esperado = fondo inicial + ventas en efectivo (neto de cambio) − cancelaciones en efectivo + ingresos − retiros, **cuando** hay pagos mixtos, cambio, cancelaciones, ingresos y retiros, **entonces** el cálculo es correcto.

---

### Historia 4 - Ingresos y retiros de efectivo (Prioridad: P2)

Durante el turno se registran ingresos (por ejemplo, más cambio) y retiros (por ejemplo, envío a resguardo o pago menor), con monto y motivo obligatorio.

**Por qué esta prioridad**: mejora la precisión del arqueo, pero el turno funciona sin ella.

**Prueba independiente**: registrar un ingreso y un retiro y verificar su efecto en el efectivo esperado y en la bitácora.

**Escenarios de aceptación**:

1. **Dado** un turno abierto, **cuando** se registra un ingreso con monto y motivo, **entonces** aumenta el efectivo esperado y queda en la bitácora.
2. **Dado** un retiro mayor que el efectivo esperado en ese momento, **cuando** se intenta registrar, **entonces** se rechaza; un Cajero ve un mensaje genérico sin montos y un Administrador ve el monto disponible.
3. **Dado** un Cajero, **cuando** registra un retiro, **entonces** requiere autorización de Administrador mediante el mecanismo de autorización existente; un Administrador no la requiere.
4. **Dado** un movimiento sin motivo o con monto no positivo, **cuando** se intenta guardar, **entonces** se rechaza.
5. **Dado** un movimiento registrado, **cuando** el usuario lo solicita, **entonces** puede imprimir su comprobante.

---

### Historia 5 - Consulta de turnos (Prioridad: P2)

El Administrador consulta todos los turnos; el cajero solo ve el resumen de su turno abierto.

**Por qué esta prioridad**: da visibilidad y reimpresión del corte, pero no bloquea la operación.

**Prueba independiente**: con varios turnos de distintos usuarios, filtrar el listado, abrir un detalle y reimprimir un corte.

**Escenarios de aceptación**:

1. **Dado** un Administrador, **cuando** abre "Turnos", **entonces** ve usuario, apertura, cierre, total vendido y diferencia, con filtros por fechas, usuario y estado y páginas de 100 registros.
2. **Dado** un turno, **cuando** el Administrador abre su detalle, **entonces** ve ventas, movimientos de efectivo y arqueo, y puede reimprimir el corte si está cerrado.
3. **Dado** un Cajero, **cuando** consulta su turno abierto, **entonces** ve un resumen con hora de apertura, número de ventas y total vendido (sin efectivo esperado, fondo inicial, desglose por forma de pago ni movimientos) y no accede a la pantalla "Turnos".

---

### Historia 6 - Tarjeta de turno en Inicio (Prioridad: P2)

La pantalla de inicio muestra una tarjeta con el turno actual: usuario, hora de apertura y total vendido.

**Por qué esta prioridad**: conveniencia informativa.

**Prueba independiente**: abrir turno, vender y verificar la tarjeta en Inicio.

**Escenarios de aceptación**:

1. **Dado** un turno abierto, **cuando** se muestra Inicio, **entonces** la tarjeta indica usuario, hora de apertura y total vendido.
2. **Dado** que no hay turno abierto, **cuando** se muestra Inicio, **entonces** la tarjeta indica que no hay turno abierto.

---

### Casos límite

- Fondo inicial negativo o no numérico: se rechaza.
- Intento de abrir un segundo turno con uno ya abierto (incluso en dos ventanas o tras un reinicio): se rechaza.
- Conteo de efectivo igual al esperado: no exige comentario.
- Conteo de 0 con efectivo esperado mayor que 0: se trata como faltante y exige comentario.
- Cierre por Administrador de un turno ajeno: el conteo lo captura el Administrador y queda registrado quién cerró. Si el otro usuario dejó una venta en curso guardada, se avisa, se pide confirmación y se descarta, y el descarte queda en la bitácora.
- Turno cerrado: no admite ventas, cancelaciones ni movimientos.
- Cancelación de una venta en efectivo que dejaría el efectivo esperado en negativo (por ejemplo, tras un retiro): se rechaza sin revelar el monto esperado; se puede registrar un ingreso y volver a intentar.
- Falla durante apertura, movimiento o cierre: no queda información parcial.
- El cajero con turno abierto de otro usuario no puede vender, pero sí cerrar sesión.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema DEBE exigir un turno abierto, propiedad del usuario actual, para realizar ventas.
- **FR-002**: El sistema DEBE solicitar la apertura de turno al entrar al Punto de venta sin turno abierto.
- **FR-003**: El sistema DEBE capturar el fondo inicial en la apertura, permitiendo 0 solo con confirmación explícita.
- **FR-004**: El sistema DEBE permitir un solo turno abierto por caja a la vez.
- **FR-005**: El sistema DEBE impedir que un usuario venda en el turno de otro y que se abra otro turno mientras exista uno abierto.
- **FR-006**: El sistema DEBE conservar el turno abierto al cerrar la aplicación y permitir que el mismo usuario lo continúe al iniciar sesión.
- **FR-007**: El sistema DEBE registrar en cada venta su turno y su cajero.
- **FR-008**: El sistema DEBE permitir cancelar solo ventas del turno abierto actual y descontar el efectivo de la venta cancelada del efectivo esperado; DEBE rechazar la cancelación si el efectivo esperado quedaría negativo, sin revelar su monto.
- **FR-009**: El sistema DEBE permitir registrar ingresos y retiros con monto positivo y motivo obligatorio.
- **FR-010**: El sistema DEBE rechazar un retiro mayor que el efectivo esperado en ese momento. Al Cajero se le muestra un rechazo genérico sin montos; al Administrador se le muestra el monto disponible.
- **FR-011**: El sistema DEBE exigir autorización de Administrador, por el mecanismo existente, para los retiros de un Cajero.
- **FR-012**: El sistema DEBE ofrecer impresión de comprobante por cada movimiento de efectivo.
- **FR-013**: El sistema DEBE impedir cerrar un turno con una venta en curso, pidiendo terminarla o cancelarla. Excepción: cuando un Administrador cierra un turno ajeno con una venta en curso guardada del otro usuario, el sistema DEBE avisarlo, pedir confirmación y descartarla, registrando el descarte en la bitácora.
- **FR-014**: El sistema DEBE capturar el conteo de efectivo sin mostrar antes el efectivo esperado (arqueo ciego). El usuario puede volver a contar después de ver las cifras; cada conteo queda en la bitácora con el monto contado, para que se pueda reconstruir cuántas veces y cuánto contó.
- **FR-015**: El sistema DEBE calcular el efectivo esperado como fondo inicial + ventas en efectivo (neto de cambio) − cancelaciones en efectivo + ingresos − retiros.
- **FR-016**: Tras el conteo, el sistema DEBE mostrar efectivo esperado, contado, diferencia (sobrante o faltante) y totales de tarjeta y transferencia.
- **FR-017**: El sistema DEBE exigir un comentario cuando la diferencia no sea cero.
- **FR-018**: El sistema DEBE dejar el turno cerrado como inmutable al confirmar el cierre.
- **FR-019**: El sistema DEBE imprimir el corte del turno con: caja, usuario, fechas de apertura y cierre, fondo inicial, número de ventas y canceladas, totales por forma de pago, ingresos, retiros, efectivo esperado, contado y diferencia, y permitir reimprimirlo.
- **FR-019a**: El "total vendido" de un turno DEBE ser la suma de los totales de sus ventas completadas, sin incluir canceladas, con todas las formas de pago; se usa la misma cifra en Inicio, en "Turnos" y en el corte.
- **FR-020**: El sistema DEBE ofrecer a los Administradores la pantalla "Turnos" con listado (usuario, apertura, cierre, total vendido, diferencia), filtros por fechas, usuario y estado, y paginación de 100 registros.
- **FR-021**: El sistema DEBE ofrecer el detalle de turno (ventas, movimientos, arqueo) y la reimpresión del corte a los Administradores.
- **FR-022**: El sistema DEBE mostrar al cajero solo el resumen de su turno abierto: hora de apertura, número de ventas y total vendido. No DEBE mostrar el efectivo esperado ni datos que permitan deducirlo (fondo inicial, desglose por forma de pago, ingresos y retiros).
- **FR-023**: El sistema DEBE mostrar en Inicio una tarjeta con el turno actual: usuario, hora de apertura y total vendido.
- **FR-024**: Un Administrador DEBE poder cerrar el turno de otro usuario y consultar todos los turnos.
- **FR-025**: Todos los importes DEBEN manejarse con el value object de dinero en centavos.
- **FR-026**: La apertura, cada movimiento de efectivo y el cierre DEBEN registrarse cada uno en una única transacción.
- **FR-027**: Apertura, cada conteo de efectivo, cierre, cierre de un turno ajeno por Administrador, ingresos y retiros DEBEN quedar en la bitácora de auditoría.

### Entidades clave

- **Turno**: periodo de trabajo de un usuario en la caja; tiene usuario, fondo inicial, fechas de apertura y cierre, estado (abierto/cerrado) y, al cerrar, arqueo y quién lo cerró.
- **Movimiento de efectivo**: ingreso o retiro dentro de un turno, con monto, motivo, usuario, fecha y, en retiros de cajero, quién autorizó.
- **Arqueo**: resultado del cierre: efectivo esperado, contado, diferencia y comentario.
- **Venta** (existente): se liga a un turno y a su cajero.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: El 100 % de las ventas nuevas quedan ligadas a un turno y a su cajero, y ninguna se completa sin turno abierto.
- **SC-002**: Nunca existen dos turnos abiertos en la misma caja.
- **SC-003**: El efectivo esperado coincide al 100 % con el cálculo manual en los escenarios de ventas con cambio, pagos mixtos, cancelaciones, ingresos y retiros.
- **SC-004**: Un cajero abre un turno en menos de 30 segundos y completa el cierre con arqueo en menos de 2 minutos.
- **SC-005**: El cajero no ve el efectivo esperado en ninguna pantalla antes de confirmar su conteo.
- **SC-006**: Un turno cerrado no puede modificarse y su corte puede reimprimirse cualquier número de veces con las mismas cifras.
- **SC-007**: Tras cerrar y reabrir la aplicación con un turno abierto, el mismo usuario continúa su turno sin pérdida de datos en el 100 % de los casos.
- **SC-008**: El Administrador localiza un turno con filtros y abre su detalle en menos de 1 minuto.

## Supuestos

- Hay una sola caja por instalación; varias cajas, conteo por denominaciones, corte X, depósitos bancarios y cancelación de ventas de turnos cerrados quedan fuera de alcance.
- Se reutilizan usuarios, roles, autorización de Administrador, bitácora de auditoría e impresión de tickets ya existentes.
- Las ventas existentes antes de esta funcionalidad no pertenecen a ningún turno y se conservan sin cambios en consulta, pero ya no se pueden cancelar (no forman parte de ningún arqueo).
- El motivo de un movimiento y el comentario de cierre admiten hasta 250 caracteres.
- El efectivo esperado para validar retiros incluye todos los movimientos del turno hasta ese momento.
- Un Administrador puede abrir y operar su propio turno igual que un Cajero.
- El cierre de un turno ajeno por Administrador captura el conteo con el mismo arqueo y comentario obligatorio si hay diferencia.
