# Research: Devoluciones y cancelaciones

**Funcionalidad**: `013-returns-cancellations` | **Fecha**: 2026-09-30 | **Plan**: [plan.md](plan.md)

El contexto técnico no tiene incógnitas abiertas: es el mismo stack de 001 a 012 y no se agrega
ninguna dependencia. Esta investigación resuelve las decisiones que la especificación deja abiertas
y las integra con el código existente (ventas de 005, turnos de 008, autorización de 007,
licencias de 012).

Hallazgos del código que condicionan el diseño:

- Ya existe `CancelSale` (005/008): marca la venta como cancelada, regresa inventario con
  `SALE_CANCEL` y descuenta el efectivo del turno si la venta es del turno abierto. No hay
  compensación, devolución parcial ni notas de crédito.
- Las ventas **no tienen descuentos ni impuestos por línea**: el importe de una línea es
  `cantidad × precio` redondeado una vez (`SaleMath.LineAmount`). El "recálculo con impuestos y
  descuentos" de la especificación se reduce hoy al importe de línea; la regla se escribe de forma
  que siga siendo exacta si algún día existen.
- La autorización de 007 es de **usuario y contraseña de un Administrador** (`AuthorizeAdmin` emite
  una concesión de un solo uso de 2 minutos). Lo que la especificación llama "PIN" es esa
  contraseña; no se crea un PIN nuevo.
- `LicensedModule.Returns` ya existe (012) pero ningún permiso ni función lo usa todavía.
- `PaymentMethod` se guarda en `TEXT(10)`; `InventoryMovements.Type` en `TEXT(12)`.

---

## §1. Un agregado `SaleReturn` para toda cancelación o devolución

**Decisión**: `SaleReturn` (Domain/Returns) es un registro **inmutable** ligado a una venta, con tipo
(`CANCELLATION` o `PARTIAL`), motivo, autorizador, total, compensación (`REFUND` o `CREDIT_NOTE`),
líneas devueltas (`SaleReturnLine`) y, en reintegro, un renglón por forma de pago
(`SaleReturnRefund`). Tanto la cancelación completa como la parcial lo crean.

**Motivo**:

- Una sola estructura para H1 y H2 (mismo flujo, FR-011) y un historial uniforme (FR-013).
- Es el sitio donde viven motivo, autorizador y compensación; `Sale` conserva su detalle original.

**Alternativas consideradas**:

- Solo columnas en `Sales` (como hoy `CancellationReason`). No admite varias devoluciones parciales
  ni compensación por forma de pago.
- Una tabla de "eventos de venta" genérica. Es más abstracta de lo que se necesita (Principio VII).

---

## §2. Estado de la venta y cantidades devueltas

**Decisión**:

- `Sale.Status` no cambia (`COMPLETED`/`CANCELLED`): la cancelación completa sigue siendo
  `CANCELLED`, así que reportes, Inicio y turnos que ya excluyen canceladas siguen correctos.
- Se agregan `Sales.ReturnedCents` (0 por defecto) y `SaleLines.ReturnedQuantity` (0 por defecto),
  que `Sale.ApplyReturn` actualiza bajo el `Version` existente. "Parcialmente devuelta" y
  "totalmente devuelta" se **derivan** (`0 < ReturnedCents < TotalCents` y `ReturnedCents =
  TotalCents`), no son un estado guardado.
- **Una venta con devoluciones parciales no se puede cancelar completa**: lo restante se devuelve
  seleccionando todas las líneas (devolución parcial por el resto). Evita definir una "cancelación
  del saldo" con dos significados.
- Disponible por línea = cantidad vendida − `ReturnedQuantity`; nunca se devuelve más (FR-009, FR-014).

**Motivo**: cambios aditivos en la base (columnas con valor por defecto, sin reconstruir tablas) y
la concurrencia optimista ya existente de `Sale.Version` protege contra dos devoluciones simultáneas.

**Alternativas consideradas**:

- Estados nuevos `PARTIALLY_RETURNED`/`RETURNED`. Obligarían a cambiar todos los filtros de estado
  de 005, 008 y 009 y duplican lo que ya dicen las cantidades.
- Calcular lo devuelto sumando `SaleReturnLines` en cada consulta. Más lento en el listado y obliga
  a unir tablas en todos los reportes; el total derivado en `Sales` es el atajo.

---

## §3. Monto devuelto por línea y reparto entre formas de pago

**Decisión**:

- **Por línea**, con acumulado para que sea exacto: lo devuelto acumulado de una línea tras devolver
  `q` unidades es `round(importeLínea × q / cantidadVendida)` (mitad hacia arriba, enteros). El monto
  de una devolución es acumulado nuevo − acumulado previo. Devolver todo suma exactamente el
  importe de la línea, sin centavos perdidos ni sobrantes (FR-010).
- **Reparto entre formas de pago** (cierre de la clarificación 1): el total T de la devolución se
  reparte con el método del resto mayor entre los pagos de la venta, usando como peso lo que **aún
  puede devolverse** de cada pago (monto aplicado − ya reintegrado por ese pago). En la primera
  devolución equivale al peso original; después se ajusta para que ningún pago reciba más de lo
  que pagó y para que devolver todo cierre exacto en cero. Empates: orden de captura del pago.
- Los pagos son los **montos aplicados** (neto de cambio), así que un reintegro nunca supera lo
  efectivamente cobrado (caso límite de cambio entregado).
- El cálculo es una función pura en `Pos.Domain/Returns/ReturnMath`.

**Motivo**: SC-004/SC-005 exigen cuadre al centavo; el redondeo por devolución sin acumulado
acumularía errores. Pesar por lo restante evita estados imposibles (devolver más tarjeta de la
pagada) sin pedirle al operador que reparta a mano.

**Alternativas consideradas**:

- Pesar siempre por el pago original: puede dar un monto mayor al disponible en un pago tras
  varias devoluciones.
- Elección libre del operador: descartada en la clarificación (permitiría esconder faltantes).

---

## §4. Compensación: reintegro y nota de crédito

**Decisión**:

- **Reintegro**: un `SaleReturnRefund` por forma de pago con monto > 0.
  - Efectivo: estado `PAID`; sale del efectivo esperado del turno abierto actual (§6).
  - Tarjeta y transferencia: estado `PENDING_REVERSAL`; solo se anota (sin efecto en la caja). El
    Administrador lo marca `REVERSED` (FR-018). Es la única mutación permitida de un registro de
    devolución: cambia estado, fecha y usuario una sola vez, y se audita.
  - Nota de crédito usada como pago: estado `RESTORED`; el monto vuelve al saldo de **esa** nota
    (movimiento `RESTORE`), nunca a efectivo.
- **Nota de crédito**: se emite una nota nueva por el **total** devuelto (sin renglones de reintegro)
  y no mueve efectivo; el dinero cobrado se queda en caja. Una venta con pagos mixtos y compensación
  por nota genera una sola nota.

**Motivo**: es lo que pide la especificación (H1) y evita una nota por cada forma de pago. El
efectivo solo cambia cuando realmente sale dinero de la caja.

---

## §5. Notas de crédito: saldo calculado con una bitácora de movimientos

**Decisión**:

- `CreditNote`: cabecera inmutable (folio, importe inicial, devolución de origen).
- `CreditNoteMovement`: renglones inmutables `ISSUE`, `REDEEM`, `RESTORE`, con monto > 0, venta o
  devolución relacionada y usuario/fecha.
- **El saldo se calcula** (`ISSUE + RESTORE − REDEEM`), no se guarda. Todos los cambios ocurren
  dentro de la transacción `BEGIN IMMEDIATE`, que serializa a los escritores, así que dos cobros
  simultáneos con la misma nota no pueden gastar el saldo dos veces.
- Folio `NC-000001` (consecutivo `MAX + 1` en la transacción, índice único), como los folios de
  venta y de turno.
- Una venta admite a lo más un pago con nota de crédito (igual que a lo más uno en efectivo).
- Sin vencimiento ni cliente (supuestos de la especificación). Quien presenta el folio la usa.

**Motivo**: misma filosofía que el efectivo esperado de 008 §3 (derivar, no acumular): no hay un
saldo que se desincronice (SC-005). La cantidad de renglones por nota es de unidades.

**Alternativas consideradas**:

- Saldo guardado con `Version`. Obliga a mantener dos fuentes de verdad.
- Un folio aleatorio largo para que no se adivine. Los folios consecutivos son fáciles de
  adivinar; mitigación: el cajero solo ve el saldo al capturar el folio y el uso queda en la
  bitácora. El riesgo se acepta (la especificación no liga el vale a un cliente) y se documenta en
  `docs/devoluciones.md`.

---

## §6. Efectivo esperado y turnos (cierra la clarificación de turnos cerrados)

**Decisión**:

- El reintegro en efectivo se ancla al **turno abierto actual** (`SaleReturn.CashShiftId`), sin
  importar de qué turno era la venta. El turno original, si ya cerró, **no se modifica** (su
  instantánea es inmutable, 008).
- Fórmula nueva (cambia FR-015 de 008):
  `fondo + efectivo de ventas − efectivo cancelado heredado + ingresos − retiros − reintegros en efectivo`
  - `reintegros en efectivo` = suma de renglones `PAID` en efectivo con ese `CashShiftId`.
  - `efectivo cancelado heredado` conserva el comportamiento de 008 solo para ventas canceladas
    **sin** `SaleReturn` (canceladas antes de esta versión, o con el módulo Devoluciones sin
    licencia, §12). Se calcula con `NOT EXISTS (SaleReturns)` sobre ventas canceladas del turno.
  - Una cancelación con nota de crédito no descuenta efectivo (el dinero sigue en caja).
- `ShiftSalesTotals` gana `CashRefundsCents`, `NonCashRefundsCents` (tarjeta y transferencia
  pendientes o reversadas) y `CreditNotesIssuedCents`; `TotalSoldCents` pasa a ser neto de
  devoluciones parciales (`TotalCents − ReturnedCents` de las completadas).
- Un reintegro en efectivo exige un turno abierto; si el efectivo esperado quedaría negativo se
  rechaza con `InsufficientCash(null)`, sin revelar el monto (regla de 008). El mensaje sugiere
  nota de crédito o registrar un ingreso.
- Un Cajero solo puede devolver efectivo si el turno abierto es **suyo** (como vender, 008); un
  Administrador puede hacerlo sobre cualquier turno abierto (`ManageShifts`).
- Con el módulo Turnos sin licencia no hay turno ni límite de efectivo: se omite esa parte (012,
  igual que `CancelSale` hoy).
- Se agregan tres columnas nulas a `CashShifts` para la instantánea del corte (FR-016); los turnos
  ya cerrados quedan con nulos y el corte los muestra como 0.

**Motivo**: es lo que decidió el responsable (devoluciones tardías con el reintegro en la caja
abierta) y mantiene intactos los turnos cerrados y sus arqueos.

**Alternativas consideradas**:

- Reabrir o corregir el turno original: contradice la inmutabilidad del arqueo.
- Registrar el reintegro como un "retiro" de caja: perdería el vínculo con la venta y duplicaría la
  bitácora.

---

## §7. Autorización: siempre una concesión de Administrador, también para el Administrador

**Decisión**:

- Permisos nuevos: `ProcessReturns` (iniciar; ambos roles), `ApproveReturns` (autorizar; solo
  Administrador, **autorizable**) y `ManageCreditNotes` (listar notas y reintegros pendientes,
  marcarlos y configurar el plazo; solo Administrador). Los tres pertenecen al módulo Devoluciones
  en `ModuleAccess`.
- El caso de uso **siempre exige** una concesión de `ApproveReturns` (`AuthorizationGrantId`), sin
  mirar el rol de quien opera. Si falta o no es válida devuelve `Forbidden(ApproveReturns,
  CanBeAuthorized: true)` y no cambia nada (FR-002, criterio 1).
- La interfaz pide la autorización **antes** de enviar la operación (usuario y contraseña de un
  Administrador, con `AuthorizeAdmin` existente). Un Administrador que opera se autoriza a sí mismo
  capturando su propia contraseña (clarificación 4). `AuthorizeAdmin` ya acepta eso: solo verifica
  credenciales de administrador y emite la concesión ligada al solicitante.
- `AuthorizeAdmin` ya registra intentos fallidos (`ADMIN_AUTHORIZATION_DENIED`) y aplica el bloqueo
  de 007: cubre FR-012 (intentos fallidos) y el caso límite del PIN repetido.
- La concesión se consume **dentro** de la transacción, solo después de validar la venta, el plazo y
  las cantidades; así una validación fallida no la gasta. Si el guardado falla después de consumirla,
  se pide de nuevo (la concesión es de un solo uso).

**Alternativas consideradas**:

- Reutilizar `CancelSales` y su regla "si ya lo tienes, no pides autorización". No cumple que el
  Administrador también confirme con su contraseña.
- Un PIN numérico nuevo por usuario: agrega un secreto y su administración sin necesidad.

---

## §8. Inventario

**Decisión**:

- Cancelación completa: se conserva `SALE_CANCEL`. Su etiqueta visible pasa de "Cancelación de
  venta" a **"Devolución por venta cancelada"** (solo texto en `Strings.resx`; el código y los
  datos no cambian).
- Devolución parcial: movimiento nuevo `SALE_RETURN` ("Devolución de venta", 11 caracteres, cabe en
  `TEXT(12)`), por la cantidad devuelta de cada línea con inventario controlado.
- Ambos se generan con `ProductStock` (nuevo `RecordSaleReturn`, análogo a `RecordSaleCancellation`),
  que no revisa el estado actual del producto (caso límite de producto inactivo o borrado).
- Con el módulo Inventario sin licencia no se generan movimientos (012, como hoy).
- `MovementType.IsIncrease` incluye ya todo lo que no sea `AdjustOut`/`Sale`; `SaleReturn` suma.
- La pantalla de movimientos y el reporte de inventario leen la etiqueta del tipo: hay que
  registrar el tipo nuevo ahí.

**Motivo**: la restauración usa la misma regla probada de inventario (004) y la prueba obligatoria
de consistencia (Principio VI) cubre los dos tipos.

---

## §9. Plazo máximo

**Decisión**: `ReturnsSettings { ReturnWindowDays }` (30 por defecto, entre 1 y 3650) guardado en
preferencias con `IPreferencesStore`, igual que `ReportSettings` y `SecuritySettings`. Se compara la
fecha de la venta (UTC) contra la actual. Rige por igual para cancelación y devolución parcial; el
Administrador no puede saltarse el plazo (puede ampliarlo). Cambiarlo se audita
(`RETURN_SETTINGS_CHANGED`).

**Motivo**: el plazo es una política del negocio, no un dato de las ventas, y las preferencias
locales ya resuelven eso sin migración. Costo mínimo, sin dependencias.

---

## §10. Folios y esquema

**Decisión**:

- `SaleReturn.Number` con folio `D-000001` y `CreditNote.Number` con folio `NC-000001`, ambos
  `MAX + 1` en la transacción con índice único (`Duplicate` → reintento como hace `ConfirmSale`).
- `PaymentMethod.CreditNote` con código **`CREDIT`** (6 caracteres): `SalePayments.Method` es
  `TEXT(10)` y `CREDIT_NOTE` (11) obligaría a cambiar la columna, lo que SQLite resuelve
  reconstruyendo la tabla. `SalePayments.CreditNoteId` es una columna nula sin llave foránea (mismo
  criterio que `Sales.CashShiftId` en 008 §5).
- Una sola migración `ReturnsAndCreditNotes`: 5 tablas nuevas y 6 columnas agregadas con valor por
  defecto o nulas; **sin reconstruir** `Sales`, `SaleLines`, `SalePayments` ni `CashShifts`. Una
  prueba revisa el SQL generado.
- `Version` 0.7.0 → 0.8.0.

---

## §11. Totales y reportes

**Decisión**: todo lugar que suma `Sales.TotalCents` de ventas completadas usa
`TotalCents − ReturnedCents` (FR-015):

- Inicio (`GetDashboardAsync`) y totales por turno (`GetShiftTotalsAsync`).
- Listado de turnos y detalle (`CashShiftRepository`).
- Reportes de ventas y de arqueo (`SalesReportReader`, `CashCountReportReader`).
- Resumen del turno del cajero (`GetMyShiftSummary` lee los totales del turno).

El importe de la **fila** de una venta (listado, detalle, reporte por venta) sigue mostrando el total
original; el detalle agrega lo devuelto y el neto. Los tickets originales no se tocan.

Las ventas canceladas siguen excluidas, como hoy. Un reporte de devoluciones queda fuera de alcance.

---

## §12. Licencia (012)

**Decisión**:

- Los permisos nuevos pertenecen al módulo Devoluciones. Sin licencia desaparecen del menú la página
  "Devoluciones y vales" y las acciones de devolución parcial y de nota de crédito (el pago con nota
  también se bloquea, `ModuleNotLicensed`).
- **La cancelación completa básica se conserva** como función base (012, caso límite de
  dependencias entre módulos): con el módulo inactivo, `CancelSale` funciona como en 005/008
  (permiso `CancelSales`, motivo, inventario, descuento de efectivo con la regla heredada, sin
  compensación ni autorización nueva y sin `SaleReturn`). Con el módulo activo, el flujo nuevo
  reemplaza al anterior. Por eso `CancelSales` se conserva.

**Motivo**: sin esto, vencer la evaluación impediría cancelar cualquier venta, que ya es una función
del producto.

---

## §13. Impresión

**Decisión**: `PrintSource.CreditNote(creditNoteId)` en `PrintTicket` y `CreditNoteTicketBuilder` con
la tubería de 006: folio, saldo, fecha, folio de la venta de origen y, al emitir, se imprime sin
pedir confirmación. Reimprimir: `ManageCreditNotes`, o quien emitió la nota en la misma sesión
(`ProcessReturns` y creador). Una falla de impresión no revierte la devolución (se ofrece reintentar).
Los tickets de devolución y de cancelación **no** se imprimen (clarificación 2, opción A).

---

## §14. Interfaz

**Decisión** (detalle en [contracts/ui.md](contracts/ui.md)):

- En `SaleDetailView`, el flujo "Cancelar venta" actual crece a "Devolver o cancelar": selección de
  líneas y cantidades, motivo, compensación, vista previa del monto y del reparto, autorización y
  confirmación. La cancelación completa es el atajo "Seleccionar todo + cancelar la venta".
- El cobro (`CheckoutView`) agrega "Nota de crédito": capturar folio → ver saldo → aplicar.
- Página nueva "Devoluciones y vales" (Administrador, módulo Devoluciones) con notas de crédito,
  reintegros pendientes de reversa y el plazo.
- El detalle de venta muestra el historial de cancelación y devoluciones (FR-013).

---

## §15. Pruebas (política mínima de la constitución v1.2.0)

- **Domain**: `ReturnMath` (importe acumulado y reparto proporcional con topes: SC-004), reglas de
  `SaleReturn` y de `Sale.ApplyReturn` (no exceder lo vendido, no cancelar con devoluciones previas),
  saldo de nota de crédito, `CashShiftMath` con reintegros, `RolePermissions`/`ModuleAccess`.
- **Application/Infrastructure con SQLite real** (en `Pos.Infrastructure.Tests`, como 008):
  cancelación con efectivo, tarjeta, mixto y nota; devolución parcial acumulada; concurrencia de
  doble cancelación; uso de nota con saldo insuficiente; autorización obligatoria incluso para
  Administrador; plazo; atomicidad (si falla el guardado no queda nada); consistencia de
  inventario con `SALE_CANCEL` y `SALE_RETURN` (obligatoria).
- **Migración**: bases de ejemplo hasta `v0.7.0.db`, más una `v0.8.0.db`; el SQL sin reconstrucciones.
- Sin pruebas de ViewModels, vistas, mapeos ni constructores de tickets.
