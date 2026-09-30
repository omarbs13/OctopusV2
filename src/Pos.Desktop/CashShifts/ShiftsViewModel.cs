using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.SearchShifts;
using Pos.Application.Users;
using Pos.Application.Users.ListCashiers;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.CashShifts;

namespace Pos.Desktop.CashShifts;

/// <summary>Opción del filtro de estado de "Turnos".</summary>
public sealed record ShiftStatusOption(CashShiftStatus? Status, string Label);

/// <summary>Opción del filtro de usuario; el valor nulo significa "Todos".</summary>
public sealed record ShiftUserOption(Guid? UserId, string Label);

/// <summary>Fila del listado de turnos con los textos ya formateados.</summary>
public sealed record ShiftRow(ShiftListItemDto Item)
{
    public Guid Id => Item.Id;

    public string Folio => Item.Folio;

    public string UserName => Item.OpenedByName;

    public string OpenedText => Item.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string ClosedText => Item.ClosedAtUtc is { } closed
        ? closed.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : string.Empty;

    public string SoldText => MoneyConverter.Format(Item.TotalSoldCents);

    /// <summary>Con signo: sobrante positivo, faltante negativo; vacío en los turnos abiertos.</summary>
    public string DifferenceText => Item.DifferenceCents switch
    {
        null => string.Empty,
        0 => MoneyConverter.Format(0),
        > 0 and var over => "+" + MoneyConverter.Format(over),
        var shortage => "-" + MoneyConverter.Format(Math.Abs(shortage.Value)),
    };

    public bool IsShort => Item.DifferenceCents < 0;

    public bool IsOver => Item.DifferenceCents > 0;
}

/// <summary>Pantalla "Turnos" del administrador (Historia 5): listado filtrable y paginado con el detalle de cada turno.</summary>
public sealed partial class ShiftsViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<ShiftDetailViewModel> _detailFactory;

    private int _searchVersion;
    private bool _suppressAutoSearch;

    public ShiftsViewModel(UseCases useCases, OperationRunner runner, Func<ShiftDetailViewModel> detailFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _detailFactory = detailFactory;
        UserOptions = [new ShiftUserOption(null, Strings.Shifts_UserAll)];
        StatusOptions =
        [
            new(null, Strings.Shifts_StatusAll),
            new(CashShiftStatus.Open, Strings.Shifts_StatusOpen),
            new(CashShiftStatus.Closed, Strings.Shifts_StatusClosed),
        ];
        _suppressAutoSearch = true;
        SelectedUser = UserOptions[0];
        SelectedStatus = StatusOptions[0];
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
        _suppressAutoSearch = false;
    }

    public override string Title => Strings.Nav_Shifts;

    public override FormHost Forms { get; } = new();

    public ObservableCollection<ShiftUserOption> UserOptions { get; }

    public IReadOnlyList<ShiftStatusOption> StatusOptions { get; }

    public ObservableCollection<ShiftRow> Rows { get; } = [];

    [ObservableProperty]
    public partial ShiftRow? SelectedRow { get; set; }

    [ObservableProperty]
    public partial ShiftUserOption SelectedUser { get; set; }

    [ObservableProperty]
    public partial ShiftStatusOption SelectedStatus { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(FirstPageCommand), nameof(PreviousPageCommand), nameof(NextPageCommand), nameof(LastPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(LastPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Products_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override async Task OnActivatedAsync()
    {
        await LoadUsersAsync();
        await SearchAsync();
    }

    partial void OnSelectedStatusChanged(ShiftStatusOption value) => RestartSearch();

    partial void OnSelectedUserChanged(ShiftUserOption value) => RestartSearch();

    partial void OnFromDateChanged(DateTime? value) => RestartSearch();

    partial void OnToDateChanged(DateTime? value) => RestartSearch();

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task FirstPageAsync() => GoToPageAsync(1);

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task LastPageAsync() => GoToPageAsync(TotalPages);

    /// <summary>Enter o doble clic: abre el detalle del turno seleccionado.</summary>
    [RelayCommand]
    private async Task OpenDetailAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var detail = _detailFactory();
        detail.Changed += (_, _) => _ = SearchAsync();
        if (await detail.LoadAsync(row.Id))
        {
            await Forms.OpenAsync(detail, FormPresentation.FullScreen);
        }
    }

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

    /// <summary>Medianoche local del día elegido, expresada en UTC.</summary>
    private static DateTime LocalMidnightToUtc(DateTime date) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();

    private async Task LoadUsersAsync()
    {
        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "ListarUsuariosDeTurnos",
            () => _useCases.RunAsync<ListCashiersHandler, Result<IReadOnlyList<UserOption>>>(h => h.HandleAsync(CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selected = SelectedUser.UserId;
        _suppressAutoSearch = true;
        try
        {
            UserOptions.Clear();
            UserOptions.Add(new ShiftUserOption(null, Strings.Shifts_UserAll));
            foreach (var user in result.Value)
            {
                UserOptions.Add(new ShiftUserOption(user.Id, user.FullName));
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

        // Desde 00:00 del primer día hasta 00:00 del día siguiente al último: [desde, hasta+1) en UTC.
        var query = new SearchShiftsQuery(
            FromDate is { } from ? LocalMidnightToUtc(from) : null,
            ToDate is { } to ? LocalMidnightToUtc(to.AddDays(1)) : null,
            SelectedUser.UserId,
            SelectedStatus.Status,
            CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarTurnos",
            () => _useCases.RunAsync<SearchShiftsHandler, Result<ShiftPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Status"] = query.Status?.ToString(), ["Page"] = query.Page });

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
            Rows.Add(new ShiftRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;
    }
}
