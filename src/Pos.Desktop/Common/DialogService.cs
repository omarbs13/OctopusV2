using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Pos.Desktop.Common.Scanner;
using Pos.Desktop.Forms;
using Pos.Desktop.Resources;
using Pos.Desktop.Shell;

namespace Pos.Desktop.Common;

internal sealed class DialogService : IDialogService, IDialogVisibility
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly IdleMonitor? _idle;
    private readonly List<DialogWindow> _open = [];

    public DialogService(IClassicDesktopStyleApplicationLifetime lifetime, IdleMonitor? idle = null)
    {
        _lifetime = lifetime;
        _idle = idle;
    }

    public Action? ScanIgnored { get; set; }

    /// <summary>Oculta los diálogos abiertos sin cerrarlos (bloqueo por inactividad).</summary>
    public void HideAll()
    {
        foreach (var dialog in _open.ToList())
        {
            dialog.Hide();
        }
    }

    /// <summary>Vuelve a mostrar los diálogos que se ocultaron.</summary>
    public void ShowAll()
    {
        foreach (var dialog in _open.ToList())
        {
            dialog.Show();
            dialog.Activate();
        }
    }

    public Task ShowMessageAsync(string title, string message) =>
        ShowAsync(new DialogWindow(title, message, [new DialogButton(Strings.Common_Accept, true, IsAccent: true)], true));

    /// <summary>La opción predeterminada es la segura: cancelar.</summary>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText) =>
        (bool)await ShowAsync(new DialogWindow(
            title,
            message,
            [new DialogButton(confirmText, true), new DialogButton(Strings.Common_Cancel, false)],
            false));

    /// <summary>La primaria es la opción predeterminada (Enter); Esc elige la secundaria.</summary>
    public async Task<bool> AskAsync(string title, string message, string primaryText, string secondaryText) =>
        (bool)await ShowAsync(new DialogWindow(
            title,
            message,
            [new DialogButton(primaryText, true, IsAccent: true), new DialogButton(secondaryText, false)],
            true,
            escapeResult: false));

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

    public async Task<FileSelection?> PickOpenFileAsync(string title, IReadOnlyList<FileTypeFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var owner = FindOwner();
        if (owner is null)
        {
            return null;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [.. filters.Select(f => new FilePickerFileType(f.Name) { Patterns = [.. f.Patterns] })],
        });

        if (files.Count == 0)
        {
            return null;
        }

        var file = files[0];
        var properties = await file.GetBasicPropertiesAsync();
        return new FileSelection(file.Name, (long)(properties.Size ?? 0), file.OpenReadAsync);
    }

    private async Task<object> ShowAsync(DialogWindow dialog)
    {
        Track(dialog);
        if (ScanIgnored is { } onScanIgnored)
        {
            GuardScans(dialog, onScanIgnored);
        }

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

    /// <summary>La actividad dentro de un diálogo cuenta como actividad de la sesión, y el diálogo se puede ocultar al bloquear.</summary>
    private void Track(DialogWindow dialog)
    {
        _open.Add(dialog);
        dialog.Closed += (_, _) => _open.Remove(dialog);
        if (_idle is not null)
        {
            void OnActivity(object? sender, RoutedEventArgs e) => _idle.Touch();
            dialog.AddHandler(InputElement.KeyDownEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
            dialog.AddHandler(InputElement.PointerPressedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
            dialog.AddHandler(InputElement.PointerMovedEvent, OnActivity, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    /// <summary>
    /// Detector propio de la ventana (la vista del Punto de venta no recibe su teclado): no intercepta
    /// caracteres; el Enter que cierra una ráfaga no llega al diálogo y el campo enfocado recupera su texto.
    /// </summary>
    private static void GuardScans(DialogWindow dialog, Action onScanIgnored)
    {
        var detector = new ScanBurstDetector();
        dialog.AddHandler(
            InputElement.TextInputEvent,
            (_, e) => detector.OnText(e.Text ?? string.Empty, Stopwatch.GetTimestamp(), ScanFocus.Snapshot(ScanFocus.FocusedTextBox(dialog))),
            RoutingStrategies.Tunnel);
        dialog.AddHandler(
            InputElement.KeyDownEvent,
            (_, e) =>
            {
                if (e.Key == Key.Tab)
                {
                    detector.OnTerminator(Terminator.Tab, Stopwatch.GetTimestamp());
                }
                else if (e.Key == Key.Enter
                    && detector.OnTerminator(Terminator.Enter, Stopwatch.GetTimestamp()) is { IsBurst: true } reading)
                {
                    e.Handled = true;
                    ScanFocus.Restore(dialog, reading.FocusedSnapshot);
                    onScanIgnored();
                }
            },
            RoutingStrategies.Tunnel);
    }

    private Window? FindOwner() =>
        _lifetime.Windows.FirstOrDefault(w => w.IsActive && w is not DialogWindow)
        ?? _lifetime.MainWindow;
}
