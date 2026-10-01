# Especificación de funcionalidad: Devoluciones y cancelaciones

**Rama de funcionalidad**: `013-returns-cancellations`

**Creado**: 2026-09-30

**Estado**: Borrador

**Entrada**: Devoluciones y cancelaciones: gestión de retiros de productos y reintegros de dinero. Permitir cancelar o devolver ventas parcial o totalmente, con reintegro de dinero o nota de crédito, y restaurar inventario.

## Clarifications

### Session 2026-09-30

- Q: En una venta con pagos mixtos, ¿de qué forma de pago sale un reintegro parcial? → A: Proporcional al peso de cada forma de pago en la venta original.

## Objetivo

Que el negocio pueda deshacer una venta, completa o en parte, con control: autorización de un responsable, inventario restaurado, dinero devuelto al cliente (reintegro) o conservado como saldo a favor (nota de crédito), y un rastro de auditoría que no se puede alterar.

## Conceptos

- **Cancelación**: anula una venta completa.
- **Devolución parcial**: el cliente regresa algunas líneas o parte de las cantidades de una venta; el resto de la venta permanece vigente.
- **Reintegro**: devolución del dinero al cliente. Si la venta se pagó en efectivo, sale de la caja; si fue con tarjeta, se anota para que se reverse manualmente en la terminal bancaria.
- **Nota de crédito (vale)**: documento con saldo a favor del cliente, utilizable como forma de pago en compras futuras.
- **Autorización**: aprobación de un Administrador mediante su contraseña (en esta especificación "PIN" es la contraseña de Administrador de la spec 007; no se crea un PIN nuevo). Siempre es obligatoria, también cuando quien opera es Administrador.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Cancelar una venta completa con autorización y reintegro o nota de crédito (Prioridad: P1)

Desde "Consultar ventas", el operador selecciona una venta completada y la cancela indicando un motivo obligatorio. Un Administrador debe autorizarla. El operador elige cómo se compensa al cliente: reintegro del dinero o nota de crédito. El inventario se restaura automáticamente.

**Por qué esta prioridad**: es el flujo base; sin autorización y compensación al cliente la cancelación actual queda incompleta y expone al negocio a abusos.

**Prueba independiente**: cancelar una venta en efectivo con reintegro, otra con tarjeta con reintegro y otra con nota de crédito; verificar estado, inventario, efectivo de caja, anotación de reversa y saldo del vale.

**Escenarios de aceptación**:

1. **Dado** una venta completada, **cuando** el operador elige cancelar y deja el motivo vacío, **entonces** no se permite continuar.
2. **Dado** un motivo capturado, **cuando** quien opera no es Administrador, **entonces** se solicita el PIN de un Administrador y sin una autorización válida la cancelación no se realiza.
3. **Dado** que quien opera es Administrador, **cuando** confirma, **entonces** su propia identidad cuenta como autorización y se le pide su PIN para confirmar.
4. **Dado** una cancelación autorizada, **cuando** se confirma, **entonces** por cada línea con control de inventario se genera un movimiento "Devolución por venta cancelada" que regresa la cantidad vendida.
5. **Dado** una venta pagada en efectivo y la opción "Reintegro", **cuando** se confirma, **entonces** el efectivo devuelto se descuenta del efectivo esperado del turno.
6. **Dado** una venta pagada con tarjeta y la opción "Reintegro", **cuando** se confirma, **entonces** queda anotado un reintegro pendiente de reversa manual con monto, venta y fecha, sin afectar el efectivo de caja.
7. **Dado** una venta con pago mixto y la opción "Reintegro", **cuando** se confirma, **entonces** el monto se reparte proporcionalmente al peso de cada forma de pago en la venta original (efectivo a caja, tarjeta a reversa manual, nota de crédito a su saldo); en una cancelación completa equivale a devolver exactamente lo pagado con cada una.
8. **Dado** la opción "Nota de crédito", **cuando** se confirma, **entonces** se crea un vale con saldo igual al total cancelado, sin movimiento de efectivo.
9. **Dado** una venta ya cancelada, **cuando** se intenta cancelar de nuevo, **entonces** se rechaza.
10. **Dado** cualquier falla durante el proceso, **cuando** ocurre, **entonces** no queda ningún cambio parcial (ni venta cancelada sin compensación ni compensación sin venta cancelada).

---

### Historia 2 - Devolución parcial de líneas y cantidades (Prioridad: P2)

En el detalle de una venta, el operador selecciona las líneas y cantidades que el cliente devuelve. El sistema recalcula el monto a devolver (incluyendo impuestos y descuentos proporcionales) y sigue el mismo flujo de motivo, autorización y compensación que la cancelación completa.

**Por qué esta prioridad**: caso muy frecuente en la práctica, pero depende del flujo de la Historia 1 y puede entregarse después.

**Prueba independiente**: devolver parte de una línea de una venta de varias líneas y verificar monto, inventario, compensación y que la venta sigue vigente por el resto.

**Escenarios de aceptación**:

1. **Dado** el detalle de una venta, **cuando** el operador marca líneas y captura cantidades a devolver, **entonces** ve el monto recalculado a devolver antes de confirmar.
2. **Dado** una cantidad a devolver mayor que la disponible (vendida menos ya devuelta), **cuando** intenta confirmar, **entonces** se rechaza.
3. **Dado** una devolución parcial autorizada, **cuando** se confirma, **entonces** se generan movimientos de inventario por las cantidades devueltas y la venta queda marcada como parcialmente devuelta, conservando su detalle original.
4. **Dado** una venta con devoluciones parciales previas, **cuando** se devuelve el resto, **entonces** el sistema permite acumular devoluciones hasta agotar lo vendido.
5. **Dado** una devolución parcial, **cuando** se elige reintegro o nota de crédito, **entonces** aplica la misma regla que en la cancelación completa pero por el monto devuelto.
6. **Dado** que se devuelven todas las cantidades vendidas en una o varias devoluciones, **cuando** se completa, **entonces** la venta se muestra como totalmente devuelta.

---

### Historia 3 - Auditoría e historial inmutable (Prioridad: P1)

Cada cancelación o devolución registra quién la realizó, cuándo, el motivo y quién la autorizó, y aparece en la bitácora. Los registros no se pueden editar ni borrar, pero se consultan en el historial de la venta.

**Por qué esta prioridad**: sin rastro confiable no hay control sobre un flujo con dinero y es obligatorio para el dueño del negocio.

**Prueba independiente**: realizar una cancelación y una devolución parcial y verificar bitácora e historial de la venta; confirmar que no existe forma de modificarlos.

**Escenarios de aceptación**:

1. **Dado** una cancelación o devolución confirmada, **cuando** se consulta la bitácora, **entonces** aparece una entrada con usuario, fecha y hora, motivo, usuario que autorizó, venta, monto y tipo de compensación.
2. **Dado** una venta con cancelación o devoluciones, **cuando** se abre su detalle, **entonces** se ve el historial de esos eventos junto con la venta original sin cambios.
3. **Dado** un intento de autorización fallido (PIN incorrecto), **cuando** ocurre, **entonces** queda registrado en la bitácora sin revelar el PIN.
4. **Dado** una cancelación o devolución registrada, **cuando** cualquier usuario, incluido un Administrador, busca editarla o eliminarla, **entonces** no existe esa opción.

---

### Historia 4 - Usar una nota de crédito como forma de pago (Prioridad: P2)

Al cobrar una venta, el cajero puede aplicar una nota de crédito con saldo como forma de pago, total o parcialmente, combinándola con otras formas de pago. El saldo se descuenta por lo usado.

**Por qué esta prioridad**: sin poder usarla, la nota de crédito no tiene valor para el cliente.

**Prueba independiente**: emitir una nota de crédito, pagar una venta menor con ella y verificar saldo restante; pagar una venta mayor combinándola con efectivo.

**Escenarios de aceptación**:

1. **Dado** una nota de crédito con saldo, **cuando** el cajero captura su folio en el cobro, **entonces** el sistema muestra su saldo disponible.
2. **Dado** una venta menor que el saldo, **cuando** se paga con la nota, **entonces** el saldo disminuye por el total de la venta.
3. **Dado** una venta mayor que el saldo, **cuando** se paga con la nota más otra forma de pago, **entonces** el saldo queda en 0 y el resto se cobra con la otra forma.
4. **Dado** un folio inexistente o sin saldo, **cuando** se intenta usar, **entonces** se rechaza con mensaje claro.
5. **Dado** que se cancela una venta pagada (en todo o parte) con una nota de crédito, **cuando** se elige reintegro, **entonces** la parte pagada con nota vuelve como saldo a esa nota, nunca como efectivo.
6. **Dado** una nota de crédito, **cuando** se consulta, **entonces** se ven su saldo, origen, usos y fecha de emisión.

---

### Casos límite

- Venta de un turno cerrado o anterior a los turnos: se permite cancelar o devolver; el reintegro en efectivo sale del turno abierto actual y el turno cerrado no se modifica. Requiere un turno abierto solo si el reintegro es en efectivo.
- Efectivo esperado insuficiente para un reintegro en efectivo: se rechaza sin revelar el monto esperado al Cajero, igual que hoy; se ofrece nota de crédito o registrar un ingreso.
- Producto de una línea ahora inactivo, borrado o con inventario desactivado: la devolución regresa la cantidad al producto igualmente cuando controla inventario; si no controla inventario no genera movimiento.
- Sin turno abierto: no se puede realizar un reintegro en efectivo.
- Dos operadores intentan cancelar o devolver la misma venta a la vez: solo uno tiene éxito; el otro ve el estado actualizado sin duplicar inventario ni dinero.
- Descuentos o impuestos sobre la venta: el monto devuelto es proporcional a lo que el cliente pagó por esas unidades. Hoy las ventas no tienen descuentos ni impuestos por línea (el importe de línea es cantidad × precio); el cálculo debe seguir siendo exacto si existieran.
- Venta con devoluciones parciales previas: no puede cancelarse como cancelación completa (se rechaza como estado inválido); el resto se devuelve con una devolución parcial hasta agotar lo vendido.
- Venta fuera del plazo máximo: se rechaza; el Administrador puede ampliar el plazo en la configuración.
- Propina o cambio entregado: el reintegro nunca supera lo efectivamente pagado, neto de cambio.
- PIN incorrecto repetido: tras varios intentos fallidos se bloquea temporalmente la autorización, conforme a la política de acceso existente.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema DEBE permitir cancelar una venta completada desde "Consultar ventas" exigiendo un motivo no vacío.
- **FR-002**: El sistema DEBE exigir autorización de un Administrador (PIN) para toda cancelación o devolución, sea iniciada por un Cajero o por un Administrador (que puede autorizarse con su propio PIN); sin autorización válida no se realiza ningún cambio.
- **FR-003**: El sistema DEBE generar movimientos de inventario por las cantidades que regresan, solo para productos que controlan inventario: "Devolución por venta cancelada" en una cancelación completa y "Devolución de venta" en una devolución parcial.
- **FR-004**: El sistema DEBE ofrecer al confirmar dos formas de compensación: reintegro o nota de crédito.
- **FR-005**: En reintegro, el sistema DEBE repartir el monto entre las formas de pago originales de forma proporcional a su peso en la venta (sin elección libre del operador), redondeando para que la suma sea exacta, y DEBE descontar del efectivo esperado del turno la parte pagada en efectivo y anotar como reintegro pendiente de reversa manual la parte pagada con tarjeta.
- **FR-006**: El sistema DEBE permitir cancelar o devolver ventas de cualquier turno, incluidos turnos cerrados y ventas anteriores a los turnos; el reintegro en efectivo se registra en el turno abierto actual (que debe existir) y el turno original no se modifica. Esto reemplaza la regla de la spec 008 que rechazaba la cancelación de ventas de turnos cerrados.
- **FR-006a**: El sistema DEBE rechazar cancelaciones y devoluciones de ventas con más antigüedad que el plazo máximo, que el Administrador puede configurar (30 días por defecto), indicando que la venta excede el plazo.
- **FR-007**: El sistema DEBE permitir crear una nota de crédito con saldo igual al monto cancelado o devuelto, con folio único, y consultarla.
- **FR-007a**: El sistema DEBE imprimir al crear una nota de crédito un ticket con folio, saldo, fecha y venta de origen, y permitir reimprimirlo desde la consulta de notas de crédito.
- **FR-008**: El sistema DEBE permitir pagar una venta total o parcialmente con una nota de crédito, descontando el saldo usado y sin permitir saldo negativo.
- **FR-009**: El sistema DEBE permitir devoluciones parciales por línea y cantidad, validando que no se exceda lo vendido menos lo ya devuelto, y mostrar el monto recalculado antes de confirmar.
- **FR-010**: El sistema DEBE calcular el monto devuelto proporcionalmente a lo pagado por las unidades devueltas (incluyendo impuestos y descuentos) y nunca devolver más de lo pagado.
- **FR-011**: El sistema DEBE registrar la cancelación o devolución, el inventario restaurado y el reintegro o la nota de crédito en una única transacción atómica.
- **FR-012**: El sistema DEBE registrar en la bitácora de auditoría cada cancelación y devolución con usuario, fecha y hora, motivo, usuario que autorizó, venta, monto y tipo de compensación; los intentos de autorización fallidos también se registran sin el PIN.
- **FR-013**: Las cancelaciones y devoluciones DEBEN ser inmutables: no se editan ni eliminan; la venta original conserva su detalle y muestra su historial de eventos.
- **FR-014**: El sistema DEBE impedir cancelar una venta ya cancelada y devolver más de lo disponible, incluso con operaciones concurrentes.
- **FR-015**: Las ventas canceladas se excluyen de los totales de ventas y las devoluciones parciales se descuentan de ellos; las cifras de Inicio, "Turnos" y corte deben coincidir.
- **FR-016**: El corte de turno DEBE mostrar los reintegros en efectivo, los reintegros de tarjeta pendientes de reversa y las notas de crédito emitidas durante el turno.
- **FR-017**: El sistema DEBE funcionar sin conexión a internet.
- **FR-018**: El sistema DEBE permitir consultar los reintegros de tarjeta pendientes de reversa manual y marcarlos como reversados, quedando el cambio en la bitácora. Solo el Administrador puede listar notas de crédito y reintegros pendientes y marcarlos; el Cajero solo consulta el saldo de un vale por folio al cobrar.

### Entidades clave

- **Venta**: transacción original; conserva su detalle y adquiere estado (completada, parcialmente devuelta, totalmente devuelta, cancelada).
- **Devolución / cancelación**: evento inmutable ligado a una venta: tipo, motivo, usuario, autorizador, fecha, líneas y cantidades, monto y compensación.
- **Línea devuelta**: línea de la venta original, cantidad devuelta y monto proporcional.
- **Reintegro**: devolución de dinero por forma de pago original; el de tarjeta tiene estado pendiente o reversado.
- **Nota de crédito**: folio, saldo, saldo inicial, origen (devolución), fecha y usos.
- **Movimiento de inventario "Devolución por venta cancelada"**: regreso de existencias por producto.
- **Entrada de bitácora**: registro de auditoría del evento.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: Un operador completa una cancelación completa con autorización y compensación en menos de 1 minuto.
- **SC-002**: El 100 % de las cancelaciones y devoluciones tienen motivo, usuario, autorizador y fecha en la bitácora, y 0 pueden realizarse sin autorización.
- **SC-003**: Tras cualquier cancelación o devolución, las existencias de inventario coinciden al 100 % con el cálculo manual.
- **SC-004**: El efectivo esperado del turno coincide al 100 % con el cálculo manual en escenarios con reintegros en efectivo, tarjeta, pagos mixtos y notas de crédito.
- **SC-005**: La suma de saldos de notas de crédito más sus usos coincide al 100 % con lo emitido.
- **SC-006**: Ante una falla a mitad del proceso, 0 operaciones quedan parcialmente aplicadas.

## Supuestos

- Se reutilizan usuarios, roles, PIN, autorización de Administrador, bitácora, turnos de caja, movimientos de inventario y la pantalla "Consultar ventas".
- Ya existe cancelación completa de ventas (spec 005) y su efecto en el turno (spec 008); esta funcionalidad la amplía con autorización, compensación, devoluciones parciales y notas de crédito.
- La nota de crédito se identifica por un folio y no está ligada a un cliente específico; quien presenta el folio puede usarla. Sin vencimiento ni cobro de comisiones.
- La reversa de tarjeta se hace fuera del sistema, en la terminal bancaria; el sistema solo la anota y permite marcarla como realizada.
- La nota de crédito no puede convertirse en efectivo; cancelar una venta pagada con ella restituye el saldo.
- Fuera de alcance: cambios de producto como flujo propio, reversas automáticas con el banco, clientes registrados y reportes dedicados de devoluciones.
