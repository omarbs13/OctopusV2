using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Pos.Desktop.Resources;

namespace Pos.Desktop.Forms;

/// <summary>Etiqueta de campo; con <see cref="IsRequired"/> muestra un asterisco y lo anuncia como obligatorio.</summary>
public partial class FieldLabel : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FieldLabel, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<FieldLabel, bool>(nameof(IsRequired));

    public FieldLabel()
    {
        InitializeComponent();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        ArgumentNullException.ThrowIfNull(change);
        if (change.Property == TextProperty || change.Property == IsRequiredProperty)
        {
            LabelText.Text = Text;
            RequiredMark.IsVisible = IsRequired;
            AutomationProperties.SetName(this, IsRequired ? $"{Text} ({Strings.Form_Required})" : Text);
        }
    }
}
