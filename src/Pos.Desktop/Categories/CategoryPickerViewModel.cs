using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Application.Categories.ListCategoryOptions;
using Pos.Application.Licensing;
using Pos.Desktop.Common;
using Pos.Desktop.Resources;
using Pos.Domain.Licensing;

namespace Pos.Desktop.Categories;

public enum CategoryPickerMode
{
    /// <summary>Formulario de producto: "Sin categoría" y las activas; la actual inactiva solo como valor actual.</summary>
    Assignment,

    /// <summary>Filtros de listados y reportes: "Todas", "Sin categoría" y todas las no borradas.</summary>
    Filter,
}

/// <summary>
/// Opción del selector. <see cref="ToString"/> devuelve el texto, que el <c>ComboBox</c> editable usa para
/// buscar al escribir.
/// </summary>
public sealed record CategoryPickerOption(CategoryFilter Filter, string Label)
{
    public Guid? CategoryId => Filter.CategoryId;

    public override string ToString() => Label;
}

/// <summary>
/// Selector de categoría (016, research §15) que comparten el formulario de producto y los filtros de
/// Productos y Reportes. Solo carga opciones con <c>ListCategoryOptions</c>; no aplica reglas. Con el módulo
/// Categorías inactivo se oculta (025, FR-007).
/// </summary>
public sealed partial class CategoryPickerViewModel : ViewModelBase
{
    /// <summary>Con más opciones que estas se puede escribir para filtrar (spec, casos límite).</summary>
    public const int SearchableThreshold = 15;

    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;
    private readonly ILicenseState? _license;
    private bool _suppress;

    public CategoryPickerViewModel(UseCases useCases, OperationRunner runner, CategoryPickerMode mode, ILicenseState? license = null)
    {
        _license = license;
        _useCases = useCases;
        _runner = runner;
        Mode = mode;
        var first = mode == CategoryPickerMode.Filter
            ? new CategoryPickerOption(CategoryFilter.All, Strings.Category_FilterAll)
            : new CategoryPickerOption(CategoryFilter.Uncategorized, CategoryMessages.Uncategorized);
        Options.Add(first);
        _suppress = true;
        SelectedOption = first;
        _suppress = false;
    }

    public CategoryPickerMode Mode { get; }

    /// <summary>El módulo Categorías está activo; sin él no se muestra el selector.</summary>
    public bool IsAvailable => _license?.IsModuleActive(LicensedModule.Categories) != false;

    public ObservableCollection<CategoryPickerOption> Options { get; } = [];

    [ObservableProperty]
    public partial CategoryPickerOption? SelectedOption { get; set; }

    [ObservableProperty]
    public partial bool IsSearchable { get; private set; }

    /// <summary>El usuario eligió otra opción (no se dispara al cargar).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Filtro elegido; "Todas" si no hay selección.</summary>
    public CategoryFilter Filter => SelectedOption?.Filter ?? CategoryFilter.All;

    /// <summary>Categoría elegida para asignar; nula = "Sin categoría".</summary>
    public Guid? SelectedCategoryId => SelectedOption?.CategoryId;

    /// <summary>
    /// Carga las opciones y selecciona <paramref name="currentId"/> si se indica. En modo asignación, si la
    /// categoría actual está inactiva se agrega como "{nombre} (inactiva)" solo para este producto.
    /// </summary>
    public async Task LoadAsync(Guid? currentId = null, string? currentName = null)
    {
        var includeInactive = Mode == CategoryPickerMode.Filter;
        var (completed, result) = await _runner.RunQuietlyResultAsync(
            "ListarCategorias",
            () => _useCases.RunAsync<ListCategoryOptionsHandler, Result<IReadOnlyList<CategoryOptionDto>>>(
                h => h.HandleAsync(new ListCategoryOptionsQuery(includeInactive), CancellationToken.None)));
        var categories = completed && result is { IsSuccess: true } ? result.Value : [];

        var keep = currentId is null ? SelectedOption?.Filter : CategoryFilter.Only(currentId.Value);
        _suppress = true;
        try
        {
            Options.Clear();
            if (Mode == CategoryPickerMode.Filter)
            {
                Options.Add(new CategoryPickerOption(CategoryFilter.All, Strings.Category_FilterAll));
            }

            Options.Add(new CategoryPickerOption(CategoryFilter.Uncategorized, CategoryMessages.Uncategorized));
            foreach (var category in categories)
            {
                Options.Add(new CategoryPickerOption(CategoryFilter.Only(category.Id), CategoryMessages.Display(category.Name, category.IsActive)));
            }

            if (Mode == CategoryPickerMode.Assignment
                && currentId is { } id
                && categories.All(c => c.Id != id)
                && currentName is not null)
            {
                Options.Add(new CategoryPickerOption(CategoryFilter.Only(id), CategoryMessages.Display(currentName, isActive: false)));
            }

            SelectedOption = Options.FirstOrDefault(o => o.Filter == keep) ?? Options[0];
            IsSearchable = Options.Count > SearchableThreshold;
        }
        finally
        {
            _suppress = false;
        }
    }

    partial void OnSelectedOptionChanged(CategoryPickerOption? value)
    {
        if (!_suppress && value is not null)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
