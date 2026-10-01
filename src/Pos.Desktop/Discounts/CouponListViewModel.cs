using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Discounts;
using Pos.Application.Discounts.Coupons.SearchCoupons;
using Pos.Application.Discounts.Coupons.SetCouponActive;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Domain.Discounts;

namespace Pos.Desktop.Discounts;

/// <summary>Fila del listado de cupones con los textos ya formateados (FR-009).</summary>
public sealed record CouponRow(CouponListItemDto Item)
{
    public Guid Id => Item.Id;

    public string Code => Item.Code;

    public string DiscountText => Item.Mode == DiscountMode.Percent ? Item.Discount.ToString() : MoneyConverter.Format(Item.Value);

    public string ValidityText => string.Format(
        CultureInfo.CurrentCulture,
        Strings.Coupon_Validity,
        DiscountMessages.FormatDate(Item.StartsOn),
        DiscountMessages.FormatDate(Item.EndsOn));

    public string StatusText => DiscountMessages.StatusText(Item.Status);

    public string UsesText => Item.UsesCount.ToString("N0", CultureInfo.CurrentCulture);

    public string RemainingText => Item.RemainingUses is { } remaining
        ? remaining.ToString("N0", CultureInfo.CurrentCulture)
        : Strings.Coupon_Unlimited;

    public bool IsActive => Item.IsActive;
}

/// <summary>Opción del filtro de estado; <c>Status</c> nulo = todos.</summary>
public sealed record CouponStatusOption(CouponStatus? Status, string Label);

/// <summary>
/// Descuentos > Cupones (015, Historia 3, FR-009): búsqueda por código, filtro por estado, paginación de 100,
/// alta, edición y activar o desactivar. Solo invoca casos de uso.
/// </summary>
public sealed partial class CouponListViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Func<CouponFormViewModel> _formFactory;
    private bool _suppress;

    public CouponListViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, Func<CouponFormViewModel> formFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _formFactory = formFactory;
        StatusOptions =
        [
            new CouponStatusOption(null, Strings.Coupon_AllStatuses),
            .. Enum.GetValues<CouponStatus>().Select(s => new CouponStatusOption(s, DiscountMessages.StatusText(s))),
        ];
        _suppress = true;
        SelectedStatus = StatusOptions[0];
        _suppress = false;
    }

    public override string Title => Strings.Coupon_ListTitle;

    public override FormHost Forms { get; } = new();

    public IReadOnlyList<CouponStatusOption> StatusOptions { get; }

    public ObservableCollection<CouponRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CouponStatusOption SelectedStatus { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(ToggleActiveCommand))]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    public partial CouponRow? SelectedRow { get; set; }

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
        string.Format(CultureInfo.CurrentCulture, Strings.Coupon_PageSummary, TotalCount, CurrentPage, TotalPages);

    /// <summary>"Activar" para un cupón desactivado; "Desactivar" para los demás.</summary>
    public string ToggleText => SelectedRow is { IsActive: false } ? Strings.Coupon_Activate : Strings.Coupon_Deactivate;

    public override Task OnActivatedAsync() => SearchAsync();

    partial void OnSelectedStatusChanged(CouponStatusOption value)
    {
        if (!_suppress)
        {
            CurrentPage = 1;
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private Task SearchNowAsync()
    {
        CurrentPage = 1;
        return SearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private Task PreviousPageAsync()
    {
        CurrentPage--;
        return SearchAsync();
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private Task NextPageAsync()
    {
        CurrentPage++;
        return SearchAsync();
    }

    [RelayCommand]
    private async Task NewCouponAsync()
    {
        var form = _formFactory();
        form.Saved += (_, id) => _ = OnSavedAsync(id);
        await Forms.OpenAsync(form, FormPresentation.SidePanel);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var form = _formFactory();
        form.Saved += (_, id) => _ = OnSavedAsync(id);
        if (await form.LoadAsync(row.Id))
        {
            await Forms.OpenAsync(form, FormPresentation.SidePanel);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var command = new SetCouponActiveCommand(row.Id, !row.IsActive, row.Item.Version);
        var (completed, result) = await _runner.RunAsync(
            "ActivarODesactivarCupon",
            () => _useCases.RunAsync<SetCouponActiveHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["CouponId"] = row.Id, ["Active"] = command.Active });
        if (!completed || result is null)
        {
            return;
        }

        if (result.Error is { } error)
        {
            await _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, error switch
            {
                Conflict => Strings.Coupon_Conflict,
                NotFound => Strings.Coupon_NotFound,
                ModuleNotLicensed => Strings.License_ModuleNotLicensed,
                Forbidden => Strings.Common_Forbidden,
                _ => Strings.Common_UnexpectedError,
            });
        }

        await OnSavedAsync(row.Id);
    }

    private bool HasSelection() => SelectedRow is not null;

    private bool HasPreviousPage() => CurrentPage > 1;

    private bool HasNextPage() => CurrentPage < TotalPages;

    private async Task OnSavedAsync(Guid couponId)
    {
        await SearchAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.Id == couponId);
    }

    private async Task SearchAsync()
    {
        var query = new SearchCouponsQuery(SearchText, SelectedStatus.Status, CurrentPage);
        var (completed, result) = await _runner.RunAsync(
            "BuscarCupones",
            () => _useCases.RunAsync<SearchCouponsHandler, Result<CouponPage>>(h => h.HandleAsync(query, CancellationToken.None)),
            new Dictionary<string, object?> { ["Page"] = query.Page });
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selectedId = SelectedRow?.Id;
        Rows.Clear();
        foreach (var item in result.Value.Items)
        {
            Rows.Add(new CouponRow(item));
        }

        TotalCount = result.Value.TotalCount;
        TotalPages = result.Value.TotalPages;
        CurrentPage = result.Value.Page;
        IsEmpty = Rows.Count == 0;
        SelectedRow = Rows.FirstOrDefault(r => r.Id == selectedId);
    }
}
