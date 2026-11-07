using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mphoto.Core;

namespace Mphoto.Controls;

public sealed class CanvasView : FrameworkElement
{
    private const double MinZoom = 0.02;
    private const double MaxZoom = 32;

    private static readonly Brush BackdropBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x13, 0x13, 0x17)));
    private static readonly Brush ShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)));
    private static readonly Pen FramePen = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x45, 0x45, 0x52)), 1));
    private static readonly DrawingBrush CheckerBrush = CreateCheckerBrush();
    private static readonly Pen CropShadePen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), 1));

    private readonly DispatcherTimer _antsTimer;
    private readonly DashStyle _antsDash = new(new[] { 4.0, 4.0 }, 0);
    private Geometry? _selectionGeometry;
    private double _dashOffset;

    public CanvasView()
    {
        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        _antsTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _antsTimer.Tick += (_, _) =>
        {
            _dashOffset = (_dashOffset + 1) % 8;
            _antsDash.Offset = _dashOffset;
            InvalidateVisual();
        };
        Unloaded += (_, _) => _antsTimer.Stop();
    }

    public int DocWidth { get; private set; }
    public int DocHeight { get; private set; }
    public double Zoom { get; private set; } = 1;
    public Vector Pan { get; private set; }

    public BitmapSource? Image { get; set; }
    public bool ShowSelection { get; set; } = true;

    public Point? BrushCursor { get; set; }
    public double CursorRadius { get; set; }
    public bool ShowRing { get; set; }

    public Geometry? LiveGeometry { get; set; }
    public Geometry? ShapeGeometry { get; set; }
    public Brush? ShapeFill { get; set; }
    public Color ShapeColor { get; set; } = Colors.White;
    public bool ShapeFilled { get; set; }
    public double ShapeStrokeWidth { get; set; } = 2;

    public Rect? CropRect { get; set; }

    public event EventHandler? ViewChanged;
    public event EventHandler? ViewportChanged;

    public Size Viewport => RenderSize;

    public void SetDocumentSize(int width, int height)
    {
        DocWidth = width;
        DocHeight = height;
        InvalidateVisual();
    }

    public Point ScreenToDoc(Point point) =>
        new((point.X - Pan.X) / Zoom, (point.Y - Pan.Y) / Zoom);

    public Point DocToScreen(Point point) =>
        new(point.X * Zoom + Pan.X, point.Y * Zoom + Pan.Y);

    public bool IsInsideDoc(Point screenPoint)
    {
        var doc = ScreenToDoc(screenPoint);
        return doc.X >= 0 && doc.Y >= 0 && doc.X <= DocWidth && doc.Y <= DocHeight;
    }

    public void Fit(Size viewport)
    {
        if (DocWidth <= 0 || DocHeight <= 0 || viewport.Width < 4 || viewport.Height < 4)
        {
            return;
        }

        var availableWidth = Math.Max(20, viewport.Width - 72);
        var availableHeight = Math.Max(20, viewport.Height - 72);
        Zoom = Math.Clamp(Math.Min(availableWidth / DocWidth, availableHeight / DocHeight), MinZoom, 4);
        Center(viewport);
    }

    public void Center(Size viewport)
    {
        Pan = new Vector(
            Math.Round((viewport.Width - DocWidth * Zoom) / 2),
            Math.Round((viewport.Height - DocHeight * Zoom) / 2));
        RaiseViewChanged();
    }

    public void SetZoom(double zoom, Size viewport)
    {
        var next = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(next - Zoom) < 0.0001)
        {
            return;
        }

        var center = new Point(viewport.Width / 2, viewport.Height / 2);
        var docCenter = ScreenToDoc(center);
        Zoom = next;
        Pan = new Vector(center.X - docCenter.X * Zoom, center.Y - docCenter.Y * Zoom);
        RaiseViewChanged();
    }

    public void ZoomAt(Point anchor, double factor)
    {
        var next = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(next - Zoom) < 0.0001)
        {
            return;
        }

        var docPoint = ScreenToDoc(anchor);
        Zoom = next;
        Pan = new Vector(anchor.X - docPoint.X * Zoom, anchor.Y - docPoint.Y * Zoom);
        RaiseViewChanged();
    }

    public void PanBy(double dx, double dy)
    {
        Pan = new Vector(Pan.X + dx, Pan.Y + dy);
        RaiseViewChanged();
    }

    public void SetSelection(Selection? selection)
    {
        _selectionGeometry = selection is { IsActive: true } ? BuildOutline(selection) : null;

        if (_selectionGeometry != null)
        {
            if (!_antsTimer.IsEnabled)
            {
                _antsTimer.Start();
            }
        }
        else
        {
            _antsTimer.Stop();
        }

        InvalidateVisual();
    }

    public void SetImage(BitmapSource? image, int width, int height)
    {
        Image = image;
        DocWidth = width;
        DocHeight = height;
        InvalidateVisual();
    }

    public void Refresh() => InvalidateVisual();

    private void RaiseViewChanged()
    {
        ViewChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(BackdropBrush, null, new Rect(RenderSize));

        if (DocWidth <= 0 || DocHeight <= 0)
        {
            return;
        }

        var rect = new Rect(Pan.X, Pan.Y, DocWidth * Zoom, DocHeight * Zoom);
        var shadow = new Rect(rect.X + 7, rect.Y + 7, rect.Width, rect.Height);

        if (Image != null)
        {
            dc.DrawRectangle(ShadowBrush, null, shadow);
            dc.PushClip(new RectangleGeometry(rect));
            dc.DrawRectangle(CheckerBrush, null, rect);
            RenderOptions.SetBitmapScalingMode(this, Zoom >= 1 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            dc.DrawImage(Image, rect);
            dc.Pop();
        }

        dc.DrawRectangle(null, FramePen, rect);

        var transform = new MatrixTransform(Zoom, 0, 0, Zoom, Pan.X, Pan.Y);

        if (_selectionGeometry != null && ShowSelection)
        {
            dc.PushTransform(transform);
            var scale = 1 / Zoom;
            dc.DrawGeometry(null, MakePen(Color.FromArgb(210, 0, 0, 0), 1.8 * scale, false), _selectionGeometry);
            dc.DrawGeometry(null, MakePen(Colors.White, 1.1 * scale, true), _selectionGeometry);
            dc.Pop();
        }

        if (ShapeGeometry != null)
        {
            dc.PushTransform(transform);
            var stroke = MakePen(ShapeColor, Math.Max(0.5, ShapeStrokeWidth), false);
            dc.DrawGeometry(ShapeFilled ? new SolidColorBrush(ShapeColor) : null, stroke, ShapeGeometry);
            dc.Pop();
        }

        if (LiveGeometry != null)
        {
            dc.PushTransform(transform);
            var scale = 1 / Zoom;
            dc.DrawGeometry(null, MakePen(Color.FromArgb(210, 0, 0, 0), 2.0 * scale, false), LiveGeometry);
            dc.DrawGeometry(null, MakePen(Colors.White, 1.2 * scale, true), LiveGeometry);
            dc.Pop();
        }

        if (CropRect is { } crop && crop.Width > 0 && crop.Height > 0)
        {
            DrawCropOverlay(dc, crop);
        }

        if (ShowRing && BrushCursor is { } cursor)
        {
            var center = DocToScreen(cursor);
            var radius = Math.Max(2.5, CursorRadius * Zoom);
            dc.DrawEllipse(null, MakePen(Color.FromArgb(200, 0, 0, 0), 3, false), center, radius, radius);
            dc.DrawEllipse(null, MakePen(Colors.White, 1.2, false), center, radius, radius);
            dc.DrawEllipse(null, MakePen(Color.FromArgb(160, 0, 0, 0), 1, false), center, 1.2, 1.2);
        }
    }

    private void DrawCropOverlay(DrawingContext dc, Rect crop)
    {
        var left = crop.X * Zoom + Pan.X;
        var top = crop.Y * Zoom + Pan.Y;
        var width = crop.Width * Zoom;
        var height = crop.Height * Zoom;
        var screen = new Rect(left, top, width, height);
        var area = new Rect(Pan.X, Pan.Y, DocWidth * Zoom, DocHeight * Zoom);

        dc.DrawRectangle(ShadowBrush, null, new Rect(area.X, area.Y, area.Width, Math.Max(0, screen.Top - area.Top)));
        dc.DrawRectangle(ShadowBrush, null, new Rect(area.X, screen.Bottom, area.Width, Math.Max(0, area.Bottom - screen.Bottom)));
        dc.DrawRectangle(ShadowBrush, null, new Rect(area.X, screen.Top, Math.Max(0, screen.Left - area.Left), screen.Height));
        dc.DrawRectangle(ShadowBrush, null, new Rect(screen.Right, screen.Top, Math.Max(0, area.Right - screen.Right), screen.Height));

        var border = MakePen(Colors.White, 1.4, true);
        dc.DrawRectangle(null, border, screen);

        var thirds = MakePen(Color.FromArgb(90, 255, 255, 255), 0.8, false);
        for (var i = 1; i < 3; i++)
        {
            var x = screen.X + screen.Width * i / 3;
            var y = screen.Y + screen.Height * i / 3;
            dc.DrawLine(thirds, new Point(x, screen.Top), new Point(x, screen.Bottom));
            dc.DrawLine(thirds, new Point(screen.Left, y), new Point(screen.Right, y));
        }

        var handleBrush = Frozen(new SolidColorBrush(Colors.White));
        var handleSize = 7.0;
        foreach (var point in new[]
                 {
                     new Point(screen.Left, screen.Top), new Point(screen.Right, screen.Top),
                     new Point(screen.Left, screen.Bottom), new Point(screen.Right, screen.Bottom),
                     new Point(screen.Left + screen.Width / 2, screen.Top),
                     new Point(screen.Left + screen.Width / 2, screen.Bottom),
                     new Point(screen.Left, screen.Top + screen.Height / 2),
                     new Point(screen.Right, screen.Top + screen.Height / 2)
                 })
        {
            dc.DrawRectangle(handleBrush, CropShadePen,
                new Rect(point.X - handleSize / 2, point.Y - handleSize / 2, handleSize, handleSize));
        }
    }

    private Pen MakePen(Color color, double thickness, bool dashed)
    {
        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, thickness);
        if (dashed)
        {
            pen.DashStyle = _antsDash;
            pen.DashCap = PenLineCap.Flat;
            pen.StartLineCap = PenLineCap.Flat;
            pen.EndLineCap = PenLineCap.Flat;
        }

        return pen;
    }

    private static Geometry BuildOutline(Selection selection)
    {
        var edges = SelectionOps.Outline(selection);
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            // Unir las aristas consecutivas en tramos largos para dibujar mucho más rápido.
            var horizontal = new Dictionary<int, List<double[]>>();
            var vertical = new Dictionary<int, List<double[]>>();

            foreach (var edge in edges)
            {
                if (Math.Abs(edge.Y1 - edge.Y2) < 0.01)
                {
                    var y = (int)Math.Round(edge.Y1);
                    if (!horizontal.TryGetValue(y, out var list))
                    {
                        horizontal[y] = list = new List<double[]>();
                    }

                    list.Add(new[] { Math.Min(edge.X1, edge.X2), Math.Max(edge.X1, edge.X2) });
                }
                else
                {
                    var x = (int)Math.Round(edge.X1);
                    if (!vertical.TryGetValue(x, out var list))
                    {
                        vertical[x] = list = new List<double[]>();
                    }

                    list.Add(new[] { Math.Min(edge.Y1, edge.Y2), Math.Max(edge.Y1, edge.Y2) });
                }
            }

            foreach (var (y, spans) in horizontal)
            {
                AddRuns(context, spans, y, true);
            }

            foreach (var (x, spans) in vertical)
            {
                AddRuns(context, spans, x, false);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static void AddRuns(StreamGeometryContext context, List<double[]> spans, int fixedCoordinate, bool horizontal)
    {
        spans.Sort((a, b) => a[0].CompareTo(b[0]));
        var start = spans[0][0];
        var end = spans[0][1];

        void Emit(double from, double to)
        {
            if (to - from < 0.01)
            {
                return;
            }

            var a = horizontal ? new Point(from, fixedCoordinate) : new Point(fixedCoordinate, from);
            var b = horizontal ? new Point(to, fixedCoordinate) : new Point(fixedCoordinate, to);
            context.BeginFigure(a, false, false);
            context.LineTo(b, true, false);
        }

        for (var i = 1; i < spans.Count; i++)
        {
            if (spans[i][0] <= end + 0.5)
            {
                end = Math.Max(end, spans[i][1]);
            }
            else
            {
                Emit(start, end);
                start = spans[i][0];
                end = spans[i][1];
            }
        }

        Emit(start, end);
    }

    private static DrawingBrush CreateCheckerBrush()
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x44)), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));

        var squares = new GeometryGroup();
        squares.Children.Add(new RectangleGeometry(new Rect(0, 0, 8, 8)));
        squares.Children.Add(new RectangleGeometry(new Rect(8, 8, 8, 8)));
        drawing.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x35)), null, squares));
        drawing.Freeze();

        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 16, 16),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
