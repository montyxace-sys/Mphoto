using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Mphoto;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Report(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Report(exception);
            }
        };
    }

    private static void Report(Exception exception)
    {
        var root = exception;
        while (root.InnerException != null)
        {
            root = root.InnerException;
        }

        var log = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Mphoto", "error.log");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.WriteAllText(log, $"{DateTime.Now:u}{Environment.NewLine}{exception}");
        }
        catch
        {
            // sin registro si el disco no está disponible
        }

        MessageBox.Show(
            "Mphoto ha encontrado un error inesperado.\n\n" + root.Message +
            "\n\nDetalle: " + log,
            "Mphoto",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
