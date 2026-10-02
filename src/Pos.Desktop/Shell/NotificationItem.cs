namespace Pos.Desktop.Shell;

/// <summary>Gravedad de una notificación: define su color (022, contracts/ui.md).</summary>
public enum NotificationSeverity
{
    Warning,
    Danger,
}

/// <summary>
/// Notificación no bloqueante del shell. <c>Key</c> identifica su tipo: una nueva con la misma clave
/// reemplaza a la visible. Pulsarla navega a <c>NavigateTo</c> con <c>Argument</c>.
/// </summary>
public sealed record NotificationItem(
    NotificationSeverity Severity,
    string Title,
    string Message,
    string? NavigateTo,
    object? Argument,
    string Key);
