# Research: Turnos de caja

**Funcionalidad**: `008-cash-shifts` | **Fecha**: 2026-09-30 | **Plan**: [plan.md](plan.md)

El contexto técnico no tiene incógnitas abiertas: el stack es el mismo de 001 a 007 y no se agrega
ninguna dependencia. Esta investigación resuelve las decisiones de diseño que la especificación deja
abiertas y las integra con el código existente (ventas de 005, impresión de 006, usuarios, permisos
y autorización de 007).

---

## §1. Agregado `CashShift` con sus movimientos

**Decisión**: `CashShift` es un agregado nuevo en `Pos.Domain/CashShifts`. Contiene:

- Los datos de apertura.
- La lista de `CashMovement` (ingresos y retiros).
- Al cerrar, el arqueo y una instantánea de los totales.

`CashMovement` solo se crea con `CashShift.RecordDeposit` o `CashShift.RecordWithdrawal`, igual que
`InventoryMovement` solo se crea con `ProductStock.Record` (004). Es inmutable y lleva un
`Sequence` consecutivo dentro del turno.

**Motivo**:

- La regla "un turno cerrado no admite movimientos" (FR-018) y la de "un retiro no excede el
  efectivo esperado" (FR-010) necesitan el estado del turno. Viven en el mismo agregado y se prueban
  sin base (Principio III).
- La concurrencia optimista de `CashShift.Version` protege el cierre contra un movimiento
  simultáneo.

**Alternativas consideradas**:

- `CashMovement` como agregado independiente. Separaría la regla del turno cerrado de su estado y
  obligaría a validarla en Application.
- Guardar un saldo acumulado en el turno. Duplica un dato que se deriva de ventas y movimientos, y
  puede desincronizarse (ver §3).

---

## §2. Un solo turno abierto por caja (FR-004, SC-002)

**Decisión**:

- `CashShifts.RegisterCode` es texto con el valor fijo `"CAJA-1"` (`CashRegister.Default`).
- Un índice único filtrado garantiza un solo turno abierto:
  `IX_CashShifts_OpenPerRegister ON CashShifts(RegisterCode) WHERE Status = 'OPEN'`.
- `OpenShift` también lo verifica dentro de la transacción de escritura (`BEGIN IMMEDIATE`). El
  índice cubre el caso de dos ventanas o de un reinicio a mitad de la operación: la segunda
  inserción falla con `Duplicate` y se traduce a `ShiftAlreadyOpen`.
- El corte imprime la caja como "Caja 1".

**Motivo**:

- La verificación en el caso de uso da el mensaje correcto, y el índice hace imposible el estado
  inválido aunque falle la aplicación (Principio IV).
- `RegisterCode` es la única preparación para varias cajas. Cuesta una columna de texto hoy y evita
  migrar los turnos existentes si algún día hay varias cajas (Principio VII: preparación de costo
  mínimo cuya ausencia obligaría a migrar datos). No se agrega tabla ni pantalla de cajas.

**Alternativas consideradas**:

- Un índice filtrado sobre una constante (`WHERE Status = 'OPEN'` sin columna). Funciona hoy, pero
  obligaría a reconstruir el índice y a asignar la caja a todos los turnos después.
- Una tabla `CashRegisters`. No hay requisito de administrarlas (YAGNI).

---

## §3. Efectivo esperado: se calcula, no se acumula (FR-015, SC-003)

**Decisión**: el efectivo esperado se calcula en cada operación que lo necesita, **dentro** de la
transacción de escritura:

- Registrar un retiro.
- Cancelar una venta en efectivo.
- Contar y cerrar el turno.

La regla vive en `CashShiftMath.ExpectedCash` (Domain):

```text
esperado = fondo inicial
         + efectivo de todas las ventas del turno (monto aplicado, ya neto de cambio)
         − efectivo de las ventas canceladas del turno
         + ingresos − retiros
```

- El efectivo de una venta es `SalePayments.AmountCents` con `Method = 'CASH'`. Ese monto **ya es
  neto de cambio**: `Checkout.CashApplied` guarda lo aplicado a la venta, y `ReceivedCents` y
  `ChangeCents` son informativos (005).
- Las ventas se suman con una consulta agregada, `ISaleRepository.GetShiftTotalsAsync(shiftId)`. Da
  una fila `ShiftSalesTotals` con:
  - Número de ventas completadas y de canceladas.
  - Total vendido (FR-019a).
  - Importe por forma de pago de las completadas.
  - Efectivo bruto y efectivo cancelado.
- Los movimientos ya vienen con el agregado.

**Motivo**:

- La transacción `BEGIN IMMEDIATE` serializa a los escritores. El cálculo ve exactamente lo que
  existe en ese momento, y ninguna venta puede colarse entre el cálculo y la escritura.
- No hay un saldo guardado que pueda desincronizarse con una cancelación o una falla parcial.
- La fórmula es una función pura con enteros en centavos. Se prueba con los escenarios de SC-003
  sin base.
- El costo es una consulta indexada por `Sales.CashShiftId` sobre las ventas de un turno (cientos
  de filas).

**Alternativas consideradas**:

- Un saldo acumulado en `CashShifts` que se actualiza en cada venta. Obliga a tocar el turno en
  `ConfirmSale` y en `CancelSale`, lo que aumenta los conflictos de versión. Además, es un segundo
  dato que puede quedar mal.
- Calcular en memoria cargando todas las ventas. Es innecesario, porque la suma en SQL es exacta con
  enteros.

---

## §4. Importes con signo: la diferencia del arqueo

**Decisión**:

- Fondo inicial, montos de movimientos y efectivo contado son `Money`, que no admite negativos
  (FR-025).
- Los totales calculados (esperado, totales por forma de pago) son `long` en centavos, igual que
  `Sale.TotalCents`. Se convierten a `Money` solo para mostrarlos.
- La diferencia es un `long` con signo en la base (`DifferenceCents` = contado − esperado). En el
  dominio se expone como `CashDifference(Money Amount, DifferenceKind Kind)`, donde `Kind` es
  `Balanced`, `Over` (sobrante) o `Short` (faltante).

**Motivo**:

- `Money.FromCents` rechaza negativos a propósito, y un faltante es negativo.
- La suma de las ventas de un turno podría exceder el máximo de `Money` (999,999.99) en un turno
  muy grande. Con `long`, el cálculo nunca lanza.
- El signo en la base permite filtrar y ordenar por diferencia.

**Alternativas consideradas**:

- Permitir negativos en `Money`. Rompe una garantía de la que dependen precios, pagos y la
  validación de captura.
- Guardar el valor absoluto y el tipo en columnas separadas. Complica los filtros y no aporta nada.

---

## §5. Venta ligada al turno (FR-001, FR-007, SC-001)

**Decisión**:

- `Sales.CashShiftId` es una columna nueva y **nula**. Las ventas anteriores a 0.6.0 quedan con
  `NULL` y se consultan igual que hoy.
- El cajero es `Sales.CreatedBy`, que ya existe (007). No se agrega columna.
- `Sale.Register` recibe `cashShiftId`.
- En `ConfirmSaleHandler`, **dentro** de la transacción y después de la verificación de
  idempotencia por `DraftId`:
  - Carga el turno abierto.
  - Sin turno, devuelve `ShiftRequired`.
  - Con el turno de otro usuario, devuelve `ShiftOwnedByOther(OpenedByName)`.
  - Solo si el turno es del usuario actual, registra la venta con `CashShiftId`.
- Hay un índice `IX_Sales_CashShiftId_Status` y **no** hay llave foránea hacia `CashShifts`.

**Motivo**:

- En SQLite, agregar una columna nula es un `ALTER TABLE ADD COLUMN`, sin reconstruir `Sales`.
  Agregar una llave foránea a una tabla existente obliga a EF Core a reconstruirla. Es la misma
  decisión que tomó 007 con los campos de auditoría (007 research §3). La integridad la garantiza el
  caso de uso, que solo liga ventas al turno abierto leído en la misma transacción.
- Verificar la idempotencia primero conserva el comportamiento de 005: el reintento de una venta ya
  registrada devuelve esa venta aunque el turno se haya cerrado después.

**Alternativas consideradas**:

- Una llave foránea en `Sales`. Reconstruiría la tabla más grande de la base en cada instalación.
- Asignar el turno en el `AuditingInterceptor`. Oculta una regla de negocio en la persistencia.

---

## §6. Cancelación de ventas dentro del turno (FR-008)

**Decisión**: `CancelSaleHandler` agrega, dentro de la transacción y antes de `sale.Cancel`:

1. Si `sale.CashShiftId` es nulo o distinto del turno abierto, devuelve
   `InvalidState("La venta pertenece a un turno cerrado")`. Esto incluye las ventas anteriores a
   0.6.0: no tienen turno y la especificación deja fuera la cancelación de turnos cerrados.
2. Si la venta tiene pago en efectivo, calcula el esperado (§3) y verifica que
   `esperado − efectivo de la venta ≥ 0`. Si no, devuelve `InsufficientCash(AvailableCents: null)`
   con el mensaje "No hay efectivo suficiente en caja para devolver esta venta. Registre un ingreso e
   intente de nuevo". El monto **nunca** se revela, a ningún rol (clarificación 1).

Las reglas de permiso (`CancelSales`, autorizable) y de propiedad de 007 no cambian. Un
administrador puede cancelar cualquier venta del turno abierto, aunque sea de otro usuario.

**Motivo**: la regla depende de dos agregados (venta y turno) y de una consulta. Se orquesta en el
caso de uso, y la aritmética se queda en `CashShiftMath` (Domain).

**Alternativas consideradas**: permitir cancelar ventas sin turno (anteriores a 0.6.0). Su efectivo
no pertenece a ningún arqueo; cancelarlas descuadraría el turno abierto sin razón.

---

## §7. Permisos nuevos

**Decisión**: se agregan tres valores **al final** de `Permission`:

| Permiso | Administrador | Cajero | Autorizable | Cubre |
|---|---|---|---|---|
| `OperateShift` | Sí | Sí | — | Abrir el propio turno, registrar ingresos, cerrar el propio turno, ver el resumen del propio turno, imprimir comprobantes de movimientos del propio turno |
| `WithdrawCash` | Sí | No | **Sí** | Registrar retiros (FR-011) |
| `ManageShifts` | Sí | No | No | Pantalla "Turnos", detalle, reimprimir el corte, cerrar el turno de otro usuario, registrar movimientos en el turno de otro usuario (por ejemplo, un ingreso para poder cancelar una venta, §6) y ver el efectivo disponible al rechazar un retiro |

- `RolePermissions.Authorizable` pasa a ser `{ CancelSales, OpenDrawerWithoutSale, WithdrawCash }`.
- El retiro del cajero usa el diálogo de autorización y las concesiones de un solo uso de 007
  (`AuthorizeAdmin`, `IAuthorizationGrants`), sin cambios.
- `IAccessControl` gana `HasAsync(Permission)`. Consulta el permiso **sin** registrar un rechazo en
  el log. Se usa para decidir si el rechazo de un retiro incluye el monto disponible. Tener
  `ManageShifts` = ver montos.

**Motivo**:

- Se reutiliza el punto único rol → permisos (007 FR-012) y el mecanismo de autorización existente,
  como pide FR-011.
- `HasAsync` evita llenar el log de advertencias falsas cada vez que un cajero recibe el rechazo
  genérico.

**Alternativas consideradas**:

- Reutilizar `Sell` para operar el turno. Mezcla la venta con el control de efectivo y dejaría a un
  futuro rol de "solo venta" con acceso al cierre.
- Un permiso por operación (abrir, ingresar, cerrar). El cajero siempre tiene los tres; separarlos
  no aporta nada.

---

## §8. Arqueo ciego en dos pasos (FR-014, FR-016, FR-017, SC-005)

**Decisión**: el cierre usa dos casos de uso:

1. **`CountShiftCash(ShiftId, CountedCents)`** recibe el conteo y devuelve `ShiftCountResult`:
   esperado, contado, diferencia y totales de tarjeta y transferencia.
   - Deja una entrada `SHIFT_CASH_COUNTED` en la bitácora con el monto contado.
   - Rechaza con `SaleInProgress` o `HeldSaleWillBeDiscarded` (§9) **antes** de revelar cifras.
2. **`CloseShift(ShiftId, ExpectedVersion, CountedCents, ShownExpectedCents, Comment, DiscardHeldSale)`**
   recalcula todo en la transacción:
   - Si el esperado ya no es el mostrado, devuelve `ShiftChanged`. La interfaz vuelve a mostrar las
     cifras.
   - Exige comentario si la diferencia no es cero.
   - Cierra el turno con la instantánea de totales (§10) y audita.

La interfaz no pide el esperado antes del paso 1. Ningún caso de uso que vea un cajero devuelve el
esperado, el fondo inicial, el desglose por forma de pago ni los movimientos (FR-022), salvo
`CountShiftCash` una vez capturado el conteo.

**Motivo**:

- Separar "revelar" de "cerrar" permite mostrar las cifras y pedir el comentario sin dejar el turno
  cerrado a medias (FR-026).
- Auditar cada conteo revelado deja rastro si alguien cuenta varias veces hasta cuadrar. Es la
  debilidad clásica del arqueo ciego, y así se detecta sin bloquear al cajero.
- `ShownExpectedCents` evita cerrar con cifras distintas de las que vio el usuario.

**Alternativas consideradas**:

- Un solo caso de uso con un modo "vista previa". Mezcla una lectura con efectos (la bitácora) y una
  escritura en la misma firma.
- No permitir volver a contar tras revelar. Deja al cajero atorado si se equivocó al teclear; la
  bitácora es suficiente control.

---

## §9. Venta en curso al cerrar (FR-013)

**Decisión**:

- La "venta en curso" es el borrador durable del **dueño del turno** en `SaleDrafts`. Solo existe
  si tiene al menos una línea, porque `SaveSaleDraft` lo borra cuando queda vacío (005).
- El dueño cierra su propio turno y tiene borrador: se devuelve `SaleInProgress`. La interfaz pide
  terminar o cancelar la venta; el Punto de venta ya tiene el carrito en pantalla.
- Un administrador cierra el turno de otro usuario que tiene borrador:
  - Sin `DiscardHeldSale = true`, se devuelve `HeldSaleWillBeDiscarded`, que pide la confirmación.
  - Con la confirmación, `CloseShift` borra el borrador con `ISaleDraftStore.RemoveForAsync` en la
    misma transacción y audita `HELD_SALE_DISCARDED` (el evento de 007), con el turno en los
    detalles (clarificación 3).
- El borrador del administrador que cierra no se toca: pertenece a su propia sesión, no al turno
  cerrado.
- Antes de contar, la interfaz espera el guardado pendiente del borrador
  (`PointOfSaleViewModel.FlushDraftAsync`) para que la verificación vea el estado real.

**Motivo**: se reutilizan el borrador por usuario y el descarte auditado de 007 (research §11). No
hay un concepto nuevo de "venta en curso".

---

## §10. Inmutabilidad del turno cerrado y reimpresión idéntica (FR-018, SC-006)

**Decisión**:

- Al cerrar, `CashShift.Close` guarda una **instantánea** en columnas del propio turno:
  - Número de ventas y de canceladas, y total vendido.
  - Efectivo, tarjeta y transferencia de las ventas completadas, y efectivo cancelado.
  - Ingresos y retiros.
  - Esperado, contado, diferencia y comentario.
  - `ClosedAt` y `ClosedBy`.
- El corte, el listado y el detalle de un turno cerrado leen la instantánea, no recalculan.
- `PosDbContext.RejectImmutableChanges` (hoy `RejectMovementChanges`) también rechaza:
  - Modificar o borrar un `CashMovement`.
  - Modificar un `CashShift` cuyo `Status` original es `CLOSED`.
- En el dominio, `CashShift` rechaza movimientos y un segundo cierre cuando está cerrado.

**Motivo**:

- La reimpresión da exactamente las mismas cifras aunque cambie la lógica de cálculo en una versión
  futura.
- La doble barrera (dominio y persistencia) es la misma que protege los movimientos de inventario y
  la bitácora (004, 005).

**Alternativas consideradas**: recalcular el corte en cada reimpresión. Hoy daría lo mismo, porque
un turno cerrado no admite cambios, pero depende de que la fórmula nunca cambie.

---

## §11. Numeración de turnos y comprobantes

**Decisión**:

- `CashShift.Number` es consecutivo y único. Se calcula con `MAX(Number) + 1` dentro de la
  transacción, igual que el folio de venta (005 research §4).
- El formato es `T-000123` (`ShiftFolio`).
- El comprobante de un movimiento se identifica con el folio del turno y la secuencia: `T-000123-02`.

**Motivo**:

- El operador y el soporte necesitan referirse a "el turno 123". El GUID v7 sigue siendo la llave
  (Principio IV).
- `PrintTicket` usa un folio para nombrar el archivo de la impresora virtual (006).

---

## §12. Impresión: corte y comprobante de movimiento (FR-012, FR-019)

**Decisión**:

- `PrintSource` gana dos variantes: `ShiftReport(Guid ShiftId)` y `CashMovement(Guid MovementId)`.
- `PrintTicketHandler` las construye con un `ShiftTicketBuilder` nuevo en `Printing/Ticket`. Ese
  builder reutiliza el encabezado del negocio, `TextWrap` y los anchos de 32 y 48 columnas de
  `TicketBuilder`.
- **Corte**:
  - "CORTE DE CAJA" (o "REIMPRESIÓN").
  - Caja, turno, usuario que abrió, fechas locales de apertura y cierre, y "Cerrado por" si fue otro
    usuario.
  - Fondo inicial, número de ventas y de canceladas.
  - Totales por forma de pago, efectivo cancelado, ingresos y retiros.
  - Esperado, contado, diferencia (sobrante o faltante) y comentario.
- **Comprobante**: "INGRESO DE EFECTIVO" o "RETIRO DE EFECTIVO", con folio del movimiento, fecha,
  usuario, monto, motivo, "Autorizó" en los retiros autorizados y una línea de firma.
- **Acceso**:
  - Corte: `ManageShifts`, o que el usuario actual sea quien cerró el turno. Así el cajero imprime
    el corte al cerrar.
  - Comprobante: `ManageShifts`, o que el movimiento sea del turno del usuario actual y lo haya
    registrado él.
- El corte se imprime **después** de confirmar el cierre. Una falla de impresión no revierte el
  cierre: se muestra el error con "Reintentar", igual que el ticket de venta (006 FR-009).

**Motivo**: se reutiliza toda la tubería de 006 (configuración, impresora virtual, manejo de
fallas) y ningún dispositivo bloquea una operación de efectivo (Principio I).

**Alternativas consideradas**: un caso de uso `PrintShiftReport` separado. Duplica la carga de
configuración y el manejo de fallas de `PrintTicketHandler`.

---

## §13. Consultas y resúmenes

**Decisión**:

- **`GetCurrentShift`** (`OperateShift`) devuelve `CurrentShiftSummary`, o `null` si no hay turno:
  - Id, versión, número, apertura (UTC) y nombre de quien abrió.
  - `IsMine`, número de ventas completadas y total vendido.
  - Lo usan el Punto de venta, la tarjeta de Inicio (Historia 6) y el resumen del cajero
    (Historia 5, escenario 3).
  - Nunca incluye cifras de efectivo (FR-022).
- **`SearchShifts`** (`ManageShifts`):
  - Filtros: rango de fechas de apertura (UTC, límite superior exclusivo), `UserId?` (quien abrió) y
    `Status?`.
  - Páginas de 100, de la apertura más reciente a la más antigua.
  - Para los turnos abiertos, el total vendido se calcula con una subconsulta agrupada sobre la
    página (a lo más 100 turnos). Para los cerrados, se toma de la instantánea.
  - La diferencia solo existe en los cerrados.
- **`GetShiftDetail`** (`ManageShifts`) devuelve:
  - Todos los datos del turno.
  - Ventas del turno (folio, hora, total, formas de pago, estado).
  - Movimientos (folio, tipo, monto, motivo, usuario, autorizador).
  - El arqueo: la instantánea si está cerrado; si está abierto, el esperado calculado al momento,
    visible solo para el administrador.

**Motivo**: "total vendido" tiene una sola definición, suma de `TotalCents` de las ventas
`COMPLETED` del turno (FR-019a). La calcula una sola consulta del repositorio y se usa en Inicio,
en "Turnos" y en el corte.

---

## §14. Interfaz

**Decisión**:

- **Punto de venta**: al activarse consulta `GetCurrentShift` y muestra uno de tres estados.
  1. **Sin turno**: panel de apertura.
     - Captura del fondo inicial con `MoneyConverter`.
     - Si el fondo es 0, una confirmación explícita antes de abrir (FR-003).
     - La captura de productos queda deshabilitada.
  2. **Turno de otro usuario**: panel bloqueado con el mensaje "Hay un turno abierto de {nombre}.
     Debe cerrarse antes de vender".
     - El administrador ve el botón "Cerrar ese turno" (Historia 1, escenario 5).
     - El cajero solo puede cerrar sesión.
  3. **Turno propio**: la venta normal.
     - Una barra de turno con apertura, número de ventas y total vendido.
     - Botones "Ingreso", "Retiro" y "Cerrar turno".
- **Diálogos** (en `ModalHost`, como el cobro):
  - Apertura.
  - Movimiento (tipo, monto, motivo, y "Imprimir comprobante" al terminar).
  - Cierre en tres pasos: conteo, cifras con comentario y confirmación, e impresión.
- **Pantalla "Turnos"**: en el grupo Ventas, orden 20, con permiso `ManageShifts`.
  - Listado con filtros y paginación, como "Ventas realizadas".
  - Detalle con pestañas: ventas, movimientos y arqueo.
  - Botones "Reimprimir corte" (turno cerrado) y "Cerrar turno" (turno abierto).
- **Tarjeta de Inicio**: `CurrentShiftCard`, métrica de orden 100 (antes de las de ventas, que
  tienen 110). Su permiso es `OperateShift`, así que la ven los dos roles.
- Si `ConfirmSale` devuelve `ShiftRequired` o `ShiftOwnedByOther` (por ejemplo, un administrador
  cerró el turno desde otra ventana), el Punto de venta refresca su estado. El carrito sigue en el
  borrador.

**Motivo**: son los mismos patrones de 005 a 007 (páginas registradas por módulo, `ModalHost`,
`OperationRunner`, tarjetas con `AddDashboardCard`). La interfaz solo coordina casos de uso
(Principio III).

---

## §15. Migración `CashShifts` y base de ejemplo

**Decisión**:

- `Version` pasa de 0.5.0 a 0.6.0 en `Directory.Build.props`.
- **Antes** de crear la migración se genera `v0.6.0.db` con el esquema de `UsersAndRoles`. Trae un
  administrador, un cajero y ventas de ambos. Es el mismo procedimiento de 007 (docs/migraciones.md).
- La migración `CashShifts`:
  - Crea `CashShifts` y `CashMovements` con sus índices. `CashMovements.CashShiftId` tiene llave
    foránea `Restrict`, porque la tabla es nueva.
  - Agrega `Sales.CashShiftId` (nula) con `ALTER TABLE ADD COLUMN`, sin reconstruir.
  - Agrega `IX_Sales_CashShiftId_Status`.
- `CashShiftsMigrationTests`, como `SalesModuleMigrationTests`, revisa el SQL generado. Falla si
  aparece un `DROP TABLE` o la reconstrucción (`ef_temp_`) de una tabla existente.
- `SampleDatabaseUpgradeTests` verifica que, tras migrar cualquier base de ejemplo:
  - Todas las ventas conservan sus datos y tienen `CashShiftId` nulo.
  - `CashShifts` está vacía.
  - El índice único filtrado existe con `WHERE "Status" = 'OPEN'`.

**Motivo**: es obligatorio por el Principio IV y el VI. Revisar el SQL evita reconstruir `Sales` en
producción por accidente.

---

## §16. Pruebas (política mínima, constitución v1.2.0)

Solo se prueban reglas con cálculo de dinero, validaciones de integridad y las pruebas siempre
obligatorias. Cada regla tiene una prueba del caso válido y una del caso límite más importante.

| Proyecto | Prueba | Caso válido | Caso límite |
|---|---|---|---|
| Domain | `CashShiftMathTests` (SC-003) | Fondo, venta en efectivo con cambio, pago mixto, cancelación, ingreso y retiro dan el esperado manual | Una cancelación que dejaría el esperado negativo se detecta |
| Domain | `CashShiftTests` | Retiro igual al esperado se acepta; cierre con diferencia y comentario | Retiro de 1 centavo más que el esperado se rechaza; conteo 0 con esperado > 0 exige comentario; turno cerrado rechaza movimientos |
| Domain | `RolePermissionsTests` (existente, se actualiza) | Matriz del cajero con `OperateShift` | Autorizables = cancelar, cajón y retiro |
| Application | `ConfirmSaleShiftTests` (SC-001) | Venta ligada al turno propio | Rechazo sin turno y con turno de otro usuario |
| Application | `CancelSaleShiftTests` | Cancelación en el turno abierto | Venta de turno cerrado o sin turno; efectivo insuficiente sin monto |
| Application | `RegisterCashMovementTests` | Ingreso del cajero; retiro del administrador | Retiro del cajero sin concesión → `Forbidden`; excedente: cajero sin monto, administrador con monto |
| Application | `CloseShiftTests` | Cierre propio cuadrado | Borrador propio → `SaleInProgress`; administrador con borrador ajeno exige confirmación, descarta y audita |
| Application | `RestrictedOperationsTests` (existente, SC-002 de 007) | — | Agrega los casos de uso nuevos restringidos para el cajero |
| Infrastructure | `CashShiftPersistenceTests` (SQLite real) | Totales del turno por consulta = cálculo manual | Dos aperturas simultáneas: una falla por el índice único (SC-002); modificar un turno cerrado o un movimiento lanza |
| Infrastructure | `SampleDatabaseUpgradeTests` + `v0.6.0.db` y `CashShiftsMigrationTests` | Todas las bases migran | Sin reconstruir `Sales` |
| Arquitectura | Sin cambios | — | Las carpetas nuevas quedan cubiertas por las reglas existentes |

No se agregan pruebas de ViewModels, vistas, DTOs ni del constructor del ticket del corte, que es un
mapeo sin reglas.
