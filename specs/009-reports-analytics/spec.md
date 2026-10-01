# Especificación de funcionalidad: Reportes y análisis

**Rama de funcionalidad**: `009-reports-analytics`

**Creado**: 2026-09-30

**Estado**: Borrador

**Entrada**: Reportes y análisis: dashboards de ventas, arqueo de caja e inventario, exportables a PDF.

## Objetivo

Dar al Administrador y, cuando corresponda, al Cajero una visión clara del negocio y del estado de la caja para tomar decisiones: cuánto se vende y cómo se cobra, si el efectivo de los turnos cuadra y cómo está el inventario. Los reportes se pueden llevar a un documento imprimible.

## Conceptos

- **Reporte**: pantalla de solo lectura que resume información del negocio para un período.
- **Período**: rango de fechas elegido por el usuario, en la zona horaria local.
- **Período anterior**: rango de igual duración que termina justo antes de que empiece el período elegido.
- **Diferencia de arqueo**: efectivo contado menos efectivo esperado de un turno cerrado; su porcentaje es la diferencia entre el efectivo esperado.
- **Umbral de alerta**: porcentaje de diferencia a partir del cual un turno se marca como alerta; 5 % por defecto.

## Clarifications

### Session 2026-09-30

- Q: ¿Qué datos de su turno abierto ve el Cajero en "Caja > Mi turno"? → A: Fondo inicial, ventas, ingresos y retiros; efectivo esperado, contado y diferencia solo con el turno cerrado. Esto modifica lo decidido en 008, donde el Cajero solo veía hora de apertura, número de ventas y total vendido.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia 1 - Dashboard de ventas por período (Prioridad: P1)

El Administrador abre "Reportes > Ventas", elige un período (hoy, ayer, últimos 7 días, este mes, mes anterior o rango personalizado) y ve tarjetas con total vendido, cantidad de ventas, ticket promedio y totales en efectivo, tarjeta y transferencia; una gráfica de línea de ventas por día y una tabla de detalle de ventas ordenable por folio, fecha, cajero y total, filtrable por cajero. Puede activar el comparativo con el período anterior para ver la variación porcentual del total vendido. Las ventas canceladas no se incluyen.

**Por qué esta prioridad**: es la pregunta más frecuente del dueño del negocio: cuánto se vendió y cómo se cobró.

**Prueba independiente**: con ventas de varios días, cajeros y formas de pago (y alguna cancelada), abrir el reporte, elegir un período y comprobar que tarjetas, gráfica y tabla coinciden con las ventas completadas de ese período.

**Escenarios de aceptación**:

1. **Given** ventas completadas y una cancelada en el período, **When** el Administrador abre el reporte, **Then** las tarjetas y la tabla no incluyen la cancelada y el ticket promedio es total vendido entre cantidad de ventas.
2. **Given** ventas pagadas en efectivo, tarjeta y transferencia, **When** se consulta el período, **Then** los totales por forma de pago suman el total vendido.
3. **Given** un período elegido y el comparativo activado, **When** el total del período anterior es X y el del actual es Y, **Then** se muestran X, Y y la variación (Y − X) / X en porcentaje.
4. **Given** un período anterior sin ventas, **When** se activa el comparativo, **Then** la variación se muestra como no calculable (no como error ni infinito).
5. **Given** el filtro por un cajero, **When** se aplica, **Then** tarjetas, gráfica y tabla reflejan solo sus ventas.
6. **Given** un usuario con rol Cajero, **When** intenta abrir "Reportes > Ventas", **Then** no ve la opción ni puede acceder.

---

### Historia 2 - Dashboard de arqueo de caja (Prioridad: P1)

El Administrador abre "Reportes > Arqueo", elige un período con los mismos presets y ve el resumen por turno: cajero, apertura, cierre, fondo inicial, total vendido, ingresos, retiros, efectivo esperado, contado, diferencia y porcentaje de diferencia. Una gráfica de barras muestra la diferencia por turno (positiva en verde, negativa en rojo). Los totales del período indican turnos cerrados, total vendido y diferencia acumulada. Puede filtrar por cajero. Los turnos con diferencia mayor al umbral se marcan como alerta.

**Por qué esta prioridad**: detecta faltantes y sobrantes de efectivo, que es dinero real del negocio.

**Prueba independiente**: con turnos cerrados con diferencias de distinto tamaño, abrir el reporte y comprobar cifras, colores, totales y alertas.

**Escenarios de aceptación**:

1. **Given** un turno cerrado con efectivo esperado 1,000.00 y contado 940.00, **When** se consulta, **Then** se muestra diferencia −60.00 y −6 %, marcada como alerta con el umbral de 5 %.
2. **Given** un turno con diferencia de +2 %, **When** se consulta, **Then** la barra es verde y no hay alerta.
3. **Given** el Administrador cambia el umbral a 10 %, **When** se vuelve a consultar, **Then** el turno de −6 % deja de ser alerta.
4. **Given** turnos de varios cajeros, **When** se filtra por uno, **Then** tabla, gráfica y totales solo consideran sus turnos.
5. **Given** un turno todavía abierto en el período, **When** se consulta, **Then** aparece como "En curso" sin efectivo contado, sin diferencia y sin contarse en turnos cerrados ni en la diferencia acumulada.
6. **Given** un turno cuyo efectivo esperado es cero, **When** se calcula el porcentaje, **Then** se muestra como no calculable y no como error.

---

### Historia 3 - Dashboard de inventario (Prioridad: P1)

El Administrador y el Cajero abren "Reportes > Inventario", eligen una fecha o rango y ven el estado del inventario **al final del período** (no los movimientos): tarjetas con total de productos, activos, con existencia baja (≤ mínimo) y sin existencia (= 0); gráfica de pastel con la distribución normal / baja / sin existencia; y tabla con nombre, SKU, existencia, existencia mínima, unidad de medida y estado. La tabla se puede filtrar por estado, ordenar por existencia, nombre o SKU, buscar por nombre o SKU y se pagina de 100 en 100. No muestra costos, valuación ni márgenes.

**Por qué esta prioridad**: evita quedarse sin producto y orienta las compras.

**Prueba independiente**: con productos y movimientos de inventario en fechas distintas, consultar una fecha intermedia y comprobar que las existencias son las vigentes a esa fecha.

**Escenarios de aceptación**:

1. **Given** un producto con existencia 10 al inicio de mes y una salida de 10 el día 20, **When** se consulta un período que termina el día 15, **Then** aparece con existencia 10; si termina el día 25, aparece sin existencia.
2. **Given** un producto con existencia igual a su mínimo, **When** se consulta, **Then** su estado es "baja".
3. **Given** productos en los tres estados, **When** se filtra por "sin existencia", **Then** la tabla solo muestra esos y las tarjetas y la gráfica no cambian.
4. **Given** más de 100 productos, **When** se abre la tabla, **Then** se muestran 100 por página con navegación entre páginas.
5. **Given** una búsqueda por parte del nombre o del SKU, **When** se aplica, **Then** se listan solo los productos que coinciden.
6. **Given** un usuario Cajero, **When** abre el reporte, **Then** ve el inventario completo en solo lectura y sin costos.

---

### Historia 4 - Exportación a PDF (Prioridad: P1)

En cada dashboard, el botón "Exportar" genera un PDF descargable en A4 horizontal con título, rango de fechas, filtros aplicados, métricas principales, tabla de detalle y gráficas. Cada página lleva un encabezado con los datos del negocio (nombre, dirección, teléfono) y un pie con la fecha de generación y el usuario que exportó.

**Por qué esta prioridad**: el dueño necesita compartir e imprimir los reportes (contador, socios, archivo).

**Prueba independiente**: aplicar filtros en cada dashboard, exportar y revisar que el PDF refleja lo mismo que la pantalla, con encabezado y pie correctos.

**Escenarios de aceptación**:

1. **Given** un dashboard con filtros aplicados, **When** se exporta, **Then** el PDF indica el rango y los filtros y su contenido coincide con la pantalla.
2. **Given** una tabla que no cabe en una página, **When** se exporta, **Then** continúa en las páginas siguientes repitiendo encabezado de tabla, con encabezado y pie en todas.
3. **Given** turnos abiertos en el período, **When** se exporta el arqueo, **Then** el PDF no incluye efectivo esperado, contado ni diferencia de esos turnos.
4. **Given** el reporte de inventario con más de 100 productos, **When** se exporta, **Then** el PDF incluye todos los productos que cumplen el filtro, no solo la página visible.
5. **Given** datos del negocio sin capturar, **When** se exporta, **Then** el PDF se genera sin encabezado vacío roto y se indica que faltan los datos del negocio.

---

### Historia 5 - Exportación a Excel (Prioridad: P2)

Cada dashboard ofrece también exportar a XLSX con hojas separadas: resumen (tarjetas y filtros), detalle (tabla) y gráficas como imágenes incrustadas. El archivo es editable para que el usuario pueda trabajar los datos.

**Por qué esta prioridad**: útil para análisis propio, pero el PDF cubre la necesidad principal.

**Prueba independiente**: exportar un reporte, abrirlo en una hoja de cálculo y comprobar hojas, cifras numéricas (no texto) y gráficas.

**Escenarios de aceptación**:

1. **Given** un dashboard con datos, **When** se exporta a Excel, **Then** el archivo tiene hojas de resumen, detalle y gráficas, con el mismo contenido y filtros que la pantalla.
2. **Given** importes y fechas en el detalle, **When** se abre el archivo, **Then** son valores numéricos y de fecha que se pueden ordenar y sumar.
3. **Given** turnos abiertos en el período, **When** se exporta el arqueo, **Then** se aplica la misma omisión de datos sensibles que en el PDF.

---

### Historia 6 - Acceso del Cajero a su turno (Prioridad: P2)

En "Caja > Mi turno", el Cajero ve y exporta a PDF el resumen de su propio turno: fondo inicial, ventas realizadas, ingresos, retiros y, si el turno está cerrado, efectivo esperado contra contado. No ve turnos de otros ni reportes globales.

**Por qué esta prioridad**: da transparencia al Cajero sobre su turno sin abrir información del resto del negocio.

**Prueba independiente**: con dos cajeros con turnos, iniciar sesión con uno y comprobar que solo ve y exporta el suyo.

**Escenarios de aceptación**:

1. **Given** un Cajero con turno abierto, **When** abre "Caja > Mi turno", **Then** ve fondo inicial, ventas, ingresos y retiros de su turno, sin efectivo esperado ni contado.
2. **Given** un turno ya cerrado del Cajero, **When** lo consulta, **Then** además ve efectivo esperado, contado y diferencia.
3. **Given** un Cajero, **When** intenta ver el turno de otro cajero o los reportes de ventas y arqueo, **Then** no tiene acceso.
4. **Given** el resumen de su turno, **When** lo exporta, **Then** se genera un PDF con el mismo contenido, encabezado del negocio y pie con su nombre.

---

### Historia 7 - Alertas en Inicio (Prioridad: P3)

En Inicio, el Administrador ve una tarjeta "Alertas" con las diferencias de arqueo mayores al umbral y los productos críticos con existencia baja. El Administrador puede marcar productos como críticos.

**Por qué esta prioridad**: avisa sin tener que abrir los reportes; aporta menos que los reportes mismos.

**Prueba independiente**: generar un turno con diferencia mayor al umbral y marcar un producto crítico con existencia baja; comprobar que ambos aparecen en la tarjeta.

**Escenarios de aceptación**:

1. **Given** un turno cerrado reciente con diferencia mayor al umbral, **When** el Administrador abre Inicio, **Then** la tarjeta lo lista con cajero, fecha y diferencia.
2. **Given** un producto marcado como crítico con existencia igual o menor a su mínimo, **When** se abre Inicio, **Then** aparece en la tarjeta.
3. **Given** ninguna alerta, **When** se abre Inicio, **Then** la tarjeta indica que no hay alertas.

---

### Casos límite

- Período sin datos: cada reporte muestra "Sin datos en este período" en lugar de pantalla vacía o error, y el botón Exportar no genera un documento vacío engañoso.
- Rango personalizado inválido (fecha final anterior a la inicial o fechas futuras sin sentido): se impide consultar y se explica el motivo.
- Turno que cruza la medianoche o el límite del período: se asigna al día de su apertura para el arqueo; las ventas se asignan a su propia fecha.
- Venta cancelada dentro del período: excluida de todos los totales; las ventas de períodos anteriores canceladas después no alteran el histórico ya consultado al recalcular, pues se calcula siempre con el estado actual de la venta.
- Cajero dado de baja o inactivo: sus ventas y turnos siguen apareciendo en los reportes.
- Producto inactivo o que no controla inventario: no se incluye en el reporte de inventario.
- Cambio de zona horaria o de horario de verano: los días se delimitan en la hora local del equipo.
- Exportación de un período grande: el usuario ve que se está generando y puede continuar usando el sistema.

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **FR-001**: El sistema MUST ofrecer las pantallas "Reportes > Ventas", "Reportes > Arqueo" y "Reportes > Inventario" y la vista "Caja > Mi turno".
- **FR-002**: Ventas y Arqueo MUST ser accesibles solo al Administrador; Inventario, al Administrador y al Cajero en solo lectura; "Mi turno", al Cajero únicamente para su propio turno.
- **FR-003**: Ventas y Arqueo MUST ofrecer selector de período con los presets hoy, ayer, últimos 7 días, este mes, mes anterior y rango personalizado.
- **FR-004**: El reporte de ventas MUST mostrar total vendido, cantidad de ventas, ticket promedio y totales en efectivo, tarjeta y transferencia, excluyendo ventas canceladas.
- **FR-005**: El reporte de ventas MUST mostrar una gráfica de ventas por día y una tabla de detalle ordenable por folio, fecha, cajero y total, con filtro por cajero.
- **FR-006**: El reporte de ventas MUST permitir activar el comparativo con el período anterior y mostrar ambos totales y la variación porcentual, o "no calculable" si el período anterior es cero.
- **FR-007**: El reporte de arqueo MUST mostrar por turno: cajero, apertura, cierre, fondo inicial, total vendido, ingresos, retiros, efectivo esperado, contado, diferencia y porcentaje de diferencia, con filtro por cajero.
- **FR-008**: El reporte de arqueo MUST mostrar una gráfica de barras de diferencia por turno (positiva en verde, negativa en rojo) y los totales del período: turnos cerrados, total vendido y diferencia acumulada.
- **FR-009**: El reporte de arqueo MUST marcar como alerta los turnos cuya diferencia absoluta en porcentaje supere el umbral configurable, 5 % por defecto; solo el Administrador puede cambiarlo.
- **FR-010**: Los turnos abiertos MUST mostrarse como "En curso" sin efectivo contado ni diferencia, y excluirse de los totales de turnos cerrados y de diferencia.
- **FR-011**: El reporte de inventario MUST calcular existencias al cierre del período a partir del historial de movimientos de inventario, no mostrar los movimientos del período.
- **FR-012**: El reporte de inventario MUST mostrar tarjetas (total, activos, existencia baja, sin existencia), gráfica de distribución de estados y tabla con nombre, SKU, existencia, mínimo, unidad y estado.
- **FR-013**: La tabla de inventario MUST permitir filtrar por estado, ordenar por existencia, nombre y SKU, buscar por nombre o SKU y paginar de 100 en 100.
- **FR-014**: El reporte de inventario MUST NOT mostrar costos, valuación ni márgenes.
- **FR-015**: Cada dashboard MUST tener un botón "Exportar" que genere un PDF descargable en A4 horizontal con título, rango, filtros aplicados, métricas, tabla de detalle y gráficas.
- **FR-016**: Cada página del PDF MUST llevar encabezado con nombre, dirección y teléfono del negocio, y pie con fecha y hora de generación y usuario que exportó.
- **FR-017**: Los PDF y las exportaciones MUST NOT incluir efectivo esperado, contado ni diferencia de turnos abiertos o en curso.
- **FR-018**: El PDF de inventario MUST incluir todos los registros que cumplan los filtros, sin limitarse a la página visible.
- **FR-019**: Cada dashboard MUST permitir exportar a XLSX con hojas de resumen, detalle y gráficas como imágenes, con importes y fechas como valores numéricos y de fecha. *(P2)*
- **FR-020**: "Caja > Mi turno" MUST mostrar al Cajero fondo inicial, ventas, ingresos y retiros de su turno, y efectivo esperado, contado y diferencia solo si el turno está cerrado, con exportación a PDF. *(P2)*
- **FR-021**: Inicio MUST mostrar al Administrador una tarjeta "Alertas" con diferencias de arqueo sobre el umbral y productos críticos con existencia baja; el Administrador puede marcar productos como críticos. *(P3)*
- **FR-022**: Todos los importes MUST manejarse y mostrarse con el tipo de dinero del sistema, y todas las fechas en la zona horaria local.
- **FR-023**: Los reportes MUST ser de solo lectura: consultar o exportar no modifica ningún dato.
- **FR-024**: Los reportes MUST generarse bajo demanda con los datos vigentes al consultar, sin resultados pre-calculados ni guardados.
- **FR-025**: Cuando un período no tenga datos, el reporte MUST mostrar "Sin datos en este período".
- **FR-026**: El sistema MUST denegar el acceso a reportes no autorizados tanto en la navegación como al invocarse directamente.
- **FR-027**: Las exportaciones MUST registrarse en la bitácora con usuario, reporte y período, sin incluir los datos exportados.

### Entidades clave

- **Período**: fecha inicial y final (en hora local) y período anterior equivalente.
- **Venta**: folio, fecha, cajero, total, forma(s) de pago y estado (completada o cancelada); ya existente.
- **Turno**: cajero, apertura, cierre, fondo inicial, movimientos, efectivo esperado y contado; ya existente.
- **Movimiento de inventario**: historial por producto que permite conocer la existencia en una fecha; ya existente.
- **Producto**: nombre, SKU, unidad, existencia mínima, activo, marca de crítico (nueva, P3).
- **Configuración de reportes**: umbral de alerta de arqueo (5 % por defecto).
- **Datos del negocio**: nombre, dirección y teléfono para el encabezado; ya existentes.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: El Administrador obtiene total vendido, desglose por forma de pago y comparativo de cualquier período en menos de 30 segundos desde que abre la pantalla.
- **SC-002**: Con 10,000 ventas en el período, cada dashboard muestra sus resultados en menos de 2 segundos.
- **SC-003**: Las cifras de cada reporte coinciden al 100 % con el cálculo manual sobre los mismos datos en las pruebas de aceptación (ventas sin canceladas, totales por forma de pago, diferencias y alertas de arqueo, existencias a la fecha).
- **SC-004**: El 100 % de los PDF generados incluye datos del negocio, rango, filtros, fecha de generación y usuario, y las tablas son legibles sin cortar columnas.
- **SC-005**: Ningún usuario Cajero puede ver ventas globales, arqueo global ni turnos de otros en ninguna prueba de acceso.
- **SC-006**: Un PDF de un período típico (hasta 10,000 ventas) se genera en menos de 10 segundos.
- **SC-007**: Todo período sin datos muestra el mensaje "Sin datos en este período" sin errores en el 100 % de los casos probados.

## Supuestos

- La vista "Caja > Mi turno" es nueva y muestra al Cajero más que el resumen de 008 (fondo inicial, ingresos y retiros). El resumen de 008 que usan el Punto de venta e Inicio no cambia.
- Los datos de ventas, turnos, usuarios e inventario provienen de las funcionalidades ya existentes; esta funcionalidad no cambia cómo se registran.
- El historial de movimientos de inventario guarda la existencia resultante de cada movimiento (004), por lo que permite conocer la existencia en una fecha pasada.
- El Cajero puede exportar a PDF el reporte de inventario (solo lectura, sin costos); "restricciones" significa sin costos y sin cambiar datos.
- La exportación a Excel entrega valores simples, sin fórmulas ni protección; "modificar" significa que el archivo es editable.
- Un turno se asigna al período por su fecha de apertura; las ventas, por su fecha de venta.
- La alerta de arqueo aplica a la diferencia absoluta (faltante o sobrante) sobre el efectivo esperado.
- Existencia baja significa existencia mayor que cero y menor o igual al mínimo; sin existencia significa igual a cero; un producto sin mínimo definido nunca está en estado bajo.
- El reporte separado de cancelaciones queda fuera de esta fase.
- Una instalación tiene una sola caja y un solo negocio.
- Las descargas se guardan en la ubicación que el usuario elija al exportar.

## Fuera de alcance

- Envío por email y programación automática de reportes.
- Reportes de proveedores, clientes, márgenes y costos; valuación de inventario.
- Auditoría de cambios de usuarios.
- Análisis predictivo o pronósticos.
- Reporte de ventas canceladas.
