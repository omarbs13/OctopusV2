# Contrato: casos de uso y puertos de Application

**Funcionalidad**: `015-discounts-promotions`

Todos los casos de uso devuelven `Result` o `Result<T>`, verifican el permiso con `IAccessControl` y
responden `ModuleNotLicensed(Discounts)` si el módulo está inactivo. Los errores de negocio son tipos
explícitos; las excepciones solo se usan para fallas inesperadas.

## Casos de uso nuevos

### `Discounts/ApproveDiscount`

```csharp
record ApproveDiscountCommand(
    Guid DraftId,
    DiscountScope Scope,            // Line | Order
    Guid? ProductId,                // obligatorio si Scope = Line
    DiscountMode Mode, long Value,
    long BaseCents,                 // importe de la línea o subtotal que vio el operador
    Guid? GrantId);                 // concesión de ApproveDiscounts; null si quien opera es Administrador
→ Result<DiscountApprovalDto(Guid ApprovalId, Guid AuthorizedBy, string AuthorizedByName)>
```

- Calcula el equivalente con `DiscountMath`. Si no supera el límite, responde `ApprovalNotNeeded`
  y no guarda nada.
- Para continuar exige `ApproveDiscounts`: un Administrador lo tiene por rol, y un Cajero necesita una
  concesión válida. Con una concesión inválida o vencida responde `AuthorizationRequired`.
- Guarda `DiscountApproval` y `DISCOUNT_AUTHORIZED` en una sola transacción.
- **No** modifica la venta: el `ApprovalId` lo lleva el `Cart` o el borrador.

### `Discounts/ResolveCoupon` (lo usa `FindProductsForSale`, y también se expone solo para "Aplicar cupón")

```csharp
record ResolveCouponQuery(string Code) → Result<CouponLookupDto?>
record CouponLookupDto(Guid CouponId, string Code, DiscountMode Mode, long Value, CouponStatus Status);
```

- Devuelve `null` si el código no existe y el estado si existe. La interfaz traduce los estados distintos
  de `Active` en los mensajes de [ui.md](ui.md).

### `Discounts/Coupons/SaveCoupon`, `SearchCoupons`, `GetCoupon`, `SetCouponActive`

- Requieren `ManageDiscounts`.
- `SaveCouponCommand(Guid? Id, string Code, DiscountMode Mode, string ValueText, DateOnly StartsOn,
  DateOnly EndsOn, int? UsageLimit, int ExpectedVersion)`. FluentValidation valida la forma y
  `Coupon` valida las reglas.
- Errores: `CodeDuplicated`, `CodeCollidesWithProduct`, `CouponHasUses` (al cambiar código o valor) y `Conflict` (por `Version`).
- `SearchCouponsQuery(string? Text, CouponStatus? Status, int Page)` devuelve una lista paginada de 100
  con vigencia, estado, usos realizados y usos restantes.
- Cada alta, edición o desactivación queda en la bitácora.

### `Discounts/Settings/GetDiscountSettings`, `SaveDiscountSettings`

- `SaveDiscountSettings` requiere `ManageDiscounts` y admite un `LimitBasisPoints` de 0 a 10 000.
  Registra `DISCOUNT_LIMIT_CHANGED` con el valor anterior y el nuevo.
- `GetDiscountSettings` requiere `ApplyDiscounts`, porque el punto de venta necesita el límite para
  saber cuándo pedir autorización antes de aplicar.

### `Discounts/GetDiscountReport`

```csharp
record DiscountReportQuery(ReportPeriod Period, Guid? CashierId, DiscountKind? Kind, int Page = 1, int PageSize = 100);
record DiscountReport(long TotalDiscountCents, int Count, IReadOnlyList<DiscountReportRow> Rows, long TotalRows);
record DiscountReportRow(Guid SaleId, string Folio, DateTime CreatedAtUtc, string CashierName,
    DiscountKind Kind, DiscountMode Mode, long Value, long AmountCents, string? AuthorizedByName, string? CouponCode);
```

- Requiere `ViewDiscountReport`. Solo incluye ventas completadas.
- Se exporta con el `ExportReportHandler` existente, agregando el tipo de reporte `Discounts`.

## Casos de uso que cambian

| Caso de uso | Cambio |
|---|---|
| `AuthorizeAdmin` (007) | `AuthorizeAdminCommand` agrega `string? Context` opcional, que se agrega al detalle de `ADMIN_AUTHORIZATION_DENIED`/`GRANTED`. Nunca incluye la contraseña. |
| `ConfirmSale` | `ConfirmLineInput` agrega `LineDiscountInput? Discount (Mode, Value, ApprovalId?)`, y `ConfirmSaleCommand` agrega `OrderDiscountInput? OrderDiscount` (manual con `ApprovalId?` o `CouponCode`). Reconstruye el `Cart` con los descuentos, valida contra el límite vigente y la aprobación indicada por cada `ApprovalId` (research §7; también para el Administrador), revalida y consume el cupón, y guarda `SaleDiscount`s. Si el módulo está inactivo y hay descuentos, responde `ModuleNotLicensed`. Errores nuevos: `DiscountApprovalRequired(scope, productId?)`, `CouponNotValid(status)` y `OrderDiscountRemoved`. Acepta un total de 0 sin pagos. |
| `FindProductsForSale` | Busca en este orden: producto exacto, después cupón exacto (si el módulo está activo) y al final por nombre. `LookupKind.Coupon` y `ProductLookup.Coupon: CouponLookupDto?` son nuevos. |
| `SaveSaleDraft` / `GetSaleDraft` | Guardan y recuperan los descuentos y el cupón (data-model, sección "Borrador"). `RecoveredDraft` agrega `DiscountsDropped: bool` cuando el módulo está inactivo. |
| `GetSale` | `SaleDetailDto` agrega `SubtotalCents`, `DiscountCents` y `Discounts: IReadOnlyList<SaleDiscountDto>` (tipo, modalidad, valor, monto, línea, cupón, aplicador y autorizador por nombre). `SaleLineDto` agrega `OriginalAmountCents` y `LineDiscountCents`. |
| `GetSalesReport` | `SalesTotals.DiscountCents`. |
| `SaleReturnProcessor` (cancelación completa) | Si la venta tiene un `SaleDiscount` de tipo `COUPON` y el módulo está activo, ejecuta `Coupon.ReleaseUse()` y registra `COUPON_USE_RELEASED` en la misma transacción. |

## Puertos nuevos

```csharp
interface ICouponRepository   // específico del agregado
{
    Task<Coupon?> GetAsync(Guid id, CancellationToken ct);
    Task<Coupon?> FindByCodeAsync(string normalizedCode, CancellationToken ct);
    Task<CouponPage> SearchAsync(string? text, CouponStatus? status, DateOnly today, int page, CancellationToken ct);
    void Add(Coupon coupon);
    Task<SaveOutcome> SaveChangesAsync(CancellationToken ct);
}

interface IDiscountApprovalStore
{
    void Add(DiscountApproval approval);
    Task<IReadOnlyList<DiscountApproval>> ListForDraftAsync(Guid draftId, Guid requestedBy, CancellationToken ct);
}

interface IDiscountSettingsStore { DiscountSettings Load(); void Save(DiscountSettings settings); }

interface IDiscountReportReader
{
    Task<DiscountReport> ReadAsync(DiscountReportQuery query, ReportWindow window, CancellationToken ct);
}
```

`IProductRepository` agrega `ExistsWithCodeAsync(string code)`, que compara contra el código de barras
o el SKU de productos no borrados y se usa para detectar la colisión con un cupón.
