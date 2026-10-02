using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Application.Categories.DeleteCategory;
using Pos.Application.Categories.SearchCategories;
using Pos.Application.Categories.SetCategoryActive;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Categories;

/// <summary>Fila del catálogo de categorías con los textos ya formateados.</summary>
public sealed record CategoryRow(CategoryListItemDto Item)
{
    public Guid Id => Item.Id;

    public string Name => Item.Name;

    public string Description => Item.Description ?? string.Empty;

    public string ProductsText => Item.ProductCount.ToString("N0", CultureInfo.CurrentCulture);

    public string StatusText => Item.IsActive ? Strings.Category_Active : Strings.Category_Inactive;

    public bool IsActive => Item.IsActive;
}

/// <summary>Opción del filtro de estado.</summary>
public sealed record CategoryStatusOption(CategoryStatusFilter Status, string Label);

/// <summary>
/// Catálogos > Categorías (016, Historia 1): búsqueda, filtro de estado, alta, edición, desactivar o
/// reactivar con confirmación y eliminar. Solo invoca casos de uso (Principio III).
/// </summary>
public sealed partial class CategoriesViewModel : PageViewModel
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly IDialogService _dialogs;
    private readonly Func<CategoryFormViewModel> _formFactory;
    private bool _suppress;

    public CategoriesViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs, Func<CategoryFormViewModel> formFactory)
    {
        _useCases = useCases;
        _runner = runner;
        _dialogs = dialogs;
        _formFactory = formFactory;
        StatusOptions =
        [
            new CategoryStatusOption(CategoryStatusFilter.Active, Strings.Category_StatusActive),
            new CategoryStatusOption(CategoryStatusFilter.Inactive, Strings.Category_StatusInactive),
            new CategoryStatusOption(CategoryStatusFilter.All, Strings.Category_StatusAll),
        ];
        _suppress = true;
        SelectedStatus = StatusOptions[0];
        _suppress = false;
    }

    public override string Title => Strings.Category_ListTitle;

    public override FormHost Forms { get; } = new();

    public IReadOnlyList<CategoryStatusOption> StatusOptions { get; }

    public ObservableCollection<CategoryRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial CategoryStatusOption SelectedStatus { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(ToggleActiveCommand), nameof(DeleteCommand))]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    public partial CategoryRow? SelectedRow { get; set; }

    /// <summary>"Reactivar" para una inactiva; "Desactivar" para las activas.</summary>
    public string ToggleText => SelectedRow is { IsActive: false } ? Strings.Category_Activate : Strings.Category_Deactivate;

    public override Task OnActivatedAsync() => SearchAsync();

    partial void OnSelectedStatusChanged(CategoryStatusOption value)
    {
        if (!_suppress)
        {
            _ = SearchAsync();
        }
    }

    [RelayCommand]
    private Task SearchNowAsync() => SearchAsync();

    [RelayCommand]
    private async Task NewCategoryAsync()
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

        var command = new SetCategoryActiveCommand(row.Id, !row.IsActive, row.Item.Version);
        var result = await SetActiveAsync(command);

        // FR-005: con productos se muestra cuántos y solo se desactiva si el Administrador confirma.
        if (result?.Error is ConfirmationRequired confirmation)
        {
            if (!await _dialogs.ConfirmAsync(Strings.Category_DeactivateTitle, CategoryMessages.ConfirmDeactivate(confirmation.ProductCount), Strings.Category_Deactivate))
            {
                return;
            }

            result = await SetActiveAsync(command with { Confirmed = true });
        }

        if (result is null)
        {
            return;
        }

        if (result.Error is { } error)
        {
            await ShowErrorAsync(error);
        }

        await OnSavedAsync(row.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedRow is not { } row)
        {
            return;
        }

        var question = string.Format(CultureInfo.CurrentCulture, Strings.Category_DeleteConfirm, row.Name);
        if (!await _dialogs.ConfirmAsync(Strings.Category_DeleteTitle, question, Strings.Category_Delete))
        {
            return;
        }

        var command = new DeleteCategoryCommand(row.Id, row.Item.Version);
        var (completed, result) = await _runner.RunAsync(
            "EliminarCategoria",
            () => _useCases.RunAsync<DeleteCategoryHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["CategoryId"] = row.Id });
        if (!completed || result is null)
        {
            return;
        }

        if (result.Error is { } error)
        {
            await ShowErrorAsync(error);
        }

        await SearchAsync();
    }

    private bool HasSelection() => SelectedRow is not null;

    private async Task<Result?> SetActiveAsync(SetCategoryActiveCommand command)
    {
        var (completed, result) = await _runner.RunAsync(
            "ActivarODesactivarCategoria",
            () => _useCases.RunAsync<SetCategoryActiveHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
            new Dictionary<string, object?> { ["CategoryId"] = command.Id, ["Active"] = command.IsActive });
        return completed ? result : null;
    }

    private Task ShowErrorAsync(Error error) =>
        _dialogs.ShowMessageAsync(Strings.Common_InfoTitle, error switch
        {
            CategoryInUse inUse => CategoryMessages.InUse(inUse.ProductCount),
            Conflict => CategoryMessages.Conflict,
            NotFound => Strings.Category_NotFound,
            Forbidden => Strings.Common_Forbidden,
            _ => Strings.Common_UnexpectedError,
        });

    private async Task OnSavedAsync(Guid categoryId)
    {
        await SearchAsync();
        SelectedRow = Rows.FirstOrDefault(r => r.Id == categoryId);
    }

    private async Task SearchAsync()
    {
        var query = new SearchCategoriesQuery(SearchText, SelectedStatus.Status);
        var (completed, result) = await _runner.RunAsync(
            "BuscarCategorias",
            () => _useCases.RunAsync<SearchCategoriesHandler, Result<IReadOnlyList<CategoryListItemDto>>>(h => h.HandleAsync(query, CancellationToken.None)));
        if (!completed || result is not { IsSuccess: true })
        {
            return;
        }

        var selectedId = SelectedRow?.Id;
        Rows.Clear();
        foreach (var item in result.Value)
        {
            Rows.Add(new CategoryRow(item));
        }

        IsEmpty = Rows.Count == 0;
        SelectedRow = Rows.FirstOrDefault(r => r.Id == selectedId);
    }
}
