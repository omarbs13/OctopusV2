# Research: Proveedores y compras básicas

Decisiones de diseño para la spec [020-suppliers-purchases](spec.md). Todas parten de lo existente:

- Inventario (004): `ProductStock`, `InventoryMovement`, `MovementType` en Domain;
  `IInventoryRepository` en Application; `InventoryRepository` en Infrastructure.
- Clientes (014): `Customer` como molde del catálogo (RUC único normalizado, `SearchText`, activo/inactivo).
- Bitácora (018): `IAuditLog.Add(AuditRecord)`, instantáneas `*AuditFields` y `AuditChanges`.
- Permisos (007) y licencia por módulo (012): `Permission`, `RolePermissions`, `ModuleAccess`.
- Reportes (009): grupo "Reportes", página de 100 filas (`ReportPaging.ScreenPageSize`).

No hay ningún "NEEDS CLARIFICATION" abierto: la spec cerró sus cinco dudas en la sesión del
2026-10-01 y el resto se decide aquí.

## §1 Compra como agregado inmutable con líneas

- **Decisión**: agregado `Purchase` (tabla `Purchases`) con colección de `PurchaseLine` (tabla
  `PurchaseLines`), ambos en `Pos.Domain/Purchases`.
  - `Purchase.Register(...)` recibe proveedor, factura, fecha, líneas e impuestos ya interpretados,
    valida todas las reglas de la compra y calcula importes (§4).
  - La única transición es `Void(reason, userId, utcNow)`: `ACTIVE → VOIDED`. No hay métodos para
    editar datos ni líneas (FR-017).
  - La inmutabilidad también se protege en persistencia, en `PosDbContext.RejectImmutableChanges`
    (§10).
- **Por qué**: igual que `SaleReturn` y `CustomerPayment`, la compra es un registro contable que solo
  admite anularse completo (spec, Clarificación 1).
- **Alternativas descartadas**:
  - Guardar las líneas como JSON en `Purchases`: el detalle y el enlace con los movimientos (§3)
    necesitan filas propias con índice.
  - Permitir editar la compra y recalcular movimientos: contradice FR-017 y complica el kárdex.

## §2 Tipos de movimiento "Entrada de compra" y "Anulación de compra"

- **Decisión**: dos valores nuevos en `MovementType`:
  - `Purchase`, código `PURCHASE`, aumenta la existencia.
  - `PurchaseVoid`, código `PURCH_VOID`, disminuye la existencia.
  - Los códigos caben en la columna existente `InventoryMovements.Type` (`HasMaxLength(12)`).
    `PURCHASE_VOID` tendría 13 caracteres; cambiar la longitud haría que EF reconstruya la tabla
    `InventoryMovements` en SQLite, la más grande de la base. Se evita.
  - `IsIncrease()` devuelve `false` para `PurchaseVoid`.
- **Cómo se generan solo por una compra (FR-014)**:
  - `ProductStock.Record(...)` (movimientos manuales) rechaza `Purchase` y `PurchaseVoid` igual que
    hoy rechaza los de venta.
  - `RegisterMovementValidator` ya solo acepta `Initial`, `Receipt`, `AdjustIn` y `AdjustOut`; no
    cambia.
  - Métodos nuevos en `ProductStock`:
    - `RecordPurchase(quantity, unit, productActive, tracksInventory, reference)`: valida cantidad,
      decimales, producto activo y que controla inventario, máximo de existencia.
    - `RecordPurchaseVoid(quantity, productActive, tracksInventory, reference)`: exige producto activo
      y que controla inventario (caso límite de la spec) y **existencia suficiente**; nunca deja la
      existencia bajo cero (FR-017b).
  - `Reference` = número de factura tal como se capturó (recortado). Su máximo (50) coincide con
    `InventoryMovement.ReferenceMaxLength`.
- **Alternativas descartadas**:
  - Reutilizar `Receipt` ("Entrada") con una referencia: no se distingue de las entradas manuales
    en el kárdex ni en el filtro (FR-014).
  - Un `AdjustOut` para la anulación: exigiría motivo en el movimiento y se confundiría con ajustes.

## §3 Enlace entre la línea de compra y sus movimientos

- **Decisión**: el enlace vive en `PurchaseLines`, no en `InventoryMovements`:
  - `PurchaseLine.MovementId` (único, FK Restrict → `InventoryMovements`): el `PURCHASE` que generó.
  - `PurchaseLine.VoidMovementId` (nulo, único filtrado, FK Restrict): el `PURCH_VOID` de la anulación.
  - El kárdex (`SearchMovements`) hace dos *left join* desde el movimiento a `PurchaseLines` (por
    `MovementId` y por `VoidMovementId`) y de ahí a `Purchases`, para mostrar el proveedor
    (`SupplierName` guardado en la compra) y abrir la compra. `MovementDto` gana `PurchaseId?` y
    `SupplierName?`.
- **Por qué**: agregar una columna con llave foránea a `InventoryMovements` hace que EF reconstruya la
  tabla en SQLite (`AddForeignKey` no se soporta con `ALTER TABLE`). Con el enlace en una tabla nueva
  la migración solo crea tablas (§12).
- **Alternativa descartada**: escribir el proveedor en `InventoryMovement.Reason`. `Reason` es el motivo
  de los ajustes; mezclarlos confunde el kárdex y la bitácora.

## §4 Importes: línea, subtotal, impuestos y total

- **Decisión**: `PurchaseMath` en Domain.
  - Importe de línea = `cantidad × costo unitario`, redondeado "mitad hacia arriba" al centavo, con la
    misma fórmula entera que `SaleMath.LineAmount` (milésimas × centavos + 500, entre 1,000). Se
    reutiliza esa función para tener una sola regla de redondeo.
  - Subtotal = Σ importes de línea ya redondeados (caso límite de la spec).
  - Impuestos: un solo importe capturado ≥ 0; vacío = 0 (FR-010a). Se interpreta con `Money.Parse`.
  - Total = subtotal + impuestos.
  - Se guardan los tres en centavos (`SubtotalCents`, `TaxCents`, `TotalCents`) y el importe de cada
    línea (`AmountCents`).
- **Límites**: importe de línea, subtotal, impuestos y total ≤ `Money.MaxCents` ($999,999.99), igual
  que una venta. Si se excede, la compra se rechaza con un mensaje que lo indica. Una factura mayor
  se captura en dos compras (no se espera en el negocio de este POS).
  - Desbordamiento: 9,999,999.999 × $999,999.99 cabe en `long` (≈ 1 × 10¹⁸); la suma usa `checked`.
- **Bonificación (FR-008, FR-008a)**: una línea con costo $0.00 es bonificación. No se guarda un
  indicador: `PurchaseLine.IsBonus => UnitCostCents == 0`. La compra se rechaza si el subtotal es 0.
- **Cálculo durante la captura (Principio III)**: el ViewModel no suma. Llama al caso de uso sin
  base de datos `CalculatePurchaseTotals`, que usa `PurchaseMath` y devuelve importes, bandera de
  bonificación y errores por línea. Es la misma función que usa `RegisterPurchase` al guardar, así
  lo mostrado y lo guardado coinciden.
- **Ejemplos de la spec (pruebas de dominio)**: 5 × $12.50 + 2.5 kg × $40.00 = $162.50, + $26.00 =
  $188.50; 3 × $10.00 + 1.255 kg × $20.00 = $30.00 + $25.10 = $55.10, + $8.82 = $63.92.

## §5 Factura duplicada por proveedor

- **Decisión**:
  - `Purchase.InvoiceNumber`: lo capturado, recortado (para mostrar).
  - `Purchase.InvoiceKey`: recortado y en mayúsculas invariantes (para comparar), como
    `Customer.NormalizeTaxId`.
  - Índice único **filtrado** `IX_Purchases_Supplier_InvoiceKey` sobre (`SupplierId`, `InvoiceKey`)
    `WHERE "Status" = 'ACTIVE'`: al anular, la factura queda libre (Historia 2, escenario 13).
  - `RegisterPurchase` busca la compra vigente dentro de la transacción para devolver
    `DuplicateInvoice(purchaseId, invoiceDate, registeredAtUtc)` con datos de la existente
    (escenario 5). El índice es la última barrera ante dos guardados casi simultáneos (caso límite):
    la violación se traduce al mismo error.
- **Alternativa descartada**: solo la verificación en código. `BEGIN IMMEDIATE` ya serializa, pero el
  índice protege también contra defectos futuros sin costo.

## §6 Transacción, concurrencia y fallos al guardar

- **Decisión**: `RegisterPurchase` y `VoidPurchase` usan `IWriteTransactions.BeginAsync`
  (`BEGIN IMMEDIATE`), como `ConfirmSale` y `CancelSale`.
  - Dentro de la transacción: proveedor activo, productos (activos, que controlan inventario, decimales
    de su unidad), duplicado de factura, existencias con `GetStocksAsync`, movimientos, bitácora y un
    solo `SaveChangesAsync`.
  - `ProductStock.Version` sigue siendo el token de concurrencia; con escritores serializados no hay
    pérdida de cantidades (caso límite: dos compras del mismo producto).
  - Una compra de 200 líneas se guarda en un solo `SaveChanges` (caso límite). Medición en la prueba de
    atomicidad.
  - Ante una excepción no se confirma la transacción: no queda compra, movimiento ni existencia
    (FR-012, SC-006). El ViewModel conserva la captura y muestra el mensaje genérico (Principio I,
    escenario 12).
- **Anulación contra una venta simultánea (caso límite)**: la venta y la anulación se serializan; la
  anulación lee la existencia ya afectada por la venta y se rechaza si no alcanza.

## §7 Anulación de compras

- **Decisión**: `VoidPurchase(purchaseId, expectedVersion, reason)`.
  - Permiso `VoidPurchases`, solo Administrador y **no autorizable** (FR-026): un Cajero no puede pedir
    autorización para anular.
  - Motivo obligatorio, hasta 250 caracteres (`InventoryMovement.ReasonMaxLength`, mismo valor que
    `SaleReturn`).
  - Orden: licencia → permiso → motivo → compra existe → estado `ACTIVE` (`InvalidState` "La compra
    ya está anulada") → versión → por cada línea: producto activo, que controla inventario y con
    existencia ≥ cantidad. Si alguna falla, `PurchaseVoidBlocked` con **todas** las líneas afectadas y
    su causa (FR-017b), sin cambiar nada.
  - Genera un `PURCH_VOID` por línea, enlaza `VoidMovementId`, marca la compra `VOIDED` con
    `VoidedAt`, `VoidedBy` y `VoidReason`, y agrega `PURCHASE_VOIDED` a la bitácora.
  - `Purchase.Void` rechaza una segunda anulación (no se reactiva).
- **Alternativa descartada**: anular dejando la existencia negativa. La spec lo prohíbe (Clarificación 1).

## §8 Datos congelados al registrar (FR-016)

- **Decisión**: la compra guarda `SupplierName`; cada línea guarda `ProductName`, `ProductSku` y
  `UnitCode`. El detalle y el reporte muestran estas copias, no los datos actuales.
  - `UnitCode` se congela porque los decimales y el nombre de la unidad salen del catálogo fijo
    `UnitsOfMeasure` (sembrado con `HasData`), que no cambia.
- **Precedente**: `SaleLine` con el producto y `Receivable.CustomerName` (014).

## §9 Proveedor

- **Decisión**: agregado `Supplier` (tabla `Suppliers`) en `Pos.Domain/Suppliers`, con el molde de
  `Customer`:
  - Nombre obligatorio (≤ 150), RUC opcional (≤ 20, recortado y en mayúsculas, único entre todos si no
    es nulo), teléfono opcional (≤ 30), email opcional con `Customer.IsValidEmail` (se mueve a
    `Pos.Domain/Common/EmailAddress.IsValid` para compartirla; `Customer.IsValidEmail` la invoca),
    dirección opcional (≤ 300).
  - Condiciones de pago: `PaymentTerms` = `CASH` | `CREDIT`; `CreditDays` nulo en contado y 1–365 en
    crédito (FR-002). Al pasar a contado se limpia `CreditDays`.
  - `SearchText` = nombre + RUC con `TextNormalizer.ForSearch`, para buscar por cualquiera (FR-004).
  - `IsActive`; desactivar y reactivar siempre se permite. No hay borrado: la spec solo pide
    desactivar (FR-006) y así un proveedor con compras nunca se elimina.
  - RUC duplicado → `SupplierTaxIdInUse(supplierId, name)` para mostrar el proveedor existente
    (Historia 1, escenario 2). Se verifica en la transacción; el índice único filtrado es la última
    barrera.
- **Alternativa descartada**: tabla común de "terceros" para clientes y proveedores. No hay ningún
  requisito que las relacione y obligaría a migrar `Customers`.

## §10 Inmutabilidad en persistencia

- **Decisión**: `PosDbContext.RejectImmutableChanges` agrega:
  - `PurchaseLine`: solo se permite cambiar `VoidMovementId` de nulo a un valor (al anular).
    Cualquier otra modificación o borrado lanza `InvalidOperationException`.
  - `Purchase`: no se borra; una compra `VOIDED` no se modifica; en una `ACTIVE` solo pueden cambiar
    `Status`, `VoidedAt`, `VoidedBy`, `VoidReason` y los campos de auditoría (`UpdatedAt/By`,
    `Version`).
- **Por qué**: igual que `ShiftCut` y `CashShift` cerrado, la regla se prueba sobre SQLite real
  (`PurchaseImmutabilityTests`).

## §11 Permisos y licencia

- **Decisión**: cuatro permisos nuevos, todos solo del Administrador (`AdminPermissions` incluye todos
  los valores del enum) y ninguno autorizable:

  | Permiso | Uso |
  |---|---|
  | `ManageSuppliers` | Catálogo de proveedores (FR-024) |
  | `RegisterPurchases` | "Inventario > Entrada de mercancía" (FR-025); independiente de `RegisterMovements` |
  | `VoidPurchases` | Anular compras (FR-026) |
  | `ViewPurchaseReport` | "Reportes > Compras" y el detalle de una compra (FR-024) |

  - `ModuleAccess.Required` los asigna a `LicensedModule.Inventory` (spec, Supuestos).
  - El Cajero no recibe ninguno (Historia 2, escenario 16). Como `RolePermissions` es fijo (007), un
    rol nuevo con `RegisterPurchases` se agregaría en el futuro sin tocar `RegisterMovements`.
- **Alternativa descartada**: un único permiso "Compras". La spec pide separar registrar compras
  (FR-025) y reservar la anulación al Administrador (FR-026).

## §12 Migración `SuppliersAndPurchases`

- **Decisión**: una migración de EF Core que solo crea tablas e índices:
  - `CREATE TABLE` `Suppliers`, `Purchases`, `PurchaseLines`.
  - Índices de [data-model.md](data-model.md).
  - **Sin** `ALTER TABLE` sobre tablas existentes, sin reconstrucciones (`ef_temp_`) y sin datos.
    Los tipos nuevos de movimiento son solo valores nuevos en una columna de texto existente.
- `Version` 0.13.0 → **0.14.0**. Base de ejemplo `v0.14.0.db` con proveedores (activo, inactivo, sin
  RUC), una compra vigente con bonificación y una anulada.
- **Requisito previo**: la carpeta `SampleDatabases` no tiene `v0.13.0.db` aunque
  [docs/migraciones.md](../../docs/migraciones.md) la describe. Se genera primero con el código de la
  0.13.0 (commit `614a463`), antes de agregar la migración nueva, para que `SampleDatabaseUpgradeTests`
  cubra la migración desde 0.13.0.
- `SuppliersAndPurchasesMigrationTests` revisa el SQL (tres `CREATE TABLE`, sin `ALTER TABLE`,
  `DROP TABLE`, `UPDATE` ni `ef_temp_`) y que `v0.13.0.db` migre sin perder movimientos ni existencias.

## §13 Reporte de compras

- **Decisión**: `IPurchaseReportReader.SearchAsync(PurchaseReportFilter)` en Application, implementado
  en `Infrastructure/Reports/PurchaseReportReader`.
  - Filtros: `SupplierId?` (incluye inactivos), `FromDate?` y `ToDate?` (fecha de factura, inclusivos),
    `MinTotalCents?` y `MaxTotalCents?` (total con impuestos), `IncludeVoided`, página.
  - Orden: `InvoiceDate` desc, `CreatedAt` desc, `Id` desc.
  - Página de `ReportPaging.ScreenPageSize` (100).
  - Acumulados en **una consulta agregada** sobre todo el filtro, contando solo `Status = 'ACTIVE'`:
    `COUNT`, `SUM(SubtotalCents)`, `SUM(TaxCents)`, `SUM(TotalCents)` (FR-021, FR-021a). Las anuladas
    se listan con "Anulada" cuando se piden, sin sumarse.
  - `LineCount` se guarda en `Purchases` para no agrupar líneas en cada página.
  - Índices: (`InvoiceDate`, `CreatedAt`, `Id`) y (`SupplierId`, `InvoiceDate`). Con 10,000 compras
    (SC-005) cada consulta tarda decenas de milisegundos; una prueba explícita
    `PurchaseReportPerformanceTests` lo mide con 10,000 compras (< 2 s, objetivo < 300 ms).
  - Validación del filtro en el caso de uso (FR-023): fecha inicial > final, mínimo > máximo o importes
    negativos → `ValidationFailed` por campo.
- **Sin exportación a PDF o Excel**: la spec no la pide (Principio VII).
- **Detalle**: `GetPurchase(id)` devuelve encabezado, líneas, estado y datos de anulación.

## §14 Bitácora (FR-018)

- **Decisión**: eventos nuevos en `AuditActions`, entidades `Supplier` y `Purchase`:

  | Código | Texto | Contenido |
  |---|---|---|
  | `SUPPLIER_CREATED` | "Proveedor creado" | `AuditChanges.Created(SupplierAuditFields.Snapshot)` |
  | `SUPPLIER_UPDATED` | "Proveedor modificado" | Solo los campos que cambiaron; sin entrada si no cambió nada |
  | `SUPPLIER_DEACTIVATED` | "Proveedor desactivado" | Cambio de "Estado" |
  | `SUPPLIER_ACTIVATED` | "Proveedor activado" | Cambio de "Estado" |
  | `PURCHASE_REGISTERED` | "Compra registrada" | `EntityName` "Compra {factura} · {proveedor}"; cambios: Proveedor, Factura, Fecha de factura, Líneas, Subtotal, Impuestos, Total |
  | `PURCHASE_VOIDED` | "Compra anulada" | Mismo `EntityName`, `Reason` = motivo; cambios: Estado "Vigente" → "Anulada", Subtotal, Impuestos, Total |

  - Grupo nuevo del filtro de entidad: `AuditEntityGroup.Purchases` ("Proveedores y compras") con
    `Supplier` y `Purchase`.
- **Alternativa descartada**: registrar cada línea en la bitácora. El detalle de la compra ya las
  guarda y son inmutables; la bitácora registra quién, cuánto y cuándo.

## §15 Interfaz

- **Navegación**:
  - Grupo "Inventario" (orden 20): Existencias (0), Movimientos (10), **Entrada de mercancía (20,
    `RegisterPurchases`)**, **Proveedores (30, `ManageSuppliers`)**.
  - Grupo "Reportes" (orden 7): **Compras (25, `ViewPurchaseReport`)**, entre Inventario (20) y
    Créditos (30).
  - Las páginas nuevas se registran desde `PurchasesModule` (Desktop/Purchases); el reporte, en
    `ReportsModule`, según su convención.
- **Captura de compra (SC-003, 10 líneas en < 3 min)**:
  - Selector de productos reutilizando `ProductPickerViewModel` (busca por nombre, SKU o código de
    barras sobre `SearchStock`, que ya excluye productos que no controlan inventario y, por omisión,
    inactivos; escenario 9).
  - Agregar un producto ya presente enfoca su línea (escenario 10, FR-009); el caso de uso también
    rechaza líneas repetidas.
  - Teclado: Enter agrega el producto y pasa a cantidad; Tab a costo; Enter vuelve al buscador.
  - Totales en vivo con `CalculatePurchaseTotals` (§4).
- **Detalle de compra**: vista compartida que se abre desde el reporte y desde el kárdex (movimiento
  con `PurchaseId`). Botón "Anular compra" visible solo con `VoidPurchases` y si está vigente.
- **Kárdex**: `MovementTypeLabels` agrega "Entrada de compra" y "Anulación de compra"; el filtro por
  tipo los ofrece; la columna de referencia muestra la factura y el proveedor.

## §16 Pruebas (constitución v1.2.0, política mínima)

- **Domain** (`Pos.Domain.Tests`):
  - `PurchaseMathTests`: redondeo por línea, suma de redondeados, límites y los dos ejemplos de §4.
  - `PurchaseTests`: subtotal 0 rechazado, producto repetido, impuestos negativos, fecha futura,
    anulación doble.
  - `ProductStockTests` (ampliada): `RecordPurchase` suma; `RecordPurchaseVoid` rechaza existencia
    insuficiente y producto inactivo; `Record` rechaza los tipos de compra.
  - `SupplierTests`: crédito sin días o con 0, RUC normalizado, email inválido.
  - `RolePermissionsTests` y `ModuleAccess` (ajustadas): el Cajero no tiene los permisos nuevos y
    pertenecen a `Inventory`.
- **Infrastructure sobre SQLite real** (`Pos.Infrastructure.Tests/Purchases`):
  - `RegisterPurchaseTests`: existencia exacta, movimientos con factura, producto sin cambios
    (SC-002), factura duplicada con mayúsculas y espacios, reutilización tras anular.
  - `PurchaseAtomicityTests`: fallo inyectado al guardar → sin compra, movimientos ni existencia
    (SC-006); compra de 200 líneas.
  - `PurchaseConcurrencyTests`: dos compras simultáneas del mismo producto; dos con la misma factura
    (solo una se guarda).
  - `VoidPurchaseTests`: existencia insuficiente, producto inactivo, anulación doble.
  - `PurchaseImmutabilityTests` (§10).
  - `PurchaseReportTests`: acumulados de todas las páginas, exclusión de anuladas, filtros inválidos.
  - `SupplierUseCaseTests`: RUC único entre activos e inactivos.
  - `InventoryConsistencyTests` (**obligatoria**, ampliada): compras y anulaciones mezcladas con
    ventas y ajustes mantienen existencia = último `ResultingStock` y secuencias 1..N.
  - Migración: `SuppliersAndPurchasesMigrationTests` y `SampleDatabaseUpgradeTests` con `v0.13.0.db` y
    `v0.14.0.db`.
- **Arquitectura**: sin reglas nuevas; las carpetas `Suppliers/` y `Purchases/` quedan cubiertas por
  las pruebas de dependencias existentes.
- Sin pruebas de ViewModels, vistas ni DTOs.
