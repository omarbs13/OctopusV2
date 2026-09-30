namespace Pos.Application.Abstractions;

/// <summary>
/// Preferencias locales de la máquina (por ejemplo, el estado del menú), fuera de la base de datos
/// del negocio. Un valor inexistente o dañado se lee como nulo.
/// </summary>
public interface IPreferencesStore
{
    T? Load<T>(string key)
        where T : class;

    /// <summary>Guarda el valor; un error se registra y no interrumpe al operador.</summary>
    void Save<T>(string key, T value)
        where T : class;
}
