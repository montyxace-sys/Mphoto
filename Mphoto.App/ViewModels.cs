using System.ComponentModel;
using System.Windows.Media.Imaging;
using Mphoto.Core;

namespace Mphoto;

public sealed class LayerEntry : INotifyPropertyChanged
{
    public LayerEntry(Layer layer, BitmapSource thumbnail)
    {
        Layer = layer;
        Thumbnail = thumbnail;
    }

    public Layer Layer { get; }
    public BitmapSource Thumbnail { get; }
    public string Name => Layer.Name;
    public string Info => $"{BlendModes.DisplayName(Layer.Blend)} · {(int)Math.Round(Layer.Opacity * 100)}%";

    public bool Visible
    {
        get => Layer.Visible;
        set
        {
            if (Layer.Visible == value)
            {
                return;
            }

            Layer.Visible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visible)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshInfo() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Info)));
}

public sealed class HistoryEntry
{
    public HistoryEntry(string label, int target, bool isCurrent = false)
    {
        Label = label;
        Target = target;
        IsCurrent = isCurrent;
    }

    public string Label { get; }
    public int Target { get; }
    public bool IsCurrent { get; }
}
