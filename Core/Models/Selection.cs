namespace Mphoto.Core;

public sealed class Selection
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public byte[]? Mask { get; private set; }

    public bool IsActive => Mask != null;

    public Selection(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public void Set(byte[]? mask)
    {
        Mask = mask;
    }

    public void SetAll()
    {
        var mask = new byte[Width * Height];
        Array.Fill(mask, (byte)255);
        Mask = mask;
    }

    public void Clear()
    {
        Mask = null;
    }

    public void Reset(int width, int height)
    {
        Width = width;
        Height = height;
        Mask = null;
    }

    public double OpacityAt(int index)
    {
        if (Mask == null)
        {
            return 1.0;
        }

        return Mask[index] / 255.0;
    }
}
