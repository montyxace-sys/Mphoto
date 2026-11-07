using System.Reflection;
using System.Windows;

namespace Mphoto.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version == null
            ? "Versión 1.0.0"
            : $"Versión {version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
