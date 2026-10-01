using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pos.Desktop.Common;

namespace Pos.Desktop.Sales;

/// <summary>"Aplicar cupón" (015, FR-011): captura del código; la validación y la aplicación las hace el Punto de venta.</summary>
public sealed partial class CouponEntryViewModel : ViewModelBase
{
    private readonly Func<string, Task<string?>> _apply;
    private readonly Action _close;

    /// <param name="apply">Busca y aplica el cupón; devuelve el mensaje de la causa o nulo si se aplicó.</param>
    /// <param name="close">Cierra sin cambios.</param>
    public CouponEntryViewModel(Func<string, Task<string?>> apply, Action close)
    {
        _apply = apply;
        _close = close;
    }

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsBusy { get; private set; }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            ErrorMessage = await _apply(Code);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => _close();

    private bool CanApply() => !IsBusy;
}
