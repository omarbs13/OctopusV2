# Research: Gestión de clientes y crédito

**Funcionalidad**: `014-customers-credit` | **Fecha**: 2026-10-01 | **Plan**: [plan.md](plan.md)

El contexto técnico no tiene incógnitas abiertas: es el mismo stack de 001 a 013 y no se agrega
ninguna dependencia. Esta investigación resuelve las decisiones que la especificación deja abiertas
y las integra con el código existente: ventas (005), autorización (007), turnos (008), reportes
(009), licencias (012) y devoluciones (013).

Hallazgos del código que condicionan el diseño:

- `LicensedModule.CreditAndCustomers` ya existe (012), pero ningún permiso ni función lo usa todavía.
- `PaymentMethod` se guarda en `TEXT(10)`, y el código `CREDIT` ya está ocupado por la **nota de
  crédito** de 013. La venta a crédito necesita un código distinto.
- `Sale.Register` exige que la suma de los pagos sea igual al total. `ShiftSalesTotals` suma el
  efectivo, la tarjeta y la transferencia por método. Una forma de pago que no sea ninguna de esas
  tres no entra al efectivo esperado sin cambiar nada.
- `SaleReturnProcessor` (013) reparte cada devolución entre los pagos originales (`ReturnPlanner`).
  También crea un `SaleReturnRefund` por pago, y el efectivo reintegrado se suma por
  `SaleReturnRefunds.Method = CASH` del turno de la devolución.
- `IWriteTransactions` abre transacciones serializables (`BEGIN IMMEDIATE`), así que solo hay un
  escritor a la vez. Las lecturas dentro de la transacción ven datos firmes.
- La autorización de 007 es con usuario y contraseña de un Administrador: `AuthorizeAdmin` emite una
  concesión de un solo uso que dura 2 minutos (`IAuthorizationGrants`).
- Las configuraciones por instalación (`ReturnsSettings`, `ReportSettings`) se guardan en
  preferencias locales (`IPreferencesStore`), sin tabla.

---

## §1. La venta a crédito es una forma de pago `ACCOUNT`

**Decisión**: se agrega `PaymentMethod.OnAccount` con el código `ACCOUNT`. Una venta a crédito es una
venta normal (`Sale.Register`) con **un solo pago** `ACCOUNT` por el 100 % del total, que cumple la
clarificación de que no hay enganche ni pago mixto. En la misma transacción se crea una **cuenta por
cobrar** (`Receivable`, §3) ligada a la venta y al cliente.

**Motivo**:

- Se conserva la regla de `Sale.Register` (pagos = total) y el resto del flujo de venta no cambia:
  inventario, folio, ticket e idempotencia por `DraftId` (FR-008).
- El efectivo esperado y los totales de tarjeta y transferencia excluyen `ACCOUNT` sin cambios, porque
  se calculan por método (FR-008, SC-007).
- `ACCOUNT` cabe en `TEXT(10)`, así que la tabla `SalePayments` no se toca.

**Alternativas consideradas**:

- Columna `Sales.CustomerId` más una venta sin pagos: rompe la invariante de pagos y exige reconstruir
  `Sales` si se agrega una llave foránea en SQLite.
- Reutilizar `CREDIT`: ese código ya significa nota de crédito y mezclaría ambos conceptos en
  reportes y devoluciones.

`Sale.Register` agrega una regla: un pago `ACCOUNT` debe ser el único pago de la venta.
`Checkout.SetOnAccount()` reemplaza los demás pagos.

## §2. El cliente se liga a través de la cuenta por cobrar, no de la venta

**Decisión**: `Sales` **no** gana columna de cliente. La relación venta–cliente vive en
`Receivables.SaleId` (único) y `Receivables.CustomerId`. La cuenta guarda una copia del nombre del
cliente (`CustomerName`), que es la que imprime el ticket, igual que `SaleLine` copia los datos del
producto.

**Motivo**: según la especificación, solo las ventas a crédito tienen cliente. Con esta decisión la
tabla `Sales` no se toca (ni columnas ni reconstrucción), y el detalle de venta lee el cliente con una
consulta por índice.

**Alternativas consideradas**: `Sales.CustomerId` nulo agrega una columna que estaría vacía en casi
todas las ventas y duplica lo que ya dice la cuenta por cobrar.

## §3. Cuenta por cobrar con saldo guardado y libro de movimientos inmutable

**Decisión**:

- **`Receivable`**, una por venta a crédito, guarda `OriginalCents`, `BalanceCents` y `Status`
  (`PENDING`, `PAID`, `CANCELLED`).
- **`ReceivableEntry`** es el libro **inmutable** de todo lo que mueve ese saldo. Los tipos son
  `PAYMENT` (aplicación de un abono), `PAYMENT_VOID`, `RETURN`, `EXCESS_OUT` y `EXCESS_IN` (§8).
- Invariante: `BalanceCents = OriginalCents + Σ entradas con signo`, y nunca es negativo. Solo lo
  cambian los métodos de `Receivable`.
- **Saldo del cliente** = Σ `BalanceCents` de sus cuentas `PENDING`. Se calcula y no se guarda en el
  cliente. Todas las pantallas lo leen de **una sola consulta**: `ICustomerRepository.GetBalanceAsync`
  y su versión por lote para el reporte (FR-015).

**Motivo**:

- La aplicación de abonos "a la más antigua primero" (FR-011), la anulación (FR-014) y las devoluciones
  (FR-016) actúan **por venta**. El saldo por venta debe estar disponible y ser exacto.
- Guardar `BalanceCents` permite un índice (`CustomerId`, `Status`, `CreatedAt`) para el orden FIFO y el
  reporte, sin sumar todo el libro en cada consulta. Es el mismo precedente que `Sales.ReturnedCents`
  de 013.
- El libro es la "Aplicación de abono" que pide la especificación y la auditoría del saldo. Una prueba
  compara `BalanceCents` contra la suma del libro (SC-004).

**Alternativas consideradas**:

- Saldo solo calculado desde el libro, como la nota de crédito de 013: cada consulta FIFO y el reporte
  tendrían que agrupar todo el libro.
- Saldo guardado en `Customer`: un segundo valor derivado que puede divergir del de las ventas.

## §4. Límite de crédito: `saldo + total ≤ límite`, verificado dentro de la transacción

**Decisión**: `CreditPolicy.Check(balanceCents, limitCents, saleCents)` en Domain devuelve
`Within` o `Exceeded(excessCents)`. La regla es `≤` (caso límite de la especificación).
`ConfirmSale` la evalúa **dentro** de la transacción `BEGIN IMMEDIATE`, con el saldo recién leído,
así que dos ventas a crédito simultáneas no pueden pasar el límite entre las dos.

- Si se excede, `ConfirmSale` devuelve `CreditLimitExceeded(ExcessCents)` y no registra nada.
  La interfaz pide la autorización (007) y reintenta con la concesión.
- La concesión es del permiso nuevo `ApproveCreditOverLimit`, que es autorizable y solo tiene el
  Administrador. Si quien opera es Administrador, ya tiene el permiso y no se le pide su contraseña
  otra vez, igual que `CancelSales` en 007. La bitácora registra la autorización en ambos casos
  (FR-007).
- La venta solo se rechaza si se excede el límite. Las ventas vencidas solo generan un aviso
  (caso límite).

**Alternativas consideradas**: verificar el límite solo en la interfaz no protege contra la
concurrencia ni contra cambios de saldo entre la vista y la confirmación.

## §5. Abono: FIFO, idempotente, siempre dentro de un turno

**Decisión**:

- `PaymentAllocator.Allocate(amountCents, pendientes)` es una función pura en Domain. Recibe las
  cuentas `PENDING` del cliente en orden de la venta (`CreatedAt`, `Id`) y devuelve
  `[(ReceivableId, AmountCents)]`. Rechaza montos ≤ 0 o mayores que el saldo total (FR-010, FR-011).
- `RegisterCustomerPayment` hace todo en una transacción. El orden es: licencia → permiso →
  validación → turno abierto propio (salvo `ManageShifts`) → idempotencia → cliente y saldo → reparto →
  `CustomerPayment` + entradas `PAYMENT` → estados de las cuentas → bitácora → guardar.
- **Doble clic y concurrencia**: el comando lleva un `RequestId` generado por la interfaz al abrir el
  formulario, con índice único. Un reintento con el mismo `RequestId` devuelve el abono ya registrado,
  como `DraftId` en las ventas. La transacción serializada vuelve a leer el saldo, así que nunca queda
  negativo.
- **Turno**: todo abono, con cualquier forma de pago, exige un turno abierto (FR-012) y guarda su
  `CashShiftId`. Si el módulo Turnos no tiene licencia, no hay turnos que controlar y el abono se
  registra sin turno, como las ventas (012, FR-021).
- **Formas de pago**: efectivo, tarjeta y transferencia. Se rechazan `CREDIT` (nota de crédito) y
  `ACCOUNT` (FR-009).
- **Folio**: `AB-000001`, con `MAX + 1` dentro de la transacción e índice único, como las devoluciones.
- El abono guarda `BalanceBeforeCents` y `BalanceAfterCents` para el recibo (FR-013).

## §6. Efectivo del turno y corte

**Decisión**: `ShiftSalesTotals` gana cinco acumulados, con valor 0 por defecto:

- `OnAccountSalesCents`: ventas a crédito del turno, solo informativo.
- `CustomerPaymentsCashCents`: abonos en efectivo hechos en este turno.
- `CustomerPaymentsNonCashCents`: abonos con tarjeta y transferencia hechos en este turno.
- `CustomerPaymentVoidsCashCents`: efectivo devuelto por anulaciones hechas en este turno.
- `CustomerPaymentVoidsNonCashCents`: anulaciones de abonos con tarjeta o transferencia hechas en este
  turno, solo informativo.

`CashShiftMath.ExpectedCash` suma `CustomerPaymentsCashCents` y resta `CustomerPaymentVoidsCashCents`
(FR-012, SC-007). `CashShift` guarda los cinco valores en su instantánea de cierre (columnas nulas en
turnos anteriores) y el ticket de corte los imprime en un bloque "Crédito".

- Los abonos en efectivo **no** son `CashMovement` de ingreso: así no se confunden con los ingresos
  manuales ni con su folio `T-000123-02`, y el corte puede mostrarlos por separado (FR-012).
- Anular un abono en efectivo exige que el esperado del turno actual alcance (`CashShiftMath.CanRefund`).
  Si no alcanza, se rechaza con `InsufficientCash(null)` sin revelar el monto (regla de 008).

**Alternativas consideradas**: registrar el abono como ingreso y la anulación como retiro
(`CashMovement`) reutiliza el cálculo, pero mezcla el abono con los movimientos manuales y no separa los
abonos que no son en efectivo.

## §7. Anulación de abono

**Decisión**: `VoidCustomerPayment` exige siempre una concesión de `VoidCustomerPayments`, también al
Administrador (se autoriza capturando su propia contraseña, como `ApproveReturns` en 013). También exige
un motivo (≤ 250) y un turno abierto (FR-014). Si el módulo Turnos no tiene licencia, se anula sin turno
y sin verificar efectivo, igual que el registro (§5).

- Dentro de la transacción, por cada entrada `PAYMENT` del abono se agrega una entrada `PAYMENT_VOID`
  del mismo monto en la misma cuenta. La cuenta vuelve a `PENDING` si estaba `PAID`.
- `CustomerPayment.Void(...)` es la única mutación del abono: `ACTIVE → VOIDED`, una sola vez. Guarda
  quién la hizo, cuándo, el motivo, el autorizador y `VoidCashShiftId`.
- **Restricción nueva**: si alguna cuenta a la que se aplicó el abono tuvo **después** una devolución o
  cancelación (entradas `RETURN` o `EXCESS_OUT` posteriores), la anulación se rechaza con
  `InvalidState`. Lo abonado ya se reaplicó o se reintegró en esa devolución (§8), y revertirlo dejaría
  un saldo mayor que lo que queda de la venta. Es la opción más segura, deja visible el motivo al
  operador y ya figura en la especificación (FR-014, "Casos límite").
- Para un abono en efectivo se aplica la regla de efectivo de §6.

## §8. Cancelación y devolución de una venta a crédito

**Decisión**: `SaleReturnProcessor` (013) atiende la parte de la venta que se pagó con `ACCOUNT`. Una
función pura de Domain, `CreditReturnSettlement.Settle(montoDevuelto, cuentaDeLaVenta,
otrasPendientes)`, calcula:

1. **Devuelto** (`R`): se registra como entrada `RETURN` por −`R` en la propia cuenta.
2. **Excedente** (`E` = máx(0, `R` − saldo de la cuenta)): es lo que el cliente ya había abonado sobre
   la parte devuelta. Se libera con una entrada `EXCESS_OUT` por +`E` en la propia cuenta, que queda
   en saldo − `R` + `E` ≥ 0 (0 si hubo excedente). Ese mismo excedente se aplica como `EXCESS_IN`
   (negativo) a las **otras** cuentas pendientes del cliente, de la más antigua a la más reciente
   (`PaymentAllocator`).
3. **Reintegro en efectivo** (`CashRefund` = `E` − reaplicado): es lo que sobra cuando ya no queda
   deuda. Sale del **turno abierto**, con las reglas de `ReturnCashGate` de 013 (turno utilizable y
   efectivo suficiente). No genera entrada en el libro, porque no cambia ningún saldo: queda en el
   renglón `CASH` de la devolución.

En la devolución (`SaleReturnRefund`) el pago `ACCOUNT` produce:

- un renglón `ACCOUNT` / `SETTLED` por `R` − `CashRefund`;
- si hay `CashRefund`, un renglón `CASH` / `PAID` ligado al mismo `SalePaymentId`. Así entra solo en
  `CashRefundsCents` del turno, que 013 ya suma por método.

**Reglas**:

- La compensación de una venta a crédito siempre es `REFUND`. Se rechaza `CREDIT_NOTE` ("no existen
  notas de crédito ni saldo a favor" para estas ventas).
- La cuenta pasa a `CANCELLED` cuando la venta se cancela completa o queda totalmente devuelta. Si no,
  pasa a `PAID` o se queda en `PENDING` según su saldo.
- La cancelación **sin** licencia de Devoluciones (ruta heredada de 005/008, research §12 de 013)
  también usa `CreditReturnSettlement`. Una venta a crédito nunca se cancela sin ajustar su saldo.
- La vista previa (`PreviewReturn`) muestra, para estas ventas, cuánto reduce el saldo, cuánto se
  aplica a otras ventas y cuánto se reintegra en efectivo. La interfaz no calcula nada.
- Todo ocurre en la transacción ya existente de la devolución (FR-016, FR-019).

**Alternativas consideradas**: generar un saldo a favor en el cliente queda descartado por la
clarificación del 2026-10-01.

## §9. Plazo de pago configurable

**Decisión**: `ReceivablesSettings { PaymentTermDays = 30 }` (rango 1–3650) en preferencias locales,
con `IReceivablesSettingsStore`. Es el mismo patrón que `ReturnsSettings`, sin tabla. Lo edita el
Administrador (`ManageCustomerCredit`) y se audita como `CREDIT_SETTINGS_CHANGED`.

- El vencimiento **no se guarda**: una cuenta está vencida si `hoy local > fecha local de la venta +
  plazo` y su saldo es > 0. Si cambia el plazo, cambia el estado de todas las cuentas. Es lo esperado
  con un plazo único para todo el negocio (supuestos de la especificación).
- "Días vencido" del cliente es el atraso de su cuenta pendiente más antigua (FR-018). Se calcula en
  días calendario locales, como los periodos de 009.

## §10. Clientes: datos, validación y búsqueda

**Decisión**:

- `Customer` es un agregado con los campos estándar del Principio IV: auditoría, `DeletedAt` y
  `Version`.
- **Activo/inactivo** es `IsActive`. `DeletedAt` no se usa para desactivar.
- **Longitudes**: nombre ≤ 120, teléfono ≤ 30 (obligatorio), email ≤ 254 y RUC ≤ 20.
- **RUC**: se guarda recortado y en mayúsculas, con índice **único filtrado** (`TaxId IS NOT NULL`). Es
  único también entre clientes inactivos, porque identifica a la misma persona.
- **Email**: una sola regla, `Customer.IsValidEmail` en Domain (contiene `@` y un dominio con punto). El
  validador de FluentValidation la reutiliza con `Must(Customer.IsValidEmail)` en lugar de
  `EmailAddress()`, para que una entrada aceptada por el validador nunca la rechace el dominio con una
  excepción.
- **Búsqueda (FR-003)**: la columna `SearchText` (`TextNormalizer`, sin acentos, en minúsculas) concatena
  nombre, teléfono y RUC. La búsqueda usa `LIKE %texto%`. Con miles de clientes responde en menos de
  200 ms (SC-001).
- **Rol (FR-020)**: si el alta la hace un Cajero, el caso de uso **ignora** límite y modalidad y crea el
  cliente "solo efectivo" con límite 0. Al editar, solo `ManageCustomerCredit` puede cambiar límite y
  modalidad. Si un Cajero lo intenta, recibe `Forbidden`.
- **Desactivar** exige saldo 0 (FR-004) dentro de la transacción. Un cliente inactivo no aparece en el
  punto de venta, pero conserva su historial y sigue visible en "Clientes" con un filtro.
- Bajar el límite por debajo del saldo está permitido. El cliente queda "al límite" (caso límite).

## §11. Permisos y licencia

**Decisión**: estos son los permisos nuevos. Todos pertenecen a `LicensedModule.CreditAndCustomers` en
`ModuleAccess`.

| Permiso | Cajero | Administrador | Autorizable |
|---|---|---|---|
| `ManageCustomers` (alta, edición de datos, consulta) | ✓ | ✓ | |
| `SellOnCredit` | ✓ | ✓ | |
| `RegisterCustomerPayments` | ✓ | ✓ | |
| `ManageCustomerCredit` (límite, modalidad, desactivar, plazo) | | ✓ | |
| `ApproveCreditOverLimit` | | ✓ | ✓ |
| `VoidCustomerPayments` | | ✓ | ✓ (siempre exigida) |
| `ViewReceivables` (Reportes > Créditos) | | ✓ | |

Sin licencia, el módulo desaparece de la navegación, el punto de venta no ofrece "Venta a crédito" y
los casos de uso devuelven `ModuleNotLicensed`. Las ventas y cuentas ya registradas se conservan, y
las devoluciones de ventas a crédito siguen ajustando el saldo, porque es integridad de datos y no una
función nueva.

## §12. Recibo de abono y ticket de venta a crédito

**Decisión**:

- `PrintSource.CustomerPaymentSource(paymentId)` y `CustomerPaymentReceiptBuilder` en
  `Printing/Ticket` imprimen el recibo con folio, fecha, cliente, monto, forma de pago, referencia,
  saldo anterior y saldo nuevo (FR-013). Si el abono está anulado, el recibo reimpreso lo indica con
  la leyenda "ANULADO".
- `TicketBuilder` imprime para las ventas a crédito el nombre del cliente y "A crédito: $X" en lugar
  del bloque de pagos (Historia 2, escenario 5).
- Si la impresión falla, el abono no se revierte. El recibo se reimprime desde el historial del
  cliente (caso límite).

## §13. Reporte "Créditos"

**Decisión**: `IReceivablesReportReader.GetAsync(filtro)` devuelve una fila por cliente con saldo > 0:
nombre, saldo, límite, fecha del último abono vigente, días vencido y banderas de estado. Los totales
(saldo total y número de clientes) se calculan **sobre las filas filtradas** (Historia 4, escenario 3).

- **Estados**: `Overdue` (días vencido > 0), `Current` (no vencido) y `AtLimit` (saldo ≥ límite). Un
  cliente puede estar vencido y al límite a la vez. "Al día" y "vencido" se excluyen entre sí;
  "al límite" es independiente.
- Se consulta al abrir la página y al pulsar "Actualizar". No hay caché, así que siempre refleja lo
  último registrado (escenario 4).
- La página vive en el grupo Reportes y la protege `ViewReceivables`. Depende de la licencia
  Crédito y clientes, no de Reportes avanzados.
- Una prueba verifica que el total del reporte es igual a la suma de `GetBalanceAsync` de cada
  cliente (SC-006).

## §14. Migración `CustomersAndCredit` (0.9.0)

**Decisión**:

- Tablas nuevas: `Customers`, `Receivables`, `ReceivableEntries` y `CustomerPayments`, con sus
  índices.
- Columnas nuevas en `CashShifts`: `OnAccountSalesCents`, `CustomerPaymentsCashCents`,
  `CustomerPaymentsNonCashCents`, `CustomerPaymentVoidsCashCents` y
  `CustomerPaymentVoidsNonCashCents` (`long?`), agregadas con `AddColumn` y sin reconstruir la tabla.
- `SalePayments.Method` y `SaleReturnRefunds.Status` no cambian de tamaño, porque `ACCOUNT` y `SETTLED`
  caben.
- Las llaves foráneas solo van **de las tablas nuevas** hacia `Sales` y `CashShifts`, para no
  reconstruir tablas existentes.
- `Version` cambia de 0.8.0 a 0.9.0, con una base de ejemplo nueva según
  [docs/migraciones.md](../../docs/migraciones.md).

## §15. Pruebas (política mínima de la constitución v1.2.0)

**Domain**: las pruebas cubren el caso válido y el caso límite más importante de:

- `CreditPolicy`: igual al límite pasa, un centavo más no pasa.
- `PaymentAllocator`: FIFO, y monto mayor que el saldo.
- `Receivable`: invariante del saldo y transiciones de estado.
- `CustomerPayment.Void`: solo una vez.
- `CreditReturnSettlement`: reduce, reaplica y reintegra.
- `CashShiftMath` con abonos y anulaciones.
- Validaciones de `Customer`: límite negativo y email.
- `RolePermissions` y `ModuleAccess`.

**Infrastructure (SQLite real)**:

- Venta a crédito sobre el límite, con y sin concesión.
- Atomicidad de la venta a crédito ante una falla.
- Abono idempotente por `RequestId` y abonos concurrentes que no dejan saldo negativo.
- Anulación que revierte saldo y estados.
- Cancelación de una venta parcialmente abonada, con excedente reaplicado y reintegrado.
- RUC duplicado rechazado por el índice.
- Total del reporte igual a la suma de saldos.
- `BalanceCents` igual a la suma del libro después de una secuencia mixta (SC-004).
- La prueba de consistencia de inventario (obligatoria) se amplía con una venta a crédito y su
  cancelación.

**Migración**: bases de ejemplo hasta 0.9.0 y SQL sin reconstrucciones.

**Arquitectura**: sin cambios en las reglas. Las carpetas nuevas `Customers/` y `Receivables/` quedan
cubiertas por las pruebas existentes.

**No se prueban** ViewModels, vistas ni los constructores del recibo.
