# Contrato: interfaz de usuario

**Funcionalidad**: `015-discounts-promotions`

Todo se muestra solo si el módulo "Descuentos y promociones" está activo y el usuario tiene el permiso
correspondiente. Los ViewModels no calculan importes: los obtienen del `Cart` (Domain) o de los casos de
uso (Principio III).

## Punto de venta

| Elemento | Comportamiento |
|---|---|
| Acción "Descuento" en la línea (botón y atajo **F7**) | Abre un diálogo pequeño con la modalidad (% o $) y el valor, y muestra la vista previa del importe final. Al aceptar: si no supera el límite, aplica; si lo supera y quien opera es Cajero, abre la autorización de Administrador de 007 (usuario y contraseña), después llama a `ApproveDiscount` y aplica con el `ApprovalId`. Si quien opera es Administrador, llama a `ApproveDiscount` sin concesión. Con cancelación o contraseña incorrecta, la línea no cambia. "Quitar descuento" no pide autorización. |
| Línea con descuento | Muestra el importe original **tachado**, una etiqueta "-10%" o "-$15.00" y el importe final en negrita. |
| Acción "Descuento a la venta" (**Shift+F7**) | Usa el mismo diálogo, sobre el subtotal. Si ya hay un cupón, pregunta "¿Reemplazar el cupón VERANO10 por este descuento?". |
| Captura de código en el campo de productos | Si el resultado es `LookupKind.Coupon` con estado `Active`, aplica el cupón. Si ya había un descuento global, primero pregunta si lo reemplaza. Si el estado es otro, muestra el mensaje y la venta no cambia. |
| Acción "Aplicar cupón" | Abre un campo de código y sigue el mismo flujo. |
| Pie de la venta | Muestra Subtotal, "Descuento (10%)" o "Cupón VERANO10" con el monto negativo y un botón para quitarlo, y el Total. |
| Aviso de descuento global retirado | "El descuento de $X se quitó porque supera el subtotal. Vuelva a aplicarlo si corresponde." |
| Al cobrar, `DiscountApprovalRequired` | Señala la línea o la venta afectada y abre la autorización. Después se reintenta el cobro. |
| Al cobrar, `CouponNotValid` | Muestra el mensaje según el estado, retira el cupón, recalcula el total y no cobra hasta que el cajero confirme de nuevo. |
| Venta de total 0 | El cobro muestra "Total $0.00" y confirma sin pagos. |
| Venta conservada sin licencia | Muestra el aviso "Los descuentos de la venta conservada se quitaron porque el módulo Descuentos y promociones no está activo." |

### Mensajes de cupón (español, sin detalles técnicos)

| Estado | Mensaje |
|---|---|
| no existe | "El código {código} no corresponde a ningún producto ni cupón." |
| `Inactive` | "El cupón {código} está desactivado." |
| `NotStarted` | "El cupón {código} es válido a partir del {fecha}." |
| `Expired` | "El cupón {código} venció el {fecha}." |
| `Exhausted` | "El cupón {código} ya alcanzó su límite de usos." |
| ya hay cupón | "La venta ya tiene el cupón {código}; quítelo para aplicar otro." |

## Administración: "Descuentos" (menú, solo Administrador con `ManageDiscounts`)

- **Cupones**:
  - Listado con búsqueda por código, filtro por estado y paginación de 100. Columnas: código, descuento,
    vigencia, estado, usos y restantes.
  - Formulario corto con código, modalidad, valor, desde, hasta y límite de usos (vacío = sin límite).
  - Con usos registrados, el código, la modalidad y el valor quedan en solo lectura con la nota "El cupón
    ya se usó; solo puede cambiar la vigencia, el límite o desactivarlo."
  - Desactivar y activar.
- **Configuración**: "Descuento máximo sin autorización (%)", con 2 decimales.
- **Reporte** (`ViewDiscountReport`):
  - Selector de período con los presets de 009, filtro por cajero y por tipo (línea, venta, cupón).
  - Tarjetas con el total descontado y la cantidad de descuentos.
  - Tabla con folio, fecha, cajero, tipo, valor, monto, autorizador y cupón.
  - Exportación con los formatos de 009.

## Consultar ventas (detalle)

- Las líneas muestran el original, el descuento y el neto.
- Hay una sección "Descuentos" con tipo, valor, monto, "Aplicó" y "Autorizó".

## Reporte de ventas (009)

- Una tarjeta adicional muestra el "Total descontado" del período.
