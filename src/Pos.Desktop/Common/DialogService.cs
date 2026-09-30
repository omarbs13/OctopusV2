using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Common;

internal sealed class DialogService : IDialogService
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;

    public DialogService(IClassicDesktopStyleApplicationLifetime lifetime) => _lifetime = lifetime;

    public Task ShowMessageAsync(string title, string message) =>
        ShowAsync(new DialogWindow(title, message, Strings.Common_Accept, cancelText: null));

    public Task<bool> ConfirmAsync(string title, string message, string confirmText) =>
        ShowAsync(new DialogWindow(title, message, confirmText, Strings.Common_Cancel));

    public async Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string extension)
    {
        var owner = FindOwner();
        if (owner is null)
        {
            return null;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] },
            ],
        });

        return file?.TryGetLocalPath();
    }

    private async Task<bool> ShowAsync(DialogWindow dialog)
    {
        var owner = FindOwner();
        if (owner is not null)
        {
            return await dialog.ShowDialog<bool>(owner);
        }

        // Sin ventana principal (por ejemplo, durante el arranque): ventana independiente.
        var closed = new TaskCompletionSource<bool>();
        dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowInTaskbar = true;
        dialog.Closed += (_, _) => closed.TrySetResult(dialog.Result);
        dialog.Show();
        return await closed.Task;
    }

    private Window? FindOwner() =>
        _lifetime.Windows.FirstOrDefault(w => w.IsActive && w is not DialogWindow)
        ?? _lifetime.MainWindow;
}
