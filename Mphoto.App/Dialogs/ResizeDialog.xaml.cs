using System.Windows;

namespace Mphoto.Dialogs;

public partial class ResizeDialog : Window
{
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;

    public int ResultWidth { get; private set; }
    public int ResultHeight { get; private set; }

    public ResizeDialog(int width, int height)
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);
        DialogChrome.NumericOnly(WidthBox, HeightBox);

        _sourceWidth = width;
        _sourceHeight = height;
        ResultWidth = width;
        ResultHeight = height;
        CurrentText.Text = $"Tamaño actual: {width} × {height} píxeles";
        WidthBox.Text = width.ToString();
        HeightBox.Text = height.ToString();
        Loaded += (_, _) => WidthBox.Focus();
    }

    private void Width_LostFocus(object sender, RoutedEventArgs e)
    {
        if (KeepAspect.IsChecked == true &&
            int.TryParse(WidthBox.Text, out var width) && width > 0)
        {
            var height = (int)Math.Round(width * (double)_sourceHeight / _sourceWidth);
            HeightBox.Text = Math.Clamp(height, 1, 20000).ToString();
        }
    }

    private void Height_LostFocus(object sender, RoutedEventArgs e)
    {
        if (KeepAspect.IsChecked == true &&
            int.TryParse(HeightBox.Text, out var height) && height > 0)
        {
            var width = (int)Math.Round(height * (double)_sourceWidth / _sourceHeight);
            WidthBox.Text = Math.Clamp(width, 1, 20000).ToString();
        }
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
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
