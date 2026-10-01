# Módulo de ventas: guía para soporte

Guía para soporte técnico y desarrolladores. Describe cómo se comporta el Punto de venta (versión
0.4.0) y dónde mirar cuando algo no cuadra. La especificación completa está en
[specs/005-sales-module](../specs/005-sales-module/spec.md).

## Pantallas y atajos

| Pantalla | Acceso |
|---|---|
| **Punto de venta** | Menú **Ventas → Punto de venta**, o **F9** desde cualquier pantalla |
| **Ventas realizadas** | Menú **Ventas → Ventas realizadas**, o la tarjeta "Ventas del día" de Inicio |

Atajos del Punto de venta (todos tienen también un botón de al menos 48 px, para pantalla táctil):

| Tecla | Acción |
|---|---|
| Enter | Agrega el producto del código, SKU o nombre escrito (un lector de códigos lo hace solo) |
| F2 | Buscar por nombre; con varios resultados abre una lista (↑/↓, Enter, Esc) |
| F4 o `*` | Cambiar la cantidad de la línea seleccionada (`*` solo con el campo de captura vacío) |
| Supr | Quitar la línea seleccionada |
| F12 | Cobrar |
| F8 | Cancelar la venta en curso (pide confirmación) |

En el cobro: **Enter** o **F12** confirman, **Esc** regresa a la venta conservando los pagos, **F5**
pone el monto exacto y **1 a 6** los billetes de $20, $50, $100, $200, $500 y $1,000.

## Folio

- Formato `V-000123`, consecutivo y sin huecos: se calcula como `MAX(FolioNumber) + 1` dentro de la
  transacción de escritura, con un índice único como segunda barrera.
- Una venta que falla **no consume folio**: la transacción se revierte completa.
- En "Ventas realizadas" el filtro de folio acepta `V-000123`, `v123` o `123`.

## Recuperación de la venta en curso

- La venta en curso se guarda en la tabla `SaleDrafts` (una sola fila) después de **cada** cambio.
- Si la aplicación se cierra de golpe, al volver a abrir el Punto de venta se ofrece recuperarla:
  "Hay una venta sin terminar con N artículos por $X. ¿Desea recuperarla?".
- Las líneas cuyo producto ya está inactivo o eliminado se recuperan señaladas y bloquean el cobro
  hasta quitarlas. Un producto que ya no existe se omite y queda un aviso en el log.
- El borrador se borra en la **misma transacción** que registra la venta, así que nunca se ofrece
  recuperar una venta ya cobrada. Si la base tiene una venta con el mismo `DraftId`, un guardado
  atrasado del borrador se ignora.
- Un error al guardar el borrador solo se registra en el log (`No se pudo guardar el borrador de la
  venta`); nunca interrumpe la captura.

## Cobro y pagos

- Tarjeta y transferencia solo se **registran** (con una referencia opcional): no hay terminal.
- Solo el efectivo da cambio. Un pago con tarjeta o transferencia nunca puede exceder el pendiente.
- En `SalePayments`, `AmountCents` es el monto **aplicado** a la venta; en efectivo también se
  guardan lo recibido y el cambio. La suma de los pagos es siempre igual al total.
- Doble pulsación de Enter: el botón se deshabilita mientras corre y, además, el `DraftId` de la venta
  tiene un índice único, así que confirmar dos veces registra una sola venta.

## Existencia negativa

- Vender más de lo que hay en existencia **se permite** después de una advertencia. La existencia
  queda en negativo y en **Existencias** se ve con el estado "Sin existencia".
- Solo las ventas pueden dejar la existencia bajo cero. Un ajuste negativo manual con existencia
  negativa se rechaza; para corregirla, registre una entrada o un ajuste positivo.
- Los movimientos de venta (`SALE`) y de cancelación (`SALE_CANCEL`) no se pueden registrar a mano
  desde el formulario de movimientos. Su referencia es el folio.

## Cancelación y devoluciones

Desde 0.8.0 (módulo Devoluciones) la cancelación y las devoluciones parciales se explican en
[devoluciones.md](devoluciones.md): autorización de un Administrador, reintegro o nota de crédito,
turnos y bitácora. Con el módulo inactivo sigue la cancelación básica:

- Cancelar exige un motivo (máximo 250 caracteres) y cancela la venta **completa**.
- Regresa a la existencia exactamente lo que salió (un movimiento `SALE_CANCEL` por línea con
  inventario), aunque el producto haya quedado inactivo, eliminado o sin control de inventario.
- Una venta cancelada no se puede volver a cancelar y nunca se borra. Las ventas canceladas no
  cuentan en las tarjetas de Inicio.
- Las devoluciones parciales descuentan de esas tarjetas; la fila de la venta conserva su importe
  original y el detalle muestra "Devuelto: X de Y" por línea y el historial de eventos.
- Forma de pago nueva (0.8.0): **Nota de crédito** (código `CREDIT`), con a lo más un pago por venta.

## Consultar la bitácora de auditoría (`AuditEntries`)

La bitácora es inmutable (la aplicación rechaza modificarla o borrarla). Desde 0.5.0 el
Administrador la consulta en **Administración → Bitácora** (ver [usuarios-y-permisos.md](usuarios-y-permisos.md)).
Cada cancelación deja una entrada `SALE_CANCELLED` con el usuario, la fecha (UTC), el folio y el
motivo; si la autorizó un administrador, `AuthorizedBy` trae su id. Para consultarla desde la base,
trabaja siempre sobre una **copia** o un respaldo, nunca sobre la base abierta:

```bash
sqlite3 respaldo.db "SELECT CreatedAt, CreatedBy, AuthorizedBy, EntityId, Details FROM AuditEntries ORDER BY CreatedAt DESC;"
```

`CreatedBy` es el usuario que realizó la operación (tabla `Users`). Los registros anteriores a 0.5.0
pertenecen al usuario "Sistema" (`00000000-0000-7000-8000-000000000001`).

## Ventas por usuario

- Cada venta queda a nombre del cajero que la hizo (`Sales.CreatedBy`) y el ticket lo muestra en la
  línea `Cajero:`.
- Un **Cajero** solo consulta y reimprime **sus propias** ventas; el **Administrador** ve todas y puede
  filtrar por cajero.
- El borrador de la venta en curso es **uno por usuario** (`SaleDrafts.UserId`): al cerrar sesión o
  cambiar de usuario se conserva y se ofrece al volver a entrar con el mismo usuario.

## Diagnóstico rápido

- `Venta registrada. SaleId=… Folio=… Lines=… TotalCents=…`: cada venta correcta (información).
- `Venta no registrada: cambió un precio o una disponibilidad`: el operador vio el total nuevo antes de
  cobrar.
- `Venta no registrada por conflicto`: otra operación se adelantó; la venta se conserva en pantalla.
- Ningún log contiene referencias de pago (Principio VIII).
- Para comprobar la consistencia de un respaldo: la existencia de cada producto (`ProductStocks.OnHand`)
  debe ser igual a la suma con signo de sus `InventoryMovements` (entradas y cancelaciones suman;
  ajustes negativos y ventas restan), y para cada venta la suma de `SaleLines.AmountCents` debe ser
  igual a `Sales.TotalCents` y a la suma de `SalePayments.AmountCents`.
