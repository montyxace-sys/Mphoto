namespace Mphoto.Core;

public sealed class PixelEdit
{
    public PixelEdit(Layer layer, int x, int y, int width, int height, byte[] before, byte[] after, string name = "Edición")
    {
        Layer = layer;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Before = before;
        After = after;
        Name = name;
    }

    public Layer Layer { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] Before { get; }
    public byte[] After { get; }
    public string Name { get; }

    public long Bytes => Before.LongLength + After.LongLength;

    public void Undo() => LayerRegion.Write(Layer, X, Y, Width, Height, Before);

    public void Redo() => LayerRegion.Write(Layer, X, Y, Width, Height, After);
}

public static class LayerRegion
{
    public static (int X, int Y, int W, int H) Clamp(Layer layer, int x, int y, int width, int height)
    {
        var x0 = Math.Clamp(x, 0, layer.Width);
        var y0 = Math.Clamp(y, 0, layer.Height);
        var x1 = Math.Clamp(x + width, 0, layer.Width);
        var y1 = Math.Clamp(y + height, 0, layer.Height);
        return (x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
    }

    public static byte[] Read(Layer layer, int x, int y, int width, int height)
    {
        var (cx, cy, cw, ch) = Clamp(layer, x, y, width, height);
        var data = new byte[cw * ch * 4];
        for (var row = 0; row < ch; row++)
        {
            var source = ((cy + row) * layer.Width + cx) * 4;
            Buffer.BlockCopy(layer.Pixels, source, data, row * cw * 4, cw * 4);
        }

        return data;
    }

    public static void Write(Layer layer, int x, int y, int width, int height, byte[] data)
    {
        var (cx, cy, cw, ch) = Clamp(layer, x, y, width, height);
        if (cw == 0 || ch == 0)
        {
            return;
        }

        var rowBytes = cw * 4;
        for (var row = 0; row < ch; row++)
        {
            var target = ((cy + row) * layer.Width + cx) * 4;
            Buffer.BlockCopy(data, row * rowBytes, layer.Pixels, target, rowBytes);
        }
    }
}

public sealed class HistoryManager
{
    private readonly List<PixelEdit> _undo = new();
    private readonly List<PixelEdit> _redo = new();
    private long _bytes;

    public int MaxSteps { get; set; } = 40;
    public long MaxBytes { get; set; } = 512L * 1024 * 1024;

    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? NextUndoName => _undo.Count > 0 ? _undo[^1].Name : null;
    public string? NextRedoName => _redo.Count > 0 ? _redo[^1].Name : null;
    public int UndoCount => _undo.Count;
    public IReadOnlyList<string> UndoSteps => _undo.Select(edit => edit.Name).ToList();
    public IReadOnlyList<string> RedoSteps => _redo.Select(edit => edit.Name).ToList();

    public void Push(PixelEdit edit)
    {
        _redo.Clear();
        _undo.Add(edit);
        _bytes += edit.Bytes;
        Trim();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        var edit = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        edit.Undo();
        _redo.Add(edit);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        var edit = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        edit.Redo();
        _undo.Add(edit);
        _bytes += edit.Bytes;
        Trim();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _bytes = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Trim()
    {
        while (_undo.Count > MaxSteps || (_bytes > MaxBytes && _undo.Count > 1))
        {
            _bytes -= _undo[0].Bytes;
            _undo.RemoveAt(0);
        }
    }
}

public static class EditRecorder
{
    public static PixelEdit Record(Layer layer, int x, int y, int width, int height, Action action, string name = "Edición")
    {
        var (cx, cy, cw, ch) = LayerRegion.Clamp(layer, x, y, width, height);
        var before = LayerRegion.Read(layer, cx, cy, cw, ch);
        action();
        var after = LayerRegion.Read(layer, cx, cy, cw, ch);
        return new PixelEdit(layer, cx, cy, cw, ch, before, after, name);
    }
}
