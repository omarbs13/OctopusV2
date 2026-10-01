# Especificación de funcionalidad: Descuentos y promociones

**Rama de funcionalidad**: `015-discounts-promotions`

**Creado**: 2026-10-01

**Estado**: Borrador

**Entrada**: Descuentos y promociones: descuentos por línea, venta total y cupones. Aplicar descuentos a productos o ventas, controlados y auditados.

## Objetivo

Que el cajero pueda aplicar descuentos a una línea, a la venta completa o mediante un cupón, con cálculo exacto en centavos, con autorización de un Administrador cuando el descuento supera el límite permitido, y con un rastro que indique quién aplicó cada descuento y quién lo autorizó.

## Conceptos

- **Descuento por línea**: reducción sobre el importe de una línea de la venta (cantidad × precio), expresada como porcentaje o como monto fijo.
- **Descuento global**: reducción sobre el subtotal de la venta, después de los descuentos por línea, expresada como porcentaje o como monto fijo.
- **Cupón**: código registrado por el Administrador que otorga un descuento global (porcentaje o monto fijo), con vigencia y límite de usos.
- **Límite de descuento**: porcentaje máximo que un Cajero puede aplicar sin autorización. Lo configura el Administrador; 10 % por defecto.
- **Porcentaje equivalente**: para comparar con el límite, un descuento de monto fijo se expresa como porcentaje del importe sobre el que se aplica (importe de la línea o subtotal de la venta).
- **Autorización**: aprobación de un Administrador mediante su contraseña, como en las specs 007, 013 y 014. En esta especificación "PIN" es esa contraseña; no se crea un PIN nuevo.

## Clarifications

### Session 2026-10-01

- Q: ¿Se pueden combinar un cupón y un descuento global manual en la misma venta? → A: No, son excluyentes; al aplicar uno cuando ya existe el otro, el sistema ofrece reemplazarlo.
- Q: ¿Los descuentos y cupones son parte de la venta básica o un módulo licenciado? → A: Son un módulo licenciado nuevo, "Descuentos y promociones", dentro de la licencia modular de la spec 012, y abarca descuento por línea, descuento global, cupones y reporte de descuentos.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Descuento por línea (Prioridad: P2)

En el punto de venta, el cajero selecciona una línea y le aplica un descuento en porcentaje o en monto fijo. Si el porcentaje equivalente supera el límite de descuento, se pide la autorización de un Administrador. La línea muestra el importe original tachado y el importe final.

**Por qué esta prioridad**: es el descuento más frecuente en mostrador (producto dañado, cliente frecuente, precio de liquidación) y hoy no existe una forma controlada de hacerlo.

**Prueba independiente**: se puede probar agregando un producto a la venta, aplicando un descuento de línea dentro y fuera del límite, cobrando y verificando el total, el ticket y la bitácora.

**Escenarios de aceptación**:

1. **Dado** una línea de 2 × 50.00 (importe 100.00) y un límite de 10 %, **cuando** el cajero aplica 10 %, **entonces** la línea muestra 100.00 tachado y 90.00 como importe final, sin pedir autorización.
2. **Dado** la misma línea, **cuando** el cajero aplica un monto fijo de 15.00 (15 %), **entonces** el sistema exige autorización de un Administrador antes de aplicarlo.
3. **Dado** la solicitud de autorización, **cuando** un Administrador captura su contraseña correcta, **entonces** el descuento se aplica y queda registrado con el cajero que lo aplicó y el Administrador que lo autorizó; con contraseña incorrecta o al cancelar, la línea queda sin cambios.
4. **Dado** que quien vende es Administrador, **cuando** aplica un descuento que supera el límite, **entonces** no se le pide contraseña y queda registrado como aplicador y autorizador.
5. **Dado** una línea de 3 × 33.33 (importe 99.99), **cuando** se aplica 15 %, **entonces** el descuento es 15.00 (14.9985 redondeado a centavos, mitad alejándose de cero) y el importe final 84.99.
6. **Dado** una línea con descuento, **cuando** el cajero lo quita, **entonces** la línea vuelve a su importe original sin pedir autorización.
7. **Dado** un descuento de monto fijo mayor que el importe de la línea, o un porcentaje menor o igual a 0 o mayor a 100, **cuando** se intenta aplicar, **entonces** se rechaza con un mensaje claro.

---

### Historia 2 - Descuento en la venta total (Prioridad: P2)

Con las líneas ya capturadas, el cajero aplica un descuento global en porcentaje o monto fijo sobre el subtotal (ya descontados los descuentos por línea). Se aplica el mismo control de autorización que en la Historia 1, comparando contra el subtotal.

**Por qué esta prioridad**: cubre negociaciones sobre la compra completa ("te dejo todo en 500") sin repartir el descuento a mano entre las líneas.

**Prueba independiente**: se puede probar con una venta de varias líneas sin descuentos de línea, aplicando un descuento global dentro y fuera del límite y verificando el total cobrado y el ticket.

**Escenarios de aceptación**:

1. **Dado** una venta con subtotal 200.00 y límite de 10 %, **cuando** el cajero aplica un descuento global de 20.00, **entonces** el total queda en 180.00 sin pedir autorización.
2. **Dado** la misma venta, **cuando** el cajero aplica 25 %, **entonces** se exige autorización de un Administrador, con el mismo comportamiento que en la Historia 1.
3. **Dado** una venta con descuentos de línea, **cuando** se aplica un descuento global en porcentaje, **entonces** se calcula sobre el subtotal ya descontado, no sobre los importes originales.
4. **Dado** una venta con descuento global en porcentaje, **cuando** se agrega, quita o modifica una línea, **entonces** el descuento global se recalcula sobre el nuevo subtotal.
5. **Dado** un descuento global de monto fijo, **cuando** el subtotal baja por debajo de ese monto, **entonces** el sistema avisa y retira el descuento global; el cajero debe volver a aplicarlo.
6. **Dado** un descuento global aplicado, **cuando** se cobra, **entonces** el descuento se reparte entre las líneas en proporción a su importe, de modo que la suma de lo repartido es exactamente el descuento global.

---

### Historia 3 - Cupones (Prioridad: P2)

El Administrador registra cupones con código, tipo (porcentaje o monto fijo), valor, vigencia (fecha de inicio y de fin) y límite de usos opcional. En el punto de venta, al escanear o capturar el código del cupón en el mismo campo donde se capturan los productos, el descuento se aplica automáticamente a la venta. Un cupón inexistente, inactivo, fuera de vigencia o agotado se rechaza con un mensaje que indica la causa.

**Por qué esta prioridad**: permite promociones planeadas (volantes, redes sociales) sin que el cajero decida el monto ni pida autorización en cada venta.

**Prueba independiente**: se puede probar creando un cupón, aplicándolo en una venta, cobrando, y verificando que el contador de usos aumenta y que un cupón vencido o agotado se rechaza.

**Escenarios de aceptación**:

1. **Dado** el listado de cupones, **cuando** el Administrador registra un cupón "VERANO10" de 10 % vigente del 1 al 31 de octubre con 100 usos, **entonces** aparece en el listado con su vigencia, usos realizados (0) y usos restantes (100).
2. **Dado** un cupón vigente, **cuando** el cajero escanea o captura su código en la venta, **entonces** el descuento se aplica automáticamente y la venta muestra el código y el monto descontado.
3. **Dado** un cupón aplicado, **cuando** se cobra la venta, **entonces** el contador de usos del cupón aumenta en uno.
4. **Dado** un código que no existe, un cupón desactivado, un cupón cuya vigencia no ha empezado o ya terminó, o un cupón que alcanzó su límite de usos, **cuando** se captura, **entonces** se rechaza con un mensaje que indica la causa y la venta no cambia.
5. **Dado** un cupón aplicado a una venta conservada, **cuando** la venta se retoma y se cobra después de que el cupón venció o se agotó, **entonces** el sistema retira el cupón, avisa al cajero y recalcula el total antes de cobrar.
6. **Dado** un cupón de monto fijo mayor que el subtotal, **cuando** se aplica, **entonces** el descuento se limita al subtotal y el total no queda negativo.
7. **Dado** un cupón, **cuando** se aplica, **entonces** no se pide autorización aunque supere el límite de descuento, porque el Administrador lo autorizó al crearlo.
8. **Dado** un cupón con usos registrados, **cuando** el Administrador intenta cambiar su código, tipo o valor, **entonces** se rechaza; solo puede cambiar la vigencia, el límite de usos o desactivarlo.

---

### Historia 4 - Auditoría y desglose (Prioridad: P3)

Cada descuento queda registrado con su tipo, valor, monto, quién lo aplicó y, si aplica, quién lo autorizó. Los descuentos aparecen desglosados en el ticket, en el detalle de la venta y en los reportes.

**Por qué esta prioridad**: los descuentos son una salida de dinero; sin trazabilidad no se pueden detectar abusos, pero el control preventivo (autorización) ya está cubierto por las historias anteriores.

**Prueba independiente**: se puede probar realizando ventas con distintos descuentos y verificando el ticket, el detalle de la venta, la bitácora y el reporte de descuentos.

**Escenarios de aceptación**:

1. **Dado** una venta cobrada con descuentos, **cuando** se imprime el ticket, **entonces** cada línea con descuento muestra el importe original, el descuento y el importe final; el descuento global y el cupón (con su código) aparecen antes del total, junto con el total ahorrado.
2. **Dado** una venta con descuentos, **cuando** se consulta en "Consultar ventas", **entonces** se ve cada descuento con tipo, valor, monto, usuario que lo aplicó y usuario que lo autorizó.
3. **Dado** una autorización de descuento correcta o fallida, **cuando** se consulta la bitácora, **entonces** aparece una entrada con cajero, autorizador (si lo hubo), venta, tipo y monto del descuento; los intentos fallidos no revelan la contraseña.
4. **Dado** un período, **cuando** el Administrador abre el reporte de descuentos, **entonces** ve el total descontado y el detalle por venta, filtrable por cajero y por tipo (línea, global, cupón), y el total de descuentos se refleja en el reporte de ventas.
5. **Dado** el alta, la edición o la desactivación de un cupón, o un cambio del límite de descuento, **cuando** se consulta la bitácora, **entonces** aparece quién lo hizo y cuándo.

---

### Casos límite

- Cambio de cantidad en una línea con descuento: el porcentaje se recalcula sobre el nuevo importe; un monto fijo se conserva y, si supera el nuevo importe, el cambio se rechaza hasta ajustar o quitar el descuento. Si el cambio hace que el porcentaje equivalente supere el límite, se pide autorización de nuevo.
- Descuento exactamente igual al límite: se permite sin autorización (la regla es "≤ límite").
- Descuento del 100 % de una línea o de la venta: se permite con autorización si supera el límite; una venta de total 0 se puede cobrar sin pago.
- Cupón y descuento global en la misma venta: son excluyentes. Si ya hay un descuento global y se captura un cupón válido (o al revés), el sistema pregunta si se reemplaza; si el cajero no confirma, la venta no cambia. Reemplazar por un descuento global que supere el límite exige autorización.
- Módulo "Descuentos y promociones" sin licencia (evaluación vencida o módulo no comprado): no se ofrecen descuentos, cupones, gestión de cupones, límite de descuento ni reporte de descuentos; un código de cupón capturado se trata como producto no encontrado. Una venta conservada con descuentos se retoma sin ellos, con un aviso al cajero. Las ventas ya cobradas conservan y muestran sus descuentos (ticket, reimpresión, "Consultar ventas", total descontado del reporte de ventas), y las devoluciones y cancelaciones siguen usando los importes descontados. La cancelación de una venta con cupón no devuelve el uso mientras no haya licencia.
- Un segundo cupón en la misma venta: se rechaza; se permite un cupón por venta.
- Código capturado que coincide con un producto y con un cupón: se trata como producto. No se permite registrar un cupón cuyo código coincida con el código de barras o la clave de un producto existente.
- Códigos de cupón: se comparan sin distinguir mayúsculas de minúsculas ni espacios al inicio o al final.
- Vigencia: se evalúa en la fecha local del equipo; el día de inicio y el de fin son válidos completos.
- Límite de usos: dos ventas cobradas casi al mismo tiempo con el último uso disponible; solo una se cobra con el cupón y la otra recibe el aviso de cupón agotado.
- Cancelación completa de una venta con cupón (spec 013): el uso se devuelve al cupón. Una devolución parcial no devuelve el uso.
- Devoluciones (spec 013): el monto a devolver por las unidades devueltas se calcula con lo efectivamente pagado, es decir, neto de los descuentos de línea y de la parte repartida del descuento global o del cupón.
- Venta a crédito (spec 014): la verificación del límite de crédito usa el total ya descontado.
- Cambio del límite de descuento mientras hay ventas en curso: al cobrar, los descuentos se revalidan con el límite vigente; uno que ahora lo supere y no tenga autorización exige autorizarlo antes de cobrar. Los ya autorizados siguen válidos.
- Venta conservada (spec 007): conserva sus descuentos y autorizaciones; los cupones se revalidan al cobrar.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema DEBE permitir aplicar a cada línea de la venta en curso un descuento en porcentaje (mayor que 0 y hasta 100, con hasta 2 decimales) o en monto fijo (mayor que 0 y no mayor que el importe de la línea). Cada línea admite un solo descuento.
- **FR-002**: El sistema DEBE permitir aplicar a la venta en curso un descuento global en porcentaje o monto fijo, calculado sobre el subtotal después de los descuentos por línea. Un monto fijo mayor que el subtotal se rechaza al aplicarlo. La venta admite un solo descuento global.
- **FR-003**: Todo monto de descuento DEBE calcularse en centavos y redondearse con la regla de la spec 005 (mitad alejándose de cero). El total de la venta nunca puede ser negativo. Un descuento que redondea a $0.00 se rechaza al aplicarlo.
- **FR-004**: Al cobrar, el descuento global y el del cupón DEBEN repartirse entre las líneas en proporción a su importe después del descuento de línea, de forma que la suma repartida sea exactamente el descuento aplicado (el residuo de redondeo se asigna por resto mayor y, en empate, por orden de captura).
- **FR-005**: El Administrador DEBE poder configurar el límite de descuento (porcentaje entre 0 y 100; 10 % por defecto). Si el porcentaje equivalente de un descuento por línea o global supera el límite, el sistema DEBE exigir autorización de un Administrador antes de aplicarlo. Si quien opera es Administrador, no se le pide contraseña y queda registrado como autorizador.
- **FR-006**: Sin autorización válida (contraseña incorrecta o cancelación) el descuento NO DEBE aplicarse. Los intentos fallidos cuentan para el bloqueo de la spec 007 (FR-005).
- **FR-007**: El cajero DEBE poder quitar o reemplazar un descuento antes de cobrar sin autorización; reemplazarlo por uno que supere el límite exige autorización.
- **FR-008**: En el punto de venta, cada línea con descuento DEBE mostrar el importe original tachado y el importe final; la venta DEBE mostrar subtotal, descuento global o de cupón y total.
- **FR-009**: El Administrador DEBE poder registrar, listar, buscar, editar y desactivar cupones con: código único, tipo (porcentaje o monto fijo), valor, fecha de inicio, fecha de fin y límite de usos opcional (sin límite si se deja vacío). El listado DEBE mostrar vigencia, estado (vigente, por iniciar, vencido, agotado, inactivo), usos realizados y usos restantes.
- **FR-010**: Un cupón con usos registrados NO DEBE permitir cambiar su código, tipo ni valor; solo su vigencia, su límite de usos (no menor que los usos realizados) o su estado. Los cupones no se borran físicamente.
- **FR-011**: Al escanear o capturar un código en el campo de productos del punto de venta, si no corresponde a un producto y sí a un cupón, el sistema DEBE validarlo y aplicarlo automáticamente como descuento de la venta. También DEBE poder capturarse desde una opción "Aplicar cupón".
- **FR-012**: El sistema DEBE rechazar un cupón inexistente, inactivo, fuera de vigencia o agotado, con un mensaje que indique la causa, sin modificar la venta. Al cobrar DEBE revalidar el cupón; si ya no es válido, lo retira, avisa y recalcula el total antes de cobrar.
- **FR-013**: Una venta admite como máximo un cupón. El cupón y el descuento global manual son excluyentes: al aplicar uno cuando ya existe el otro, el sistema DEBE pedir confirmación para reemplazarlo. Aplicar un cupón no requiere autorización ni se compara con el límite de descuento.
- **FR-014**: El uso de un cupón DEBE contarse al cobrar la venta, de forma atómica con la venta, de modo que nunca se superen los usos permitidos. La cancelación completa de la venta (spec 013) DEBE devolver el uso.
- **FR-015**: Cada descuento registrado en una venta DEBE conservar: tipo (línea, global o cupón), modalidad (porcentaje o monto), valor capturado, monto descontado en centavos, código del cupón si aplica, usuario que lo aplicó, usuario que lo autorizó si aplica, y fecha y hora. Esta información es inmutable una vez cobrada la venta.
- **FR-016**: El ticket DEBE mostrar por línea con descuento el importe original, el descuento y el importe final; y antes del total, el descuento global o el cupón (con su código) y el total ahorrado.
- **FR-017**: "Consultar ventas" DEBE mostrar los descuentos de cada venta con los datos de FR-015.
- **FR-018**: El reporte de ventas (spec 009) DEBE incluir el total descontado del período. El sistema DEBE ofrecer un reporte de descuentos por período con total descontado y detalle por venta (folio, fecha, cajero, tipo, valor, monto, autorizador, cupón), filtrable por cajero y tipo, y exportable como los demás reportes.
- **FR-019**: El sistema DEBE registrar en la bitácora: las autorizaciones de descuento (cajero, autorizador, venta, tipo y monto), los intentos fallidos sin la contraseña, el alta, edición y desactivación de cupones, y los cambios del límite de descuento.
- **FR-020**: Los cálculos de devoluciones y cancelaciones (spec 013) y la verificación de límite de crédito (spec 014) DEBEN usar los importes ya descontados.
- **FR-021**: El acceso DEBE respetar los roles: el Cajero aplica descuentos (con autorización si superan el límite) y cupones; solo el Administrador gestiona cupones, configura el límite de descuento y ve el reporte de descuentos.
- **FR-022**: Toda la funcionalidad (descuento por línea, descuento global, cupones, límite de descuento y reporte de descuentos) DEBE depender de un módulo licenciado nuevo, "Descuentos y promociones", que se agrega a la licencia modular de la spec 012 con su propio identificador opaco y queda incluido en el período de evaluación. Sin licencia, estas opciones NO DEBEN mostrarse ni poder invocarse, y la venta básica DEBE seguir operando sin descuentos, como define la spec 012 para módulos bloqueados.
- **FR-023**: Los datos históricos de descuentos DEBEN conservarse y mostrarse en ventas ya cobradas aunque el módulo pierda la licencia.

### Entidades clave

- **Descuento de venta**: descuento aplicado a una línea o a la venta completa; tipo, modalidad, valor, monto, cupón (si aplica), aplicador, autorizador, fecha. Pertenece a una venta y, si es por línea, a una línea.
- **Cupón**: código, tipo, valor, fecha de inicio, fecha de fin, límite de usos, usos realizados, estado activo/inactivo.
- **Configuración de descuentos**: límite de descuento sin autorización (porcentaje).
- **Línea de venta** (existente, spec 005): se amplía con importe original, descuento propio, parte repartida del descuento global y importe final.
- **Entrada de bitácora** (existente, spec 007): autorizaciones de descuento y cambios de cupones y configuración.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: El 100 % de los descuentos calculados coincide al centavo con el cálculo manual usando la regla de redondeo documentada, y la suma de los importes finales de las líneas es igual al total de la venta.
- **SC-002**: El 100 % de los descuentos que superan el límite tienen un autorizador registrado; 0 se aplican sin autorización.
- **SC-003**: El 100 % de las ventas con descuento muestran el desglose en el ticket, en "Consultar ventas" y en el reporte de descuentos, y el total descontado del reporte coincide con la suma de los descuentos de las ventas del período.
- **SC-004**: El 100 % de los cupones inexistentes, inactivos, vencidos o agotados se rechazan, y ningún cupón supera su límite de usos.
- **SC-005**: Un cajero aplica un descuento por línea o global dentro del límite en menos de 10 segundos, y un cupón escaneado se aplica en menos de 2 segundos.

## Supuestos

- La autorización reutiliza la de Administrador con contraseña de las specs 007, 013 y 014; "PIN" en la descripción se refiere a esa contraseña.
- Las ventas no tienen impuestos por línea en esta fase (spec 005); si existieran, el descuento se aplicaría antes de impuestos.
- El límite de descuento es único para todos los Cajeros; límites por usuario, por producto o por categoría quedan fuera de alcance.
- Los cupones aplican a la venta completa; cupones por producto o categoría, compra mínima, límite de usos por cliente y generación masiva de códigos quedan fuera de alcance.
- Quedan fuera de alcance las promociones automáticas (2×1, precio por volumen, combos, descuentos por horario) y los programas de lealtad.
- El orden de cálculo es: descuentos por línea, después descuento global o de cupón sobre el subtotal resultante.
- El motivo del descuento no es obligatorio en esta fase.
- Se reutilizan venta, venta conservada, ticket, "Consultar ventas", reportes, bitácora, cancelaciones y devoluciones, crédito y licencia modular (specs 005, 006, 007, 009, 012, 013, 014).
