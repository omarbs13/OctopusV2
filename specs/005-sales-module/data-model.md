# Data Model: Módulo de ventas

**Feature**: `005-sales-module` | **Date**: 2026-09-30 | **Research**: [research.md](research.md)

Convenciones que ya existen en el proyecto:

- Ids GUID v7 generados en el dominio.
- Fechas en UTC.
- Dinero en centavos (`long`) y cantidades en milésimas (`long`).
- `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` y `Version` los asigna `AuditingInterceptor`.
- Los tipos y estados se guardan como códigos de texto estables.

## Dominio nuevo (`Pos.Domain/Sales`)

### `Cart` (en memoria; venta en curso)

| Miembro | Tipo | Regla |
|---|---|---|
| `DraftId` | `Guid` | GUID v7 al crear el carrito; es la clave de idempotencia (research §4) |
| `Lines` | `IReadOnlyList<CartLine>` | En orden de captura |
| `Total` | `Money` | Suma exacta de `CartLine.Amount` |
| `Add(product, quantity = 1)` | | Si el producto ya está, incrementa esa línea (FR-003). Rechaza productos no vendibles (FR-007) |
| `SetQuantity(productId, Quantity)` | | Cantidad > 0, que respete los decimales de la unidad (FR-005) y cuyo importe de línea y total no pasen de `Money.MaxCents`. Si falla, conserva el valor anterior |
| `Remove(productId)` | | |
| `ApplyCurrentPrices(prices)` | | Reemplaza precios con los vigentes y marca `IsUnavailable` (research §5) |
| `CanCheckout` | `bool` | Tiene líneas, total > 0 y ninguna línea no disponible |
| `Restore(draftId, lines)` | static | Reconstruye desde el borrador |

### `CartLine`

`ProductId`, `Name`, `Sku`, `UnitCode`, `DecimalPlaces`, `TracksInventory`, `UnitPrice` (`Money`),
`Quantity` (`Quantity`), `Amount` (`Money` = `SaleMath.LineAmount`) e `IsUnavailable` (`bool`,
con `UnavailableReason`: `Inactive` o `Deleted`).

### `SaleMath`

`LineAmount(Quantity, Money) → Money`: `(milésimas × centavos + 500) / 1000`, con `checked`. Lanza
`DomainException` si pasa de `Money.MaxCents` (research §2).

### `Checkout` (en memoria; cobro)

| Miembro | Regla |
|---|---|
| `Total` | Total de la venta |
| `Payments` | A lo más un `Cash(Received)` y cero o más `Card`/`Transfer(Amount, Reference?)` |
| `AddNonCash(method, amount, reference)` | `amount > 0` y `amount ≤ Pending`. Referencia hasta 50 caracteres |
| `SetCashReceived(amount)` / `QuickAmount(bill \| Exact)` | Exact = `Pending` sin contar el efectivo |
| `NonCashTotal`, `CashApplied = Total − NonCashTotal`, `Change = Received − CashApplied` | |
| `Shortfall` | `max(0, Total − NonCashTotal − Received)` |
| `CanConfirm` | `Shortfall == 0` y, si hay efectivo, `Change ≥ 0` |
| `ToPayments()` | Pagos a registrar: monto aplicado, recibido y cambio (research §6) |

`PaymentMethod`: `Cash` (`CASH`), `Card` (`CARD`) y `Transfer` (`TRANSFER`).

### `Sale` (agregado, tabla `Sales`)

| Campo | Tipo | Columna / restricción |
|---|---|---|
| `Id` | `Guid` | PK, v7 |
| `FolioNumber` | `long` | Único `IX_Sales_Folio`. Se muestra como `V-{0:000000}` |
| `DraftId` | `Guid` | Único `IX_Sales_DraftId` (idempotencia) |
| `Total` | `Money` | `TotalCents INTEGER` = Σ líneas = Σ `AmountCents` de pagos |
| `Status` | `SaleStatus` | `COMPLETED` o `CANCELLED` (texto, 10) |
| `CancellationReason` | `string?` | Máximo 250; obligatorio si está cancelada |
| `CancelledAt` / `CancelledBy` | `DateTime?` / `Guid?` | Solo si está cancelada |
| `CreatedAt` / `CreatedBy` | | Fecha de la venta y usuario (FR-030: usuario de sistema) |
| `UpdatedAt` / `UpdatedBy` / `Version` | | `Version` es el token de concurrencia |
| `Lines` | `SaleLine[]` | 1..n |
| `Payments` | `SalePayment[]` | 1..n |

Índices: `IX_Sales_CreatedAt (CreatedAt, Id)` y `IX_Sales_Status_CreatedAt (Status, CreatedAt)`.

Transiciones de estado:

```text
(nueva) ──Sale.Register(...)──▶ Completed ──Sale.Cancel(reason, utcNow, user)──▶ Cancelled
                                               Cancelled ──Cancel──▶ DomainException (FR-035)
```

`Sale.Register(folio, draftId, lines, payments)` valida:

- Al menos una línea.
- Total > 0.
- Σ pagos aplicados = total.
- A lo más un pago en efectivo; solo ese tiene recibido y cambio.

### `SaleLine` (tabla `SaleLines`)

| Campo | Tipo | Nota |
|---|---|---|
| `Id` | `Guid` | PK |
| `SaleId` | `Guid` | FK `Sales`, `Restrict` (las ventas no se borran) |
| `Position` | `int` | Orden de captura. Único `(SaleId, Position)` |
| `ProductId` | `Guid` | FK `Products` (Restrict) |
| `ProductName` | `string` (200) | Copia (FR-023) |
| `ProductSku` | `string` (50) | Copia |
| `UnitCode` / `DecimalPlaces` | `string` (3) / `int` | Copia, para mostrar la cantidad |
| `UnitPrice` | `Money` | `UnitPriceCents` |
| `QuantityThousandths` | `long` | `Quantity` |
| `Amount` | `Money` | `AmountCents` = `SaleMath.LineAmount` |
| `SaleMovementId` | `Guid?` | FK `InventoryMovements`. Nulo si el producto no controlaba inventario |
| `CancellationMovementId` | `Guid?` | FK `InventoryMovements`. Se asigna al cancelar si hubo `SaleMovementId` |

Índice: `IX_SaleLines_Product (ProductId)` para los productos más vendidos.

### `SalePayment` (tabla `SalePayments`)

| Campo | Tipo | Nota |
|---|---|---|
| `Id` | `Guid` | PK |
| `SaleId` | `Guid` | FK `Sales` |
| `Method` | `PaymentMethod` | `CASH`, `CARD` o `TRANSFER` |
| `AmountCents` | `long` | Monto aplicado a la venta, > 0 |
| `ReceivedCents` / `ChangeCents` | `long?` | Solo `CASH` |
| `Reference` | `string?` (50) | Solo `CARD` o `TRANSFER` |

## Cambios al dominio existente

### `StockLevel` (nuevo, `Pos.Domain/Inventory`)

Milésimas `long` con signo, en el rango ±`Quantity.MaxStockThousandths`. Tiene `IsNegative`, los
operadores `+`/`−` con `Quantity` y la comparación con `Quantity`.

### `ProductStock`

- `OnHand`: `Quantity` → `StockLevel`. La columna no cambia.
- `RecordSale(quantity, unit, reference)` (nuevo): exige `TracksInventory`. La cantidad debe ser
  > 0 y respetar los decimales. **Puede dejar la existencia negativa.** Produce `SALE`.
- `RecordSaleCancellation(quantity, reference)` (nuevo): suma sin revisar estado ni configuración
  actual del producto (research §9). Produce `SALE_CANCEL`.
- `Record(...)` (existente): sin cambios en sus reglas. `AdjustOut` sigue sin poder dejar la
  existencia bajo cero; con existencia negativa se rechaza.
- `WouldGoNegative(StockLevel, Quantity)` y `WouldExceedMaximum(StockLevel, Quantity)` reciben el
  nuevo tipo.
- `IsShort(StockLevel onHand, Quantity requested)` (nuevo): `requested > onHand`. Sirve para la
  advertencia de venta.

### `MovementType`

| Tipo | Código | Signo | Motivo |
|---|---|---|---|
| `Sale` | `SALE` | − | No |
| `SaleCancellation` | `SALE_CANCEL` | + | No |

`IsIncrease()` se actualiza. Los tipos de venta **no** se pueden registrar desde el formulario de
movimientos: `RegisterMovementValidator` solo acepta `Receipt`, `AdjustIn`, `AdjustOut` e
`Initial`. La referencia del movimiento es el folio (`V-000123`).

### `InventoryMovement.ResultingStock`

`Quantity` → `StockLevel`.

### `StockStatusRule.Evaluate(StockLevel, Quantity?)`

`onHand <= 0` → `Out` (FR-026). El predicado SQL se actualiza igual.

## Tablas de apoyo nuevas

### `SaleDrafts` (borrador; research §7)

| Campo | Tipo | Nota |
|---|---|---|
| `Slot` | `int` | PK, siempre 1 (un solo borrador por instalación) |
| `DraftId` | `Guid` | |
| `LinesJson` | `TEXT` | `[{productId, quantityThousandths, unitPriceCents}]` en orden |
| `UpdatedAt` | `DateTime` | |

Se escribe con upsert y se borra al registrar la venta (en la misma transacción) o al cancelarla
el operador. No es una entidad de negocio (ver Complexity Tracking).

### `AuditEntries` (bitácora; research §10)

| Campo | Tipo | Nota |
|---|---|---|
| `Id` | `Guid` | PK v7 |
| `Action` | `string` (40) | `SALE_CANCELLED` |
| `EntityType` | `string` (40) | `Sale` |
| `EntityId` | `Guid` | |
| `Details` | `string?` (500) | `"Folio V-000123. Motivo: …"` |
| `CreatedAt` / `CreatedBy` | | Interceptor |

Índice `(EntityType, EntityId)`. Es inmutable: el guardián de `PosDbContext` rechaza modificarla o
borrarla.

## Invariantes verificadas por pruebas (Principio VI)

1. Σ `SaleLine.AmountCents` = `Sale.TotalCents` = Σ `SalePayment.AmountCents`.
2. `ProductStocks.OnHand` = Σ movimientos con signo, después de ventas y cancelaciones
   (FR-027, SC-006).
3. Los folios son consecutivos (1..n) y únicos, incluso con confirmaciones concurrentes (SC-005).
4. Una falla dentro de `ConfirmSale` deja 0 filas nuevas en `Sales`, `SaleLines`,
   `SalePayments`, `InventoryMovements` y `AuditEntries`, y el borrador intacto (SC-003).
5. Confirmar dos veces el mismo `DraftId` produce una sola venta (FR-020).
6. `SaleLine` conserva nombre, SKU y precio aunque el producto cambie (SC-007).
