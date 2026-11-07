namespace Mphoto.Core;

public static class Filters
{
    public static void Blur(Layer layer, byte[]? mask, int radius)
    {
        if (radius <= 0)
        {
            return;
        }

        var width = layer.Width;
        var height = layer.Height;
        var current = ImageIo.Premultiply(layer.Pixels);
        var temp = new byte[current.Length];

        for (var pass = 0; pass < 3; pass++)
        {
            BoxHorizontal(current, temp, width, height, radius);
            BoxVertical(temp, current, width, height, radius);
        }

        MergePremultiplied(layer, current, mask);
    }

    public static void Sharpen(Layer layer, byte[]? mask, double amount)
    {
        var strength = Math.Clamp(amount, 0, 300) / 100.0;
        if (strength <= 0)
        {
            return;
        }

        var width = layer.Width;
        var height = layer.Height;
        var premultiplied = ImageIo.Premultiply(layer.Pixels);
        var temp = new byte[premultiplied.Length];
        var blurred = new byte[premultiplied.Length];
        Buffer.BlockCopy(premultiplied, 0, blurred, 0, premultiplied.Length);

        BoxHorizontal(blurred, temp, width, height, 1);
        BoxVertical(temp, blurred, width, height, 1);

        var result = new byte[premultiplied.Length];
        for (var i = 0; i < premultiplied.Length; i += 4)
        {
            for (var c = 0; c < 3; c++)
            {
                var value = premultiplied[i + c] + strength * (premultiplied[i + c] - blurred[i + c]);
                result[i + c] = (byte)Math.Clamp(Math.Round(value), 0, 255);
            }

            result[i + 3] = premultiplied[i + 3];
        }
        MergePremultiplied(layer, result, mask);
    }

    public static void Pixelate(Layer layer, byte[]? mask, int blockSize)
    {
        var block = Math.Max(2, blockSize);
        var width = layer.Width;
        var height = layer.Height;
        var premultiplied = ImageIo.Premultiply(layer.Pixels);
        var result = new byte[premultiplied.Length];

        for (var by = 0; by < height; by += block)
        {
            for (var bx = 0; bx < width; bx += block)
            {
                long b = 0, g = 0, r = 0, a = 0;
                var count = 0;
                for (var y = by; y < Math.Min(by + block, height); y++)
                {
                    for (var x = bx; x < Math.Min(bx + block, width); x++)
                    {
                        var i = (y * width + x) * 4;
                        b += premultiplied[i];
                        g += premultiplied[i + 1];
                        r += premultiplied[i + 2];
                        a += premultiplied[i + 3];
                        count++;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                var ab = (byte)(a / count);
                var bb = (byte)(b / count);
                var gb = (byte)(g / count);
                var rb = (byte)(r / count);

                for (var y = by; y < Math.Min(by + block, height); y++)
                {
                    for (var x = bx; x < Math.Min(bx + block, width); x++)
                    {
                        var i = (y * width + x) * 4;
                        result[i] = bb;
                        result[i + 1] = gb;
                        result[i + 2] = rb;
                        result[i + 3] = ab;
                    }
                }
            }
        }

        MergePremultiplied(layer, result, mask);
    }

    public static void Emboss(Layer layer, byte[]? mask, double strength)
    {
        var amount = Math.Clamp(strength, 0, 300) / 100.0;
        var width = layer.Width;
        var height = layer.Height;
        var source = ImageIo.Premultiply(layer.Pixels);
        var result = new byte[source.Length];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                var left = ((y * width) + Math.Max(0, x - 1)) * 4;
                var upLeft = ((Math.Max(0, y - 1) * width) + Math.Max(0, x - 1)) * 4;
                var downRight = ((Math.Min(height - 1, y + 1) * width) + Math.Min(width - 1, x + 1)) * 4;

                var leftLum = Adjustments.Luminance(source[left + 2], source[left + 1], source[left]);
                var upLum = Adjustments.Luminance(source[upLeft + 2], source[upLeft + 1], source[upLeft]);
                var downLum = Adjustments.Luminance(source[downRight + 2], source[downRight + 1], source[downRight]);

                var value = (byte)Math.Clamp(128 + (leftLum + upLum - 2 * downLum) * amount, 0, 255);
                result[i] = (byte)(value * source[i + 3] / 255);
                result[i + 1] = (byte)(value * source[i + 3] / 255);
                result[i + 2] = (byte)(value * source[i + 3] / 255);
                result[i + 3] = source[i + 3];
            }
        }

        MergePremultiplied(layer, result, mask);
    }

    private static void MergePremultiplied(Layer layer, byte[] premultiplied, byte[]? mask)
    {
        var pixels = layer.Pixels;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = premultiplied[i + 3];
            double weight = mask == null ? 1.0 : mask[i / 4] / 255.0;
            if (weight <= 0)
            {
                continue;
            }

            byte b = 0, g = 0, r = 0;
            if (alpha > 0)
            {
                b = (byte)Math.Min(255, (premultiplied[i] * 255 + alpha / 2) / alpha);
                g = (byte)Math.Min(255, (premultiplied[i + 1] * 255 + alpha / 2) / alpha);
                r = (byte)Math.Min(255, (premultiplied[i + 2] * 255 + alpha / 2) / alpha);
            }

            if (weight < 0.999)
            {
                pixels[i] = (byte)Math.Round(pixels[i] + (b - pixels[i]) * weight);
                pixels[i + 1] = (byte)Math.Round(pixels[i + 1] + (g - pixels[i + 1]) * weight);
                pixels[i + 2] = (byte)Math.Round(pixels[i + 2] + (r - pixels[i + 2]) * weight);
                pixels[i + 3] = (byte)Math.Round(pixels[i + 3] + (alpha - pixels[i + 3]) * weight);
            }
            else
            {
                pixels[i] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = alpha;
            }
        }
    }

    private static void BoxHorizontal(byte[] source, byte[] target, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

            for (var k = -radius; k <= radius; k++)
            {
                var i = (row + Math.Clamp(k, 0, width - 1)) * 4;
                sumB += source[i];
                sumG += source[i + 1];
                sumR += source[i + 2];
                sumA += source[i + 3];
            }

            for (var x = 0; x < width; x++)
            {
                var i = (row + x) * 4;
                target[i] = (byte)(sumB / window);
                target[i + 1] = (byte)(sumG / window);
                target[i + 2] = (byte)(sumR / window);
                target[i + 3] = (byte)(sumA / window);

                var remove = (row + Math.Clamp(x - radius, 0, width - 1)) * 4;
                var add = (row + Math.Clamp(x + radius + 1, 0, width - 1)) * 4;
                sumB += source[add] - source[remove];
                sumG += source[add + 1] - source[remove + 1];
                sumR += source[add + 2] - source[remove + 2];
                sumA += source[add + 3] - source[remove + 3];
            }
        }
    }

    private static void BoxVertical(byte[] source, byte[] target, int width, int height, int radius)
    {
        var window = radius * 2 + 1;
        var stride = width * 4;
        for (var x = 0; x < width; x++)
        {
            int sumB = 0, sumG = 0, sumR = 0, sumA = 0;

            for (var k = -radius; k <= radius; k++)
            {
                var i = Math.Clamp(k, 0, height - 1) * stride + x * 4;
                sumB += source[i];
                sumG += source[i + 1];
                sumR += source[i + 2];
                sumA += source[i + 3];
            }

            for (var y = 0; y < height; y++)
            {
                var i = y * stride + x * 4;
                target[i] = (byte)(sumB / window);
                target[i + 1] = (byte)(sumG / window);
                target[i + 2] = (byte)(sumR / window);
                target[i + 3] = (byte)(sumA / window);

                var remove = Math.Clamp(y - radius, 0, height - 1) * stride + x * 4;
                var add = Math.Clamp(y + radius + 1, 0, height - 1) * stride + x * 4;
                sumB += source[add] - source[remove];
                sumG += source[add + 1] - source[remove + 1];
                sumR += source[add + 2] - source[remove + 2];
                sumA += source[add + 3] - source[remove + 3];
            }
        }
    }
}
