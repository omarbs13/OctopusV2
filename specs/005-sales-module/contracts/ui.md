# Contrato: interfaz (Pos.Desktop/Sales)

Los textos van en `Resources/Strings.resx` (español). Los importes se muestran con
`MoneyConverter` y las cantidades con `QuantityConverter`. Los ViewModels solo coordinan: los
cálculos vienen de `Cart` y `Checkout` (Domain) y de los casos de uso.

## Navegación

- `SalesModule.AddSalesModule()` registra el grupo "Ventas" (`sales`, icono `Icon.Sales`, orden 5,
  justo después de Inicio y antes de Catálogos, que tiene orden 10) con dos pantallas:
  - "Punto de venta" (`sales.pos`, orden 0).
  - "Ventas realizadas" (`sales.history`, orden 10).
- Reemplaza a `AddSalesPlaceholders()` en `HostBuilder`. Las tarjetas de ventas de Inicio pasan a
  este módulo.
- Acceso directo global: **F9** abre el Punto de venta desde cualquier pantalla (`KeyBinding` en
  `MainWindow` hacia `Navigator.NavigateAsync("sales.pos")`). El menú muestra "F9" junto a la
  opción.
- `PointOfSaleViewModel` implementa `ILeaveGuard`. Salir con líneas no pregunta, porque la venta
  queda en el borrador. Solo espera a que termine el guardado pendiente.

## Punto de venta (`PointOfSaleView`)

Distribución:

- A la izquierda, el campo de captura (siempre con foco al volver a la pantalla) y la tabla de
  líneas.
- A la derecha, el total en grande (≥ 48 px), el número de artículos y los botones grandes para
  táctil (alto mínimo de 48 px).

| Acción | Tecla | Botón | Comportamiento |
|---|---|---|---|
| Capturar | Enter en el campo | — | Cada Enter encola una lectura (research §8). Con coincidencia exacta agrega; con varias, abre el selector; si no hay, muestra un aviso en la barra (4 s) y la venta no cambia |
| Buscar | **F2** | Buscar | Enfoca el campo. Si hay texto no exacto, abre el selector de resultados (lista navegable con ↑/↓ y Enter, Esc para cerrar) |
| Cambiar cantidad | **F4** o `*` | Cantidad | Edita la cantidad de la línea seleccionada en un cuadro. Enter aplica; si es inválida, muestra el mensaje y conserva el valor anterior |
| Mover selección | ↑ / ↓ | — | Entre líneas |
| Quitar línea | **Supr** | Quitar | Quita la línea seleccionada sin confirmar |
| Cobrar | **F12** | Cobrar (el más prominente) | Deshabilitado si `!Cart.CanCheckout`. Ejecuta `ReviewSale`; si hay advertencia de existencia, pide confirmación; luego abre el cobro |
| Cancelar venta | **F8** | Cancelar venta | Confirmación "¿Cancelar la venta en curso?" (Sí/No); al confirmar, vacía y descarta el borrador |

La barra inferior siempre muestra "F2 Buscar · F4 Cantidad · Supr Quitar · F12 Cobrar · F8
Cancelar" (FR-008).

Líneas:

- Columnas: nombre, SKU, cantidad, precio e importe.
- Una línea no disponible se muestra con fondo de advertencia y el texto "Producto inactivo" o
  "Producto eliminado". Mientras exista, Cobrar está deshabilitado y se muestra el mensaje "Quite
  las líneas señaladas para cobrar".

Mensajes:

- Producto no vendible: "{Nombre} está inactivo y no se puede vender" o "{Nombre} fue eliminado y
  no se puede vender".
- Código inexistente: "No se encontró ningún producto con el código {código}".

Recuperación:

- Al activar la pantalla por primera vez en la sesión, si `GetSaleDraft` devuelve un borrador, se
  muestra un diálogo "Hay una venta sin terminar con N artículos por $X. ¿Desea recuperarla?"
  con dos opciones: Recuperar (predeterminada) y Descartar.

## Cobro (`CheckoutView`, diálogo modal sobre el Punto de venta)

- Encabezado: total de la venta, pagado y **pendiente** o **cambio** en grande.
- Efectivo:
  - Campo "Recibido".
  - Botones de monto rápido: Exacto, $20, $50, $100, $200, $500 y $1,000.
  - Teclas: **F5** para Exacto y **1–6** para los billetes, cuando el foco no está en un campo de
    texto.
- Tarjeta / Transferencia:
  - Campo "Monto" (inicia con el pendiente) y "Referencia" opcional, con el botón Agregar pago.
  - La lista de pagos agregados permite quitarlos (**Supr**).
  - Si el monto excede el pendiente: "El monto con tarjeta o transferencia no puede exceder el
    pendiente de $X".
- Faltante: "Faltan $X" en color de error. Confirmar está deshabilitado mientras `!CanConfirm`.
- **Enter** o **F12**: Confirmar cobro. El comando se deshabilita mientras corre (FR-020).
- **Esc**: regresa a la venta sin perder las líneas. Los pagos capturados se conservan mientras no
  cambie el total (US3, escenario 7).
- Resultados de `ConfirmSale`:
  - Éxito o `AlreadyRegistered`: se muestra "Venta V-000123 registrada · Cambio $X" (el cambio en
    grande) hasta el siguiente escaneo o Enter. Luego el carrito queda vacío con un `DraftId`
    nuevo.
  - `SaleChanged`: se cierra el cobro, se aplica la revisión y se muestra "Los precios o la
    disponibilidad cambiaron; revise el total antes de cobrar".
  - `Conflict` o una falla inesperada: "No se pudo registrar la venta. La venta se conservó;
    intente de nuevo." El carrito y el borrador quedan intactos.

## Ventas realizadas (`SalesHistoryView`)

- Filtros:
  - Desde / Hasta (fechas locales, convertidas a un rango UTC `[desde 00:00, hasta+1 00:00)`).
  - Folio.
  - Estado: Todas, Completadas o Canceladas.
- Paginación de 100, igual que Movimientos.
- Columnas: Folio, Fecha (local `dd/MM/yyyy HH:mm`), Total, Formas de pago ("Efectivo, Tarjeta")
  y Estado. Las canceladas se muestran atenuadas con la etiqueta "Cancelada".
- Enter o doble clic abre el detalle.

## Detalle de venta (`SaleDetailView`, panel o diálogo)

- Encabezado: folio, fecha y estado. Si está cancelada, también motivo, fecha y usuario.
- Líneas: nombre, SKU, cantidad, precio e importe, con los valores guardados.
- Pagos: forma, monto, recibido, cambio y referencia.
- El botón "Cancelar venta" solo aparece si la venta está completada. Abre un formulario con el
  campo "Motivo" (obligatorio, máximo 250) y la confirmación "Se regresarán las existencias y la
  venta quedará cancelada".
- Errores:
  - `InvalidState`: "Esta venta ya está cancelada".
  - `Conflict`: "La venta cambió; vuelva a abrirla".

## Inicio

Las tres tarjetas de gráfica se reemplazan por tarjetas reales en `Pos.Desktop/Sales/SalesCards.cs`
(mismos `Order` 110, 120 y 130). Todas usan una sola llamada a `GetSalesDashboard` por activación,
compartida por las tres.

- **Ventas del día**: valor "$X" y "N ventas". Con cero ventas muestra "$0.00 · 0 ventas" (no el
  estado vacío).
- **Últimos 7 días**:
  - `ChartCard` con 7 barras (etiqueta de día corto "lun 28") y su importe.
  - Una barra en 0 se dibuja vacía.
  - Si los 7 días están en 0, se muestra el estado vacío "Sin ventas en los últimos 7 días".
- **Productos más vendidos**: hasta 5 barras horizontales (nombre y cantidad). Sin datos, se
  muestra el estado vacío "Aún no hay ventas".
- Navegación: "Ventas del día" lleva a Ventas realizadas filtrada por hoy.

`ChartCard : DashboardCard` agrega `IReadOnlyList<ChartBar> Bars`, con `ChartBar(string Label,
string ValueText, double Ratio)`. `HomeView` usa una plantilla para `ChartCard` que dibuja las
barras con `Border` proporcional.
