# Research: Descuentos y promociones

**Funcionalidad**: `015-discounts-promotions` | **Fecha**: 2026-10-01 | **Plan**: [plan.md](plan.md)

El contexto técnico no tiene incógnitas abiertas: es el mismo stack de 001 a 014 y no se agrega
ninguna dependencia. Esta investigación resuelve cómo encajan los descuentos en la venta (005), la
autorización (007), el ticket (006), los reportes (009), la licencia (012), las devoluciones (013) y
el crédito (014).

Hallazgos del código que condicionan el diseño:

- `Cart` (Domain) calcula el total como la suma de `CartLine.Amount` (`SaleMath.LineAmount`, mitad
  hacia arriba). `ConfirmSale` reconstruye el `Cart` desde el comando con los precios vigentes; la
  interfaz nunca envía importes.
- `SaleLine.AmountCents` es la base de todo lo posterior: `Sale.TotalCents` es su suma y
  `ReturnMath.LineRefund` devuelve en proporción a ese importe.
- `Sale.Register` y `Checkout.CanConfirm` exigen total > 0; `ConfirmSaleValidator` exige al menos un pago.
- `AuthorizeAdmin` emite concesiones de un solo uso que **vencen en 2 minutos** (`IAuthorizationGrants`),
  pensadas para consumirse en la operación inmediata (cancelar, crédito al cobrar).
- El borrador de la venta (`SaleDraft.LinesJson`) solo guarda producto, cantidad y precio.
- `FindProductsForSale` busca primero coincidencia exacta de código de barras o SKU y después por nombre.
- `ReturnMath.Allocate` ya reparte un monto por resto mayor con suma exacta y topes.
- Las configuraciones por instalación viven en preferencias locales (`IPreferencesStore`), sin tabla.
- `IWriteTransactions` usa `BEGIN IMMEDIATE`: un solo escritor a la vez.

---

## §1. Representación del valor del descuento

- **Decisión**: el porcentaje se guarda en **puntos base** (entero, 1–10 000 = 0.01 %–100 %); el monto
  fijo en **centavos** (`Money`). Un value object `DiscountValue` (modalidad + entero) valida rangos y
  parsea lo capturado ("12.5" → 1250 pb; más de 2 decimales es error, sin redondear, como `Money.Parse`).
- **Por qué**: todo entero (Principio IV); 2 decimales de porcentaje bastan en mostrador.
- **Alternativas**: `decimal` en el dominio (rechazada: se guardaría como texto o real en SQLite);
  porcentaje entero (rechazada: no permite 12.5 %).

## §2. Cálculo y redondeo

- **Decisión**: `DiscountMath` (Domain):
  - Porcentaje: `descuento = (base × pb + 5 000) / 10 000` (mitad hacia arriba, igual que
    `SaleMath.LineAmount`; con importes positivos equivale a "mitad alejándose de cero").
  - Monto fijo: `descuento = valor`, rechazado si `valor > base` en la línea; en el global se
    retira con aviso (spec, Historia 2 escenario 5).
  - Orden: importe de línea → descuento de línea → subtotal → descuento global o cupón → total.
- **Ejemplo verificado**: 99.99 × 15 % = 1 499 850 / 10 000 = 1 499.85 → 1 500 centavos; final 84.99.
- **Alternativas**: redondeo bancario (rechazada: la spec 005 fijó una sola regla).

## §3. Comparación con el límite

- **Decisión**: el límite se guarda en puntos base. Un descuento **supera** el límite si
  `descuento × 10 000 > límite × base` (multiplicación cruzada entera, sin redondear el porcentaje
  equivalente). Con base 0 no hay descuento posible.
- **Por qué**: "exactamente igual al límite" debe pasar sin autorización (spec, casos límite); calcular
  el porcentaje y redondearlo daría falsos positivos o negativos.

## §4. Reparto del descuento global y del cupón entre las líneas

- **Decisión**: al construir la venta, el descuento de venta se reparte en proporción al importe de
  cada línea **después** de su descuento propio, por resto mayor con empate por orden de captura.
  Se extrae `ReturnMath.Allocate` a `Domain/Common/Proportional.Allocate` y `ReturnMath` lo reutiliza.
- **Por qué**: así cada `SaleLine.AmountCents` es lo efectivamente pagado y **las devoluciones de 013
  quedan correctas sin cambiar `ReturnMath`** (FR-020). La suma de las líneas sigue siendo el total.
- **Alternativas**: guardar el descuento global solo a nivel de venta y prorratear al devolver
  (rechazada: cambia 013 y el reporte por producto).

## §5. Cómo se guarda en la venta

- **Decisión**:
  - `SaleLine` agrega `OriginalAmountCents` (cantidad × precio), `LineDiscountCents` y
    `OrderDiscountCents` (parte repartida). `AmountCents` pasa a ser el **importe neto**:
    `original − línea − venta`.
  - `Sale` agrega `DiscountCents` (suma de todos los descuentos) para reportes sin agregaciones.
  - Tabla nueva `SaleDiscounts`, inmutable: un renglón por descuento aplicado (línea, venta o cupón),
    con modalidad, valor capturado, monto, cupón, aplicador, autorizador y fecha (FR-015).
  - La migración copia `AmountCents` en `OriginalAmountCents` para las ventas existentes
    (`UPDATE`, sin reconstrucción de tabla).
- **Por qué**: las ventas anteriores quedan con descuento 0 y siguen mostrándose igual; los
  reportes existentes que suman `AmountCents` o `TotalCents` siguen siendo correctos (ingreso neto).

## §6. Venta con total 0

- **Decisión**: `Sale.Register` acepta total ≥ 0; con total 0 la venta no lleva pagos.
  `Checkout.CanConfirm` acepta total 0 sin pagos. `ConfirmSaleValidator` deja de exigir pagos y el
  manejador valida que haya pagos si el total es mayor que 0. Una venta a crédito con total 0 se rechaza.
- **Por qué**: la spec permite un descuento del 100 % con autorización. El turno no cambia (no hay pagos).

## §7. Autorización: concesión de 2 minutos frente a descuento aplicado minutos antes del cobro

- **Problema**: la concesión de 007 vence en 2 minutos y se consume una vez; el descuento se autoriza
  al aplicarlo, pero la venta puede cobrarse mucho después (o retomarse tras cerrar sesión).
- **Decisión**: caso de uso `ApproveDiscount` que se invoca **al aplicar** el descuento:
  1. Consume la concesión de `ApproveDiscounts` (o, si quien opera es Administrador, usa su propia identidad).
  2. Guarda una fila `DiscountApprovals` ligada al `DraftId`, al solicitante, al alcance (producto o
     venta) y al **porcentaje equivalente aprobado** en puntos base.
  3. Registra `DISCOUNT_AUTHORIZED` en la bitácora (FR-019).
  4. Devuelve el `ApprovalId`, que el `Cart` y el borrador conservan con el descuento.
- En `ConfirmSale`, cada descuento manual que supera el límite **vigente** necesita la aprobación que
  indica su `ApprovalId`, que debe ser del mismo `DraftId`, solicitante y alcance, y con porcentaje
  aprobado ≥ al equivalente actual. No hay excepción para el Administrador: al aplicar un descuento
  sobre el límite también pasa por `ApproveDiscount` (sin concesión), así toda autorización queda en
  `DiscountApprovals` y en la bitácora.
  Si falta, se responde `DiscountApprovalRequired` con el alcance, y la interfaz pide autorización.
- Esto cubre los casos límite de la spec:
  - Si baja la cantidad con monto fijo, el equivalente sube y la aprobación ya no lo cubre: se pide de nuevo.
  - Si baja el límite, se revalida al cobrar.
  - La venta conservada guarda el `ApprovalId` y sobrevive al reinicio.
- **Alternativas**: alargar la concesión (rechazada: cambia la seguridad de 007); guardar la
  aprobación solo en memoria del ViewModel (rechazada: la interfaz no es fuente de verdad y se
  pierde al conservar la venta).

## §8. Permisos y licencia

- **Decisión**: módulo nuevo `LicensedModule.Discounts` con un GUID fijo nuevo en `ModuleCatalog`
  (generado una sola vez con `Guid.CreateVersion7`). Hay 4 permisos nuevos y todos se mapean a ese
  módulo en `ModuleAccess`:

  | Permiso | Roles | Autorizable |
  |---|---|---|
  | `ApplyDiscounts` | Cajero y Administrador | No |
  | `ApproveDiscounts` | Administrador | Sí |
  | `ManageDiscounts` (cupones y límite) | Administrador | No |
  | `ViewDiscountReport` | Administrador | No |

- Sin licencia, el punto de venta oculta las acciones de descuento, `FindProductsForSale` no busca
  cupones y `ConfirmSale` rechaza descuentos con `ModuleNotLicensed`. Al retomar un borrador, los
  descuentos se quitan con aviso (spec, casos límite).
- La herramienta del proveedor que emite licencias debe conocer el GUID nuevo: se documenta en
  `docs/descuentos.md`.
- El reporte de descuentos vive en "Descuentos > Reporte" y no depende de `AdvancedReports`. El total
  descontado del reporte de ventas sí pertenece a ese reporte (y a su módulo).

## §9. Cupones

- **Decisión**: agregado `Coupon`:
  - Datos: `Code` normalizado (recortado y en mayúsculas, 3–30 caracteres `A-Z 0-9 -`), `Value`
    (`DiscountValue`), `StartsOn` y `EndsOn` (`DateOnly` local, inclusivos), `UsageLimit` (int?),
    `UsesCount`, `IsActive` y los campos de auditoría y `Version`.
  - El código es único por índice.
  - El estado se deriva con la fecha local de hoy: inactivo > agotado > vencido > por iniciar > vigente.
  - Con usos registrados no se cambian código ni valor (FR-010). No hay borrado físico, solo desactivación.
- **Colisión con productos**: al crear el cupón se rechaza un código igual a un código de barras o
  SKU de un producto no borrado. Al capturar, `FindProductsForSale` busca así: (1) producto exacto,
  (2) cupón exacto si el módulo está activo, (3) por nombre. El cupón encontrado se devuelve con su
  estado; si no es válido, la interfaz muestra la causa.
- **Usos**: se cuentan en la transacción de `ConfirmSale` (`BEGIN IMMEDIATE` + `Version`), así que
  dos ventas no pueden consumir el último uso. La cancelación completa (`SaleReturnProcessor`)
  decrementa `UsesCount` si el módulo está activo; la devolución parcial no lo hace.
- **Alternativas**: contar al aplicar (rechazada: ventas abandonadas consumirían usos).

## §10. Límite de descuento como preferencia local

- **Decisión**: `DiscountSettings { LimitBasisPoints = 1000 }` en `IPreferencesStore`, como
  `ReturnsSettings` y `ReceivablesSettings`. El cambio queda en la bitácora (`DISCOUNT_LIMIT_CHANGED`).
- **Por qué**: es una instalación de una sola caja; no hace falta tabla (Principio VII).

## §11. Borrador y venta conservada

- **Decisión**: `DraftLineDto` agrega `Discount` opcional (modalidad, valor, `ApprovalId?`), y
  `StoredDraft` agrega `OrderDiscount` opcional (manual o código de cupón). El JSON acepta borradores
  viejos sin estos campos.
- Al recuperar:
  - El cupón se revalida al cobrar (FR-012).
  - Sin licencia, los descuentos se descartan con aviso.

## §12. Ticket, consulta y reportes

- **Ticket** ([contracts/ticket-format.md](contracts/ticket-format.md)):
  - Una línea con descuento agrega un renglón "Desc. 10%  -$10.00" bajo la línea y muestra el importe neto.
  - Antes del TOTAL aparecen SUBTOTAL, "Descuento" o "Cupón VERANO10" y "Usted ahorró".
- **Consulta de la venta**: `SaleDetailDto` agrega `SubtotalCents`, `DiscountCents` y `Discounts`
  (lista con aplicador y autorizador por nombre). `SaleLineDto` agrega `OriginalAmountCents` y `LineDiscountCents`.
- **Reporte de ventas**: `SalesTotals.DiscountCents` (suma de `Sales.DiscountCents` de las ventas
  completadas del período).
- **Reporte de descuentos**:
  - `IDiscountReportReader` consulta `SaleDiscounts` unido a `Sales` (completadas).
  - Tiene filtros de período, cajero y tipo, paginación de 100 y exportación con `ExportReportHandler`.
  - Para el total se usa la misma consulta que la tabla, así que coinciden (SC-003).

## §13. Bitácora

- **Decisión**: acciones nuevas en `AuditActions`:

  | Acción | Cuándo |
  |---|---|
  | `DISCOUNT_AUTHORIZED` | Al aprobar. Incluye `DraftId`, alcance, modalidad, valor, equivalente y autorizador. |
  | `DISCOUNT_APPLIED_AUTHORIZED` | Al cobrar. Incluye folio, monto, aplicador y autorizador. |
  | `COUPON_CREATED` | Al crear un cupón. |
  | `COUPON_UPDATED` | Al editar un cupón. |
  | `COUPON_DEACTIVATED` | Al desactivar un cupón. |
  | `COUPON_USE_RELEASED` | Al devolver el uso por una cancelación. |
  | `DISCOUNT_LIMIT_CHANGED` | Al cambiar el límite. |

  Los intentos fallidos ya los registra `AuthorizeAdmin` (`ADMIN_AUTHORIZATION_DENIED`, sin contraseña,
  con el cajero como solicitante). Para que la entrada incluya el contexto que pide la spec (venta, tipo
  y monto), `AuthorizeAdminCommand` agrega un `Context` opcional que el punto de venta llena con el
  descuento y el `DraftId`, y que se agrega al detalle de la entrada. Sin `Context`, el detalle no cambia.

## §14. Pruebas (política mínima, constitución v1.2.0)

- **Domain**:
  - `DiscountValue.Parse`, con rango y decimales.
  - `DiscountMath`: redondeo, monto mayor que la base, y comparación con el límite cuando es igual o mayor.
  - `Proportional.Allocate`, con suma exacta.
  - `Cart`: líneas y global, recálculo al cambiar la cantidad, retiro del monto fijo global, exclusión entre cupón y global.
  - `Coupon`: estado por fecha y usos, edición bloqueada con usos.
  - `Sale.Register` con total 0.
  - `RolePermissions` y `ModuleAccess`.
- **Casos de uso sobre SQLite real**:
  - `ConfirmSale` con y sin aprobación: cubierta, insuficiente o de otro `DraftId`.
  - El último uso del cupón en dos confirmaciones.
  - Cupón vencido al cobrar.
  - Cancelación que devuelve el uso.
  - Devolución parcial de una línea con descuento global: monto exacto.
  - Total del reporte de descuentos igual a la suma de las ventas.
  - Código de cupón que choca con un producto.
- **Obligatorias**: migración de las bases de ejemplo (incluye `OriginalAmountCents = AmountCents`),
  consistencia de inventario y arquitectura.
- Sin pruebas de ViewModels, vistas ni ticket.
