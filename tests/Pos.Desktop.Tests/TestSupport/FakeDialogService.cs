using Pos.Desktop.Common;
using Pos.Desktop.Forms;

namespace Pos.Desktop.Tests.TestSupport;

public sealed class FakeDialogService : IDialogService
{
    public List<(string Title, string Message)> Messages { get; } = [];

    public List<string> Confirmations { get; } = [];

    /// <summary>Respuesta que dará ConfirmAsync.</summary>
    public bool ConfirmResult { get; set; }

    /// <summary>Ruta que devolverá PickSaveFileAsync.</summary>
    public string? SaveFilePath { get; set; }

    public string? LastSuggestedFileName { get; private set; }

    /// <summary>Respuesta que dará AskUnsavedChangesAsync.</summary>
    public UnsavedChangesChoice UnsavedChoice { get; set; } = UnsavedChangesChoice.KeepEditing;

    public int UnsavedQuestions { get; private set; }

    public Task<UnsavedChangesChoice> AskUnsavedChangesAsync()
    {
        UnsavedQuestions++;
        return Task.FromResult(UnsavedChoice);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add(message);
        return Task.FromResult(ConfirmResult);
    }

    /// <summary>Archivo que devolverá PickOpenFileAsync; nulo simula que el operador canceló.</summary>
    public FileSelection? OpenFile { get; set; }

    public Task<FileSelection?> PickOpenFileAsync(string title, IReadOnlyList<FileTypeFilter> filters) =>
        Task.FromResult(OpenFile);

    public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string extension)
    {
        LastSuggestedFileName = suggestedFileName;
        return Task.FromResult(SaveFilePath);
    }
}
