using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.CashShifts;
using Pos.Application.CashShifts.SearchShiftCuts;
using Pos.Application.Users;
using Pos.Application.Users.ListCashiers;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.CashShifts;

namespace Pos.Desktop.CashShifts;

/// <summary>Opción del filtro de tipo del histórico; nulo significa "Todos".</summary>
public sealed record ShiftCutTypeOption(ShiftCutType? Type, string Label);

/// <summary>Fila del histórico de cortes con los textos ya formateados.</summary>
public sealed record ShiftCutRow(ShiftCutListItemDto Item)
{
    public Guid Id => Item.Id;

    public string TypeText => Item.Type == ShiftCutType.Readout ? Strings.Cut_TypeReadout : Strings.Cut_TypeClosing;

    public string Folio => Item.Folio;

    public string GeneratedText => Item.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string UserName => Item.GeneratedByName;

    public string ShiftFolio => Item.ShiftFolio;

    public string SoldText => MoneyConverter.Format(Item.TotalSoldCents);

    /// <summary>Solo en los Z: "Sobrante $X", "Faltante $X" o "Cuadrado"; "—" en los X.</summary>
    public string DifferenceText => Item.DifferenceCents switch
    {
        null => "—",
        0 => Strings.Shift_Balanced,
        > 0 and var over => $"{Strings.Shift_Over} {MoneyConverter.Format(over)}",
        var shortage => $"{Strings.Shift_Short} {MoneyConverter.Format(Math.Abs(shortage.Value))}",
    };

    public bool IsShort => Item.DifferenceCents < 0;

    public bool IsOver => Item.DifferenceCents > 0;
}

/// <summary>
/// "Caja > Histórico de cortes" (017, Historia 3): Cortes X y Z filtrables por tipo, fechas y usuario,
/// paginados de 100 en 100; cada corte se abre y se reimprime con sus cifras originales.
/// </summary>
public sealed partial class ShiftCutsViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly Func<ShiftCutDetailViewModel> _detailFactory;

    private int _searchVersion;

    public ShiftCutsViewModel(UseCases useCases, OperationRunner runner, Func<ShiftCutDetailViewModel> detailFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _detailFactory = detailFactory;
        UserOptions = [new ShiftUserOption(null, Strings.Shifts_UserAll)];
        TypeOptions =
        [
            new(null, Strings.Cut_TypeAll),
            new(ShiftCutType.Readout, Strings.Cut_TypeReadout),
            new(ShiftCutType.Closing, Strings.Cut_TypeClosing),
        ];
        SelectedUser = UserOptions[0];
        SelectedType = TypeOptions[0];
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
    }

    public override string Title => Strings.Nav_CashCuts;

    public override FormHost Forms { get; } = new();

    public ObservableCollection<ShiftUserOption> UserOptions { get; }

    public IReadOnlyList<ShiftCutTypeOption> TypeOptions { get; }

    public ObservableCollection<ShiftCutRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDetailCommand))]
    public partial ShiftCutRow? SelectedRow { get; set; }

    [ObservableProperty]
    public partial ShiftUserOption SelectedUser { get; set; }

    [ObservableProperty]
    public partial ShiftCutTypeOption SelectedType { get; set; }

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
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    public partial long TotalCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    public partial int TotalPages { get; private set; } = 1;

    public string PageSummary =>
        string.Format(CultureInfo.CurrentCulture, Strings.Products_PageSummary, TotalCount, CurrentPage, TotalPages);

    public override async Task OnActivatedAsync()
    {
        await LoadUsersAsync();
        await SearchAsync();
    }

    [RelayCommand]
    private Task SearchFromFirstPageAsync()
    {
        CurrentPage = 1;
        return SearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync() => GoToPageAsync(CurrentPage - 1);

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync() => GoToPageAsync(CurrentPage + 1);

    /// <summary>Doble clic, Enter o "Ver": abre el corte para consultarlo y reimprimirlo.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task OpenDetailAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var detail = _detailFactory();
        if (await detail.LoadAsync(row.Id, isReprint: true))
        {
            await Forms.OpenAsync(detail, FormPresentation.FullScreen);
        }
    }

    private bool HasSelection() => SelectedRow is not null;

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private Task GoToPageAsync(int page)
    {
        CurrentPage = page;
        return SearchAsync();
    }

    private async Task LoadUsersAsync()
    {
        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "ListarUsuariosDeCortes",
            () => _useCases.RunAsync<ListCashiersHandler, Result<IReadOnlyList<UserOption>>>(h => h.HandleAsync(CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selected = SelectedUser.UserId;
        UserOptions.Clear();
        UserOptions.Add(new ShiftUserOption(null, Strings.Shifts_UserAll));
        foreach (var user in result.Value)
        {
            UserOptions.Add(new ShiftUserOption(user.Id, user.FullName));
        }

        SelectedUser = UserOptions.FirstOrDefault(o => o.UserId == selected) ?? UserOptions[0];
    }

    private async Task SearchAsync()
    {
        var version = Interlocked.Increment(ref _searchVersion);
        var query = new SearchShiftCutsQuery(
            SelectedType.Type,
            FromDate is { } from ? DateOnly.FromDateTime(from) : null,
            ToDate is { } to ? DateOnly.FromDateTime(to) : null,
            SelectedUser.UserId,
            CurrentPage);

        var (completed, result) = await _runner.RunAsync(
            "BuscarCortes",
            () => _useCases.RunAsync<SearchShiftCutsHandler, Result<ShiftCutPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Type"] = query.Type?.ToString(), ["Page"] = query.Page });

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
            Rows.Add(new ShiftCutRow(item));
        }

        var page = result.Value;
        TotalCount = page.TotalCount;
        TotalPages = page.TotalPages;
        CurrentPage = page.Page;
        IsEmpty = Rows.Count == 0;
    }
}
