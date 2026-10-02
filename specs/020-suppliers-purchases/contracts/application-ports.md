# Contrato: casos de uso y puertos de Application

Interfaz que Desktop consume e Infrastructure implementa. Las firmas son orientativas; los nombres
exactos se fijan al implementar.

- Todos los casos de uso devuelven `Result` o `Result<T>` y reservan las excepciones para fallas
  inesperadas (Principio III).
- Todos verifican primero el permiso con `IAccessControl.CheckAsync`, que también aplica la licencia
  `Inventory` vía `ModuleAccess` (`Forbidden` / `ModuleNotLicensed`).
- Cada escritura es una sola transacción `BEGIN IMMEDIATE` e incluye su entrada de bitácora (FR-012,
  FR-017a, FR-018).

## Errores nuevos (`Abstractions/Error.cs`)

| Error | Significado | Mensaje al operador |
|---|---|---|
| `SupplierTaxIdInUse(Guid SupplierId, string Name)` | Otro proveedor (activo o inactivo) ya tiene ese RUC (FR-003) | "El RUC ya está registrado para el proveedor {nombre}" |
| `DuplicateInvoice(Guid PurchaseId, DateOnly InvoiceDate, DateTime RegisteredAtUtc)` | Ya hay una compra vigente del proveedor con esa factura (FR-011) | "La factura {número} de {proveedor} ya está registrada (compra del {fecha})" |
| `PurchaseVoidBlocked(IReadOnlyList<PurchaseVoidBlocker> Lines)` | Alguna línea no se puede revertir (FR-017b) | "No se puede anular la compra:" + una línea por producto: "{producto}: existencia {actual}, se requieren {cantidad}" / "{producto}: está inactivo; actívelo primero" / "{producto}: ya no controla inventario" |

`PurchaseVoidBlocker(Guid ProductId, string ProductName, VoidBlockReason Reason, long OnHandThousandths, long RequiredThousandths)`,
con `VoidBlockReason` = `InsufficientStock` | `ProductInactive` | `NotTracked`.

Se reutilizan: `ValidationFailed` (por campo y por línea), `NotFound`, `Conflict` (versión),
`InvalidState` ("La compra ya está anulada"), `Forbidden`, `ModuleNotLicensed`.

**Campos de error por línea**: `Lines[{i}].Product`, `Lines[{i}].Quantity`, `Lines[{i}].UnitCost`, para
que la interfaz identifique la línea (escenario 7). Campos de la compra: `SupplierId`,
`InvoiceNumber`, `InvoiceDate`, `Tax`, `Lines`, `Subtotal`.

## Puertos nuevos o ampliados

| Puerto | Miembros | Implementación |
|---|---|---|
| `ISupplierRepository` (nuevo, `Application/Suppliers`) | `GetAsync(id)`; `FindByTaxIdAsync(taxId)`; `Add(supplier)`; `SearchAsync(SupplierSearch)` → página; `ListForFilterAsync()` (todos, con estado); `ListActiveAsync(text?)` (selector de compras); `SaveChangesAsync()` → `SaveOutcome` (índice de RUC → duplicado) | `Infrastructure/Suppliers/SupplierRepository` |
| `IPurchaseRepository` (nuevo, `Application/Purchases`) | `GetAsync(id)` con líneas y seguimiento; `FindActiveByInvoiceAsync(supplierId, invoiceKey)`; `Add(purchase)`; `GetDetailAsync(id)` → `PurchaseDetailDto`; `SaveChangesAsync()` → `SaveOutcome` (índice de factura → duplicado; versión → conflicto) | `Infrastructure/Purchases/PurchaseRepository` |
| `IPurchaseReportReader` (nuevo, `Application/Reports`) | `SearchAsync(PurchaseReportFilter)` → `PurchaseReportPage` (filas + acumulados de todo el filtro) | `Infrastructure/Reports/PurchaseReportReader` |
| `IInventoryRepository` (existente) | `MovementDto` gana `PurchaseId?` y `SupplierName?` (research §3); `SearchMovementsAsync` hace los dos *left join* | `InventoryRepository` |
| `IWriteTransactions`, `IAuditLog`, `IClock`, `IUserSession`, `IProductRepository` | Sin cambios | — |

## Casos de uso nuevos

### Proveedores (`Pos.Application/Suppliers/…`)

| Caso de uso | Permiso | Entrada | Salida | Reglas |
|---|---|---|---|---|
| `CreateSupplier` | `ManageSuppliers` | `Name`, `TaxId?`, `Phone?`, `Email?`, `Address?`, `PaymentTerms`, `CreditDaysText?` | `Result<Guid>` | FR-001–FR-003. RUC repetido → `SupplierTaxIdInUse`. Audita `SUPPLIER_CREATED` |
| `UpdateSupplier` | `ManageSuppliers` | `Id`, `ExpectedVersion`, mismos datos | `Result` | Igual que crear. Audita `SUPPLIER_UPDATED` solo con los campos cambiados |
| `SetSupplierActive` | `ManageSuppliers` | `Id`, `ExpectedVersion`, `Active` | `Result` | Siempre permitido (FR-006). Audita `SUPPLIER_DEACTIVATED` / `SUPPLIER_ACTIVATED` |
| `SearchSuppliers` | `ManageSuppliers` | `Text?`, `IncludeInactive`, página (100) | `SupplierPage { Id, Name, TaxId, Phone, PaymentTerms, CreditDays, IsActive, Version }` | Por nombre o RUC normalizados (FR-004) |
| `GetSupplier` | `ManageSuppliers` | `Id` | `SupplierDto` | `NotFound` |
| `ListSuppliersForPurchase` | `RegisterPurchases` | `Text?` | ≤ 50 `{ Id, Name, TaxId }` | Solo activos (FR-007) |
| `ListSuppliersForReport` | `ViewPurchaseReport` | — | `{ Id, Name, IsActive }` | Incluye inactivos (Historia 3, escenario 5) |

### Compras (`Pos.Application/Purchases/…`)

| Caso de uso | Permiso | Entrada | Salida | Reglas |
|---|---|---|---|---|
| `CalculatePurchaseTotals` | `RegisterPurchases` | `Lines[{ ProductId, DecimalPlaces, QuantityText, UnitCostText }]`, `TaxText?` | `PurchaseTotalsDto { Lines[{ AmountCents?, IsBonus, Errors }], SubtotalCents, TaxCents, TotalCents, Errors }` | Sin base de datos; usa `PurchaseMath` (research §4). Lo invoca la interfaz en cada cambio |
| `RegisterPurchase` | `RegisterPurchases` | `SupplierId`, `InvoiceNumber`, `InvoiceDate`, `Lines[{ ProductId, QuantityText, UnitCostText }]`, `TaxText?` | `Result<PurchaseRegisteredDto { PurchaseId, SubtotalCents, TaxCents, TotalCents }>` | Orden abajo. Audita `PURCHASE_REGISTERED` |
| `VoidPurchase` | `VoidPurchases` | `PurchaseId`, `ExpectedVersion`, `Reason` | `Result` | Orden en research §7. Audita `PURCHASE_VOIDED` |
| `GetPurchase` | `ViewPurchaseReport` o `RegisterPurchases` | `PurchaseId` | `PurchaseDetailDto` | Encabezado (proveedor y nombres congelados, factura, fecha, usuario y fecha de registro, subtotal, impuestos, total, estado, anulación: fecha, usuario, motivo) y líneas (`LineNumber`, producto, SKU, cantidad, unidad, costo, importe, `IsBonus`) (FR-022) |

**Orden de `RegisterPurchase`**:

1. Permiso → validación de forma (FluentValidation): proveedor, factura (1–50 tras recortar), fecha,
   al menos una línea, sin productos repetidos, textos de cantidad, costo e impuestos.
2. Fecha de factura ≤ `DiscountDates.LocalToday(clock)`.
3. `BEGIN IMMEDIATE`.
4. Proveedor existe y está activo.
5. Cada producto existe, no está borrado, está activo y controla inventario; la cantidad respeta los
   decimales de su unidad (escenarios 7 y 9).
6. `PurchaseMath`: importes, subtotal > 0, límites (FR-008a, FR-010, FR-010a).
7. Compra vigente con el mismo (`SupplierId`, `InvoiceKey`) → `DuplicateInvoice`.
8. `Purchase.Register(...)`; por línea, `ProductStock.RecordPurchase` (con `ProductStock.Start` si no
   tiene fila), `AddMovement` y enlace `MovementId`.
9. Bitácora `PURCHASE_REGISTERED`.
10. Un solo `SaveChangesAsync`; duplicado por índice → `DuplicateInvoice`; conflicto → `Conflict`.
11. `Commit`. Registro en Serilog con proveedor, factura, número de líneas y total.

Todos los errores de los pasos 1–7 se juntan en un solo `ValidationFailed` cuando son de campo, para
mostrar todo lo que falta de una vez (escenario 6).

### Reporte (`Pos.Application/Reports/GetPurchaseReport`)

| Caso de uso | Permiso | Entrada | Salida |
|---|---|---|---|
| `GetPurchaseReport` | `ViewPurchaseReport` | `SupplierId?`, `FromDate?`, `ToDate?`, `MinTotalText?`, `MaxTotalText?`, `IncludeVoided`, `Page` | `PurchaseReportPage { Rows[{ PurchaseId, InvoiceDate, SupplierName, InvoiceNumber, LineCount, SubtotalCents, TaxCents, TotalCents, RegisteredByName, IsVoided }], TotalCount, Page, PageSize = 100, PurchaseCount, SubtotalSumCents, TaxSumCents, TotalSumCents }` |

- Filtro inválido (fecha inicial > final, mínimo > máximo, importe negativo o mal escrito) →
  `ValidationFailed` por campo (FR-023).
- `PurchaseCount` y los acumulados cuentan solo compras vigentes de todo el filtro (FR-021, FR-021a);
  `TotalCount` (para paginar) incluye las anuladas listadas.

## Casos de uso existentes que cambian

| Caso de uso | Cambio |
|---|---|
| `SearchMovements` | El filtro por tipo acepta `Purchase` y `PurchaseVoid`; `MovementDto` trae `PurchaseId?` y `SupplierName?` |
| `RegisterMovement` | Sin cambio de código: su validador ya rechaza los tipos de compra; `ProductStock.Record` también |
| `Customer.IsValidEmail` | Delegada a `EmailAddress.IsValid` (misma regla) |

## Catálogo de bitácora (`AuditActions`)

| Código | Texto | Entidad |
|---|---|---|
| `SUPPLIER_CREATED` | "Proveedor creado" | `Supplier` |
| `SUPPLIER_UPDATED` | "Proveedor modificado" | `Supplier` |
| `SUPPLIER_DEACTIVATED` | "Proveedor desactivado" | `Supplier` |
| `SUPPLIER_ACTIVATED` | "Proveedor activado" | `Supplier` |
| `PURCHASE_REGISTERED` | "Compra registrada" | `Purchase` |
| `PURCHASE_VOIDED` | "Compra anulada" | `Purchase` |

`SupplierAuditFields.Snapshot`: Nombre, RUC, Teléfono, Email, Dirección, Condiciones de pago
("Contado" / "Crédito a {n} días"), Estado. Grupo de filtro nuevo `AuditEntityGroup.Purchases`
("Proveedores y compras") con las entidades `Supplier` y `Purchase`.
