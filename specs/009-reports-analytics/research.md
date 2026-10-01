# Investigación: Reportes y análisis

Cada decisión indica qué se eligió, por qué y qué alternativas se descartaron. Ninguna queda con
`NEEDS CLARIFICATION`.

## §1. Lectores de solo lectura en lugar de repositorios de agregado

- **Decisión**: tres interfaces de lectura en Application (`ISalesReportReader`,
  `ICashCountReportReader`, `IInventoryReportReader`), cada una con un único método de consulta que
  devuelve DTOs ya agregados. Se implementan en Infrastructure con EF Core y `AsNoTracking`.
- **Razón**: un reporte cruza varios agregados (ventas, turnos, usuarios, productos, movimientos) y
  no modifica nada; meterlo en `ISaleRepository` o `ICashShiftRepository` los engordaría. La
  constitución pide repositorios específicos por agregado, no prohíbe lectores de consulta.
- **Descartado**: reutilizar `SearchAsync` de cada repositorio (no agrega ni pagina igual); un
  repositorio genérico de reportes (prohibido por el Principio VII).

## §2. Rendimiento con 10,000 ventas

- **Decisión**: las sumas y conteos se hacen en SQL (`GroupBy` + `Sum`). Las ventas por día se
  obtienen proyectando solo `CreatedAt` y `TotalCents` y agrupando en memoria por fecha local,
  porque la zona horaria no se puede aplicar en SQLite. Con 10,000 filas de dos columnas el costo es
  de milisegundos.
- La tabla de detalle de ventas se ordena y pagina en SQL (100 por página, igual que Inventario);
  los totales no dependen de la página.
- Totales por forma de pago: misma fórmula que `GetShiftTotalsAsync` (008): `SalePayments.AmountCents`
  ya es neto de cambio, de ventas completadas; así efectivo + tarjeta + transferencia = total vendido.
- **Índices**: `IX_Sales_Status_CreatedAt` y `IX_Sales_CreatedBy_CreatedAt` ya cubren el filtro por
  estado, fecha y cajero; `IX_CashShifts_OpenedAt` cubre el arqueo. Para inventario se agrega
  `IX_InventoryMovements_Product_CreatedAt` (`ProductId`, `CreatedAt`) en la migración, porque hoy
  solo existe (`ProductId`, `Sequence`) y (`CreatedAt`, `Id`).
- **Verificación**: prueba de rendimiento en `quickstart.md` con 10,000 ventas sembradas.

## §3. Reglas de cálculo en Domain

- `ReportPeriod(FromDate, ToDate)`: fechas locales inclusivas; rechaza fin anterior al inicio y
  rangos mayores a 366 días. Presets: hoy, ayer, últimos 7 días (incluye hoy), este mes, mes anterior
  y personalizado; se resuelven contra la fecha local actual. `Previous()` devuelve el rango de igual
  duración que termina el día anterior al inicio (los 7 días previos para "últimos 7 días"; para
  "este mes" los mismos días de duración inmediatamente anteriores, no el mes calendario anterior).
- `VariationMath.PercentBasisPoints(previous, current)`: entero con signo en centésimas de por ciento
  (1,250 = 12.50 %), o `null` si `previous` es 0. Nunca usa `double`.
- `CashDifferenceRule.PercentBasisPoints(differenceCents, expectedCents)`: `difference * 10000 /
  expected` redondeado a media hacia afuera de cero; `null` si el esperado es 0 o negativo.
  `IsAlert(basisPoints, thresholdBasisPoints)`: `abs(basisPoints) > threshold`. El 5 % es 500 y
  exactamente 5.00 % no es alerta ("mayor que").
- **Razón**: son los cálculos con dinero que la constitución (Principio VI) exige probar.

## §4. Inventario al final del período

- **Decisión**: para cada producto activo o inactivo que controla inventario y fue creado antes del
  límite superior, la existencia es `ResultingStockThousandths` del movimiento con mayor `Sequence`
  cuyo `CreatedAt` es anterior al límite superior exclusivo (inicio del día siguiente al fin del
  período, en UTC). Sin movimientos hasta esa fecha, la existencia es 0.
- El mínimo, el nombre, el SKU y la unidad son los actuales: no hay historial de esos campos. Esto se
  documenta en la pantalla con el texto "Mínimos y datos del producto: valores actuales".
- **"Activos"**: cuenta productos con `IsActive = true` hoy; la especificación no pide reconstruir el
  estado de activación.
- **Estado**: se evalúa con `StockStatusRule` (Domain): ≤ 0 sin existencia; > 0 y ≤ mínimo baja; otro
  caso normal. Se replica en SQL igual que en 004.
- **Productos incluidos**: los que controlan inventario, no están borrados y se crearon antes del
  límite. Se incluyen activos e inactivos; la tarjeta "activos" los distingue del "total". La tabla, la
  gráfica y las tarjetas usan el mismo conjunto para que los números cuadren. A diferencia de
  Existencias (004), no se oculta a los inactivos, porque "total de productos" y "productos activos"
  son tarjetas distintas en la especificación.
- Consulta: una subconsulta correlacionada por producto apoyada en el índice nuevo de §2.
- **Descartado**: reconstruir restando movimientos posteriores a la existencia actual (propenso a
  error si hay ajustes); una tabla de fotos diarias (contradice "sin pre-cálculo").

## §5. Permisos

- **Decisión**: permiso nuevo `ViewReports` asignado solo al administrador (por `RolePermissions`, que
  ya le da todos). Ventas y Arqueo lo exigen. Inventario exige `ViewInventory` (ambos roles). "Mi
  turno" exige `OperateShift` y solo devuelve el turno cuyo dueño es el usuario actual.
- Cada caso de uso verifica el permiso con `IAccessControl` (como `GetSalesDashboardHandler`), no
  solo el menú (FR-026).
- **"Mi turno"**: muestra el turno abierto del usuario; si no tiene, permite elegir entre sus últimos
  turnos cerrados (los 10 más recientes). Consulta por `OpenedBy = usuario actual`; nunca acepta un
  id de turno de otro usuario (si se pide uno ajeno responde "no encontrado").
- `GetCurrentShift` (008) no cambia: el Punto de venta e Inicio siguen con el resumen mínimo.

## §6. PDF con SkiaSharp (sin librería nueva)

- **Decisión**: generar el PDF con `SKDocument.CreatePdf` de SkiaSharp, que ya es dependencia de
  `Pos.Infrastructure`. A4 horizontal (842 × 595 pt). Un `PdfReportWriter` dibuja encabezado, pie,
  tarjetas, gráficas y tabla paginada.
- **Paginación de tablas**: altura de fila fija; se calcula cuántas filas caben, se repite el
  encabezado de columnas y el encabezado/pie del documento en cada página.
- **Fuente**: `SKTypeface.FromFamilyName` con lista de respaldo (`Inter`, `Segoe UI`, `DejaVu Sans`,
  `Liberation Sans`, `Arial`) y, si ninguna existe, `SKTypeface.Default`. El tipo de letra se
  incrusta en el PDF. Se verifica en Linux y Windows que los acentos y la eñe se ven.
- **Razón**: evita una dependencia con licencia comunitaria condicionada (QuestPDF) y reutiliza el
  mismo código de dibujo que las gráficas. Costo: escribir la paginación de tablas, acotado porque
  solo hay tres formatos de tabla.
- **Descartado**: QuestPDF (licencia por ingresos de la empresa); PdfSharp (nueva dependencia sin
  ventaja sobre lo que ya hay); imprimir desde la interfaz (no produce A4 horizontal estable).

## §7. XLSX con ClosedXML

- **Decisión**: ClosedXML 0.105.1 (MIT), solo en `Pos.Infrastructure`. Hojas: "Resumen" (título,
  período, filtros, métricas), "Detalle" (tabla con importes y fechas como valores) y "Gráficas"
  (imágenes PNG del renderizador de §8).
- **Justificación de la dependencia** (Principio VII): un XLSX correcto con imágenes incrustadas
  requiere empaquetado OpenXML; escribirlo a mano es costoso y frágil. ClosedXML es administrado,
  multiplataforma y de licencia permisiva.
- **Formato**: importes como número con formato `#,##0.00` (se convierte de centavos enteros a
  `decimal`, solo para la celda); fechas locales como fecha y hora de Excel; sin fórmulas ni
  protección (hoja editable, según el supuesto de la especificación).
- **Descartado**: generar CSV (no cumple "hojas e imágenes"); escribir OpenXML con
  `System.IO.Packaging` (más código que adoptar la librería).

## §8. Gráficas: un solo renderizador

- **Decisión**: `ChartRenderer` (Infrastructure, SkiaSharp) dibuja línea, barras con signo (verde/rojo)
  y pastel a partir de un `ChartSpec` neutral (Application). La pantalla muestra el PNG resultante en
  un control de imagen, el PDF dibuja el mismo `ChartSpec` en vectores sobre el lienzo del documento y
  el XLSX incrusta el PNG.
- **Razón**: una sola implementación para pantalla y exportaciones garantiza que lo exportado se ve
  igual que la pantalla, sin librería de gráficas (en 005 se decidió no usar una). Las tarjetas de
  Inicio siguen dibujando barras con controles de Avalonia.
- **Costo aceptado**: las gráficas de pantalla no son interactivas (sin tooltip) y se vuelven a
  dibujar al cambiar el tamaño de la ventana (con un retardo corto); basta para la especificación.
- **Colores**: verde y rojo con etiqueta de signo además del color, para no depender solo del color.
- **Descartado**: LiveCharts2/ScottPlot (dependencia pesada para tres gráficas simples).

## §9. Umbral de alerta de arqueo

- **Decisión**: una preferencia `ReportSettings.CashDifferenceAlertBasisPoints` (por defecto 500) en
  `IPreferencesStore`, igual que `SecuritySettings` (007). Se edita en la pantalla Arqueo (solo
  administrador, permiso `ManageSettings`) y se valida entre 0.01 % y 100 %.
- **Razón**: es un valor único por instalación sin historial; no justifica tabla ni migración.
- El cambio se audita (`REPORT_SETTINGS_CHANGED`).

## §10. Producto crítico (P3)

- **Decisión**: columna booleana `Products.IsCritical`, no nula, valor 0 por defecto, agregada con
  `AddColumn` (sin reconstrucción de tabla). Un caso de uso `SetProductCritical` la cambia (permiso
  `ManageProducts`). Se marca desde la tabla de Existencias y el formulario de producto.
- **Razón**: el dato pertenece al producto y debe viajar con sus respaldos.
- La tarjeta de alertas cuenta productos críticos activos, con existencia baja o agotada según
  `StockStatusRule`, con la existencia actual.
- **Descartado**: lista de críticos en preferencias (se desincroniza al borrar productos).

## §11. Exportar: flujo y contenido seguro

- **Decisión**: `ExportReport` recibe el tipo de reporte, el formato, los mismos parámetros que la
  pantalla y devuelve bytes y nombre de archivo sugerido. La interfaz abre el selector "Guardar
  como" (`DialogService` ya tiene `SaveFilePickerAsync`) y escribe el archivo. Así Application no
  toca el sistema de archivos.
- El `ReportDocument` se arma en Application: aquí se quitan efectivo esperado, contado y diferencia
  de turnos abiertos (FR-017) y se incluyen todos los registros del filtro, sin paginar (FR-018). Una
  prueba unitaria cubre la omisión.
- El encabezado usa `BusinessProfile`; si falta, el documento lo indica (Historia 4, escenario 5).
- Bitácora: `REPORT_EXPORTED` con reporte, formato, período y usuario (FR-027).
- Nombre sugerido: `ventas_2026-09-01_2026-09-30.pdf`, sin espacios ni acentos.
- Un período sin datos no genera documento: el caso de uso responde con el mensaje "Sin datos en este
  período" (Casos límite).

## §12. Pruebas (política mínima, Principio VI)

- **Domain**: `VariationMath` (caso válido, período anterior en cero), `CashDifferenceRule` (−6 %,
  justo en el umbral 5.00 %, esperado 0), `ReportPeriod` (presets y período anterior, rango inválido).
- **Application**: permisos de los casos de uso (Cajero rechazado en Ventas y Arqueo; "Mi turno" no
  devuelve turnos ajenos); omisión de datos de turnos abiertos en `ReportDocument`.
- **Infrastructure** (SQLite real): ventas sin canceladas y totales por forma de pago que suman el
  total; inventario a una fecha pasada con movimientos posteriores; arqueo con turno abierto; la
  migración y la base de ejemplo `v0.7.0.db` (obligatoria). Prueba de rendimiento con 10,000 ventas.
- **No se prueban** ViewModels, vistas, el dibujo de gráficas ni el diseño del PDF; esos se revisan
  con `quickstart.md`. Se añade una prueba mínima de humo: el PDF generado empieza con `%PDF` y el XLSX
  abre con ClosedXML con las tres hojas.
