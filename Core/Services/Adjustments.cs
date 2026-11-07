namespace Mphoto.Core;

public static class Adjustments
{
    public static void BrightnessContrast(Layer layer, byte[]? mask, double brightness, double contrast)
    {
        brightness = Math.Clamp(brightness, -100, 100);
        contrast = Math.Clamp(contrast, -100, 100);

        var factor = (259.0 * (contrast + 255)) / (255.0 * (259 - contrast));
        var offset = brightness * 2.55;
        var lut = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            lut[i] = (byte)Math.Clamp(Math.Round(factor * (i - 128) + 128 + offset), 0, 255);
        }

        Apply(layer, mask, (index, pixels) =>
        {
            pixels[index] = lut[pixels[index]];
            pixels[index + 1] = lut[pixels[index + 1]];
            pixels[index + 2] = lut[pixels[index + 2]];
        });
    }

    public static void HueSaturation(Layer layer, byte[]? mask, double hueShift, double saturationDelta)
    {
        hueShift = ((hueShift % 360) + 360) % 360;
        saturationDelta = Math.Clamp(saturationDelta, -100, 100) / 100.0;

        Apply(layer, mask, (index, pixels) =>
        {
            var (h, s, l) = RgbToHsl(pixels[index + 2], pixels[index + 1], pixels[index]);
            h = (h + hueShift / 360.0) % 1.0;
            s = Math.Clamp(saturationDelta >= 0 ? s + (1 - s) * saturationDelta : s * (1 + saturationDelta), 0, 1);
            var (r, g, b) = HslToRgb(h, s, l);
            pixels[index + 2] = r;
            pixels[index + 1] = g;
            pixels[index] = b;
        });
    }

    public static void Desaturate(Layer layer, byte[]? mask)
    {
        Apply(layer, mask, (index, pixels) =>
        {
            var gray = (byte)Luminance(pixels[index + 2], pixels[index + 1], pixels[index]);
            pixels[index] = pixels[index + 1] = pixels[index + 2] = gray;
        });
    }

    public static void Sepia(Layer layer, byte[]? mask)
    {
        Apply(layer, mask, (index, pixels) =>
        {
            var b = pixels[index];
            var g = pixels[index + 1];
            var r = pixels[index + 2];
            pixels[index + 2] = ClampByte(0.393 * r + 0.769 * g + 0.189 * b);
            pixels[index + 1] = ClampByte(0.349 * r + 0.686 * g + 0.168 * b);
            pixels[index] = ClampByte(0.272 * r + 0.534 * g + 0.131 * b);
        });
    }

    public static void Invert(Layer layer, byte[]? mask)
    {
        Apply(layer, mask, (index, pixels) =>
        {
            pixels[index] = (byte)(255 - pixels[index]);
            pixels[index + 1] = (byte)(255 - pixels[index + 1]);
            pixels[index + 2] = (byte)(255 - pixels[index + 2]);
        });
    }

    private static void Apply(Layer layer, byte[]? mask, Action<int, byte[]> action)
    {
        var pixels = layer.Pixels;
        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (pixels[index + 3] == 0)
            {
                continue;
            }

            var originalB = pixels[index];
            var originalG = pixels[index + 1];
            var originalR = pixels[index + 2];

            action(index, pixels);

            var weight = mask == null ? 1.0 : mask[index / 4] / 255.0;
            if (weight < 0.999)
            {
                pixels[index] = Mix(originalB, pixels[index], weight);
                pixels[index + 1] = Mix(originalG, pixels[index + 1], weight);
                pixels[index + 2] = Mix(originalR, pixels[index + 2], weight);
            }
        }
    }

    private static byte Mix(byte original, byte edited, double weight) =>
        (byte)Math.Clamp(Math.Round(original + (edited - original) * weight), 0, 255);

    private static byte ClampByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    public static int Luminance(int r, int g, int b) => (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b);

    private static (double H, double S, double L) RgbToHsl(int r, int g, int b)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var l = (max + min) / 2;

        if (Math.Abs(max - min) < 1e-6)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == rf)
        {
            h = ((gf - bf) / d + (gf < bf ? 6 : 0)) / 6;
        }
        else if (max == gf)
        {
            h = ((bf - rf) / d + 2) / 6;
        }
        else
        {
            h = ((rf - gf) / d + 4) / 6;
        }

        return (h, s, l);
    }

    private static (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
    {
        if (s <= 0)
        {
            var gray = ClampByte(l * 255);
            return (gray, gray, gray);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return (ToByte(HueToRgb(p, q, h + 1.0 / 3)), ToByte(HueToRgb(p, q, h)), ToByte(HueToRgb(p, q, h - 1.0 / 3)));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
