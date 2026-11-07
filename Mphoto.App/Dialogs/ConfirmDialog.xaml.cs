using System.Windows;

namespace Mphoto.Dialogs;

public partial class ConfirmDialog : Window
{
    public enum Choice
    {
        Primary,
        Secondary,
        Cancel
    }

    public Choice Result { get; private set; } = Choice.Cancel;

    public ConfirmDialog(string title, string message, string primaryLabel = "Aceptar", string? secondaryLabel = null, string cancelLabel = "Cancelar")
    {
        InitializeComponent();
        DialogChrome.Attach(this, HeaderArea, CloseButton);

        TitleText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryLabel;
        CancelButton.Content = cancelLabel;

        if (secondaryLabel == null)
        {
            SecondaryButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SecondaryButton.Content = secondaryLabel;
        }

        Loaded += (_, _) => PrimaryButton.Focus();
    }

    public static Choice Ask(Window owner, string title, string message, string primaryLabel = "Aceptar", string? secondaryLabel = null, string cancelLabel = "Cancelar")
    {
        var dialog = new ConfirmDialog(title, message, primaryLabel, secondaryLabel, cancelLabel) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        Result = Choice.Primary;
        DialogResult = true;
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
    {
        Result = Choice.Secondary;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
