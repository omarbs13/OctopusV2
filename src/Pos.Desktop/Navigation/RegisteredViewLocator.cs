using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Navigation;

/// <summary>
/// Resuelve la vista de cada ViewModel a partir de los registros de los módulos, sin reflexión
/// por nombre y sin editar App.axaml al agregar un módulo.
/// </summary>
public sealed class RegisteredViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Func<Control>> _views;

    public RegisteredViewLocator(IEnumerable<ViewRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _views = [];
        foreach (var registration in registrations)
        {
            _views.TryAdd(registration.ViewModelType, registration.CreateView);
        }
    }

    public Control? Build(object? param) =>
        param is not null && _views.TryGetValue(param.GetType(), out var create)
            ? create()
            : new TextBlock { Text = Strings.Common_ViewNotRegistered };

    public bool Match(object? data) => data is not null && _views.ContainsKey(data.GetType());
}
