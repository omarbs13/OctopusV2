# Contrato: pantallas y diálogos

Convenciones de 002 a 012: MVVM con CommunityToolkit.Mvvm, componentes en `AddComponentView`,
textos en `Strings.resx` (español), atajos y accesibilidad de 002. Los ViewModels solo invocan casos
de uso (Principio III). Rutas: [contracts/application-ports.md](application-ports.md).

## 1. Detalle de venta (`SaleDetailView`)

- Botones, visibles solo con permiso y módulo (`ProcessReturns`): **"Devolver artículos"** y
  **"Cancelar venta"** (el actual). Se ocultan si la venta está cancelada, totalmente devuelta o fuera
  del plazo (con una leyenda "Fuera del plazo de devoluciones"). "Cancelar venta" también se oculta si
  hay devoluciones previas.
- **Historial** (FR-013): sección con cada cancelación o devolución (folio D-…, fecha, usuario,
  motivo, autorizó, monto, compensación y folio de nota) debajo de las líneas. Las líneas muestran
  "Devuelto: X de Y".
- Sin módulo Devoluciones: solo queda el botón "Cancelar venta" básico de siempre.

## 2. Formulario "Devolver o cancelar" (`ReturnSaleView`, reemplaza a `CancelSaleView`)

Se abre en el mismo panel que hoy abre `CancelSaleViewModel`.

1. **Líneas**: casilla y cantidad por línea (respeta los decimales de la unidad; máximo = disponible).
   "Seleccionar todo" en cancelación completa; "Devolver artículos" parte con todo sin marcar.
2. **Motivo** (obligatorio, hasta 250) y **Compensación**: *Reintegro* | *Nota de crédito*.
3. **Resumen** vía `PreviewReturn`: total a devolver y, con *Reintegro*, el reparto por forma de pago
   (efectivo, tarjeta con "se anotará para reversa manual", nota que se restaura). Si el efectivo no
   alcanza o no hay turno utilizable se muestra el mensaje genérico y se sugiere *Nota de crédito*.
4. **Confirmar**: abre siempre el diálogo `AdminAuthorizationView` de 007 (usuario y contraseña de un
   Administrador, también si quien opera lo es). Con la concesión se invoca `CancelSale` o
   `ReturnSaleItems`.
5. **Resultado**: mensaje de éxito con el folio D-…; con nota de crédito se imprime el ticket y se
   muestra su folio y saldo. Falla de impresión: aviso con "Reintentar" sin deshacer nada.
6. Errores de concurrencia (`Conflict`): se recarga el detalle y se avisa que la venta cambió.

## 3. Cobro (`CheckoutView`)

- Forma de pago nueva **"Nota de crédito"** (solo con módulo Devoluciones): campo de folio y botón
  "Aplicar" → `GetCreditNoteBalance` → muestra el saldo y agrega el pago por
  `min(saldo, pendiente)`. Folio inválido: mensaje del error. A lo más una nota por venta.
- El resto del cobro (efectivo, tarjeta, cambio) no cambia; `Checkout` (Domain) trata la nota como un
  pago no en efectivo.

## 4. Página "Devoluciones y vales" (`ReturnsAdminView`)

Menú: grupo Ventas, orden 30, permiso `ManageCreditNotes`, módulo Devoluciones (desaparece sin
licencia). Tres pestañas:

- **Notas de crédito**: tabla (folio, fecha, importe, saldo, venta de origen), filtro por folio y
  "solo con saldo"; detalle con movimientos y botón "Reimprimir".
- **Reintegros pendientes**: tabla (folio D-…, venta, forma de pago, monto, fecha) con filtro
  Pendientes/Reversados/Todos y botón "Marcar como reversado" con confirmación.
- **Configuración**: plazo máximo en días (1–3650, 30 por defecto) con guardar.

Paginación de 100 filas como "Turnos" (008).

## 5. Turnos y corte (008)

- El detalle de turno y el corte impreso agregan: "Reintegros en efectivo", "Reintegros de tarjeta y
  transferencia pendientes de reversa" y "Notas de crédito emitidas" (FR-016). Los turnos cerrados
  antes de esta versión los muestran en 0.
- "Total vendido" ya es neto de devoluciones parciales.

## 6. Etiquetas

`SALE_CANCEL` se muestra como "Devolución por venta cancelada" y `SALE_RETURN` como "Devolución de
venta" en movimientos y reporte de inventario; `CREDIT` como "Nota de crédito" en formas de pago
(`PaymentMethodLabels`).
