using Pos.Desktop.Forms;

namespace Pos.Desktop.Common;

/// <summary>Diálogos de la aplicación; permite probar los ViewModels sin UI.</summary>
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);

    /// <summary>Pide confirmación. La opción predeterminada (Enter o Esc) es cancelar.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>
    /// Pregunta con dos opciones; la primaria es la predeterminada (Enter) y devuelve verdadero. Esc
    /// elige la secundaria. Sirve para decisiones sin riesgo, como recuperar la venta en curso.
    /// </summary>
    Task<bool> AskAsync(string title, string message, string primaryText, string secondaryText);

    /// <summary>Cambios sin guardar: Guardar, Descartar o Seguir editando (la opción predeterminada).</summary>
    Task<UnsavedChangesChoice> AskUnsavedChangesAsync();

    /// <summary>Selector para guardar un archivo; devuelve la ruta local o nulo si se cancela.</summary>
    Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string extension);

    /// <summary>Selector para abrir un archivo; devuelve nulo si se cancela.</summary>
    Task<FileSelection?> PickOpenFileAsync(string title, IReadOnlyList<FileTypeFilter> filters);
}

/// <summary>
/// Oculta y restaura los diálogos abiertos sin cerrarlos: al bloquear la sesión por inactividad nadie
/// debe poder terminar un cobro a nombre del usuario y no se pierde lo capturado (007, research §13).
/// </summary>
public interface IDialogVisibility
{
    void HideAll();

    void ShowAll();
}
