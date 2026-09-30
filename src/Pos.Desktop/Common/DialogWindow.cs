using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Pos.Desktop.Common;

/// <summary>Ventana modal simple de mensaje o confirmación.</summary>
internal sealed class DialogWindow : Window
{
    /// <summary>Verdadero si el usuario aceptó.</summary>
    public bool Result { get; private set; }

    public DialogWindow(string title, string message, string acceptText, string? cancelText)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var accept = new Button { Content = acceptText, MinWidth = 100, HorizontalContentAlignment = HorizontalAlignment.Center };
        accept.Click += (_, _) => Finish(true);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(accept);

        Button? cancel = null;
        if (cancelText is not null)
        {
            cancel = new Button { Content = cancelText, MinWidth = 100, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancel.Click += (_, _) => Finish(false);
            buttons.Children.Add(cancel);
        }

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttons,
            },
        };

        // La opción predeterminada es la segura: cancelar si existe, aceptar si es solo un mensaje.
        var defaultButton = cancel ?? accept;
        Opened += (_, _) => defaultButton.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Finish(cancel is null);
            }
        };
    }

    private void Finish(bool result)
    {
        Result = result;
        Close(result);
    }
}
