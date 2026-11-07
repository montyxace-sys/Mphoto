using System.Windows;

namespace Mphoto.Dialogs;

public partial class NewDocumentDialog : Window
{
    private readonly (int Width, int Height)[] _presets =
    {
        (1920, 1080), (2560, 1440), (3508, 2480), (2480, 3508), (1080, 1080), (1280, 720), (640, 360)
    };

    public int ResultWidth { get; private set; }
    public int ResultHeight { get; private set; }
    public bool Transparent { get; private set; }

    public NewDocumentDialog()
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);
        DialogChrome.NumericOnly(WidthBox, HeightBox);
        Loaded += (_, _) => WidthBox.Focus();
    }

    private void Preset_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PresetBox.SelectedIndex <= 0 || PresetBox.SelectedIndex > _presets.Length)
        {
            return;
        }

        var preset = _presets[PresetBox.SelectedIndex - 1];
        WidthBox.Text = preset.Width.ToString();
        HeightBox.Text = preset.Height.ToString();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text, out var width) || width < 1 || width > 20000 ||
            !int.TryParse(HeightBox.Text, out var height) || height < 1 || height > 20000)
        {
            ErrorText.Text = "Introduce una anchura y una altura entre 1 y 20000 píxeles.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        ResultWidth = width;
        ResultHeight = height;
        Transparent = BackgroundBox.SelectedIndex == 1;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
