using Pos.Application.Abstractions;
using Pos.Application.Users.Access;
using Pos.Domain.Users;

namespace Pos.Application.Reports.GetReportSettings;

/// <summary>Lee la configuración de reportes; solo quien tiene <c>ViewReports</c>.</summary>
public sealed class GetReportSettingsHandler
{
    private readonly IAccessControl _access;
    private readonly IReportSettingsStore _store;

    public GetReportSettingsHandler(IAccessControl access, IReportSettingsStore store)
    {
        _access = access;
        _store = store;
    }

    public async Task<Result<ReportSettings>> HandleAsync(CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(Permission.ViewReports, cancellationToken);
        return access.Allowed ? Result.Success(_store.Load()) : Result.Failure<ReportSettings>(access.Error!);
    }
}
