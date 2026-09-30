namespace Pos.Desktop.Navigation;

/// <summary>
/// Pantalla que acepta un argumento al navegar hacia ella (por ejemplo, un filtro desde una tarjeta
/// de Inicio). Se invoca antes de <c>OnActivatedAsync</c>.
/// </summary>
public interface INavigationArgumentReceiver
{
    void Receive(object argument);
}
