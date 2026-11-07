namespace Mphoto.Core;

public sealed class Document
{
    public string Name { get; set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public List<Layer> Layers { get; } = new();
    public int ActiveLayerIndex { get; set; }
    public Selection Selection { get; }
    public string? FilePath { get; set; }
    public bool IsDirty { get; set; }

    public Layer? ActiveLayer
    {
        get
        {
            if (Layers.Count == 0)
            {
                return null;
            }

            return Layers[Math.Clamp(ActiveLayerIndex, 0, Layers.Count - 1)];
        }
    }

    public Document(int width, int height, string name = "Sin título")
    {
        if (width < 1 || height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "El lienzo debe tener al menos 1 píxel.");
        }

        Width = width;
        Height = height;
        Name = name;
        Selection = new Selection(width, height);

        var background = new Layer("Fondo", width, height);
        for (var i = 0; i < background.Pixels.Length; i += 4)
        {
            background.Pixels[i] = 255;
            background.Pixels[i + 1] = 255;
            background.Pixels[i + 2] = 255;
            background.Pixels[i + 3] = 255;
        }

        Layers.Add(background);
    }

    public static Document FromImage(string name, int width, int height, byte[] pixels)
    {
        var document = new Document(width, height, name);
        document.Layers[0] = Layer.FromPixels("Fondo", width, height, pixels);
        return document;
    }

    internal void SetSize(int width, int height)
    {
        Width = width;
        Height = height;
        Selection.Reset(width, height);
    }

    public int IndexOf(Layer layer) => Layers.IndexOf(layer);
}
