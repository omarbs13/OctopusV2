using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Application.Abstractions;
using Pos.Application.Categories;
using Pos.Application.Categories.CreateCategory;
using Pos.Application.Categories.GetCategory;
using Pos.Application.Categories.UpdateCategory;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Categories;

/// <summary>Alta y edición de una categoría (016, FR-002, FR-004): nombre y descripción. Solo invoca casos de uso.</summary>
public sealed partial class CategoryFormViewModel : FormViewModel<Guid>
{
    private readonly UseCases _useCases;
    private readonly OperationRunner _runner;

    private Guid? _categoryId;
    private int _expectedVersion;

    public CategoryFormViewModel(UseCases useCases, OperationRunner runner, IDialogService dialogs)
        : base(dialogs)
    {
        _useCases = useCases;
        _runner = runner;
        ResetOriginalState();
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? NameError { get; private set; }

    [ObservableProperty]
    public partial string? DescriptionError { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsEditMode { get; private set; }

    public override string Title => IsEditMode ? Strings.Category_EditTitle : Strings.Category_NewTitle;

    public async Task<bool> LoadAsync(Guid categoryId)
    {
        var (completed, result) = await _runner.RunAsync(
            "CargarCategoria",
            () => _useCases.RunAsync<GetCategoryHandler, Result<CategoryDto>>(h => h.HandleAsync(new GetCategoryQuery(categoryId), CancellationToken.None)),
            new Dictionary<string, object?> { ["CategoryId"] = categoryId });
        if (!completed || result is null)
        {
            return false;
        }

        if (!result.IsSuccess)
        {
            await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, result.Error is Forbidden ? Strings.Common_Forbidden : Strings.Category_NotFound);
            return false;
        }

        var category = result.Value;
        _categoryId = category.Id;
        _expectedVersion = category.Version;
        IsEditMode = true;
        Name = category.Name;
        Description = category.Description ?? string.Empty;
        ClearErrors();
        ResetOriginalState();
        return true;
    }

    protected override async Task<bool> SaveCoreAsync()
    {
        ClearErrors();
        var description = string.IsNullOrWhiteSpace(Description) ? null : Description;
        Guid savedId;
        Result? result;
        bool completed;
        if (_categoryId is { } id)
        {
            var command = new UpdateCategoryCommand(id, Name, description, _expectedVersion);
            (completed, result) = await _runner.RunAsync(
                "EditarCategoria",
                () => _useCases.RunAsync<UpdateCategoryHandler, Result>(h => h.HandleAsync(command, CancellationToken.None)),
                new Dictionary<string, object?> { ["CategoryId"] = id });
            savedId = id;
        }
        else
        {
            var command = new CreateCategoryCommand(Name, description);
            var (created, createResult) = await _runner.RunAsync(
                "CrearCategoria",
                () => _useCases.RunAsync<CreateCategoryHandler, Result<Guid>>(h => h.HandleAsync(command, CancellationToken.None)));
            (completed, result) = (created, createResult);
            savedId = createResult is { IsSuccess: true } ? createResult.Value : Guid.Empty;
        }

        if (!completed || result is null)
        {
            return false;
        }

        if (result.Error is null)
        {
            OnSaved(savedId);
            return true;
        }

        await ShowErrorAsync(result.Error);
        return false;
    }

    protected override object CaptureState() => new CategoryFormState(Name.Trim(), Description.Trim());

    private async Task ShowErrorAsync(Error error)
    {
        switch (error)
        {
            case ValidationFailed validation:
                foreach (var field in validation.Errors)
                {
                    if (field.Field == CategoryFields.Description)
                    {
                        DescriptionError = field.Message;
                    }
                    else if (field.Field == CategoryFields.Name)
                    {
                        NameError = field.Message;
                    }
                    else
                    {
                        ErrorMessage = field.Message;
                    }
                }

                FocusField = validation.Errors.Count > 0 ? validation.Errors[0].Field : null;
                break;
            case Duplicate:
                NameError = CategoryMessages.NameDuplicate;
                FocusField = CategoryFields.Name;
                break;
            case Forbidden:
                ErrorMessage = Strings.Common_Forbidden;
                break;
            case Conflict when _categoryId is { } id:
                if (await Dialogs.ConfirmAsync(Strings.Category_ConflictTitle, CategoryMessages.Conflict, Strings.Editor_Reload))
                {
                    await LoadAsync(id);
                }

                break;
            case NotFound:
                await Dialogs.ShowMessageAsync(Strings.Common_InfoTitle, Strings.Category_NotFound);
                RaiseClosed();
                break;
            default:
                await Dialogs.ShowMessageAsync(Strings.Common_ErrorTitle, Strings.Common_UnexpectedError);
                break;
        }
    }

    private void ClearErrors()
    {
        NameError = DescriptionError = ErrorMessage = null;
        FocusField = null;
    }
}

/// <summary>Estado normalizado del formulario de categoría, para detectar cambios.</summary>
internal sealed record CategoryFormState(string Name, string Description);
