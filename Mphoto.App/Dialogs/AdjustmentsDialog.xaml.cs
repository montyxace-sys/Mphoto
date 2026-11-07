using System.Globalization;
using System.Windows;
using Mphoto.Core;

namespace Mphoto.Dialogs;

public partial class AdjustmentsDialog : Window
{
    public enum Mode
    {
        BrightnessContrast,
        HueSaturation
    }

    private readonly Mode _mode;
    private readonly byte[] _basePixels;
    private readonly int _width;
    private readonly int _height;
    private bool _ready;

    public double First => Slider1.Value;
    public double Second => Slider2.Value;

    public AdjustmentsDialog(Mode mode, byte[] previewPixels, int width, int height, double first = 0, double second = 0)
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);

        _mode = mode;
        _basePixels = previewPixels;
        _width = width;
        _height = height;

        if (mode == Mode.BrightnessContrast)
        {
            TitleText.Text = "Brillo y contraste";
            Label1.Text = "Brillo";
            Label2.Text = "Contraste";
        }
        else
        {
            TitleText.Text = "Tono y saturación";
            Label1.Text = "Tono";
            Label2.Text = "Saturación";
            Slider1.Minimum = -180;
            Slider1.Maximum = 180;
        }

        Slider1.Value = first;
        Slider2.Value = second;
        _ready = true;
        UpdateLabels();
        UpdatePreview();
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
        {
            return;
        }

        UpdateLabels();
        UpdatePreview();
    }

    private void UpdateLabels()
    {
        Value1.Text = Format(Slider1.Value);
        Value2.Text = Format(Slider2.Value);
    }

    private static string Format(double value) =>
        Math.Round(value).ToString("+0;-0;0", CultureInfo.InvariantCulture);

    private void UpdatePreview()
    {
        if (_basePixels.Length != _width * _height * 4)
        {
            return;
        }

        var layer = Layer.FromPixels("vista", _width, _height, (byte[])_basePixels.Clone());
        if (_mode == Mode.BrightnessContrast)
        {
            Adjustments.BrightnessContrast(layer, null, Slider1.Value, Slider2.Value);
        }
        else
        {
            Adjustments.HueSaturation(layer, null, Slider1.Value, Slider2.Value);
        }

        PreviewImage.Source = DialogChrome.ToBitmapSource(layer.Pixels, _width, _height);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Slider1.Value = 0;
        Slider2.Value = 0;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
