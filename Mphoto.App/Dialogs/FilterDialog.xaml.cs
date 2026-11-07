using System.Globalization;
using System.Windows;
using Mphoto.Core;

namespace Mphoto.Dialogs;

public partial class FilterDialog : Window
{
    public enum Mode
    {
        Blur,
        Sharpen,
        Pixelate,
        Emboss
    }

    private readonly Mode _mode;
    private readonly byte[] _basePixels;
    private readonly int _width;
    private readonly int _height;
    private readonly double _previewScale;
    private bool _ready;

    public double Amount => AmountSlider.Value;

    public FilterDialog(Mode mode, byte[] previewPixels, int width, int height, double previewScale, double initialAmount)
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);

        _mode = mode;
        _basePixels = previewPixels;
        _width = width;
        _height = height;
        _previewScale = Math.Clamp(previewScale, 0.01, 1);

        Configure();
        AmountSlider.Value = Math.Clamp(initialAmount, AmountSlider.Minimum, AmountSlider.Maximum);
        _ready = true;
        UpdateLabel();
        UpdatePreview();
    }

    private void Configure()
    {
        switch (_mode)
        {
            case Mode.Blur:
                TitleText.Text = "Desenfoque";
                Label1.Text = "Radio";
                DescriptionText.Text = "Suaviza la imagen. Un radio mayor produce un desenfoque más intenso.";
                AmountSlider.Minimum = 1;
                AmountSlider.Maximum = 60;
                break;
            case Mode.Sharpen:
                TitleText.Text = "Enfoque";
                Label1.Text = "Intensidad";
                DescriptionText.Text = "Realza los bordes y los detalles de la capa activa.";
                AmountSlider.Minimum = 5;
                AmountSlider.Maximum = 300;
                break;
            case Mode.Pixelate:
                TitleText.Text = "Pixelar";
                Label1.Text = "Bloque";
                DescriptionText.Text = "Agrupa los píxeles en bloques cuadrados de mayor tamaño.";
                AmountSlider.Minimum = 2;
                AmountSlider.Maximum = 64;
                break;
            default:
                TitleText.Text = "Relieve";
                Label1.Text = "Intensidad";
                DescriptionText.Text = "Convierte los bordes de la imagen en un relieve gris.";
                AmountSlider.Minimum = 10;
                AmountSlider.Maximum = 300;
                break;
        }

        AmountSlider.Value = (_mode is Mode.Blur or Mode.Pixelate) ? 8 : 100;
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
        {
            return;
        }

        UpdateLabel();
        UpdatePreview();
    }

    private void UpdateLabel()
    {
        var value = Math.Round(AmountSlider.Value);
        Value1.Text = _mode is Mode.Blur or Mode.Pixelate
            ? $"{value:0} px"
            : $"{value.ToString("0", CultureInfo.InvariantCulture)} %";
    }

    private void UpdatePreview()
    {
        if (_basePixels.Length != _width * _height * 4)
        {
            return;
        }

        var layer = Layer.FromPixels("vista", _width, _height, (byte[])_basePixels.Clone());
        var scale = _previewScale;

        switch (_mode)
        {
            case Mode.Blur:
                Filters.Blur(layer, null, Math.Max(1, (int)Math.Round(AmountSlider.Value * scale)));
                break;
            case Mode.Sharpen:
                Filters.Sharpen(layer, null, AmountSlider.Value);
                break;
            case Mode.Pixelate:
                Filters.Pixelate(layer, null, Math.Max(2, (int)Math.Round(AmountSlider.Value * scale)));
                break;
            default:
                Filters.Emboss(layer, null, AmountSlider.Value);
                break;
        }

        PreviewImage.Source = DialogChrome.ToBitmapSource(layer.Pixels, _width, _height);
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
