using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mphoto.Dialogs;

public partial class ColorPickerDialog : Window
{
    private static readonly string[] Palette = ColorUtil.Palette;

    private bool _updating;

    public Color SelectedColor { get; private set; } = Colors.Black;

    public ColorPickerDialog(Color initial)
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);
        BuildSwatches();
        ApplyColor(initial);
    }

    public static Color? Pick(Window owner, Color initial)
    {
        var dialog = new ColorPickerDialog(initial) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
    }

    private void BuildSwatches()
    {
        foreach (var hex in Palette)
        {
            if (!ColorUtil.TryParseHex(hex, out var color))
            {
                continue;
            }

            var button = new Button
            {
                Style = (Style)FindResource("SwatchButton"),
                Background = new SolidColorBrush(color),
                ToolTip = hex
            };
            button.Click += (_, _) => ApplyColor(color);
            SwatchGrid.Children.Add(button);
        }
    }

    private void ApplyColor(Color color)
    {
        _updating = true;
        SelectedColor = color;
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        RedValue.Text = color.R.ToString();
        GreenValue.Text = color.G.ToString();
        BlueValue.Text = color.B.ToString();
        HexBox.Text = ColorUtil.ToHex(color);
        PreviewSwatch.Background = new SolidColorBrush(color);
        _updating = false;
    }

    private void Rgb_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating)
        {
            return;
        }

        var color = Color.FromRgb((byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
        _updating = true;
        SelectedColor = color;
        RedValue.Text = ((int)Math.Round(RedSlider.Value)).ToString();
        GreenValue.Text = ((int)Math.Round(GreenSlider.Value)).ToString();
        BlueValue.Text = ((int)Math.Round(BlueSlider.Value)).ToString();
        HexBox.Text = ColorUtil.ToHex(color);
        PreviewSwatch.Background = new SolidColorBrush(color);
        _updating = false;
    }

    private void Hex_Changed(object sender, TextChangedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        if (ColorUtil.TryParseHex(HexBox.Text, out var color))
        {
            ApplyColor(color);
            HexBox.CaretIndex = HexBox.Text.Length;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
