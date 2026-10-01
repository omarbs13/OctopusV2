namespace Pos.Application.Discounts;

/// <summary>Almacén local de <see cref="DiscountSettings"/> (preferencias de la máquina, research §10).</summary>
public interface IDiscountSettingsStore
{
    DiscountSettings Load();

    void Save(DiscountSettings settings);
}
