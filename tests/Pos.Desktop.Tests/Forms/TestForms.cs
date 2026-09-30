using CommunityToolkit.Mvvm.ComponentModel;
using Pos.Desktop.Common;
using Pos.Desktop.Forms;
using Pos.Desktop.Tests.TestSupport;

namespace Pos.Desktop.Tests.Forms;

/// <summary>Formulario mínimo: un campo obligatorio y un código que se guarda en mayúsculas.</summary>
public sealed partial class MinimalFormViewModel : FormViewModel<string>
{
    public MinimalFormViewModel(IDialogService dialogs)
        : base(dialogs) => ResetOriginalState();

    public override string Title => "Formulario mínimo";

    public int SaveAttempts { get; private set; }

    /// <summary>Toma los valores actuales como originales (como al cargar un registro).</summary>
    public void AcceptCurrentAsOriginal() => ResetOriginalState();

    public TaskCompletionSource? SaveGate { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? NameError { get; set; }

    protected override object CaptureState() => (Name.Trim(), Code.Trim().ToUpperInvariant());

    protected override async Task<bool> SaveCoreAsync()
    {
        SaveAttempts++;
        NameError = null;
        if (SaveGate is not null)
        {
            await SaveGate.Task;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            NameError = "El nombre es obligatorio.";
            FocusField = nameof(Name);
            return false;
        }

        OnSaved(Name);
        return true;
    }
}

/// <summary>Formulario grande de prueba: 12 campos en dos secciones, 4 obligatorios (SC-004).</summary>
public sealed partial class SampleLargeFormViewModel : FormViewModel<string>
{
    public SampleLargeFormViewModel(IDialogService dialogs)
        : base(dialogs) => ResetOriginalState();

    public override string Title => "Formulario grande de prueba";

    public string[] Fields { get; } = new string[12];

    public static readonly int[] RequiredFields = [0, 1, 6, 7];

    public Dictionary<int, string> Errors { get; } = [];

    public void Set(int index, string value)
    {
        Fields[index] = value;
        OnPropertyChanged(nameof(Fields));
    }

    protected override object CaptureState() => string.Join('|', Fields.Select(f => (f ?? string.Empty).Trim()));

    protected override Task<bool> SaveCoreAsync()
    {
        Errors.Clear();
        foreach (var index in RequiredFields.Where(i => string.IsNullOrWhiteSpace(Fields[i])))
        {
            Errors[index] = "Obligatorio";
        }

        if (Errors.Count > 0)
        {
            FocusField = $"Field{Errors.Keys.Min()}";
            return Task.FromResult(false);
        }

        OnSaved("guardado");
        return Task.FromResult(true);
    }
}

/// <summary>Pantalla de prueba con un listado y formularios.</summary>
public sealed class TestFormsPageViewModel : PageViewModel
{
    public override string Title => "Pantalla de formularios";

    public override FormHost Forms { get; } = new();
}

public static class FormTestFactory
{
    public static FakeDialogService Dialogs() => new();
}
