using System.Windows;          // DependencyProperty, PropertyMetadata
using System.Windows.Controls; // UserControl

namespace SysMonitor.App.Controls;

/// <summary>
/// One row of the display: a label on the left and a value on the right.
/// Set <see cref="Label"/> and <see cref="Value"/> from XAML or from code.
/// </summary>
public partial class MetricRow : UserControl
{
    // ---- Why "dependency properties" instead of normal properties ----
    // A normal C# property is just a value. WPF needs more: it must notice when the value
    // changes (to redraw), allow {Binding ...} to it, and let styles set it.
    // A dependency property provides all of that. It always comes in two parts:
    //   1. a static "registration" (the XxxProperty field), telling WPF the property exists,
    //   2. a normal-looking C# property that reads and writes through GetValue/SetValue.

    /// <summary>Registers <see cref="Label"/> with WPF.</summary>
    // DependencyProperty.Register(name, type of the value, the class it belongs to, default value).
    // nameof(Label) = the text "Label", but checked by the compiler, so a rename can't break it.
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(object), typeof(MetricRow), new PropertyMetadata(null));

    /// <summary>
    /// What's shown on the left: usually text like "Temperature", but can be any element,
    /// such as the CPU fan selector button.
    /// </summary>
    public object? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Registers <see cref="Value"/> with WPF. Defaults to a dash until a reading arrives.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(MetricRow), new PropertyMetadata("—"));

    /// <summary>The text shown on the right, e.g. "52 °C".</summary>
    public string Value
    {
        // GetValue returns "object", so "(string)" converts it back to text.
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Builds the row from MetricRow.xaml.</summary>
    public MetricRow()
    {
        InitializeComponent();
    }
}
