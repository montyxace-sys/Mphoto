using SkiaSharp;

namespace Mphoto.Core;

public static class DrawOps
{
    public static void Stroke(Layer layer, byte[]? mask, IReadOnlyList<Pt> points, double radius,
        double hardness, double opacity, uint color, bool erase = false)
    {
        if (points.Count == 0 || radius <= 0)
        {
            return;
        }

        using var bitmap = NewCanvasBitmap(layer);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)(radius * 2),
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                Color = new SKColor(color)
            };

            if (hardness < 0.98)
            {
                var sigma = Math.Max(0.4, (1 - hardness) * radius * 0.7);
                paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)sigma);
            }

            using var path = new SKPath();
            path.MoveTo((float)points[0].X, (float)points[0].Y);
            if (points.Count == 1)
            {
                path.LineTo((float)(points[0].X + 0.01), (float)points[0].Y);
            }

            for (var i = 1; i < points.Count; i++)
            {
                path.LineTo((float)points[i].X, (float)points[i].Y);
            }

            canvas.DrawPath(path, paint);
        }

        Merge(bitmap, layer, mask, opacity, erase);
    }

    public static void Shape(Layer layer, byte[]? mask, ShapeKind kind, double x, double y, double width,
        double height, uint color, double strokeWidth, double opacity, bool filled)
    {
        using var bitmap = NewCanvasBitmap(layer);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Style = filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
                StrokeWidth = (float)Math.Max(1, strokeWidth),
                Color = new SKColor(color)
            };

            var rect = SKRect.Create((float)x, (float)y, (float)Math.Max(1, width), (float)Math.Max(1, height));
            switch (kind)
            {
                case ShapeKind.Rectangle:
                    canvas.DrawRect(rect, paint);
                    break;
                case ShapeKind.Ellipse:
                    canvas.DrawOval(rect, paint);
                    break;
                case ShapeKind.Line:
                    canvas.DrawLine((float)x, (float)y, (float)(x + width), (float)(y + height), paint);
                    break;
            }
        }

        Merge(bitmap, layer, mask, opacity, false);
    }

    public static void Text(Layer layer, byte[]? mask, string text, double x, double y, string family,
        double size, uint color, bool bold, bool italic, double opacity)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        using var bitmap = NewCanvasBitmap(layer);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var typeface = SKTypeface.FromFamilyName(
                family,
                bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(color),
                Typeface = typeface,
                TextSize = (float)Math.Max(1, size)
            };

            canvas.DrawText(text, (float)x, (float)y, paint);
        }

        Merge(bitmap, layer, mask, opacity, false);
    }

    public static bool FloodFill(Layer layer, byte[]? mask, int x, int y, uint color, int tolerance,
        out (int X, int Y, int Width, int Height) bounds)
    {
        bounds = (0, 0, 0, 0);
        if (x < 0 || y < 0 || x >= layer.Width || y >= layer.Height)
        {
            return false;
        }

        var width = layer.Width;
        var height = layer.Height;
        var pixels = layer.Pixels;
        var start = (y * width + x) * 4;
        var targetB = pixels[start];
        var targetG = pixels[start + 1];
        var targetR = pixels[start + 2];
        var targetA = pixels[start + 3];

        var fillB = (byte)(color & 0xFF);
        var fillG = (byte)((color >> 8) & 0xFF);
        var fillR = (byte)((color >> 16) & 0xFF);
        var fillA = (byte)((color >> 24) & 0xFF);

        if (targetB == fillB && targetG == fillG && targetR == fillR && targetA == fillA)
        {
            return false;
        }

        var visited = new bool[width * height];
        var stack = new Stack<int>();
        stack.Push(y * width + x);

        var minX = x;
        var maxX = x;
        var minY = y;
        var maxY = y;
        var changed = false;

        while (stack.Count > 0)
        {
            var index = stack.Pop();
            if (visited[index])
            {
                continue;
            }

            visited[index] = true;
            if (mask != null && mask[index] == 0)
            {
                continue;
            }

            var i = index * 4;
            if (Math.Abs(pixels[i] - targetB) > tolerance ||
                Math.Abs(pixels[i + 1] - targetG) > tolerance ||
                Math.Abs(pixels[i + 2] - targetR) > tolerance ||
                Math.Abs(pixels[i + 3] - targetA) > tolerance)
            {
                continue;
            }

            pixels[i] = fillB;
            pixels[i + 1] = fillG;
            pixels[i + 2] = fillR;
            pixels[i + 3] = fillA;
            changed = true;

            var px = index % width;
            var py = index / width;
            if (px < minX) minX = px;
            if (px > maxX) maxX = px;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;

            if (px > 0) stack.Push(index - 1);
            if (px < width - 1) stack.Push(index + 1);
            if (py > 0) stack.Push(index - width);
            if (py < height - 1) stack.Push(index + width);
        }

        if (changed)
        {
            bounds = (minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        return changed;
    }

    public static void Clear(Layer layer)
    {
        Array.Clear(layer.Pixels, 0, layer.Pixels.Length);
    }

    public static void Translate(Layer layer, int dx, int dy)
    {
        var width = layer.Width;
        var height = layer.Height;
        var result = new byte[layer.Pixels.Length];

        for (var y = 0; y < height; y++)
        {
            var targetY = y + dy;
            if (targetY < 0 || targetY >= height)
            {
                continue;
            }

            for (var x = 0; x < width; x++)
            {
                var targetX = x + dx;
                if (targetX < 0 || targetX >= width)
                {
                    continue;
                }

                var source = (y * width + x) * 4;
                var target = (targetY * width + targetX) * 4;
                result[target] = layer.Pixels[source];
                result[target + 1] = layer.Pixels[source + 1];
                result[target + 2] = layer.Pixels[source + 2];
                result[target + 3] = layer.Pixels[source + 3];
            }
        }

        layer.Pixels = result;
    }

    private static SKBitmap NewCanvasBitmap(Layer layer) =>
        new(new SKImageInfo(layer.Width, layer.Height, SKColorType.Bgra8888, SKAlphaType.Premul));

    private static void Merge(SKBitmap bitmap, Layer layer, byte[]? mask, double opacity, bool erase)
    {
        var source = bitmap.Bytes;
        var target = layer.Pixels;
        var count = layer.Width * layer.Height;

        for (var pixel = 0; pixel < count; pixel++)
        {
            var i = pixel * 4;
            var sourceAlpha = source[i + 3];
            if (sourceAlpha == 0)
            {
                continue;
            }

            var weight = Math.Clamp(opacity, 0, 1) * (mask == null ? 1.0 : mask[pixel] / 255.0);
            if (weight <= 0)
            {
                continue;
            }

            if (erase)
            {
                target[i + 3] = (byte)Math.Clamp(Math.Round(target[i + 3] * (1 - sourceAlpha / 255.0 * weight)), 0, 255);
                continue;
            }

            var sa = sourceAlpha / 255.0 * weight;
            var sb = source[i] * 255.0 / sourceAlpha;
            var sg = source[i + 1] * 255.0 / sourceAlpha;
            var sr = source[i + 2] * 255.0 / sourceAlpha;

            var da = target[i + 3] / 255.0;
            var ar = sa + da * (1 - sa);
            if (ar <= 0)
            {
                continue;
            }

            target[i] = (byte)Math.Clamp(Math.Round((sb * sa + target[i] * da * (1 - sa)) / ar), 0, 255);
            target[i + 1] = (byte)Math.Clamp(Math.Round((sg * sa + target[i + 1] * da * (1 - sa)) / ar), 0, 255);
            target[i + 2] = (byte)Math.Clamp(Math.Round((sr * sa + target[i + 2] * da * (1 - sa)) / ar), 0, 255);
            target[i + 3] = (byte)Math.Clamp(Math.Round(ar * 255), 0, 255);
        }
    }
}

public enum ShapeKind
{
    Rectangle,
    Ellipse,
    Line
}
