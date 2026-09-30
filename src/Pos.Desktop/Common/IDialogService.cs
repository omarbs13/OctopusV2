using Pos.Desktop.Forms;

namespace Pos.Desktop.Common;

/// <summary>Diálogos de la aplicación; permite probar los ViewModels sin UI.</summary>
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);

    /// <summary>Pide confirmación. La opción predeterminada (Enter o Esc) es cancelar.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>Cambios sin guardar: Guardar, Descartar o Seguir editando (la opción predeterminada).</summary>
    Task<UnsavedChangesChoice> AskUnsavedChangesAsync();

    /// <summary>Selector para guardar un archivo; devuelve la ruta local o nulo si se cancela.</summary>
    Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string extension);

    /// <summary>Selector para abrir un archivo; devuelve nulo si se cancela.</summary>
    Task<FileSelection?> PickOpenFileAsync(string title, IReadOnlyList<FileTypeFilter> filters);
}
