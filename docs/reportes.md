# Reportes y análisis

Guía de soporte de los reportes (funcionalidad 009, versión 0.7.0): qué muestra cada pantalla, quién
puede verla, cómo se calcula cada cifra y qué queda en la bitácora al exportar.

## Pantallas y permisos

| Pantalla | Dónde está | Quién la ve | Permiso |
|---|---|---|---|
| Ventas | Reportes > Ventas | Administrador | `ViewReports` |
| Arqueo | Reportes > Arqueo | Administrador | `ViewReports` |
| Inventario | Reportes > Inventario | Administrador y Cajero (solo lectura, sin costos) | `ViewInventory` |
| Mi turno | Ventas > Mi turno | Cajero (y Administrador, de su propio turno) | `OperateShift` |
| Alertas | Tarjeta en Inicio | Administrador | `ViewReports` |

La especificación llama a la vista del Cajero "Caja > Mi turno"; el menú no tiene un grupo "Caja", así
que vive en **Ventas**, junto a "Turnos". Cambiarla de grupo es una línea en `ReportsModule`.

El permiso se verifica en cada caso de uso, no solo en el menú: un Cajero que invocara el reporte de
ventas o de arqueo recibe "no tiene permiso". "Mi turno" consulta siempre por el dueño del turno; pedir el
id de un turno ajeno responde "no encontrado".

Los reportes son de **solo lectura**, se calculan al consultar con los datos vigentes (sin caché ni
resultados guardados) y no abren transacciones de escritura.

## Períodos

Ventas y Arqueo ofrecen: Hoy, Ayer, Últimos 7 días (incluye hoy), Este mes, Mes anterior y rango
personalizado (máximo 366 días; la fecha final no puede ser anterior a la inicial). Los días se delimitan
en la **hora local** del equipo y se convierten a UTC para consultar (`ReportPeriodResolver`).

El **período anterior** del comparativo tiene la misma duración y termina el día anterior al inicio
(para "Este mes" son los mismos días de duración inmediatamente anteriores, no el mes calendario anterior).
La variación es `(actual − anterior) / anterior`; si el anterior es 0 se muestra "No calculable".

Inventario usa una sola fecha ("Al cierre del día"): se toma la fecha final del período elegido.

## Cómo se calcula cada cifra

- **Ventas**: solo ventas completadas (las canceladas no cuentan). Ticket promedio = total vendido entre
  número de ventas, redondeado a media hacia arriba. Los pagos son netos de cambio, así que efectivo +
  tarjeta + transferencia = total vendido.
- **Arqueo**: un turno se asigna al período por su **fecha de apertura**. Diferencia = contado − esperado;
  porcentaje = diferencia / esperado (en centésimas de por ciento, media hacia afuera de cero; "No
  calculable" si el esperado es 0). Es alerta si el valor absoluto **supera** el umbral (exactamente el
  umbral no es alerta). Los turnos abiertos aparecen como "En curso", sin efectivo esperado, contado ni
  diferencia, y no cuentan en "turnos cerrados" ni en la diferencia acumulada; su total vendido sí suma.
- **Inventario**: la existencia al cierre de la fecha es el `ResultingStock` del último movimiento anterior
  al inicio del día siguiente; sin movimientos vale 0. Se incluyen productos que controlan inventario, no
  borrados y creados antes del límite, activos o inactivos (la tarjeta "Activos" los distingue). Nombre, SKU,
  unidad y mínimo son los **actuales**: no hay historial de esos campos.
  - Sin existencia: 0 o menos (las ventas pueden dejarla negativa). Baja: mayor que 0 y menor o igual al
    mínimo. Un producto sin mínimo nunca está en estado bajo.
  - Las tarjetas y la gráfica usan todos los productos; el filtro de estado, la búsqueda por nombre o SKU y
    el orden solo afectan a la tabla (100 por página). No muestra costos, valuación ni márgenes.

## Categorías (016)

Desde 0.11.0, "Reportes > Ventas" y "Reportes > Inventario" tienen un filtro **Categoría**: Todas (por
defecto), Sin categoría y cada categoría no borrada (las inactivas marcadas "(inactiva)"). Las cifras usan la
**categoría vigente** del producto al consultar; la venta no guarda la categoría. Reglas del catálogo en
[categorias.md](categorias.md).

- **Importe de una línea**: `SaleLine.AmountCents` (ya neto del descuento de línea y de su parte del
  descuento global, 015) menos lo devuelto de esa línea (`Σ SaleReturnLine.AmountCents`, 013). Unidades:
  `QuantityThousandths − ReturnedQuantity`. Solo ventas completadas: las canceladas no cuentan.
- **Cuadre**: como las líneas suman el total de cada venta y las devoluciones por línea suman lo devuelto,
  la suma de todas las categorías es **exactamente** el total vendido sin filtro, al centavo. Los porcentajes
  se muestran con un decimal y no se ajustan: pueden sumar 99.9 % o 100.1 %.
- **"Ventas por categoría"** (debajo de la gráfica): una fila por categoría con ventas en el período (más
  "Sin categoría" si tiene), con unidades, importe y % del total, ordenadas por importe; cada fila se expande
  a sus productos, de más a menos unidades. Un producto cuyas ventas se devolvieron por completo no aparece.
  Un producto borrado se muestra con el último nombre con que se vendió.
- **Unidades mezcladas**: las unidades de una categoría suman las cantidades tal cual (piezas, kilos…), por
  lo que son indicativas; el desglose muestra cada producto con su unidad.
- **Con filtro de categoría**: ventas consideradas = las que tienen al menos una línea de la categoría; el
  total, la gráfica por día, el comparativo y el descontado usan solo esas líneas; la cantidad de ventas cuenta
  cada venta una vez y el ticket promedio = total / ventas. En el detalle, cada venta muestra el importe
  **vendido** de sus líneas de la categoría, **sin restar devoluciones**, igual que la fila sin filtro muestra
  el total original; el total (tarjeta) sí las resta. La sección "Ventas por categoría" muestra solo esa
  categoría.
- **Formas de pago**: no se desglosan por categoría porque un pago cubre la venta completa y no se
  prorratea. Con filtro, sus tarjetas muestran "—" con la nota "No se desglosa por categoría", también en
  PDF y Excel.
- **Inventario**: el filtro de categoría se aplica **antes** de contar, así que limita tarjetas, gráfica y
  tabla (a diferencia del filtro de estado y la búsqueda). La tabla agrega la columna "Categoría", ordenable
  ("Sin categoría" al final).
- **Exportaciones**: los filtros aplicados incluyen "Categoría: {nombre}" o "Categoría: Sin categoría" (se
  omite con Todas). El reporte de ventas incluye la tabla "Ventas por categoría" con sus productos (con
  sangría) y la fila de total; el de inventario, la columna "Categoría".

## Umbral de alerta de arqueo

Se guarda por instalación en las preferencias locales (`reports`, valor `CashDifferenceAlertBasisPoints`;
5 % = 500). Solo quien tiene `ManageSettings` lo cambia, desde "Reportes > Arqueo"; acepta de 0.01 % a 100 %.
Cada cambio queda en la bitácora (`REPORT_SETTINGS_CHANGED`).

## Producto crítico y alertas de Inicio

La columna `Products.IsCritical` marca un producto como crítico (casilla "Producto crítico" en el formulario
de producto, visible si controla inventario, y acción "Marcar como crítico" en Existencias; requiere
`ManageProducts`). La tarjeta "Alertas" muestra hasta 10 turnos cerrados de los últimos 7 días sobre el
umbral y hasta 10 productos críticos activos con existencia baja o agotada, más el total de cada lista.

## Exportación

- **PDF**: A4 horizontal; título, período, filtros, métricas, gráficas y tabla; encabezado con nombre,
  dirección y teléfono del negocio y pie con fecha, hora y usuario en cada página; las tablas largas
  continúan en las páginas siguientes repitiendo el encabezado de columnas. Si los datos del negocio no están
  capturados, el PDF se genera sin encabezado y la pantalla avisa y ofrece ir a "Datos del negocio".
- **Excel (XLSX)**: hojas Resumen, Detalle y Gráficas (imágenes PNG). Importes y fechas son valores
  numéricos y de fecha; sin fórmulas ni protección. "Mi turno" solo se exporta a PDF.
- La exportación incluye **todos** los registros del filtro (no solo la página visible) y omite efectivo
  esperado, contado y diferencia de turnos abiertos. Un período sin datos no genera archivo
  ("Sin datos en este período").
- El archivo se guarda donde el usuario elija; el nombre sugerido es `ventas_2026-09-01_2026-09-30.pdf`,
  `arqueo_…`, `inventario_<fecha>` o `turno_T-000123`.
- Cada exportación queda en la bitácora (`REPORT_EXPORTED`) con reporte, formato, período y filtros, **nunca**
  con los datos exportados. Un error al exportar se registra con reporte, formato y período y muestra un
  mensaje sin cerrar la aplicación.

## Gráficas y tipografía

Un solo renderizador (`ChartRenderer`, SkiaSharp) dibuja línea, barras con signo (verde positivo, rojo
negativo, con el signo en la etiqueta) y pastel; lo usan la pantalla, el PDF (en vectores) y el XLSX (PNG), así
que lo exportado se ve igual que la pantalla. La tipografía se busca en este orden: Inter, Segoe UI,
DejaVu Sans, Liberation Sans, Arial; si ninguna está instalada o no tiene acentos y eñe, se usa la
predeterminada de Skia. Si en un equipo Linux el PDF muestra cuadros en lugar de letras, instale una de esas
fuentes.

## Base de datos

La migración `ReportsAnalytics` agrega `Products.IsCritical` (booleana, 0 por defecto, con `AddColumn`, sin
reconstruir `Products`) y el índice `IX_InventoryMovements_Product_CreatedAt` (`ProductId`, `CreatedAt`) que
apoya la consulta de existencias a una fecha. La base de ejemplo `v0.7.0.db` se generó con el esquema de 0.6.0,
antes de la migración. No hay tablas nuevas: los reportes leen `Sales`, `SalePayments`, `CashShifts`,
`CashMovements`, `Users`, `Products`, `ProductStocks` e `InventoryMovements`.

## Diagnóstico

- Buscar en el log `ExportarReporte` o `GuardarReporte` (operación, usuario, reporte, formato y período).
- Un reporte lento: la prueba `SalesReportPerformanceTests` fija el techo (10,000 ventas: menos de 2 s por
  reporte y menos de 10 s para el PDF). Los índices de lectura son `IX_Sales_Status_CreatedAt`,
  `IX_Sales_CreatedBy_CreatedAt`, `IX_CashShifts_OpenedAt` e `IX_InventoryMovements_Product_CreatedAt`.
