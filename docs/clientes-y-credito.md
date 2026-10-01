# Clientes y crédito: guía para soporte

Funcionalidad 014 (versión 0.9.0). Cubre el catálogo de clientes, la venta a crédito con límite, los
abonos con recibo, su anulación, las devoluciones de ventas a crédito y el reporte "Créditos".
Pertenece al módulo **Crédito y clientes** de la licencia (012).

## Conceptos

| Concepto | Qué es |
|---|---|
| **Cliente** (`Customers`) | Nombre, teléfono, email y RUC opcionales, modalidad (`CASH_ONLY` / `CREDIT`) y límite. Se activa y desactiva; nunca se borra. |
| **Venta a crédito** | Una venta normal con **un único pago** `ACCOUNT` por el total. No admite enganche ni pago mixto. |
| **Cuenta por cobrar** (`Receivables`) | Una por venta a crédito: monto original, saldo y estado (`PENDING`, `PAID`, `CANCELLED`). Nunca se borra. |
| **Libro** (`ReceivableEntries`) | Movimientos inmutables que cambian el saldo de una cuenta. |
| **Abono** (`CustomerPayments`) | Pago del cliente, con folio `AB-000001` y recibo. Solo se anula, nunca se edita ni se borra. |

El cliente se liga a la venta a través de su cuenta por cobrar (`Receivables.SaleId`), no con una
columna en `Sales`. La cuenta guarda una copia del nombre del cliente: es la que imprime el ticket aunque
después se edite el cliente.

## Saldo y libro

- El **saldo de una cuenta** (`Receivables.BalanceCents`) se guarda junto con su libro y siempre cumple:
  `BalanceCents = OriginalCents + Σ ReceivableEntries.AmountCents`, entre 0 y `OriginalCents`.
- El **saldo del cliente** no se guarda: es la suma de `BalanceCents` de sus cuentas `PENDING`. Todas las
  pantallas lo leen con la misma consulta.
- Tipos de movimiento del libro (`AmountCents` lleva signo: negativo baja el saldo):

| Tipo | Signo | Origen |
|---|---|---|
| `PAYMENT` | − | Parte de un abono aplicada a la cuenta |
| `PAYMENT_VOID` | + | Reverso exacto de un `PAYMENT` al anular el abono |
| `RETURN` | − | Monto cancelado o devuelto de la venta |
| `EXCESS_OUT` | + | Lo abonado de más sobre lo devuelto, que se libera de la cuenta |
| `EXCESS_IN` | − | Ese excedente aplicado a otra cuenta pendiente del cliente |

Consulta para verificar que todo saldo coincide con su libro (debe devolver 0 filas):

```sql
SELECT r.Id, r.SaleId, r.OriginalCents, r.BalanceCents,
       r.OriginalCents + IFNULL(SUM(e.AmountCents), 0) AS SegunLibro
FROM Receivables r
LEFT JOIN ReceivableEntries e ON e.ReceivableId = r.Id
GROUP BY r.Id
HAVING r.BalanceCents <> SegunLibro OR r.BalanceCents < 0 OR r.BalanceCents > r.OriginalCents;
```

Saldo de un cliente y sus cuentas:

```sql
SELECT c.Name, s.FolioNumber, r.OriginalCents, r.BalanceCents, r.Status, r.CreatedAt
FROM Receivables r
JOIN Customers c ON c.Id = r.CustomerId
JOIN Sales s ON s.Id = r.SaleId
WHERE c.Name LIKE '%Ana%'
ORDER BY r.CreatedAt;
```

## Límite de crédito

- Regla: `saldo + total de la venta ≤ límite` (igual al límite pasa). Se verifica **dentro** de la
  transacción de la venta, así que dos ventas simultáneas no pueden pasar el límite entre las dos.
- Si se excede, un Cajero necesita la autorización de un Administrador (`ApproveCreditOverLimit`). El
  cobro muestra "Excede el límite por $X; requiere autorización" y abre el diálogo de autorización de
  007. Si el que vende es Administrador, no se le pide la contraseña otra vez y él queda como autorizador.
- La bitácora registra `CREDIT_LIMIT_OVERRIDE` con el cajero (`CreatedBy`), el autorizador
  (`AuthorizedBy`), el cliente, la venta, el saldo previo, el límite y el monto. Una concesión inválida o
  vencida deja `ADMIN_AUTHORIZATION_DENIED`, sin la contraseña, y la venta no se registra.
- Bajar el límite por debajo del saldo está permitido: el cliente queda "al límite".
- Tener ventas vencidas no bloquea la venta; el cobro solo muestra un aviso.
- Un cliente "solo efectivo" o inactivo no aparece en el buscador del punto de venta; si cambió mientras
  se cobraba, la venta se rechaza con "Este cliente no tiene crédito disponible".
- El cliente elegido en el punto de venta **no** se guarda en la venta conservada (`SaleDrafts`). Si la
  aplicación se cierra a medio cobro, al recuperar la venta hay que volver a elegir al cliente.

## Abonos

- Se aplican **a la venta más antigua primero** (`CreatedAt`, `Id`) y cubren por completo una cuenta
  antes de pasar a la siguiente (`PaymentAllocator`). Una cuenta que llega a 0 pasa a `PAID`.
- El monto debe ser mayor que 0 y no exceder el saldo del cliente: no existe el saldo a favor.
- Formas de pago: efectivo, tarjeta o transferencia. No se aceptan nota de crédito ni crédito.
- Todo abono, con cualquier forma de pago, exige un **turno abierto** (el propio, o cualquiera con
  `ManageShifts`) y guarda su `CashShiftId`. Con el módulo Turnos sin licencia se registra sin turno.
- **Doble clic**: el formulario genera un `RequestId` al abrirse, con índice único; un reintento con el
  mismo valor devuelve el abono ya registrado.
- El abono guarda el saldo anterior y el nuevo para el recibo. Si la impresión falla el abono no se
  revierte; el recibo se reimprime desde la pestaña "Abonos" de la ficha (con la leyenda "ANULADO" si
  después se anuló).

## Anulación de abonos

- Exige siempre la autorización de un Administrador (`VoidCustomerPayments`), también si quien opera es
  Administrador, y un motivo de hasta 250 caracteres.
- Exige un turno abierto. Si el abono fue en efectivo, el esperado del turno actual debe alcanzar para
  devolverlo; si no, se rechaza **sin revelar el monto** (`InsufficientCash`). Sin licencia de Turnos se
  anula sin turno ni verificación de efectivo.
- Agrega un `PAYMENT_VOID` por cada `PAYMENT` del abono: el saldo vuelve exactamente a como estaba y las
  cuentas pagadas regresan a `PENDING`. El abono pasa a `VOIDED` (una sola vez) y sigue visible.
- **Restricción**: si alguna venta a la que se aplicó el abono se canceló o devolvió **después** del abono,
  la anulación se rechaza ("Este abono se aplicó a una venta que después se canceló o devolvió; no se
  puede anular"). Lo abonado ya se reaplicó o reintegró en esa devolución.

## Devoluciones y cancelaciones de ventas a crédito

Ocurren en la misma transacción de la devolución de 013 (`CreditSettlementService`):

1. Lo devuelto (`R`) baja el saldo de la venta (`RETURN`).
2. Si el cliente ya había abonado sobre esa parte, el excedente (`E = máx(0, R − saldo de la venta)`)
   se libera (`EXCESS_OUT`) y se aplica, de la más antigua a la más reciente, a las **otras** cuentas
   pendientes del cliente (`EXCESS_IN`).
3. Lo que sobra cuando ya no hay deuda se **reintegra en efectivo** desde el turno abierto, con las reglas
   de 013 (turno utilizable y efectivo suficiente).

- La compensación siempre es reintegro: las ventas a crédito no admiten nota de crédito.
- En `SaleReturnRefunds` el pago `ACCOUNT` produce un renglón `ACCOUNT` / `SETTLED` por lo que redujo la
  deuda y, si hubo reintegro, un renglón `CASH` / `PAID` ligado al mismo pago (solo este cuenta como
  reintegro en efectivo del turno).
- La cuenta pasa a `CANCELLED` cuando la venta se cancela completa o queda totalmente devuelta.
- La vista previa de la devolución muestra "Reduce el saldo en $X", "Se aplica a otras ventas del
  cliente $Y" y "Reintegro en efectivo $Z".
- Esto funciona aunque el módulo Crédito y clientes no tenga licencia: es integridad de datos.
- **Sin licencia de Devoluciones** la cancelación básica también ajusta el saldo; como esa ruta no
  registra devoluciones, el origen de los movimientos es la propia venta. Si la cancelación tuviera que
  devolver efectivo por lo abonado de más, se rechaza indicando que se requiere el módulo Devoluciones.

## Turno y corte

- Las ventas a crédito **no** cambian el efectivo esperado. Los abonos en efectivo lo suben y sus
  anulaciones lo bajan:

```text
esperado = … (008 y 013) + abonos en efectivo del turno − anulaciones de abonos en efectivo del turno
```

- El detalle del turno, "Mi turno" y el ticket de corte muestran un bloque "Crédito": ventas a crédito,
  abonos en efectivo, abonos con tarjeta o transferencia y anulaciones. El cierre guarda esos valores en
  `CashShifts` (`OnAccountSalesCents`, `CustomerPayments*`, `CustomerPaymentVoids*`); en los turnos
  cerrados antes de 0.9.0 son nulos y el bloque no aparece.
- Un abono cuenta en el turno en que se registró; su anulación, en el turno en que se anuló
  (`VoidCashShiftId`).

## Plazo de pago y vencidos

- Plazo único para todo el negocio, 30 días por omisión (1 a 3650), en las preferencias locales (clave
  `receivables`). Lo cambia el Administrador en "Reportes > Créditos" y queda `CREDIT_SETTINGS_CHANGED`.
- El vencimiento **no se guarda**: una cuenta está vencida si `hoy local > fecha local de la venta +
  plazo` y tiene saldo. Al cambiar el plazo cambia el estado de todas las cuentas.
- "Días vencido" de un cliente es el atraso de su cuenta pendiente más antigua.

## Reporte "Créditos"

- Una fila por cliente con saldo: saldo, límite, último abono vigente, días vencido y etiquetas.
- Filtros: "Al día" y "Vencido" se excluyen entre sí; "Al límite" (saldo ≥ límite) es independiente.
- Los totales ("Saldo total pendiente" y "Clientes con saldo") se calculan sobre las filas visibles.
- Se consulta al abrir y al pulsar "Actualizar"; no hay caché.

## Permisos y licencia

| Permiso | Cajero | Administrador | Autorizable |
|---|:-:|:-:|:-:|
| `ManageCustomers` (alta, edición de datos, consulta) | ✔ | ✔ | |
| `SellOnCredit` | ✔ | ✔ | |
| `RegisterCustomerPayments` (registrar y reimprimir abonos, pedir su anulación) | ✔ | ✔ | |
| `ManageCustomerCredit` (límite, modalidad, desactivar, plazo) | | ✔ | |
| `ApproveCreditOverLimit` | | ✔ | ✔ |
| `VoidCustomerPayments` | | ✔ | ✔ (siempre exigida) |
| `ViewReceivables` ("Reportes > Créditos") | | ✔ | |

- Si un Cajero da de alta un cliente, se crea "solo efectivo" con límite 0 aunque envíe otros valores.
  Si intenta cambiar el límite o la modalidad al editar, se rechaza.
- Desactivar exige saldo 0; el cliente inactivo no aparece en el punto de venta pero conserva su
  historial y se ve en "Clientes" con "Mostrar inactivos".
- Sin la licencia el grupo "Clientes" y "Reportes > Créditos" desaparecen, el punto de venta no ofrece
  "Venta a crédito" y los casos de uso devuelven `ModuleNotLicensed`. Lo registrado se conserva.

## Bitácora

| Evento | Cuándo |
|---|---|
| `CUSTOMER_CREATED` / `CUSTOMER_UPDATED` | Alta o edición de datos |
| `CUSTOMER_CREDIT_CHANGED` | Cambio de límite o modalidad (valores anterior y nuevo) |
| `CUSTOMER_DEACTIVATED` / `CUSTOMER_ACTIVATED` | Activar o desactivar |
| `CREDIT_SALE_REGISTERED` | Venta a crédito (cliente, saldo previo, límite, monto) |
| `CREDIT_LIMIT_OVERRIDE` | Venta sobre el límite autorizada (con el autorizador) |
| `CUSTOMER_PAYMENT_REGISTERED` | Abono (folio, monto, forma de pago, saldos) |
| `CUSTOMER_PAYMENT_VOIDED` | Anulación (folio, monto, motivo, autorizador) |
| `CREDIT_SETTINGS_CHANGED` | Cambio del plazo de pago |
| `ADMIN_AUTHORIZATION_DENIED` | Concesión inválida al exceder el límite o anular un abono |

El registro de la aplicación (Serilog) anota cada venta a crédito, abono, anulación y rechazo con el
cliente, la venta o el folio, el usuario y los montos; nunca la contraseña.

## Diagnóstico rápido

| Síntoma | Causa probable |
|---|---|
| "Venta a crédito" no aparece en el cobro | No se eligió cliente con "Cliente…", el usuario no tiene `SellOnCredit` o el módulo no tiene licencia. |
| El cliente no aparece en el buscador del punto de venta | Es "solo efectivo" o está inactivo. |
| "Registrar" del abono deshabilitado | No hay turno abierto (o es de otro usuario sin `ManageShifts`). |
| No se puede anular un abono | Ya está anulado, o la venta a la que se aplicó se canceló o devolvió después. |
| No se puede desactivar un cliente | Tiene saldo pendiente; primero debe abonar o cancelarse lo que debe. |
| El saldo no coincide con lo esperado | Ejecute la consulta de "Saldo y libro"; revise devoluciones con excedente reaplicado (`EXCESS_IN`). |
