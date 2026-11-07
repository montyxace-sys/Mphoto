using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mphoto.Dialogs;

public partial class TextDialog : Window
{
    private static readonly string[] Fonts =
    {
        "Segoe UI", "Arial", "Calibri", "Cambria", "Consolas", "Courier New",
        "Georgia", "Impact", "Tahoma", "Times New Roman", "Trebuchet MS", "Verdana"
    };

    public string TextValue => InputBox.Text;
    public string FontFamilyName => (FontBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Segoe UI";
    public double FontSizeValue { get; private set; } = 48;
    public bool Bold => BoldToggle.IsChecked == true;
    public bool Italic => ItalicToggle.IsChecked == true;
    public Color TextColor { get; private set; } = Colors.Black;

    public TextDialog(Color initialColor)
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);
        DialogChrome.NumericOnly(SizeBox);

        foreach (var font in Fonts)
        {
            FontBox.Items.Add(new ComboBoxItem { Content = font });
        }

        FontBox.SelectedIndex = 0;
        TextColor = initialColor;
        UpdateColorPreview();

        Loaded += (_, _) => InputBox.Focus();
    }

    private void UpdateColorPreview()
    {
        ColorButton.Background = new SolidColorBrush(TextColor);
        ColorText.Text = ColorUtil.ToHex(TextColor);
    }

    private void Color_Click(object sender, RoutedEventArgs e)
    {
        var picked = ColorPickerDialog.Pick(this, TextColor);
        if (picked is { } color)
        {
            TextColor = color;
            UpdateColorPreview();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            ErrorText.Text = "Escribe el texto que quieres insertar.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (!double.TryParse(SizeBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) ||
            size < 4 || size > 500)
        {
            ErrorText.Text = "El tamaño debe estar entre 4 y 500 puntos.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        FontSizeValue = size;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
