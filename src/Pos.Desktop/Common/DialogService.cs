using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Common;

internal sealed class DialogService : IDialogService
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;

    public DialogService(IClassicDesktopStyleApplicationLifetime lifetime) => _lifetime = lifetime;

    public Task ShowMessageAsync(string title, string message) =>
        ShowAsync(new DialogWindow(title, message, [new DialogButton(Strings.Common_Accept, true, IsAccent: true)], true));

    /// <summary>La opción predeterminada es la segura: cancelar.</summary>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText) =>
        (bool)await ShowAsync(new DialogWindow(
            title,
            message,
            [new DialogButton(confirmText, true), new DialogButton(Strings.Common_Cancel, false)],
            false));

    /// <summary>La opción predeterminada (Enter y Esc) es la segura: seguir editando.</summary>
    public async Task<UnsavedChangesChoice> AskUnsavedChangesAsync() =>
        (UnsavedChangesChoice)await ShowAsync(new DialogWindow(
            Strings.Unsaved_Title,
            Strings.Unsaved_Message,
            [
                new DialogButton(Strings.Unsaved_Save, UnsavedChangesChoice.Save, IsAccent: true),
                new DialogButton(Strings.Unsaved_Discard, UnsavedChangesChoice.Discard),
                new DialogButton(Strings.Unsaved_KeepEditing, UnsavedChangesChoice.KeepEditing),
            ],
            UnsavedChangesChoice.KeepEditing));

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

    private async Task<object> ShowAsync(DialogWindow dialog)
    {
        var owner = FindOwner();
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
            return dialog.Result;
        }

        // Sin ventana principal (por ejemplo, durante el arranque): ventana independiente.
        var closed = new TaskCompletionSource<object>();
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
