# Contrato: puertos y casos de uso de Application

Firmas que exponen Application y Infrastructure para esta funcionalidad. Los tipos existentes que
no cambian no se repiten.

## IAuditLog (se amplía)

```csharp
public interface IAuditLog
{
    // Existente: no cambia y se usa en los eventos sin antes y después.
    void Add(string action, string entityType, Guid entityId, string? details, Guid? authorizedBy = null);

    // Nuevo.
    void Add(AuditRecord record);

    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed record AuditRecord(
    string Action,
    string EntityType,
    Guid EntityId,
    string? EntityName = null,
    string? Details = null,
    string? Reason = null,
    IReadOnlyList<AuditFieldChange>? Changes = null,
    Guid? AuthorizedBy = null);
```

- `Add` no guarda: la entrada viaja en la transacción del caso de uso, como hoy.
- Si `Changes` es una lista vacía en un evento `*_UPDATED`, el llamador no debe invocar `Add`
  (FR-002). `AuditChanges.HasChanges` lo facilita.

## AuditChanges (nuevo, Application/Audit)

```csharp
public sealed record AuditField(string Field, string? Value);

public static class AuditChanges
{
    public static IReadOnlyList<AuditFieldChange> Compare(IReadOnlyList<AuditField> before, IReadOnlyList<AuditField> after);
    public static IReadOnlyList<AuditFieldChange> Created(IReadOnlyList<AuditField> after);
    public static IReadOnlyList<AuditFieldChange> Removed(IReadOnlyList<AuditField> before);
}
```

`before` y `after` deben tener los mismos campos en el mismo orden. Si no, es un error de
programación (`ArgumentException`).

## IAuditLogReader (se amplía)

```csharp
public sealed record AuditFilter(
    DateTime? FromUtc,
    DateTime? ToUtcExclusive,
    Guid? UserId,               // autor, autorizador o afectado
    string? Action,
    AuditEntityGroup? Entity,
    AuditRecordRef? Record);    // historial de un registro

public sealed record AuditRecordRef(string EntityType, Guid EntityId);

public sealed record AuditSearch(AuditFilter Filter, int Page, int PageSize);

public sealed record AuditRow(
    Guid Id,
    DateTime CreatedAtUtc,
    string Action,
    string EntityType,
    Guid EntityId,
    string? EntityName,
    string UserName,
    string? AuthorizedByName,
    string? Reason,
    string? Details,
    IReadOnlyList<AuditFieldChange> Changes);   // vacía si no tiene

public interface IAuditLogReader
{
    // Más reciente primero; con Record, más antigua primero.
    Task<AuditPage> SearchAsync(AuditSearch search, CancellationToken cancellationToken);

    // Todas las entradas que cumplen el filtro, en el mismo orden, para exportar.
    Task<IReadOnlyList<AuditRow>> ListAsync(AuditFilter filter, CancellationToken cancellationToken);
}
```

## Casos de uso

| Caso de uso | Entrada | Salida | Permiso | Notas |
|---|---|---|---|---|
| `SearchAuditLog` (cambia) | `SearchAuditLogQuery(FromUtc, ToUtcExclusive, UserId, Action, Entity, Record, Page)` | `Result<AuditPage>` | `ViewAuditLog` | Valida que `From < To`. Páginas de 100. |
| `ExportAuditLog` (nuevo) | `ExportAuditLogCommand(Filter, ExportFormat Format)` | `Result<AuditExport>` | `ViewAuditLog` | Exige `FromUtc` y `ToUtcExclusive`. Genera el PDF o XLSX con todas las entradas filtradas. **No** registra nada en la bitácora. |
| `ConfirmAuditExport` (nuevo) | `ConfirmAuditExportCommand(AuditExportReceipt Receipt)` | `Result` | `ViewAuditLog` | Registra `AUDIT_EXPORTED` con formato, rango, filtros y número de entradas. La interfaz lo llama solo si el archivo se guardó bien. |

```csharp
public sealed record AuditExport(ExportedFile File, AuditExportReceipt Receipt);

public sealed record AuditExportReceipt(ExportFormat Format, AuditFilter Filter, int EntryCount);
```

## Casos de uso que cambian su registro en la bitácora

| Caso de uso | Evento | Cambio |
|---|---|---|
| `CreateProduct` | `PRODUCT_CREATED` | Nuevo; `Changes = Created(snapshot)`. |
| `UpdateProduct` | `PRODUCT_UPDATED` | Nuevo; `Compare(before, after)`. No registra nada si no hay cambios. |
| `DeleteProduct` | `PRODUCT_DELETED` | Nuevo; `Removed(snapshot)`. |
| `SetProductCritical` | `PRODUCT_UPDATED` | Nuevo; un solo cambio "Crítico" (research §5). |
| `DeleteCategory` | `CATEGORY_DELETED` | Agrega `Removed(snapshot)`. No hay productos activos afectados: el borrado se rechaza si la categoría tiene productos (research §5). |
| `ConfirmSale` | `SALE_DISCOUNTS_APPLIED` | Sustituye a `DISCOUNT_APPLIED_AUTHORIZED` (research §7). |
| `CancelSale`, `SaleReturnProcessor` | `SALE_CANCELLED`, `SALE_RETURNED` | Agrega `Reason`, `EntityName` y `Changes` con un cambio por producto más "Importe" (research §8). |
| `OpenCashDrawer` | `DRAWER_OPENED` | Agrega `Reason`. |
| `CreateUser`, `UpdateUser` | `USER_*` | Agrega `Changes` y `EntityName`. |
| Categorías, clientes, cupones y configuración | eventos existentes | Agregan `Changes` (research §6). |

## Escritores de reportes (spec 009)

`IXlsxReportWriter` omite la hoja "Gráficas" cuando `ReportDocument.Charts` está vacío. Los
reportes con gráficas no cambian. Al implementar se revisa si algún reporte existente se exporta
sin gráficas; si lo hay, su archivo XLSX dejará de tener esa hoja vacía.
