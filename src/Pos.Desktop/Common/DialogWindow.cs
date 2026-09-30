using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Pos.Desktop.Common;

/// <summary>Botón de un diálogo: texto, valor que devuelve y si es de acento.</summary>
internal sealed record DialogButton(string Text, object Value, bool IsAccent = false);

/// <summary>Ventana modal simple con un mensaje y una lista de botones.</summary>
internal sealed class DialogWindow : Window
{
    /// <param name="title">Título.</param>
    /// <param name="message">Mensaje.</param>
    /// <param name="buttons">Botones, en orden de izquierda a derecha.</param>
    /// <param name="defaultResult">Valor de la opción segura: recibe el foco y se usa con Esc o al cerrar la ventana.</param>
    /// <param name="escapeResult">Valor con Esc y al cerrar la ventana, si es distinto de <paramref name="defaultResult"/>.</param>
    public DialogWindow(
        string title,
        string message,
        IReadOnlyList<DialogButton> buttons,
        object defaultResult,
        object? escapeResult = null)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        Title = title;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Result = escapeResult ?? defaultResult;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        Button? defaultButton = null;
        foreach (var definition in buttons)
        {
            var button = new Button
            {
                Content = definition.Text,
                MinWidth = 100,
                MinHeight = 36,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            if (definition.IsAccent)
            {
                button.Classes.Add("accent");
            }

            button.Click += (_, _) => Finish(definition.Value);
            panel.Children.Add(button);
            if (Equals(definition.Value, defaultResult))
            {
                defaultButton = button;
            }
        }

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                panel,
            },
        };

        Opened += (_, _) => defaultButton?.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Finish(escapeResult ?? defaultResult);
            }
        };
    }

    /// <summary>Valor del botón elegido; la opción predeterminada si se cerró sin elegir.</summary>
    public object Result { get; private set; }

    private void Finish(object result)
    {
        Result = result;
        Close(result);
    }
}
