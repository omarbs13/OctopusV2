using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Audit.ConfirmAuditExport;
using Pos.Application.Audit.ExportAuditLog;
using Pos.Application.Audit.SearchAuditLog;
using Pos.Application.Reports.Export;
using Pos.Application.Users;
using Pos.Application.Users.SearchUsers;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Administration;

/// <summary>Opción de un filtro de la bitácora; el valor nulo significa "Todos".</summary>
public sealed record AuditUserOption(Guid? UserId, string Label);

public sealed record AuditEventOption(string? Action, string Label);

public sealed record AuditEntityOption(AuditEntityGroup? Group, string Label);

/// <summary>Cambio de campo del panel de detalle; los valores nulos se muestran como "—".</summary>
public sealed record AuditChangeRow(string Field, string Before, string After);

/// <summary>Fila de la bitácora con los textos ya formateados (fecha local, evento y entidad en español).</summary>
public sealed record AuditLogRow(AuditRow Row)
{
    private const int SummaryFields = 2;
    private const string Empty = "—";

    public string DateText => Row.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    public string EventText => AuditActions.Describe(Row.Action);

    public string EntityText => AuditEntityGroups.DescribeEntry(Row.EntityType, Row.Action);

    /// <summary>Nombre legible del registro; vacío en las entradas anteriores a 0.13.0.</summary>
    public string RecordText => Row.EntityName ?? string.Empty;

    public string UserName => Row.UserName;

    public string AuthorizedByName => Row.AuthorizedByName ?? string.Empty;

    /// <summary>"Precio, Categoría (+1)": los campos cambiados, hasta 2; sin cambios, los detalles en una línea.</summary>
    public string SummaryText => Row.Changes.Count > 0
        ? string.Join(", ", Row.Changes.Take(SummaryFields).Select(c => c.Field))
            + (Row.Changes.Count > SummaryFields
                ? " " + string.Format(CultureInfo.CurrentCulture, Strings.Audit_MoreFields, Row.Changes.Count - SummaryFields)
                : string.Empty)
        : (Row.Details ?? string.Empty).ReplaceLineEndings(" ");

    public string Details => Row.Details ?? string.Empty;

    public string Reason => Row.Reason ?? string.Empty;

    public bool HasReason => !string.IsNullOrWhiteSpace(Row.Reason);

    public bool HasDetails => !string.IsNullOrWhiteSpace(Row.Details);

    public bool HasChanges => Row.Changes.Count > 0;

    public string HeaderText => string.IsNullOrEmpty(RecordText) ? EventText : $"{EventText} · {RecordText}";

    public string WhoText => Row.AuthorizedByName is { } authorizer
        ? $"{DateText} · {UserName} · {string.Format(CultureInfo.CurrentCulture, Strings.Audit_AuthorizedBy, authorizer)}"
        : $"{DateText} · {UserName}";

    public IReadOnlyList<AuditChangeRow> Changes { get; } =
        [.. Row.Changes.Select(c => new AuditChangeRow(c.Field, c.Before ?? Empty, c.After ?? Empty))];

    /// <summary>El historial necesita un registro identificado (los intentos de acceso de un usuario inexistente no lo tienen).</summary>
    public bool CanViewHistory => Row.EntityId != Guid.Empty;
}

/// <summary>
/// Bitácora de auditoría (FR-027): solo lectura, más reciente primero, filtros por fechas, usuario, evento y
/// entidad, detalle con los cambios, historial de un registro y exportación a PDF o Excel (018).
/// </summary>
public sealed partial class AuditLogViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;

    private int _searchVersion;
    private bool _suppressAutoSearch;

    public AuditLogViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        UserOptions = [new AuditUserOption(null, Strings.Audit_All)];
        EventOptions =
        [
            new AuditEventOption(null, Strings.Audit_All),
            .. AuditActions.All.Select(a => new AuditEventOption(a.Key, a.Value)),
        ];
        EntityOptions =
        [
            new AuditEntityOption(null, Strings.Audit_AllEntities),
            .. AuditEntityGroups.All.Select(g => new AuditEntityOption(g, AuditEntityGroups.Describe(g))),
        ];
        _suppressAutoSearch = true;
        SelectedUser = UserOptions[0];
        SelectedEvent = EventOptions[0];
        SelectedEntity = EntityOptions[0];

        // FR-018: la consulta inicia en el día de hoy.
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
        _suppressAutoSearch = false;
    }

    public override string Title => Strings.Audit_Title;

    public ObservableCollection<AuditUserOption> UserOptions { get; }

    public IReadOnlyList<AuditEventOption> EventOptions { get; }

    public IReadOnlyList<AuditEntityOption> EntityOptions { get; }

    public ObservableCollection<AuditLogRow> Rows { get; } = [];

    [ObservableProperty]
    public partial AuditUserOption SelectedUser { get; set; }

    [ObservableProperty]
    public partial AuditEventOption SelectedEvent { get; set; }

    [ObservableProperty]
    public partial AuditEntityOption SelectedEntity { get; set; }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(ViewHistoryCommand))]
    public partial AuditLogRow? SelectedRow { get; set; }

    /// <summary>Registro cuyo historial se muestra; nulo sin el filtro de historial.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryActive))]
    [NotifyPropertyChangedFor(nameof(HistoryText))]
    public partial AuditRecordRef? HistoryRecord { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryText))]
    public partial string HistoryName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string? FilterError { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportPdfCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportExcelCommand))]
    public partial bool IsExporting { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LastPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    public bool HasSelection => SelectedRow is not null;

    public bool IsHistoryActive => HistoryRecord is not null;

    public string HistoryText => string.Format(CultureInfo.CurrentCulture, Strings.Audit_HistoryOf, HistoryName);

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Audit_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override async Task OnActivatedAsync()
    {
        await LoadUsersAsync();
        await SearchAsync();
    }

    partial void OnSelectedUserChanged(AuditUserOption value) => RestartSearch();

    partial void OnSelectedEventChanged(AuditEventOption value) => RestartSearch();

    partial void OnSelectedEntityChanged(AuditEntityOption value) => RestartSearch();

    partial void OnFromDateChanged(DateTime? value) => RestartSearch();

    partial void OnToDateChanged(DateTime? value) => RestartSearch();

    [RelayCommand]
    private void SearchNow() => RestartSearch();

    [RelayCommand(CanExecute = nameof(CanViewHistory))]
    private void ViewHistory()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        // El historial es completo: se quitan las fechas (contracts/ui.md).
        _suppressAutoSearch = true;
        HistoryName = string.IsNullOrEmpty(row.RecordText) ? row.EntityText : row.RecordText;
        HistoryRecord = new AuditRecordRef(row.Row.EntityType, row.Row.EntityId);
        FromDate = null;
        ToDate = null;
        _suppressAutoSearch = false;
        RestartSearch();
    }

    private bool CanViewHistory() => SelectedRow is { CanViewHistory: true };

    [RelayCommand]
    private void ClearHistory()
    {
        HistoryRecord = null;
        RestartSearch();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportPdfAsync() => ExportAsync(ExportFormat.Pdf);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportExcelAsync() => ExportAsync(ExportFormat.Xlsx);

    private bool CanExport() => !IsExporting;

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private void RestartSearch()
    {
        if (_suppressAutoSearch)
        {
            return;
        }

        CurrentPage = 1;
        _ = SearchAsync();
    }

    private Task GoToPageAsync(int page)
    {
        CurrentPage = page;
        return SearchAsync();
    }

    /// <summary>Medianoche local del día elegido, expresada en UTC (mismo criterio que "Ventas realizadas").</summary>
    private static DateTime LocalMidnightToUtc(DateTime date) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

    private AuditFilter CurrentFilter() => new(
        FromDate is { } from ? LocalMidnightToUtc(from) : null,
        ToDate is { } to ? LocalMidnightToUtc(to.AddDays(1)) : null,
        SelectedUser.UserId,
        SelectedEvent.Action,
        SelectedEntity.Group,
        HistoryRecord);

    /// <summary>Todos los usuarios, activos o no, más "Sistema", para el filtro de usuario involucrado.</summary>
    private async Task LoadUsersAsync()
    {
        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "ListarUsuariosParaBitacora",
            () => _useCases.RunAsync<SearchUsersHandler, Result<UserPage>>(
                h => h.HandleAsync(new SearchUsersQuery(null, IncludeInactive: true), CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selected = SelectedUser.UserId;
        _suppressAutoSearch = true;
        try
        {
            UserOptions.Clear();
            UserOptions.Add(new AuditUserOption(null, Strings.Audit_All));
            UserOptions.Add(new AuditUserOption(SystemUser.Id, SystemUser.DisplayName));
            foreach (var user in result.Value.Items)
            {
                UserOptions.Add(new AuditUserOption(user.Id, user.UserName));
            }

            SelectedUser = UserOptions.FirstOrDefault(o => o.UserId == selected) ?? UserOptions[0];
        }
        finally
        {
            _suppressAutoSearch = false;
        }
    }

    private async Task SearchAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var filter = CurrentFilter();
        var query = new SearchAuditLogQuery(filter.FromUtc, filter.ToUtcExclusive, filter.UserId, filter.Action, filter.Entity, filter.Record, CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarBitacora",
            () => _useCases.RunAsync<SearchAuditLogHandler, Result<AuditPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Action"] = query.Action, ["Entity"] = query.Entity, ["Page"] = query.Page });

        if (!completed || result is null || version != _searchVersion)
        {
            return;
        }

        if (result.Error is ValidationFailed validation)
        {
            FilterError = validation.Errors.Count > 0 ? validation.Errors[0].Message : null;
            Rows.Clear();
            SelectedRow = null;
            IsEmpty = true;
            return;
        }

        if (!result.IsSuccess)
        {
            return;
        }

        FilterError = null;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new AuditLogRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        SelectedRow = Rows.FirstOrDefault();
        IsEmpty = Rows.Count == 0;
    }

    /// <summary>
    /// Exportación en dos pasos (018, research §12): se genera fuera del hilo de la interfaz, se guarda donde
    /// elija el usuario y solo entonces se registra. Si se cancela o falla la escritura, no queda registro.
    /// </summary>
    private async Task ExportAsync(ExportFormat format)
    {
        if (FromDate is null || ToDate is null)
        {
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Audit_ExportNeedsRange);
            return;
        }

        IsExporting = true;
        try
        {
            var command = new ExportAuditLogCommand(CurrentFilter(), format);
            var context = new Dictionary<string, object?> { ["Formato"] = format.ToString() };
            var (completed, result) = await _runner.RunAsync(
                "ExportarBitacora",
                () => Task.Run(() => _useCases.RunAsync<ExportAuditLogHandler, Result<AuditExport>>(h => h.HandleAsync(command, CancellationToken.None))),
                context);
            if (!completed || result is null)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                var message = result.Error is ValidationFailed { Errors.Count: > 0 } validation ? validation.Errors[0].Message : Strings.Reports_ExportFailed;
                await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, message);
                return;
            }

            var export = result.Value;
            var extension = Path.GetExtension(export.File.FileName).TrimStart('.');
            var path = await _dialogs.PickSaveFileAsync(Strings.Audit_SaveTitle, export.File.FileName, extension);
            if (path is null)
            {
                return;
            }

            try
            {
                await File.WriteAllBytesAsync(path, export.File.Bytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await _dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, string.Format(CultureInfo.CurrentCulture, Strings.Audit_ExportFailed, ex.Message.TrimEnd('.')));
                return;
            }

            await _runner.RunAsync(
                "ConfirmarExportacionBitacora",
                () => _useCases.RunAsync<ConfirmAuditExportHandler, Result>(h => h.HandleAsync(new ConfirmAuditExportCommand(export.Receipt), CancellationToken.None)),
                context);
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, string.Format(CultureInfo.CurrentCulture, Strings.Audit_Exported, export.Receipt.EntryCount));
            RestartSearch();
        }
        finally
        {
            IsExporting = false;
        }
    }
}
