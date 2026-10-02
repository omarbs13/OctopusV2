using Pos.Application.Abstractions;
using Pos.Application.Audit.ExportAuditLog;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Audit.ConfirmAuditExport;

/// <summary>
/// Registra <c>AUDIT_EXPORTED</c> con formato, rango, filtros y número de entradas. La interfaz lo llama
/// solo si el archivo se guardó: una exportación fallida no queda como exitosa (018, research §12).
/// </summary>
public sealed class ConfirmAuditExportHandler
{
    private readonly IAccessControl _access;
    private readonly IAuditLog _audit;
    private readonly AuditLogDocumentBuilder _builder;

    public ConfirmAuditExportHandler(IAccessControl access, IAuditLog audit, AuditLogDocumentBuilder builder)
    {
        _access = access;
        _audit = audit;
        _builder = builder;
    }

    public async Task<Result> HandleAsync(ConfirmAuditExportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var access = await _access.CheckAsync(Permission.ViewAuditLog, cancellationToken);
        if (!access.Allowed)
        {
            return Result.Failure(access.Error!);
        }

        var receipt = command.Receipt;
        var filters = AuditFilterTexts.Filters(receipt.Filter, await _builder.UserNameAsync(receipt.Filter.UserId, cancellationToken));
        var filterText = filters.Count == 0 ? "Sin filtros" : string.Join("; ", filters);
        _audit.Add(
            AuditActions.AuditExported,
            AuditActions.AuditLogEntity,
            Guid.CreateVersion7(),
            $"Formato: {receipt.Format.ToString().ToUpperInvariant()}. Rango: {AuditFilterTexts.Period(receipt.Filter)}. Filtros: {filterText}. Entradas: {receipt.EntryCount}");
        await _audit.SaveAsync(cancellationToken);
        return Result.Success();
    }
}
