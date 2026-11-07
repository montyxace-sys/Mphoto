using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mphoto.Dialogs;

public static class DialogChrome
{
    public static void Attach(Window window, UIElement header, Button closeButton)
    {
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        closeButton.Click += (_, _) => window.Close();
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                window.DragMove();
            }
        };
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                window.Close();
            }
        };
    }

    public static void NumericOnly(params TextBox[] boxes)
    {
        foreach (var box in boxes)
        {
            box.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
            DataObject.AddPastingHandler(box, (_, e) =>
            {
                if (e.DataObject.GetData(DataFormats.Text) is string text && !text.All(char.IsDigit))
                {
                    e.CancelCommand();
                }
            });
        }
    }

    public static BitmapSource ToBitmapSource(byte[] bgra, int width, int height) =>
        BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, width * 4);
}
