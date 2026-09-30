# Contrato: casos de uso y puertos (Pos.Application/Sales)

Todos los casos de uso son clases simples con `HandleAsync(input, CancellationToken)` y devuelven
`Result`/`Result<T>`. Desktop los invoca con `UseCases.RunAsync`, un ámbito por operación. Las
fallas inesperadas son excepciones que `OperationRunner` registra y traduce a un mensaje sin
detalles técnicos (FR-021).

## Errores nuevos (`Abstractions/Error.cs`)

| Error | Cuándo |
|---|---|
| `SaleChanged(IReadOnlyList<SaleLineReview> Lines)` | Al confirmar, un precio difiere del enviado o una línea dejó de ser vendible. No se guardó nada |
| `AlreadyRegistered(Guid SaleId, string Folio)` | El `DraftId` ya tiene una venta. La UI lo trata como éxito |
| `InvalidState(string Message)` | Cancelar una venta ya cancelada (FR-035) |

También se reutilizan `ValidationFailed`, `NotFound` y `Conflict`.

## Captura

### `FindProductsForSale`

- **In**: `FindProductsForSaleQuery(string Text)`.
- **Out**: `Result<ProductLookup>`, donde:
  - `ProductLookup(LookupKind Kind, IReadOnlyList<SaleProductDto> Items)`.
  - `LookupKind`: `ExactMatch`, `NameMatches` o `None`.
  - `SaleProductDto(Id, Name, Sku, Barcode, PriceCents, UnitCode, UnitName, DecimalPlaces,
    TracksInventory, OnHandThousandths?, NotSellableReason?)`.
  - `NotSellableReason`: `Inactive` o `Deleted`.
- **Reglas**:
  - Busca primero una coincidencia exacta de código de barras o de SKU (normalizado). Si no hay,
    busca por nombre, hasta 20 resultados, de los activos primero.
  - Los borrados aparecen solo por código exacto.
  - Un texto vacío da `ValidationFailed`.

### `SaveSaleDraft` / `GetSaleDraft` / `DiscardSaleDraft`

- `SaveSaleDraftCommand(Guid DraftId, IReadOnlyList<DraftLineDto> Lines)`, con
  `DraftLineDto(ProductId, QuantityThousandths, UnitPriceCents)`.
  - Hace upsert de la fila única.
  - No hace nada si existe una venta con ese `DraftId`.
  - Sin líneas, equivale a descartar.
- `GetSaleDraft` → `Result<RecoveredDraft?>`:
  - `RecoveredDraft(DraftId, IReadOnlyList<RecoveredLineDto> Lines)`.
  - Cada línea trae los datos actuales del producto y `NotSellableReason?`, para que `Cart.Restore`
    la marque (US2, escenario 3).
- `DiscardSaleDraft` borra la fila.

## Cobro y registro

### `ReviewSale`

- **In**: `ReviewSaleQuery(IReadOnlyList<ReviewLineInput> Lines)`, con
  `ReviewLineInput(ProductId, QuantityThousandths)`.
- **Out**: `Result<SaleReview>`, donde:
  - `SaleReview(IReadOnlyList<SaleLineReview> Lines)`.
  - `SaleLineReview(ProductId, CurrentPriceCents, NotSellableReason?, bool InsufficientStock,
    long? OnHandThousandths)`.
- Solo lee; no guarda nada (research §5).

### `ConfirmSale`

- **In**:
  - `ConfirmSaleCommand(Guid DraftId, IReadOnlyList<ConfirmLineInput> Lines,
    IReadOnlyList<PaymentInput> Payments)`.
  - `ConfirmLineInput(ProductId, QuantityThousandths, ExpectedUnitPriceCents)`.
  - `PaymentInput(PaymentMethod Method, long AmountCents, long? ReceivedCents, string? Reference)`.
    Para efectivo, `AmountCents` es ignorado y lo recalcula `Checkout`.
- **Out**: `Result<ConfirmedSale>`, con `ConfirmedSale(Guid SaleId, string Folio, long TotalCents,
  long ChangeCents)`.
- **Flujo** (una transacción `BEGIN IMMEDIATE`; research §4):
  1. Valida la forma: al menos 1 línea, productos sin repetir, pagos válidos.
  2. Si existe una venta con `DraftId`, devuelve `AlreadyRegistered`.
  3. Carga los productos. Si algún precio ≠ esperado o alguno no es vendible, devuelve
     `SaleChanged`.
  4. Valida la cantidad contra los decimales de la unidad. Construye las líneas con copias.
     Construye `Checkout` y exige `CanConfirm`; si no, devuelve `ValidationFailed` con el faltante.
  5. Calcula el folio con `MAX + 1`.
  6. Registra `RecordSale` por cada línea con inventario (se permite existencia negativa) y agrega
     `Sale.Register(...)`.
  7. Borra el borrador y guarda.
  8. Un conflicto de concurrencia o de índice único devuelve `Conflict`. Si fue por `DraftId`,
     devuelve `AlreadyRegistered`.
- Registra en el log `Venta registrada. SaleId, Folio, Lines, TotalCents` y, en error,
  `DraftId` y el número de líneas (Principio VIII).

## Consulta y cancelación

### `SearchSales`

- **In**: `SearchSalesQuery(DateTime? FromUtc, DateTime? ToUtcExclusive, string? FolioText,
  SaleStatus? Status, int Page = 1)`.
- `FolioText` acepta `V-000123`, `v123` o `123`. Un folio inválido da `ValidationFailed`.
- **Out**: `SalePage(Items, TotalCount, Page, PageSize = 100)`, con
  `SaleListItemDto(Id, Folio, CreatedAtUtc, TotalCents, IReadOnlyList<PaymentMethod> Methods,
  SaleStatus Status)`.
- Ordena de la más reciente a la más antigua.

### `GetSale`

- **Out**: `Result<SaleDetailDto>`, con:
  - Encabezado y versión.
  - Líneas: nombre, SKU, cantidad, decimales, unidad, precio e importe.
  - Pagos: forma, monto, recibido, cambio y referencia.
  - Datos de cancelación.
- Devuelve `NotFound` si no existe.

### `CancelSale`

- **In**: `CancelSaleCommand(Guid SaleId, int ExpectedVersion, string Reason)`.
- **Out**: `Result`. Los errores posibles:
  - `ValidationFailed`: motivo vacío o de más de 250 caracteres.
  - `NotFound`.
  - `InvalidState`: ya está cancelada.
  - `Conflict`: cambió la versión.
- En una transacción: `Sale.Cancel`, un `RecordSaleCancellation` por cada línea con
  `SaleMovementId`, y `IAuditLog.Add("SALE_CANCELLED", …)` (research §9 y §10).

## Inicio

### `GetSalesDashboard`

- **In**: `SalesDashboardQuery(IReadOnlyList<DayWindow> Days)`, donde
  `DayWindow(DateOnly LocalDate, DateTime FromUtc, DateTime ToUtcExclusive)`. Son los últimos 7
  días locales y el último es hoy.
- **Out**: `SalesDashboard(IReadOnlyList<DayTotal> Days, IReadOnlyList<TopProduct> TopProducts)`:
  - `DayTotal(LocalDate, TotalCents, Count)`.
  - `TopProduct(ProductId, Name, QuantityThousandths, DecimalPlaces)`: top 5 del periodo completo.
- Solo cuenta ventas `COMPLETED` (FR-036, SC-009).

## Puertos nuevos o ampliados

```csharp
public interface ISaleRepository                 // Application/Sales
{
    Task<bool> ExistsForDraftAsync(Guid draftId, CancellationToken ct);
    Task<ConfirmedSale?> FindByDraftAsync(Guid draftId, CancellationToken ct);
    Task<long> NextFolioNumberAsync(CancellationToken ct);           // MAX + 1, dentro de la transacción
    Task<Sale?> GetAsync(Guid id, CancellationToken ct);             // con líneas y pagos, con seguimiento
    void Add(Sale sale);
    Task<SalePage> SearchAsync(SaleSearch search, CancellationToken ct);
    Task<SaleDetailDto?> GetDetailAsync(Guid id, CancellationToken ct);
    Task<SalesDashboard> GetDashboardAsync(IReadOnlyList<DayWindow> days, CancellationToken ct);
    Task<SaveOutcome> SaveChangesAsync(CancellationToken ct);       // Conflict o Duplicate("DraftId" | "Folio")
}

public interface ISaleDraftStore                 // Application/Sales
{
    Task<StoredDraft?> LoadAsync(CancellationToken ct);
    Task SaveAsync(Guid draftId, IReadOnlyList<DraftLineDto> lines, CancellationToken ct); // ignora si ya hay venta
    void Remove();                               // se guarda con la unidad de trabajo del caso de uso
    Task DiscardAsync(CancellationToken ct);
}

public interface IAuditLog                       // Application/Abstractions
{
    void Add(string action, string entityType, Guid entityId, string? details);
}
```

Otros cambios:

- `IProductRepository` gana:
  - `FindForSaleAsync(string code, CancellationToken)`: exacto por código de barras o SKU,
    incluye inactivos y borrados.
  - `SearchForSaleAsync(string nameText, int limit, CancellationToken)`.
  - `GetManyAsync(IReadOnlyCollection<Guid> ids, bool includeDeleted, CancellationToken)`.
- `IInventoryRepository` gana `GetStocksAsync(IReadOnlyCollection<Guid> productIds,
  CancellationToken)`.
