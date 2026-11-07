namespace Mphoto.Core;

public static class Compositor
{
    public static byte[] Composite(Document document)
    {
        var pixels = new byte[document.Width * document.Height * 4];
        CompositeRegion(document, 0, 0, document.Width, document.Height, pixels);
        return pixels;
    }

    public static void CompositeRegion(Document document, int rx, int ry, int rw, int rh, byte[] dest)
    {
        var width = document.Width;
        var x0 = Math.Clamp(rx, 0, width);
        var y0 = Math.Clamp(ry, 0, document.Height);
        var x1 = Math.Clamp(rx + rw, 0, width);
        var y1 = Math.Clamp(ry + rh, 0, document.Height);

        for (var y = y0; y < y1; y++)
        {
            Array.Clear(dest, (y * width + x0) * 4, Math.Max(0, x1 - x0) * 4);
        }

        foreach (var layer in document.Layers)
        {
            if (!layer.Visible || layer.Opacity <= 0)
            {
                continue;
            }

            CompositeLayer(document, layer, x0, y0, x1, y1, dest);
        }
    }

    public static void CompositeLayerInto(Document document, Layer layer, byte[] dest)
    {
        CompositeLayer(document, layer, 0, 0, document.Width, document.Height, dest);
    }

    private static void CompositeLayer(Document document, Layer layer, int x0, int y0, int x1, int y1, byte[] dest)
    {
        var width = document.Width;
        var opacity = Math.Clamp(layer.Opacity, 0, 1);
        var blend = layer.Blend;

        for (var y = y0; y < y1; y++)
        {
            var row = y * width;
            for (var x = x0; x < x1; x++)
            {
                var index = (row + x) * 4;
                var sourceAlpha = layer.Pixels[index + 3];
                if (sourceAlpha == 0)
                {
                    continue;
                }

                var sa = sourceAlpha / 255.0 * opacity;
                if (sa <= 0)
                {
                    continue;
                }

                BlendOver(dest, index, layer.Pixels[index + 2], layer.Pixels[index + 1], layer.Pixels[index], sa, blend);
            }
        }
    }

    public static void BlendOver(byte[] dest, int index, int sourceR, int sourceG, int sourceB, double sourceAlpha, BlendMode mode)
    {
        var backdropAlpha = dest[index + 3] / 255.0;
        var outAlpha = sourceAlpha + backdropAlpha * (1 - sourceAlpha);
        if (outAlpha <= 0)
        {
            return;
        }

        dest[index + 2] = BlendChannel(dest[index + 2], sourceR, sourceAlpha, backdropAlpha, outAlpha, mode);
        dest[index + 1] = BlendChannel(dest[index + 1], sourceG, sourceAlpha, backdropAlpha, outAlpha, mode);
        dest[index] = BlendChannel(dest[index], sourceB, sourceAlpha, backdropAlpha, outAlpha, mode);
        dest[index + 3] = (byte)Math.Clamp(Math.Round(outAlpha * 255), 0, 255);
    }

    private static byte BlendChannel(int backdrop, int source, double sa, double ba, double outAlpha, BlendMode mode)
    {
        var blended = BlendModes.Blend(mode, backdrop, source);
        var value = (source * sa * (1 - ba) + backdrop * ba * (1 - sa) + blended * sa * ba) / outAlpha;
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }
}
