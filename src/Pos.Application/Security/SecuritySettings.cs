namespace Pos.Application.Security;

/// <summary>Configuración de seguridad por máquina; sin archivo o dañado devuelve los valores predeterminados.</summary>
public sealed record SecuritySettings
{
    public const int DefaultIdleLockMinutes = 15;
    public const int MaxIdleLockMinutes = 240;

    /// <summary>Minutos sin actividad antes de bloquear la sesión; 0 la desactiva (FR-023).</summary>
    public int IdleLockMinutes { get; init; } = DefaultIdleLockMinutes;
}
