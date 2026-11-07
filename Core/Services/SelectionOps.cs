namespace Mphoto.Core;

public static class SelectionOps
{
    public static void SelectRectangle(Selection selection, int x, int y, int width, int height)
    {
        var mask = new byte[selection.Width * selection.Height];
        var x0 = Math.Clamp(x, 0, selection.Width);
        var y0 = Math.Clamp(y, 0, selection.Height);
        var x1 = Math.Clamp(x + width, 0, selection.Width);
        var y1 = Math.Clamp(y + height, 0, selection.Height);

        for (var row = y0; row < y1; row++)
        {
            Array.Fill(mask, (byte)255, row * selection.Width + x0, Math.Max(0, x1 - x0));
        }

        selection.Set(mask);
    }

    public static void SelectEllipse(Selection selection, int x, int y, int width, int height)
    {
        var mask = new byte[selection.Width * selection.Height];
        if (width <= 0 || height <= 0)
        {
            selection.Set(mask);
            return;
        }

        var cx = x + width / 2.0;
        var cy = y + height / 2.0;
        var rx = width / 2.0;
        var ry = height / 2.0;

        for (var py = 0; py < selection.Height; py++)
        {
            var ny = (py + 0.5 - cy) / ry;
            for (var px = 0; px < selection.Width; px++)
            {
                var nx = (px + 0.5 - cx) / rx;
                if (nx * nx + ny * ny <= 1.0)
                {
                    mask[py * selection.Width + px] = 255;
                }
            }
        }

        selection.Set(mask);
    }

    public static void SelectPolygon(Selection selection, IReadOnlyList<Pt> points)
    {
        var mask = new byte[selection.Width * selection.Height];
        if (points.Count < 3)
        {
            selection.Set(mask);
            return;
        }

        var width = selection.Width;
        var height = selection.Height;
        var crossings = new List<double>();

        for (var y = 0; y < height; y++)
        {
            var scanY = y + 0.5;
            crossings.Clear();
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                if ((a.Y <= scanY && b.Y > scanY) || (b.Y <= scanY && a.Y > scanY))
                {
                    var t = (scanY - a.Y) / (b.Y - a.Y);
                    crossings.Add(a.X + t * (b.X - a.X));
                }
            }

            if (crossings.Count < 2)
            {
                continue;
            }

            crossings.Sort();
            for (var i = 0; i + 1 < crossings.Count; i += 2)
            {
                var start = Math.Clamp((int)Math.Ceiling(crossings[i] - 0.5), 0, width);
                var end = Math.Clamp((int)Math.Floor(crossings[i + 1] - 0.5), -1, width - 1);
                if (end >= start)
                {
                    Array.Fill(mask, (byte)255, y * width + start, end - start + 1);
                }
            }
        }

        selection.Set(mask);
    }

    public static void SelectAll(Selection selection) => selection.SetAll();

    public static void Deselect(Selection selection) => selection.Clear();

    public static void Invert(Selection selection)
    {
        if (selection.Mask == null)
        {
            return;
        }

        var mask = selection.Mask;
        for (var i = 0; i < mask.Length; i++)
        {
            mask[i] = (byte)(255 - mask[i]);
        }
    }

    public static bool IsEmpty(Selection selection)
    {
        if (selection.Mask == null)
        {
            return true;
        }

        foreach (var value in selection.Mask)
        {
            if (value > 0)
            {
                return false;
            }
        }

        return true;
    }

    public static (int X, int Y, int Width, int Height) Bounds(Selection selection)
    {
        if (selection.Mask == null)
        {
            return (0, 0, selection.Width, selection.Height);
        }

        var minX = selection.Width;
        var minY = selection.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < selection.Height; y++)
        {
            for (var x = 0; x < selection.Width; x++)
            {
                if (selection.Mask[y * selection.Width + x] == 0)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return (0, 0, 0, 0);
        }

        return (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static List<SelectionEdge> Outline(Selection selection)
    {
        var edges = new List<SelectionEdge>();
        if (selection.Mask == null)
        {
            return edges;
        }

        var mask = selection.Mask;
        var width = selection.Width;
        var height = selection.Height;

        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && mask[y * width + x] >= 128;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!Inside(x, y))
                {
                    continue;
                }

                if (!Inside(x - 1, y)) edges.Add(new SelectionEdge(x, y, x, y + 1));
                if (!Inside(x + 1, y)) edges.Add(new SelectionEdge(x + 1, y, x + 1, y + 1));
                if (!Inside(x, y - 1)) edges.Add(new SelectionEdge(x, y, x + 1, y));
                if (!Inside(x, y + 1)) edges.Add(new SelectionEdge(x, y + 1, x + 1, y + 1));
            }
        }

        return edges;
    }
}
