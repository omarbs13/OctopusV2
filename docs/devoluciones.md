# Devoluciones y cancelaciones: guía para soporte

Funcionalidad 013 (versión 0.8.0). Cubre la cancelación completa de una venta, las devoluciones
parciales por línea y cantidad, el reintegro del dinero, las notas de crédito (vales) y el rastro
de auditoría. Pertenece al módulo **Devoluciones** de la licencia (012).

## Conceptos

| Concepto | Qué es |
|---|---|
| **Cancelación** | Anula la venta completa. La venta pasa a `CANCELLED`. |
| **Devolución parcial** | Devuelve líneas o parte de sus cantidades. La venta sigue `COMPLETED`, con `Sales.ReturnedCents` y `SaleLines.ReturnedQuantity` acumulados. "Parcialmente" y "totalmente devuelta" se derivan de esos valores. |
| **Reintegro** | Devuelve el dinero por las mismas formas de pago de la venta. |
| **Nota de crédito (vale)** | Saldo a favor del cliente con folio `NC-000001`, usable como forma de pago. No se convierte en efectivo. |

Cada cancelación o devolución crea un registro inmutable `SaleReturns` (folio `D-000001`) con sus líneas
(`SaleReturnLines`) y, en reintegro, un renglón por forma de pago (`SaleReturnRefunds`).

## Autorización

- **Toda** cancelación o devolución exige la autorización de un Administrador, también cuando quien
  opera es Administrador (se autoriza con su propia contraseña). La interfaz abre el diálogo de
  autorización de 007 al confirmar; la concesión es de un solo uso y vence a los 2 minutos.
- "PIN" en la especificación es la **contraseña de Administrador**; no existe un PIN aparte.
- Los intentos fallidos quedan en la bitácora como `ADMIN_AUTHORIZATION_DENIED` sin la contraseña y
  cuentan para el bloqueo de 5 intentos / 5 minutos de siempre.
- Permisos: `ProcessReturns` (Cajero y Administrador: iniciar), `ApproveReturns` (Administrador:
  autorizar) y `ManageCreditNotes` (Administrador: listar notas y reintegros, marcarlos y cambiar el
  plazo). Un Cajero solo cancela o devuelve **sus propias** ventas.

## Reintegro: reparto por forma de pago

El monto se reparte **proporcionalmente al peso de cada forma de pago** en la venta original, sin
elección del operador, con el método del resto mayor para que la suma sea exacta. El peso es lo que
aún puede devolverse de cada pago, así que ninguna forma recibe más de lo que pagó. Funciones puras en
`ReturnMath` (Domain).

| Forma de pago original | Qué ocurre |
|---|---|
| Efectivo | Sale del efectivo esperado del turno abierto actual (`Status = PAID`). |
| Tarjeta o transferencia | Se anota como **pendiente de reversa manual** (`PENDING_REVERSAL`), sin afectar el efectivo. El Administrador lo marca como reversado en "Devoluciones y vales". |
| Nota de crédito | El monto vuelve como saldo a **esa** nota (`RESTORED`), nunca como efectivo. |

El importe de cada línea usa un acumulado (`round(importe × cantidad / vendido)`, mitad hacia arriba)
para que devolver todo en varias partes sume exactamente el importe de la línea. Hoy las ventas no
tienen descuentos ni impuestos por línea.

## Turnos y efectivo

- Se pueden cancelar o devolver ventas de **cualquier turno**, incluidos turnos cerrados y ventas
  anteriores a los turnos (0.6.0). El reintegro en efectivo se registra en el **turno abierto actual**;
  el turno cerrado no se modifica. Esto reemplaza la regla de 008 que rechazaba esas cancelaciones.
- Un reintegro en efectivo exige un turno abierto utilizable: el propio para un Cajero, cualquiera para
  quien tiene `ManageShifts`. Sin turno abierto solo se puede compensar con nota de crédito, o reintegrar
  tarjeta o transferencia.
- Si el efectivo esperado quedaría negativo se rechaza (`InsufficientCash`) **sin revelar el monto** al
  Cajero. La salida es nota de crédito o registrar un ingreso.
- Fórmula nueva:

```text
esperado = fondo + efectivo de ventas − efectivo cancelado heredado + ingresos − retiros − reintegros en efectivo
```

  "Efectivo cancelado heredado" es el de las ventas canceladas **sin** `SaleReturn` (anteriores a 0.8.0 o
  canceladas con el módulo Devoluciones sin licencia). Una cancelación con nota de crédito no mueve efectivo.
- El "total vendido" (Inicio, "Turnos", corte y reportes) es neto de devoluciones parciales
  (`TotalCents − ReturnedCents`); el importe de la fila de cada venta sigue siendo el original.
- El corte de turno muestra los reintegros en efectivo, los de tarjeta y transferencia pendientes y las
  notas de crédito emitidas durante el turno. Los turnos cerrados antes de 0.8.0 muestran 0.

## Notas de crédito

- Se emiten por el total cancelado o devuelto cuando se elige "Nota de crédito". Se imprime un ticket
  con folio, saldo, fecha y venta de origen; se reimprime desde "Devoluciones y vales".
- El **saldo se calcula** (emisión + restauraciones − usos) con `CreditNoteMovements`; no se guarda.
  Los cambios ocurren dentro de la transacción de escritura, así que dos cobros simultáneos con la misma
  nota no gastan el saldo dos veces.
- En el cobro se captura el folio, se muestra el saldo y se agrega el pago por el menor entre saldo y
  pendiente. A lo más una nota por venta. El Cajero solo consulta el saldo por folio.
- **Riesgo conocido**: el folio es consecutivo y la nota no está ligada a un cliente; quien presente el
  folio puede usarla. No tiene vencimiento ni comisiones. Cada uso queda en la bitácora
  (`CREDIT_NOTE_REDEEMED`).

## Plazo máximo

30 días por omisión (configurable de 1 a 3650 en "Devoluciones y vales", pestaña Configuración, se
guarda en las preferencias locales). Una venta con más antigüedad se rechaza (`ReturnWindowExpired`); el
Administrador puede ampliar el plazo, no saltárselo. El cambio queda como `RETURN_SETTINGS_CHANGED`.

## Licencia

- Sin el módulo Devoluciones desaparecen la página "Devoluciones y vales", "Devolver artículos" y el pago
  con nota de crédito.
- La **cancelación básica se conserva** como función base: con el módulo inactivo, "Cancelar venta"
  funciona como en 005/008 (permiso `CancelSales`, motivo, inventario, efectivo heredado; sin
  compensación, sin autorización nueva y sin `SaleReturn`).

## Inventario

- Cancelación completa: movimiento `SALE_CANCEL` ("Devolución por venta cancelada").
- Devolución parcial: movimiento `SALE_RETURN` ("Devolución de venta").
- Solo para productos que controlan inventario y con el módulo Inventario activo. Se regresa la cantidad
  aunque el producto esté inactivo o borrado.

## Bitácora y registros inmutables

| Acción | Cuándo |
|---|---|
| `SALE_CANCELLED` | Cancelación completa, con compensación, folio de devolución y nota |
| `SALE_RETURNED` | Devolución parcial, con el mismo detalle |
| `CARD_REVERSAL_DONE` | El Administrador marca una reversa de tarjeta como realizada |
| `CREDIT_NOTE_REDEEMED` | Pago con nota de crédito |
| `RETURN_SETTINGS_CHANGED` | Cambio del plazo |

`SaleReturns`, `SaleReturnLines`, `CreditNotes` y `CreditNoteMovements` no se pueden editar ni borrar
(`PosDbContext.RejectImmutableChanges`). `SaleReturnRefunds` admite una sola transición:
`PENDING_REVERSAL → REVERSED`, una vez.

## Diagnóstico rápido

Con la aplicación cerrada (`sqlite3 pos.db`):

```sql
-- Devoluciones y su compensación
SELECT Number, Kind, Compensation, TotalCents, Reason, CreatedAt FROM SaleReturns ORDER BY Number;

-- Acumulados de la venta vs. suma de líneas devueltas (deben coincidir)
SELECT s.FolioNumber, s.ReturnedCents, IFNULL(SUM(l.AmountCents), 0)
FROM Sales s LEFT JOIN SaleReturns r ON r.SaleId = s.Id AND r.Kind = 'PARTIAL'
LEFT JOIN SaleReturnLines l ON l.SaleReturnId = r.Id
GROUP BY s.Id HAVING s.ReturnedCents <> IFNULL(SUM(l.AmountCents), 0);

-- Saldo de cada nota de crédito
SELECT n.Number, SUM(CASE WHEN m.Type = 'REDEEM' THEN -m.AmountCents ELSE m.AmountCents END) AS Saldo
FROM CreditNotes n JOIN CreditNoteMovements m ON m.CreditNoteId = n.Id GROUP BY n.Id;

-- Reintegros de tarjeta pendientes de reversa
SELECT Id, Method, AmountCents FROM SaleReturnRefunds WHERE Status = 'PENDING_REVERSAL';
```

## Fuera de alcance

Cambios de producto como flujo propio, reversas automáticas con el banco, clientes registrados y
reportes dedicados de devoluciones.
