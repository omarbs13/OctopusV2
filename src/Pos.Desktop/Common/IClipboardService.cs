using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Pos.Desktop.Common;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

internal sealed class ClipboardService : IClipboardService
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;

    public ClipboardService(IClassicDesktopStyleApplicationLifetime lifetime) => _lifetime = lifetime;

    public Task SetTextAsync(string text) =>
        _lifetime.MainWindow?.Clipboard is { } clipboard ? clipboard.SetTextAsync(text) : Task.CompletedTask;
}
