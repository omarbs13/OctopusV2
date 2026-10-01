namespace Pos.Application.Receivables;

/// <summary>Almacén local de <see cref="ReceivablesSettings"/> (preferencias de la máquina, research §9).</summary>
public interface IReceivablesSettingsStore
{
    ReceivablesSettings Load();

    void Save(ReceivablesSettings settings);
}
