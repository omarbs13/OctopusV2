# Contrato de interfaz: Reportes y análisis

## Navegación

- Grupo nuevo **Reportes** (orden 7, después de Ventas), visible si el usuario tiene al menos una de
  sus páginas:
  - **Ventas**: `ViewReports`.
  - **Arqueo**: `ViewReports`.
  - **Inventario**: `ViewInventory`.
- **Mi turno**: página nueva en el grupo **Ventas**, junto a "Turnos" (008), con `OperateShift`. La
  especificación la llama "Caja > Mi turno"; el menú actual no tiene un grupo "Caja" y crearlo solo para
  una página iría contra el Principio VII. El texto visible de la página es "Mi turno".
  *(Si el responsable prefiere un grupo "Caja" se cambia en `ReportsModule` sin afectar el resto.)*
- Las páginas se registran en `ReportsModule` con `AddPage`, como `SalesModule`.

## Componentes compartidos

### Selector de período (`PeriodPickerView`)
- Botones de preset: Hoy, Ayer, Últimos 7 días, Este mes, Mes anterior, Personalizado.
- Personalizado muestra dos fechas; "Consultar" se habilita solo si el rango es válido y muestra el
  motivo si no (fin anterior al inicio, más de 366 días).
- Tras elegir un preset se consulta de inmediato. Preset inicial: Hoy (Ventas, Arqueo) o la fecha de hoy
  (Inventario).
- Inventario usa una sola fecha ("Al cierre del día") aunque la especificación hable de rango: el
  estado se evalúa al final del período, así que se ofrecen los mismos presets y se toma su fecha final.

### Estado vacío
- Sin datos: texto "Sin datos en este período", sin tarjetas ni gráficas en cero ni error. El botón
  Exportar se deshabilita.

### Carga y errores
- Mientras se consulta o exporta se muestra un indicador y el resto de la aplicación sigue usable
  (`OperationRunner`). Un error inesperado muestra un mensaje comprensible y se registra.

## Reportes > Ventas

| Zona | Contenido |
|------|-----------|
| Filtros | Período, cajero (lista de `ListCashiers`, con "Todos"), interruptor "Comparar con el período anterior" |
| Tarjetas | Total vendido, Ventas, Ticket promedio, Efectivo, Tarjeta, Transferencia |
| Comparativo (si está activo) | Período anterior X, actual Y, variación ± Z %; "No calculable" si X es 0 |
| Gráfica | Línea de ventas por día; eje con fechas locales; días sin ventas en 0 |
| Tabla | Folio, Fecha y hora, Cajero, Total; encabezados ordenables; 100 por página |
| Acciones | Exportar PDF, Exportar Excel |

## Reportes > Arqueo

| Zona | Contenido |
|------|-----------|
| Filtros | Período, cajero |
| Umbral | Campo "Alertar si la diferencia supera" en %; edición solo con `ManageSettings` (si no, solo lectura) |
| Tarjetas | Turnos cerrados, Total vendido, Diferencia acumulada |
| Alerta | Banner con la cantidad de turnos sobre el umbral |
| Gráfica | Barras de diferencia por turno: verde positiva, roja negativa, con signo en la etiqueta |
| Tabla | Turno, Cajero, Apertura, Cierre, Fondo, Vendido, Ingresos, Retiros, Esperado, Contado, Diferencia, % |
| Turno abierto | Fila "En curso": esperado, contado, diferencia y % vacíos; sin barra |
| Acciones | Exportar PDF, Exportar Excel |

## Reportes > Inventario

| Zona | Contenido |
|------|-----------|
| Filtros | Período/fecha, estado (Todos, Normal, Baja, Sin existencia), búsqueda por nombre o SKU |
| Tarjetas | Total de productos, Activos, Existencia baja, Sin existencia |
| Gráfica | Pastel: Normal, Baja, Sin existencia, con conteo y porcentaje |
| Tabla | Nombre, SKU, Existencia, Mínimo, Unidad, Estado; ordenable por existencia, nombre y SKU; 100 por página |
| Nota | "Existencias al cierre de la fecha. Mínimos y datos del producto: valores actuales." |
| Acciones | Exportar PDF, Exportar Excel |

El Cajero ve la misma pantalla en solo lectura; no hay costos en ninguna columna.

## Mi turno

- Turno abierto del usuario: fondo inicial, ventas (número y total), ingresos, retiros y la lista de
  sus movimientos. **No** muestra efectivo esperado ni contado mientras el turno esté abierto (arqueo
  ciego de 008).
- Si no tiene turno abierto: lista de sus últimos 10 turnos cerrados; al elegir uno se ven además
  efectivo esperado, contado y diferencia.
- Acción: Exportar PDF (mismo contenido que la pantalla, con encabezado del negocio y pie con su
  nombre).

## Tarjeta "Alertas" en Inicio (P3)

- Visible solo para el Administrador, en la lista de tarjetas de Inicio (`AddDashboardCard`).
- Dos secciones: diferencias de arqueo sobre el umbral (últimos 7 días) y productos críticos con
  existencia baja o agotada; hasta 10 de cada una y el total.
- Sin alertas: "No hay alertas".
- Marcar producto crítico: casilla "Producto crítico" en el formulario de producto y acción en
  Existencias (solo con `ManageProducts`).

## Exportar

1. El usuario pulsa Exportar PDF o Exportar Excel.
2. Se ejecuta `ExportReport` con los parámetros visibles (período, filtros, orden).
3. Se abre "Guardar como" con el nombre sugerido y la carpeta de documentos del usuario.
4. Si cancela el selector no pasa nada. Si guarda, se muestra "Reporte guardado" con la ruta.
5. Si faltan los datos del negocio, el archivo se genera sin ellos y aparece un aviso que lleva a
   "Datos del negocio".

## Textos

Toda la interfaz en español, en `Strings.resx` con prefijo `Reports_`. Importes con `MoneyConverter`;
cantidades de inventario con `QuantityConverter`; fechas en hora local.
