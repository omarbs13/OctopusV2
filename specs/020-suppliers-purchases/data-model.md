# Data Model: Proveedores y compras básicas

**Funcionalidad**: `020-suppliers-purchases` | **Fecha**: 2026-10-02 | **Plan**: [plan.md](plan.md)

Todos los importes se guardan como enteros en centavos y las cantidades en milésimas. Las fechas y
horas se guardan en UTC; la **fecha de factura** es un día de calendario (`DateOnly`, como
`Coupon.StartsOn` en 015) y se compara contra el día local (`DiscountDates.LocalToday`). Los
identificadores son GUID v7 generados en la aplicación (Principio IV). `CreatedAt/By` y
`UpdatedAt/By` los asigna el `AuditingInterceptor` existente.

## Entidades nuevas

### `Supplier`: tabla `Suppliers` (`Pos.Domain/Suppliers`)

| Campo | Tipo (C# / SQLite) | Reglas |
|---|---|---|
| `Id` | `Guid` / TEXT PK | `Guid.CreateVersion7()` |
| `Name` | `string` / TEXT(150) | Obligatorio, recortado. Puede repetirse (FR-001, FR-003) |
| `TaxId` | `string?` / TEXT(20) | RUC opcional; recortado y en mayúsculas; único si no es nulo, entre activos e inactivos (FR-003) |
| `Phone` | `string?` / TEXT(30) | Opcional, recortado |
| `Email` | `string?` / TEXT(254) | Opcional; formato válido si se captura (`EmailAddress.IsValid`) |
| `Address` | `string?` / TEXT(300) | Opcional, recortado |
| `PaymentTerms` | enum / TEXT(10) | `CASH` \| `CREDIT` (FR-002) |
| `CreditDays` | `int?` / INTEGER | Nulo en `CASH`; 1–365 en `CREDIT` |
| `IsActive` | `bool` / INTEGER | `true` al crear |
| `SearchText` | `string` / TEXT(200) | `TextNormalizer.ForSearch(nombre + RUC)` (research §9) |
| `CreatedAt/By`, `UpdatedAt/By` | auditoría | Interceptor |
| `DeletedAt` | `DateTime?` | Estándar del Principio IV; no se usa (no hay borrado, FR-006) |
| `Version` | `int` | Concurrencia optimista |

**Índices**:

- `IX_Suppliers_TaxId`: único, filtrado `TaxId IS NOT NULL`.
- `IX_Suppliers_Active_Search` sobre (`IsActive`, `SearchText`).

**Comportamiento**: `Create(...)`, `Update(...)` (recalcula `SearchText`; al pasar a `CASH` limpia
`CreditDays`), `Deactivate()`, `Activate()`. Editar el proveedor no toca las compras: estas guardan su
propio `SupplierName` (FR-005, FR-016).

### `Purchase`: tabla `Purchases` (`Pos.Domain/Purchases`)

Una compra = una factura de proveedor registrada como entrada de mercancía.

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | PK |
| `SupplierId` | `Guid` | FK (Restrict) → `Suppliers`. Activo al registrar |
| `SupplierName` | `string` / TEXT(150) | Copia del nombre al registrar (FR-016) |
| `InvoiceNumber` | `string` / TEXT(50) | Obligatorio, recortado, tal como se capturó (FR-007) |
| `InvoiceKey` | `string` / TEXT(50) | `InvoiceNumber` en mayúsculas invariantes, para el duplicado (research §5) |
| `InvoiceDate` | `DateOnly` / TEXT | Obligatoria, ≤ hoy local (FR-007) |
| `LineCount` | `int` | Número de líneas, ≥ 1 (para el reporte, research §13) |
| `SubtotalCents` | `long` | Σ `PurchaseLine.AmountCents`; > 0 (FR-008a); ≤ `Money.MaxCents` |
| `TaxCents` | `long` | ≥ 0; vacío = 0 (FR-010a) |
| `TotalCents` | `long` | `SubtotalCents + TaxCents`; ≤ `Money.MaxCents` |
| `Status` | enum / TEXT(10) | `ACTIVE` ("Vigente") \| `VOIDED` ("Anulada") |
| `VoidedAt` | `DateTime?` | UTC; solo en `VOIDED` |
| `VoidedBy` | `Guid?` | Administrador que anuló; solo en `VOIDED` |
| `VoidReason` | `string?` / TEXT(250) | Obligatorio al anular (FR-017a) |
| `CreatedAt/By` | auditoría | Fecha de registro y usuario que registró (FR-020, FR-022) |
| `UpdatedAt/By`, `Version` | estándar | Solo cambian al anular |

**Índices**:

- `IX_Purchases_Supplier_InvoiceKey`: **único**, filtrado `"Status" = 'ACTIVE'` (FR-011).
- `IX_Purchases_InvoiceDate` sobre (`InvoiceDate`, `CreatedAt`, `Id`): orden y rango del reporte.
- `IX_Purchases_Supplier_InvoiceDate` sobre (`SupplierId`, `InvoiceDate`): filtro por proveedor.

Sin `DeletedAt`: una compra nunca se borra (ver Complexity Tracking del plan).

**Estados**:

```text
ACTIVE ──Void(motivo, usuario, utc)──▶ VOIDED      (sin regreso; FR-017b)
```

**Invariantes** (`Purchase.Register` y `Purchase.Void`):

- Al menos una línea; como máximo una línea por producto (FR-009).
- `SubtotalCents = Σ AmountCents`, `TotalCents = SubtotalCents + TaxCents`, `SubtotalCents > 0`.
- `Void` exige `Status = ACTIVE` y motivo recortado de 1 a 250 caracteres.

### `PurchaseLine`: tabla `PurchaseLines` (`Pos.Domain/Purchases`)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `Guid` | PK |
| `PurchaseId` | `Guid` | FK (Cascade en el modelo; nunca se borra) → `Purchases` |
| `LineNumber` | `int` | 1..N en el orden de captura |
| `ProductId` | `Guid` | FK (Restrict) → `Products`. Activo y que controla inventario al registrar |
| `ProductName` | `string` / TEXT(200) | Copia al registrar (FR-016); `Product.NameMaxLength` |
| `ProductSku` | `string` / TEXT(50) | Copia al registrar (FR-016); `Product.SkuMaxLength` |
| `UnitCode` | `string` / TEXT | Copia al registrar (`UnitOfMeasure.CodeMaxLength`); decimales y nombre de la unidad |
| `QuantityThousandths` | `long` | > 0, respeta los decimales de la unidad (FR-008) |
| `UnitCostCents` | `long` | ≥ 0, antes de impuestos (FR-010). 0 = bonificación |
| `AmountCents` | `long` | `round_half_up(cantidad × costo)` (research §4) |
| `MovementId` | `Guid` | FK (Restrict) → `InventoryMovements`; **único**. El `PURCHASE` generado (FR-013) |
| `VoidMovementId` | `Guid?` | FK (Restrict) → `InventoryMovements`; único filtrado. El `PURCH_VOID` de la anulación |

`IsBonus => UnitCostCents == 0` (no se guarda).

**Índices**:

- `IX_PurchaseLines_Purchase_LineNumber`: único (`PurchaseId`, `LineNumber`).
- `IX_PurchaseLines_Purchase_Product`: único (`PurchaseId`, `ProductId`) (FR-009).
- `IX_PurchaseLines_MovementId`: único.
- `IX_PurchaseLines_VoidMovementId`: único, filtrado `VoidMovementId IS NOT NULL`.
- `IX_PurchaseLines_ProductId`: para la FK.

Sin campos de modificación, borrado ni versión: es inmutable; solo `VoidMovementId` pasa de nulo a un
valor al anular (research §10).

## Entidades existentes que cambian

### `MovementType` (`Pos.Domain/Inventory`)

| Valor | Código | Signo | Origen |
|---|---|---|---|
| `Purchase` | `PURCHASE` | + | `RegisterPurchase`, una por línea ("Entrada de compra") |
| `PurchaseVoid` | `PURCH_VOID` | − | `VoidPurchase`, una por línea ("Anulación de compra") |

- Caben en `InventoryMovements.Type` (TEXT(12)); la tabla no cambia (research §2).
- `IsIncrease()`: `false` para `PurchaseVoid`.
- `Reference` del movimiento = número de factura; `Reason` = nulo.

### `ProductStock` (`Pos.Domain/Inventory`)

- `Record(...)` rechaza `Purchase` y `PurchaseVoid` (FR-014).
- `RecordPurchase(quantity, unit, productActive, tracksInventory, reference)`: suma; valida cantidad,
  decimales, producto activo, que controla inventario y el máximo de existencia.
- `RecordPurchaseVoid(quantity, productActive, tracksInventory, reference)`: resta; rechaza producto
  inactivo, que no controla inventario o existencia menor que la cantidad (FR-017b).

### `Permission` y `RolePermissions` (`Pos.Domain/Users`)

Valores nuevos: `ManageSuppliers`, `RegisterPurchases`, `VoidPurchases`, `ViewPurchaseReport`. Solo el
Administrador (por estar en el enum); ninguno en `CashierPermissions` ni en `Authorizable`.

### `ModuleAccess` (`Pos.Domain/Licensing`)

Los cuatro permisos nuevos → `LicensedModule.Inventory`.

### `EmailAddress` (`Pos.Domain/Common`, nuevo)

`IsValid(string?)`: la regla que hoy vive en `Customer.IsValidEmail`, que pasa a invocarla.

## Relaciones

```text
Supplier 1 ──< Purchase 1 ──< PurchaseLine >── 1 Product
                                   │  │
                    MovementId ────┘  └──── VoidMovementId (nulo)
                         ▼                        ▼
                 InventoryMovement (PURCHASE)   InventoryMovement (PURCH_VOID)
```

## Esquema: migración `SuppliersAndPurchases` (0.14.0)

- `CREATE TABLE` `Suppliers`, `Purchases`, `PurchaseLines` con sus índices.
- Ningún cambio en tablas existentes; sin reconstrucciones ni datos (research §12).
