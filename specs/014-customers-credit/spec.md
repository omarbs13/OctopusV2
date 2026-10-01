# Especificación de funcionalidad: Gestión de clientes y crédito

**Rama de funcionalidad**: `014-customers-credit`

**Creado**: 2026-10-01

**Estado**: Borrador

**Entrada**: Gestión de clientes y crédito: catálogo de clientes, límite de crédito, abonos y cuentas por cobrar. Registrar clientes de crédito, controlar su límite de crédito, registrar abonos y mantener un registro de cuentas por cobrar.

## Objetivo

Que el negocio pueda vender a crédito a clientes conocidos con un límite controlado, cobrar los saldos mediante abonos con recibo, y saber en todo momento quién debe, cuánto y desde cuándo.

## Conceptos

- **Cliente a crédito**: cliente con "crédito disponible" y un límite de crédito. Un cliente "solo efectivo" no puede recibir ventas a crédito.
- **Saldo pendiente**: suma de lo que el cliente debe por ventas a crédito, menos los abonos aplicados.
- **Venta a crédito**: venta registrada a nombre de un cliente, sin cobro inmediato, con estado "Pendiente de pago" hasta quedar saldada.
- **Abono**: pago parcial o total del saldo de un cliente, con forma de pago y referencia.
- **Vencido**: venta a crédito con saldo pendiente cuyo plazo de pago ya pasó. **Al límite**: cliente cuyo saldo pendiente alcanza o supera su límite de crédito. **Al día**: cliente sin saldo vencido.
- **Autorización**: aprobación de un Administrador mediante su contraseña, como en las specs 007 y 013.

## Clarifications

### Session 2026-10-01

- Q: ¿Una venta a crédito puede incluir un pago parcial inmediato (enganche) o siempre se registra al 100 % a crédito? → A: Siempre 100 % a crédito; un pago inmediato se registra como abono justo después.
- Q: Si se cancela o se devuelve una venta a crédito parcialmente abonada y lo abonado supera lo que queda por pagar, ¿qué se hace con el excedente? → A: Se aplica a las otras ventas pendientes del cliente (de la más antigua a la más reciente); si no queda deuda, se reintegra en efectivo desde el turno abierto, como un reintegro de la spec 013.
- Q: ¿Cómo se corrige un abono registrado por error, si los abonos no se pueden editar ni eliminar? → A: Un Administrador lo anula con su contraseña y un motivo. El abono queda "Anulado" y visible, se revierten sus aplicaciones (saldo y estado de las ventas) y, si fue en efectivo, se registra una salida de caja en el turno abierto; sin turno abierto no se puede anular.
- Q: ¿El Cajero puede dar de alta clientes y, si puede, qué límite de crédito y modalidad pueden quedar asignados? → A: Sí, pero siempre como "solo efectivo" con límite 0; solo el Administrador asigna crédito y edita el límite y la modalidad.
- Q: ¿Se pueden registrar abonos con formas de pago distintas al efectivo cuando no hay un turno de caja abierto? → A: No; cualquier abono, sea cual sea la forma de pago, requiere un turno abierto y se refleja en su corte.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Catálogo de clientes (Prioridad: P1)

El Administrador o el Cajero dan de alta clientes con nombre, teléfono, email (opcional) y RUC (opcional). El Administrador además asigna el límite de crédito y la modalidad ("crédito disponible" o "solo efectivo"); los clientes que da de alta un Cajero quedan como "solo efectivo" con límite 0 hasta que un Administrador les asigne crédito. Se pueden listar, buscar, editar y desactivar.

**Por qué esta prioridad**: sin clientes registrados no hay venta a crédito ni cuentas por cobrar; es la base de todo lo demás.

**Prueba independiente**: se puede probar creando, buscando, editando y desactivando clientes sin tocar el punto de venta.

**Escenarios de aceptación**:

1. **Dado** el formulario de alta, **cuando** se captura nombre, teléfono y límite de crédito válidos y se guarda, **entonces** el cliente aparece en el listado, activo.
2. **Dado** un listado con varios clientes, **cuando** se busca por parte del nombre, teléfono o RUC, **entonces** solo se muestran los que coinciden.
3. **Dado** un cliente existente, **cuando** se edita su límite de crédito o su modalidad ("crédito disponible" / "solo efectivo"), **entonces** el cambio se aplica a las siguientes ventas.
4. **Dado** un cliente con saldo pendiente, **cuando** se intenta desactivar, **entonces** el sistema lo impide e indica que tiene saldo pendiente.
5. **Dado** un cliente sin saldo pendiente, **cuando** se desactiva, **entonces** deja de aparecer en la selección del punto de venta y conserva su historial.
6. **Dado** un alta con nombre vacío, límite negativo, email con formato inválido o RUC duplicado, **cuando** se guarda, **entonces** se rechaza con un mensaje claro.
7. **Dado** un Cajero en el formulario de alta, **cuando** guarda un cliente nuevo, **entonces** el cliente queda como "solo efectivo" con límite 0 y el Cajero no puede cambiar el límite ni la modalidad.

---

### Historia 2 - Vender a crédito (Prioridad: P1)

En el punto de venta, el cajero selecciona un cliente con crédito disponible y registra la venta como "venta a crédito". El sistema verifica que saldo pendiente más el total de la venta no supere el límite; si lo supera, avisa y exige autorización de un Administrador.

**Por qué esta prioridad**: es el motivo de la funcionalidad; genera las cuentas por cobrar.

**Prueba independiente**: se prueba vendiendo a crédito a un cliente y verificando el estado de la venta y el nuevo saldo pendiente.

**Escenarios de aceptación**:

1. **Dado** un cliente con límite 1 000 y saldo 200, **cuando** se registra una venta a crédito de 500, **entonces** la venta queda "Pendiente de pago" y el saldo del cliente pasa a 700.
2. **Dado** un cliente con límite 1 000 y saldo 800, **cuando** se intenta una venta a crédito de 300, **entonces** el sistema avisa que excede el límite por 100 y no la registra sin autorización de un Administrador.
3. **Dado** el aviso de límite excedido, **cuando** un Administrador autoriza con su contraseña, **entonces** la venta se registra y la autorización queda en la bitácora; con contraseña incorrecta o cancelando, no se registra nada.
4. **Dado** un cliente "solo efectivo" o desactivado, **cuando** se intenta vender a crédito, **entonces** la opción no está disponible o se rechaza con un mensaje.
5. **Dado** una venta a crédito, **cuando** se confirma, **entonces** se descuenta inventario como en cualquier venta, se imprime el ticket con el nombre del cliente y el monto a crédito, y no se suma al efectivo esperado del turno.
6. **Dado** una falla a mitad del registro, **cuando** ocurre, **entonces** no queda ni venta ni saldo parcialmente aplicados.

---

### Historia 3 - Registrar abono (Prioridad: P1)

Desde "Clientes > [cliente] > Abonos", el usuario registra un abono con monto, forma de pago y referencia. El sistema descuenta el monto del saldo pendiente y genera un recibo imprimible.

**Por qué esta prioridad**: sin abonos el saldo solo crece; cierra el ciclo de cobro.

**Prueba independiente**: se prueba registrando un abono a un cliente con saldo y verificando saldo, estado de las ventas y recibo.

**Escenarios de aceptación**:

1. **Dado** un cliente con saldo 700, **cuando** se registra un abono de 300 en efectivo, **entonces** el saldo pasa a 400 y el abono aparece en su historial.
2. **Dado** un abono, **cuando** se aplica, **entonces** se asigna a las ventas a crédito más antiguas primero; las ventas que queden totalmente cubiertas pasan a "Pagada".
3. **Dado** un cliente con saldo 400, **cuando** se intenta abonar 500 o un monto cero o negativo, **entonces** se rechaza indicando el saldo máximo abonable.
4. **Dado** un abono en efectivo y un turno de caja abierto, **cuando** se registra, **entonces** se suma al efectivo esperado del turno; si no hay turno abierto, cualquier abono se rechaza, sea cual sea la forma de pago.
5. **Dado** un abono registrado, **cuando** se confirma, **entonces** se imprime un recibo con folio, fecha, cliente, monto, forma de pago, referencia, saldo anterior y saldo nuevo, y puede reimprimirse desde el historial.
6. **Dado** un abono registrado, **cuando** se consulta, **entonces** es inmutable: no se edita ni elimina.
7. **Dado** un abono registrado por error, **cuando** un Administrador lo anula con su contraseña y un motivo, **entonces** el abono queda "Anulado" y visible en el historial, el saldo del cliente y el estado de las ventas a las que se aplicó vuelven a como estaban (una venta "Pagada" vuelve a "Pendiente de pago"), y, si fue en efectivo, se registra una salida de caja en el turno abierto; sin turno abierto, o con contraseña incorrecta, no se anula.

---

### Historia 4 - Reporte de cuentas por cobrar (Prioridad: P2)

En "Reportes > Créditos", el administrador ve por cliente su saldo pendiente, fecha del último abono y días vencido, puede filtrar por estado (al día, vencido, al límite) y ve los totales acumulados.

**Por qué esta prioridad**: aporta visibilidad y cobranza, pero depende de que ya existan ventas a crédito y abonos.

**Prueba independiente**: se prueba con clientes en distintos estados y verificando filas, filtros y totales.

**Escenarios de aceptación**:

1. **Dado** clientes con saldo, **cuando** se abre el reporte, **entonces** se listan solo los clientes con saldo pendiente con sus datos actuales (saldo, último abono, días vencido).
2. **Dado** el reporte, **cuando** se filtra por "vencido", "al día" o "al límite", **entonces** solo se muestran los clientes de ese estado.
3. **Dado** el reporte, **cuando** se muestra, **entonces** los totales acumulados (saldo total pendiente y número de clientes con saldo) coinciden con la suma de las filas visibles.
4. **Dado** un abono o venta a crédito recién registrados, **cuando** se actualiza el reporte, **entonces** refleja los cambios sin intervención adicional.

---

### Casos límite

- Venta a crédito cuyo total exactamente iguala el crédito disponible: se permite sin autorización (la regla es "≤ límite").
- Reducir el límite de un cliente por debajo de su saldo actual: se permite; no se puede vender más a crédito hasta que el saldo baje, y aparece "al límite".
- Cancelación o devolución (spec 013) de una venta a crédito pendiente: reduce el saldo pendiente del cliente en lugar de generar reintegro de dinero; si la venta ya estaba parcialmente abonada y lo abonado supera lo que queda por pagar, el excedente se aplica a las otras ventas pendientes del cliente (de la más antigua a la más reciente) y, si no queda deuda, se reintegra en efectivo desde el turno abierto como un reintegro de la spec 013. No existen notas de crédito ni saldo a favor.
- Abonos concurrentes o doble clic en "Registrar": el saldo nunca queda negativo ni se duplica el abono.
- Cliente con ventas vencidas que intenta una nueva venta a crédito: se avisa pero no se bloquea (el límite es la única regla de bloqueo).
- Impresora no disponible al registrar un abono: el abono se guarda y el recibo puede reimprimirse después.
- Cliente sin teléfono o con RUC repetido: el teléfono es obligatorio; el RUC, si se captura, debe ser único.
- Anulación de un abono aplicado a una venta que después se canceló o devolvió: se rechaza con un mensaje que lo explica, porque lo abonado ya se reaplicó o reintegró en esa devolución.
- Anulación de un abono en efectivo cuando el efectivo esperado del turno no alcanza: se rechaza sin revelar el monto esperado (regla de la spec 008).
- Licencia "Crédito y clientes" vencida o retirada con saldos pendientes: los clientes, ventas a crédito y abonos se conservan, pero no se pueden registrar ni anular abonos hasta reactivar la licencia; las cancelaciones y devoluciones de ventas a crédito siguen ajustando el saldo (FR-016).

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema DEBE permitir dar de alta clientes con nombre y teléfono obligatorios, email y RUC opcionales, límite de crédito (≥ 0) y modalidad "crédito disponible" o "solo efectivo".
- **FR-002**: El sistema DEBE validar el formato de email, que el RUC sea único cuando se capture, y rechazar nombres vacíos y límites negativos.
- **FR-003**: El sistema DEBE permitir listar, buscar (por nombre, teléfono o RUC), editar y desactivar clientes; los clientes desactivados se conservan con su historial y no se ofrecen en el punto de venta.
- **FR-004**: El sistema DEBE impedir desactivar un cliente con saldo pendiente.
- **FR-005**: El punto de venta DEBE permitir seleccionar un cliente activo con crédito disponible y registrar la venta como "venta a crédito", dejándola en estado "Pendiente de pago". Una venta a crédito es siempre por el 100 % de su total: no admite enganche ni pago mixto contado + crédito; cualquier pago inmediato se registra como abono después de la venta.
- **FR-006**: Antes de registrar una venta a crédito, el sistema DEBE verificar que saldo pendiente + total de la venta ≤ límite de crédito; si no, DEBE avisar del excedente y exigir autorización de un Administrador (contraseña) para continuar. Si quien vende ya es Administrador, no se le vuelve a pedir la contraseña: la venta se registra y la bitácora lo anota a él mismo como autorizador.
- **FR-007**: Toda autorización por exceso de límite DEBE registrarse en la bitácora con cajero, autorizador, cliente, venta, saldo previo, límite y monto; los intentos fallidos también, sin la contraseña.
- **FR-008**: Una venta a crédito DEBE seguir las reglas normales de venta (inventario, impuestos, ticket) y NO DEBE sumarse al efectivo esperado del turno ni a los cobros de la caja.
- **FR-009**: El sistema DEBE permitir registrar abonos desde "Clientes > [cliente] > Abonos" con monto, forma de pago y referencia (opcional), usando las formas de pago existentes; no se permite pagar un abono con crédito del mismo cliente.
- **FR-010**: El sistema DEBE rechazar abonos de monto cero, negativo o mayor al saldo pendiente.
- **FR-011**: El sistema DEBE aplicar cada abono a las ventas a crédito pendientes de la más antigua a la más reciente, marcando como "Pagada" las que queden saldadas, y descontar el monto del saldo pendiente del cliente.
- **FR-012**: Todo abono, sea cual sea la forma de pago, DEBE registrarse dentro de un turno de caja abierto y se rechaza si no lo hay. Excepción: si el módulo "Turnos de caja" no tiene licencia, no hay turnos que controlar y el abono se registra sin turno, igual que las ventas (spec 012). Los abonos en efectivo se suman al efectivo esperado y al corte del turno; los abonos con otras formas de pago se muestran por separado en el corte de ese turno.
- **FR-013**: El sistema DEBE generar un recibo imprimible de cada abono con folio único, fecha, cliente, monto, forma de pago, referencia, saldo anterior y saldo nuevo, y permitir reimprimirlo desde el historial de abonos.
- **FR-014**: Los abonos DEBEN ser inmutables; un abono erróneo solo se corrige anulándolo: un Administrador lo autoriza con su contraseña e indica un motivo; el abono pasa a "Anulado" y se conserva visible; se revierten de forma atómica sus aplicaciones (el saldo pendiente sube y las ventas afectadas vuelven a "Pendiente de pago"); si fue en efectivo, se registra una salida de caja en el turno abierto (que se rechaza si el efectivo esperado del turno no alcanza, sin revelar el monto), y sin turno abierto no se puede anular; si el módulo "Turnos de caja" no tiene licencia, se anula sin turno, como en FR-012. No se puede anular un abono si alguna de las ventas a las que se aplicó se canceló o devolvió después del abono. La anulación (quién, cuándo, motivo, abono) y los intentos fallidos quedan en la bitácora.
- **FR-015**: El sistema DEBE calcular el saldo pendiente de un cliente de forma consistente en todas las pantallas (ficha de cliente, punto de venta, reporte).
- **FR-016**: Las cancelaciones y devoluciones de ventas a crédito (spec 013) DEBEN reducir el saldo pendiente; el excedente de lo ya abonado sobre la venta se reaplica a las otras ventas pendientes del cliente de la más antigua a la más reciente y, si no queda deuda, se reintegra en efectivo desde el turno abierto (con las reglas de reintegro de la spec 013), en una sola operación atómica.
- **FR-017**: El sistema DEBE ofrecer "Reportes > Créditos" con, por cliente con saldo: nombre, saldo pendiente, límite, fecha del último abono y días vencido, con filtro por estado (al día, vencido, al límite) y totales acumulados.
- **FR-018**: Un cliente está "vencido" si tiene alguna venta a crédito con saldo pendiente que supera el plazo de pago (por defecto 30 días desde la venta, configurable por el Administrador); "días vencido" es el atraso de la venta más antigua pendiente.
- **FR-019**: Todas las operaciones que modifican varios registros (venta a crédito, abono con su aplicación y movimiento de caja, anulación de abono, cancelación o devolución de venta a crédito) DEBEN ser atómicas.
- **FR-020**: El acceso a clientes, ventas a crédito y abonos DEBE respetar los roles (el Cajero puede dar de alta clientes, siempre como "solo efectivo" con límite 0, vender a crédito y registrar abonos; solo el Administrador asigna o edita el límite y la modalidad, desactiva clientes, anula abonos y ve el reporte), y el módulo depende de la licencia "Crédito y clientes".
- **FR-021**: El sistema DEBE funcionar sin conexión a internet.

### Entidades clave

- **Cliente**: nombre, teléfono, email, RUC, límite de crédito, modalidad (crédito disponible / solo efectivo), activo, saldo pendiente derivado.
- **Venta a crédito**: venta ligada a un cliente, con monto, saldo pendiente y estado (Pendiente de pago, Pagada, Cancelada).
- **Abono**: folio, cliente, monto, forma de pago, referencia, fecha, usuario, saldo anterior y nuevo, estado (Vigente / Anulado) y, si se anuló, quién, cuándo y motivo; inmutable salvo la anulación.
- **Aplicación de abono**: parte de un abono asignada a una venta a crédito concreta.
- **Autorización de excedente**: evento de bitácora con quién autorizó la venta que superó el límite.
- **Recibo de abono**: comprobante imprimible del abono.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: Un cajero da de alta un cliente en menos de 1 minuto y lo encuentra por búsqueda en menos de 5 segundos.
- **SC-002**: Un cajero registra una venta a crédito, incluida la selección del cliente, en menos de 30 segundos más que una venta de contado.
- **SC-003**: El 100 % de las ventas a crédito que superan el límite requieren autorización de Administrador y quedan en la bitácora; 0 se registran sin ella.
- **SC-004**: Tras cualquier secuencia de ventas a crédito, abonos y cancelaciones, el saldo pendiente de cada cliente coincide al 100 % con el cálculo manual y nunca es negativo.
- **SC-005**: Un abono se registra y su recibo se imprime en menos de 1 minuto.
- **SC-006**: Los totales de "Reportes > Créditos" coinciden al 100 % con la suma de saldos de las fichas de clientes.
- **SC-007**: El efectivo esperado del turno coincide al 100 % con el cálculo manual en escenarios con ventas a crédito y abonos en efectivo.
- **SC-008**: Ante una falla a mitad del proceso, 0 operaciones quedan parcialmente aplicadas.

## Supuestos

- Se reutilizan usuarios, roles, autorización de Administrador, bitácora, turnos de caja, formas de pago, impresión de tickets, "Consultar ventas" y el módulo licenciado "Crédito y clientes" (specs 005, 006, 007, 008, 012, 013).
- La moneda y la gestión de dinero son las del sistema actual; sin intereses, recargos por mora ni comisiones.
- El plazo de pago es único para todo el negocio (30 días por defecto, configurable); no hay plazos distintos por cliente.
- Los abonos se aplican a las ventas más antiguas primero; el usuario no elige a qué venta se aplican.
- Un cliente es una persona o negocio sin cuenta de acceso al sistema; no hay portal ni estados de cuenta enviados por correo.
- Las ventas existentes y las de contado no requieren cliente; se puede asociar un cliente solo en ventas a crédito.
- Fuera de alcance: intereses y mora, límites distintos por producto, pagos con tarjeta de crédito del negocio, estados de cuenta por correo o WhatsApp, cobranza automática, importación masiva de clientes y programas de lealtad.
