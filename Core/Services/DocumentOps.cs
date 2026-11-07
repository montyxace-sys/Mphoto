using SkiaSharp;

namespace Mphoto.Core;

public static class DocumentOps
{
    public static void Resize(Document document, int width, int height)
    {
        if (width < 1 || height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "El tamaño debe ser positivo.");
        }

        if (width == document.Width && height == document.Height)
        {
            return;
        }

        foreach (var layer in document.Layers)
        {
            ResizeLayer(layer, width, height);
        }

        document.SetSize(width, height);
        document.IsDirty = true;
    }

    private static void ResizeLayer(Layer layer, int width, int height)
    {
        using var source = ImageIo.ToPremultipliedBitmap(layer.Pixels, layer.Width, layer.Height);
        using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(target))
        {
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
            canvas.DrawBitmap(source, new SKRect(0, 0, layer.Width, layer.Height), new SKRect(0, 0, width, height), paint);
        }

        layer.Pixels = ImageIo.FromPremultipliedBitmap(target);
        layer.Width = width;
        layer.Height = height;
    }

    public static void Crop(Document document, int x, int y, int width, int height)
    {
        var x0 = Math.Clamp(x, 0, document.Width);
        var y0 = Math.Clamp(y, 0, document.Height);
        var x1 = Math.Clamp(x + width, 0, document.Width);
        var y1 = Math.Clamp(y + height, 0, document.Height);
        var w = x1 - x0;
        var h = y1 - y0;
        if (w < 1 || h < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "El recorte no puede quedar vacío.");
        }

        foreach (var layer in document.Layers)
        {
            layer.Pixels = Slice(layer.Pixels, layer.Width, x0, y0, w, h);
            layer.Width = w;
            layer.Height = h;
        }

        document.SetSize(w, h);
        document.IsDirty = true;
    }

    private static byte[] Slice(byte[] pixels, int sourceWidth, int x, int y, int width, int height)
    {
        var result = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            var source = ((y + row) * sourceWidth + x) * 4;
            Buffer.BlockCopy(pixels, source, result, row * width * 4, width * 4);
        }

        return result;
    }

    public static void Rotate(Document document, int degrees)
    {
        degrees = ((degrees % 360) + 360) % 360;
        if (degrees == 0)
        {
            return;
        }

        var swap = degrees % 180 != 0;
        var width = swap ? document.Height : document.Width;
        var height = swap ? document.Width : document.Height;

        foreach (var layer in document.Layers)
        {
            TransformLayer(layer, width, height, canvas =>
            {
                switch (degrees)
                {
                    case 90:
                        canvas.Translate(width, 0);
                        canvas.RotateDegrees(90);
                        break;
                    case 180:
                        canvas.Translate(width, height);
                        canvas.RotateDegrees(180);
                        break;
                    case 270:
                        canvas.Translate(0, height);
                        canvas.RotateDegrees(270);
                        break;
                }
            });
        }

        document.SetSize(width, height);
        document.IsDirty = true;
    }

    public static void Flip(Document document, bool horizontal)
    {
        foreach (var layer in document.Layers)
        {
            TransformLayer(layer, document.Width, document.Height, canvas =>
            {
                if (horizontal)
                {
                    canvas.Translate(document.Width, 0);
                    canvas.Scale(-1, 1);
                }
                else
                {
                    canvas.Translate(0, document.Height);
                    canvas.Scale(1, -1);
                }
            });
        }

        document.IsDirty = true;
    }

    private static void TransformLayer(Layer layer, int width, int height, Action<SKCanvas> transform)
    {
        using var source = ImageIo.ToPremultipliedBitmap(layer.Pixels, layer.Width, layer.Height);
        using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            transform(canvas);
            using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
            canvas.DrawBitmap(source, 0, 0, paint);
        }

        layer.Pixels = ImageIo.FromPremultipliedBitmap(target);
        layer.Width = width;
        layer.Height = height;
    }

    public static Layer AddLayer(Document document, string? name = null)
    {
        var layer = new Layer(name ?? $"Capa {document.Layers.Count}", document.Width, document.Height);
        var index = Math.Clamp(document.ActiveLayerIndex + 1, 0, document.Layers.Count);
        document.Layers.Insert(index, layer);
        document.ActiveLayerIndex = index;
        document.IsDirty = true;
        return layer;
    }

    public static bool DeleteLayer(Document document, int index)
    {
        if (document.Layers.Count <= 1 || index < 0 || index >= document.Layers.Count)
        {
            return false;
        }

        document.Layers.RemoveAt(index);
        document.ActiveLayerIndex = Math.Clamp(document.ActiveLayerIndex, 0, document.Layers.Count - 1);
        document.IsDirty = true;
        return true;
    }

    public static Layer DuplicateLayer(Document document, int index)
    {
        var copy = document.Layers[index].Clone();
        copy.Name = document.Layers[index].Name + " copia";
        document.Layers.Insert(index + 1, copy);
        document.ActiveLayerIndex = index + 1;
        document.IsDirty = true;
        return copy;
    }

    public static bool MergeDown(Document document, int index)
    {
        if (index <= 0 || index >= document.Layers.Count)
        {
            return false;
        }

        var upper = document.Layers[index];
        var lower = document.Layers[index - 1];
        var merged = (byte[])lower.Pixels.Clone();

        Compositor.CompositeLayerInto(document, upper, merged);

        lower.Pixels = merged;
        lower.Opacity = 1;
        lower.Blend = BlendMode.Normal;
        document.Layers.RemoveAt(index);
        document.ActiveLayerIndex = Math.Clamp(index - 1, 0, document.Layers.Count - 1);
        document.IsDirty = true;
        return true;
    }

    public static bool MoveLayer(Document document, int from, int to)
    {
        if (from < 0 || from >= document.Layers.Count || to < 0 || to >= document.Layers.Count || from == to)
        {
            return false;
        }

        var layer = document.Layers[from];
        document.Layers.RemoveAt(from);
        document.Layers.Insert(to, layer);
        document.ActiveLayerIndex = to;
        document.IsDirty = true;
        return true;
    }

    public static void ClearLayer(Layer layer) => Array.Clear(layer.Pixels, 0, layer.Pixels.Length);
}
