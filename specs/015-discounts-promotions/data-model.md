# Data Model: Descuentos y promociones

**Funcionalidad**: `015-discounts-promotions` | **Fecha**: 2026-10-01 | **Research**: [research.md](research.md)

Todos los importes están en centavos (`long`) y los porcentajes en puntos base (`int`, 10 000 = 100 %).
Las fechas son UTC, salvo la vigencia del cupón, que es una fecha local (`DateOnly`).
Los identificadores son GUID v7.

## Value objects y cálculos (Domain, sin persistencia propia)

### `DiscountValue` (`Domain/Discounts`)

| Campo | Tipo | Regla |
|---|---|---|
| `Mode` | `DiscountMode` (`Percent`, `Amount`) | — |
| `Raw` | `long` | `Percent`: 1–10 000 pb. `Amount`: 1–`Money.MaxCents`. |

- `Parse(mode, text)` acepta como máximo 2 decimales y nunca redondea.
- Se muestra como "10%", "12.5%" o "$15.00".

### `DiscountMath` (`Domain/Discounts`)

- `Amount(baseCents, value)`:
  - Con porcentaje: `(base × pb + 5 000) / 10 000`.
  - Con monto: `value`.
  - Lanza `DomainException` si el resultado es mayor que la base o si es 0 centavos (por ejemplo,
    1 % de $0.10): un descuento que redondea a $0.00 se rechaza al aplicarlo.
- `ExceedsLimit(discountCents, baseCents, limitBp)`: `discount × 10 000 > limit × base`.
- `EquivalentBasisPoints(discountCents, baseCents)`: redondeado hacia arriba. Se guarda en la
  aprobación, que cubre el descuento si `aprobado ≥ equivalente`.

### `Proportional.Allocate` (`Domain/Common`)

Reparte un monto por resto mayor (con empate por orden), con topes por elemento y suma exacta. Se
extrae de `ReturnMath.Allocate`, que pasa a delegarle.

## Venta en curso (Domain, en memoria)

### `CartLine` (cambia)

- Campos nuevos:
  - `Discount: LineDiscount?`, que es el registro `(DiscountValue Value, Guid? ApprovalId)`.
- Valores derivados:
  - `OriginalAmount`: el `Amount` actual (cantidad × precio).
  - `LineDiscountAmount`: se obtiene con `DiscountMath.Amount(OriginalAmount, Discount.Value)`.
  - `NetBeforeOrder`: `OriginalAmount − LineDiscountAmount`.

### `OrderDiscount` (nuevo, en `Cart`)

Unión discriminada:

- `Manual(DiscountValue Value, Guid? ApprovalId)`.
- `CouponApplied(Guid CouponId, string Code, DiscountValue Value)`.

Un `Cart` tiene como máximo un `OrderDiscount`, por lo que el cupón y el descuento global se excluyen (FR-013).

### `Cart` (cambia)

| Miembro | Regla |
|---|---|
| `SetLineDiscount(productId, LineDiscount?)` | Valida con `DiscountMath`. `null` quita el descuento. |
| `SetQuantity` | Recalcula el descuento. Si un monto fijo supera el nuevo importe, rechaza el cambio y conserva la cantidad anterior. |
| `SetOrderDiscount(OrderDiscount?)` | Reemplaza el descuento anterior. La confirmación del reemplazo la pide la interfaz. Rechaza un `Manual` de monto mayor que el subtotal actual (como en la línea). |
| `Subtotal` | Σ `NetBeforeOrder`. |
| `OrderDiscountAmount` | Porcentaje sobre el subtotal. Un monto se limita al subtotal solo si es cupón (Historia 3, escenario 6). |
| Recalcular tras cambios | Si un `Manual` de monto supera el subtotal, se retira y se devuelve `OrderDiscountRemoved`, para que la interfaz avise. |
| `Total` | `Subtotal − OrderDiscountAmount` ≥ 0. |
| `Allocation()` | Reparte `OrderDiscountAmount` sobre `NetBeforeOrder`. Lo usa `ConfirmSale`. |
| `CanCheckout` | Hay líneas y ninguna está no disponible. El total puede ser 0. |

## Entidades persistidas

### `Coupon` (tabla `Coupons`, agregado nuevo)

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | GUID | PK |
| `Code` | TEXT(30) | Recortado y en mayúsculas, con `[A-Z0-9-]{3,30}`. **Índice único**. |
| `Mode` | TEXT(10) | `PERCENT` o `AMOUNT` |
| `Value` | INTEGER | Puntos base o centavos, según `DiscountValue` |
| `StartsOn` | TEXT (fecha) | Fecha local, inclusiva |
| `EndsOn` | TEXT (fecha) | Fecha local, inclusiva, ≥ `StartsOn` |
| `UsageLimit` | INTEGER NULL | > 0 o nulo (sin límite) |
| `UsesCount` | INTEGER | ≥ 0 y ≤ `UsageLimit` |
| `IsActive` | INTEGER | — |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt`, `Version` | — | Principio IV. `DeletedAt` siempre nulo: no se borra. |

**Estado** (`CouponStatus`, derivado de `today`, en este orden): `Inactive`, `Exhausted`
(`UsesCount ≥ UsageLimit`), `Expired` (`today > EndsOn`), `NotStarted` (`today < StartsOn`) y `Active`.

**Métodos**:

- `Create`.
- `Update(startsOn, endsOn, usageLimit)`, que no permite un límite menor que `UsesCount`.
- `Edit(code, value)`, que lanza una excepción si `UsesCount > 0`.
- `Deactivate` y `Activate`.
- `ConsumeUse(today)`, que exige `Active`.
- `ReleaseUse()`, que no baja de 0.

### `DiscountApproval` (tabla `DiscountApprovals`, nueva)

Es la aprobación de un descuento sobre el límite. Se crea al aplicar el descuento y se consulta al cobrar (research §7).

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | GUID | PK |
| `DraftId` | GUID | Índice. Es la venta en curso. |
| `RequestedBy` | GUID | Usuario que aplicó el descuento |
| `AuthorizedBy` | GUID | Administrador |
| `Scope` | TEXT(10) | `LINE` u `ORDER` |
| `ProductId` | GUID NULL | Obligatorio si `Scope = LINE` |
| `ApprovedBasisPoints` | INTEGER | Equivalente aprobado, entre 1 y 10 000 |
| `CreatedAt` | fecha UTC | — |

Son inmutables, no tienen `Version` y no se borran. No es una entidad de negocio editable; la
desviación se justifica en el plan, en la sección Complexity Tracking.

### `Sale` (cambia)

| Columna nueva | Tipo | Regla |
|---|---|---|
| `DiscountCents` | INTEGER, default 0 | Σ de todos los `SaleDiscount.AmountCents` |

`Register(..., discounts)`:

- Exige que `total = Σ AmountCents`, con total ≥ 0.
- Con total 0, no lleva pagos.
- Exige que `DiscountCents = Σ (Original − Amount)` de las líneas.

### `SaleLine` (cambia)

| Columna nueva | Tipo | Regla |
|---|---|---|
| `OriginalAmountCents` | INTEGER | Cantidad × precio. La migración copia `AmountCents`. |
| `LineDiscountCents` | INTEGER, default 0 | ≤ `OriginalAmountCents` |
| `OrderDiscountCents` | INTEGER, default 0 | Parte repartida del descuento global o del cupón |
| `AmountCents` (existente) | — | **Importe neto** = Original − Línea − Venta ≥ 0 |

`ReturnMath.LineRefund` sigue usando `AmountCents`, así que la devolución es neta de descuentos (FR-020).

El ticket y "Consultar ventas" muestran como importe final de la línea `OriginalAmountCents −
LineDiscountCents`, no `AmountCents`: el descuento de venta se muestra una sola vez, antes del total.

### `SaleDiscount` (tabla `SaleDiscounts`, nueva, parte del agregado `Sale`)

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | GUID | PK |
| `SaleId` | GUID | FK a `Sales`, índice |
| `SaleLineId` | GUID NULL | Obligatorio si `Kind = LINE` |
| `Kind` | TEXT(10) | `LINE`, `ORDER` o `COUPON` |
| `Mode` | TEXT(10) | `PERCENT` o `AMOUNT` |
| `Value` | INTEGER | Valor capturado (puntos base o centavos) |
| `AmountCents` | INTEGER | Monto descontado, > 0 |
| `CouponId` | GUID NULL | Si `Kind = COUPON` |
| `CouponCode` | TEXT(30) NULL | Copia del código al vender |
| `AppliedBy` | GUID | Usuario que aplicó el descuento (el que vende) |
| `AuthorizedBy` | GUID NULL | Administrador, si se superó el límite |
| `CreatedAt` | fecha UTC | Momento del cobro |

Es inmutable. Hay índice por `(CreatedAt)` para el reporte, y el filtro por cajero usa `Sales.CreatedBy`.

## Preferencias

### `DiscountSettings` (`IPreferencesStore`, clave `discounts`)

| Campo | Tipo | Default | Regla |
|---|---|---|---|
| `LimitBasisPoints` | int | 1 000 (10 %) | 0–10 000 |

## Borrador (`SaleDraft.LinesJson`, compatible hacia atrás)

```text
lines[]: { productId, quantityThousandths, unitPriceCents,
           discount?: { mode, value, approvalId? } }
order?:  { kind: "MANUAL", mode, value, approvalId? } | { kind: "COUPON", code }
```

`StoredDraft` y `DraftLineDto` agregan los campos opcionales. Los borradores viejos se leen sin cambios.

## Licencia y permisos

- `LicensedModule.Discounts` tiene un GUID nuevo en `ModuleCatalog`.
- `Permission`:
  - `ApplyDiscounts`: Cajero y Administrador.
  - `ApproveDiscounts`: Administrador, autorizable.
  - `ManageDiscounts`: Administrador.
  - `ViewDiscountReport`: Administrador.
- `ModuleAccess` mapea los cuatro permisos a `Discounts`.

## Migración `DiscountsAndCoupons` (0.9.0 → 0.10.0)

- Tablas nuevas: `Coupons`, `DiscountApprovals` y `SaleDiscounts`.
- Columnas nuevas: `Sales.DiscountCents`, `SaleLines.OriginalAmountCents`, `SaleLines.LineDiscountCents`
  y `SaleLines.OrderDiscountCents`, todas con `ADD COLUMN` y default 0.
- Datos: `UPDATE SaleLines SET OriginalAmountCents = AmountCents`.
- Sin reconstrucción de tablas. Se revisa el SQL generado y se agrega la base de ejemplo 0.10.0.

## Diagrama de relaciones

```text
Sale 1─* SaleLine
Sale 1─* SaleDiscount *─0..1 SaleLine
SaleDiscount *─0..1 Coupon
DiscountApproval *─1 (DraftId = Sale.DraftId al cobrar)
```
