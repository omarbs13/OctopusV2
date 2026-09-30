using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Audit;
using Pos.Application.Audit.SearchAuditLog;
using Pos.Application.Users;
using Pos.Application.Users.SearchUsers;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Administration;

/// <summary>Opción de un filtro de la bitácora; el valor nulo significa "Todos".</summary>
public sealed record AuditUserOption(Guid? UserId, string Label);

public sealed record AuditEventOption(string? Action, string Label);

/// <summary>Fila de la bitácora con los textos ya formateados (fecha local, evento en español).</summary>
public sealed record AuditLogRow(AuditRow Row)
{
    public string DateText => Row.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    public string EventText => AuditActions.Describe(Row.Action);

    public string UserName => Row.UserName;

    public string AuthorizedByName => Row.AuthorizedByName ?? string.Empty;

    public string Details => Row.Details ?? string.Empty;
}

/// <summary>Bitácora de auditoría (FR-027): solo lectura, más reciente primero, filtros por fechas, usuario y evento.</summary>
public sealed partial class AuditLogViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    private int _searchVersion;
    private bool _suppressAutoSearch;

    public AuditLogViewModel(UseCases useCases, OperationRunner runner)
    {
        _useCases = useCases;
        _runner = runner;
        UserOptions = [new AuditUserOption(null, Strings.Audit_All)];
        EventOptions =
        [
            new AuditEventOption(null, Strings.Audit_All),
            .. AuditActions.All.Select(a => new AuditEventOption(a.Key, a.Value)),
        ];
        _suppressAutoSearch = true;
        SelectedUser = UserOptions[0];
        SelectedEvent = EventOptions[0];
        _suppressAutoSearch = false;
    }

    public override string Title => Strings.Audit_Title;

    public ObservableCollection<AuditUserOption> UserOptions { get; }

    public IReadOnlyList<AuditEventOption> EventOptions { get; }

    public ObservableCollection<AuditLogRow> Rows { get; } = [];

    [ObservableProperty]
    public partial AuditUserOption SelectedUser { get; set; }

    [ObservableProperty]
    public partial AuditEventOption SelectedEvent { get; set; }

    [ObservableProperty]
    public partial DateTime? FromDate { get; set; }

    [ObservableProperty]
    public partial DateTime? ToDate { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string? FilterError { get; private set; }

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

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Audit_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override async Task OnActivatedAsync()
    {
        await LoadUsersAsync();
        await SearchAsync();
    }

    partial void OnSelectedUserChanged(AuditUserOption value) => RestartSearch();

    partial void OnSelectedEventChanged(AuditEventOption value) => RestartSearch();

    partial void OnFromDateChanged(DateTime? value) => RestartSearch();

    partial void OnToDateChanged(DateTime? value) => RestartSearch();

    [RelayCommand]
    private void SearchNow() => RestartSearch();

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
                UserOptions.Add(new AuditUserOption(user.Id, user.FullName));
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
        var query = new SearchAuditLogQuery(
            FromDate is { } from ? LocalMidnightToUtc(from) : null,
            ToDate is { } to ? LocalMidnightToUtc(to.AddDays(1)) : null,
            SelectedUser.UserId,
            SelectedEvent.Action,
            CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarBitacora",
            () => _useCases.RunAsync<SearchAuditLogHandler, Result<AuditPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Action"] = query.Action, ["Page"] = query.Page });

        if (!completed || result is null || version != _searchVersion)
        {
            return;
        }

        if (result.Error is ValidationFailed validation)
        {
            FilterError = validation.Errors.Count > 0 ? validation.Errors[0].Message : null;
            Rows.Clear();
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
        IsEmpty = Rows.Count == 0;
    }
}
