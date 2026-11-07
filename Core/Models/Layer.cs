namespace Mphoto.Core;

public sealed class Layer
{
    public string Name { get; set; }
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public byte[] Pixels { get; set; }

    public double Opacity { get; set; } = 1.0;
    public bool Visible { get; set; } = true;
    public BlendMode Blend { get; set; } = BlendMode.Normal;

    public Layer(string name, int width, int height)
    {
        Name = name;
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public Layer Clone()
    {
        var copy = new Layer(Name, Width, Height)
        {
            Opacity = Opacity,
            Visible = Visible,
            Blend = Blend
        };
        Buffer.BlockCopy(Pixels, 0, copy.Pixels, 0, Pixels.Length);
        return copy;
    }

    public static Layer FromPixels(string name, int width, int height, byte[] pixels)
    {
        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException("El búfer no coincide con el tamaño de la capa.", nameof(pixels));
        }

        var layer = new Layer(name, width, height) { Pixels = pixels };
        return layer;
    }
}
