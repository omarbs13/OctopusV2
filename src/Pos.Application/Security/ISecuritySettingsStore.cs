namespace Pos.Application.Security;

/// <summary>Almacén local de <see cref="SecuritySettings"/> (preferencias de la máquina, research §13).</summary>
public interface ISecuritySettingsStore
{
    SecuritySettings Load();

    void Save(SecuritySettings settings);
}
