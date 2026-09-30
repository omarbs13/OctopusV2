namespace Pos.Application.Startup;

/// <summary>Paso del arranque en curso; lo muestra la pantalla de carga.</summary>
public enum StartupStep
{
    CheckingDatabase,
    BackingUp,
    Migrating,
    Restoring,
    Finishing,
}
