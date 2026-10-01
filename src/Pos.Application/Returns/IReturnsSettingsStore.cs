namespace Pos.Application.Returns;

/// <summary>Almacén local de <see cref="ReturnsSettings"/> (preferencias de la máquina, research §9).</summary>
public interface IReturnsSettingsStore
{
    ReturnsSettings Load();

    void Save(ReturnsSettings settings);
}
