using Pos.Application.Abstractions;
using Pos.Application.Business;
using Pos.Application.Reports.Export;
using Pos.Application.Users;
using Pos.Application.Users.Session;

namespace Pos.Application.Audit.ExportAuditLog;

/// <summary>
/// Arma el documento de la bitácora exportada (018, research §12): en PDF una fila por entrada con sus
/// cambios en varias líneas; en XLSX una fila por cambio de campo (FR-024).
/// </summary>
public sealed class AuditLogDocumentBuilder
{
    public const string Title = "Bitácora de auditoría";

    private readonly IBusinessProfileRepository _business;
    private readonly IUserRepository _users;
    private readonly IUserSession _session;
    private readonly IClock _clock;

    public AuditLogDocumentBuilder(IBusinessProfileRepository business, IUserRepository users, IUserSession session, IClock clock)
    {
        _business = business;
        _users = users;
        _session = session;
        _clock = clock;
    }

    public async Task<ReportDocument> BuildAsync(AuditFilter filter, ExportFormat format, IReadOnlyList<AuditRow> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(rows);

        var profile = await _business.GetAsync(cancellationToken);
        var business = profile is null ? null : new ReportBusiness(profile.TradeName, profile.Address, profile.Phone);
        var table = format == ExportFormat.Pdf ? EntryTable(rows) : ChangeTable(rows);
        return new ReportDocument(
            Title,
            AuditFilterTexts.Period(filter),
            AuditFilterTexts.Filters(filter, await UserNameAsync(filter.UserId, cancellationToken)),
            [new ReportMetric("Entradas", new CountCell(rows.Count))],
            [table],
            [],
            business,
            _clock.UtcNow,
            _session.User?.FullName ?? string.Empty);
    }

    public async Task<string?> UserNameAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id)
        {
            return null;
        }

        return id == SystemUser.Id
            ? SystemUser.DisplayName
            : (await _users.GetAsync(id, cancellationToken))?.FullName ?? SystemUser.NameOf(id);
    }

    /// <summary>PDF: una fila por entrada, con los cambios como "Campo: antes → después" en varias líneas.</summary>
    private static ReportTable EntryTable(IReadOnlyList<AuditRow> rows) => new(
        "Entradas",
        [
            new("Fecha y hora", ReportColumnType.Date),
            new("Evento", ReportColumnType.Text),
            new("Entidad", ReportColumnType.Text),
            new("Registro", ReportColumnType.Text),
            new("Usuario", ReportColumnType.Text),
            new("Autorizó", ReportColumnType.Text),
            new("Motivo", ReportColumnType.Text),
            new("Cambios", ReportColumnType.Text),
        ],
        [.. rows.Select(r => (IReadOnlyList<ReportCell>)
        [
            new DateCell(r.CreatedAtUtc),
            new TextCell(AuditActions.Describe(r.Action)),
            new TextCell(AuditEntityGroups.DescribeEntry(r.EntityType, r.Action)),
            new TextCell(r.EntityName ?? string.Empty),
            new TextCell(r.UserName),
            new TextCell(r.AuthorizedByName ?? string.Empty),
            new TextCell(r.Reason ?? string.Empty),
            new TextCell(r.Changes.Count > 0
                ? string.Join('\n', r.Changes.Select(c => $"{c.Field}: {c.Before ?? "—"} → {c.After ?? "—"}"))
                : r.Details ?? string.Empty),
        ])]);

    /// <summary>XLSX: una fila por cambio de campo, o una por entrada sin cambios (FR-024).</summary>
    private static ReportTable ChangeTable(IReadOnlyList<AuditRow> rows) => new(
        "Cambios",
        [
            new("Fecha y hora", ReportColumnType.Date),
            new("Evento", ReportColumnType.Text),
            new("Entidad", ReportColumnType.Text),
            new("Registro", ReportColumnType.Text),
            new("Usuario", ReportColumnType.Text),
            new("Autorizó", ReportColumnType.Text),
            new("Motivo", ReportColumnType.Text),
            new("Campo", ReportColumnType.Text),
            new("Antes", ReportColumnType.Text),
            new("Después", ReportColumnType.Text),
            new("Detalles", ReportColumnType.Text),
        ],
        [.. rows.SelectMany(r => r.Changes.Count > 0
            ? r.Changes.Select(c => ChangeRow(r, c.Field, c.Before, c.After))
            : [ChangeRow(r, null, null, null)])]);

    private static IReadOnlyList<ReportCell> ChangeRow(AuditRow row, string? field, string? before, string? after) =>
    [
        new DateCell(row.CreatedAtUtc),
        new TextCell(AuditActions.Describe(row.Action)),
        new TextCell(AuditEntityGroups.DescribeEntry(row.EntityType, row.Action)),
        new TextCell(row.EntityName ?? string.Empty),
        new TextCell(row.UserName),
        new TextCell(row.AuthorizedByName ?? string.Empty),
        new TextCell(row.Reason ?? string.Empty),
        new TextCell(field ?? string.Empty),
        new TextCell(before ?? string.Empty),
        new TextCell(after ?? string.Empty),
        new TextCell(row.Details ?? string.Empty),
    ];
}
