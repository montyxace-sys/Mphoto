namespace Mphoto.Core;

public enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    Darken,
    Lighten,
    Add,
    Difference
}

public static class BlendModes
{
    public static readonly BlendMode[] All = (BlendMode[])Enum.GetValues(typeof(BlendMode));

    public static string DisplayName(BlendMode mode) => mode switch
    {
        BlendMode.Normal => "Normal",
        BlendMode.Multiply => "Multiplicar",
        BlendMode.Screen => "Trama",
        BlendMode.Overlay => "Superponer",
        BlendMode.Darken => "Oscurecer",
        BlendMode.Lighten => "Aclarar",
        BlendMode.Add => "Sumar",
        BlendMode.Difference => "Diferencia",
        _ => mode.ToString()
    };

    public static int Blend(BlendMode mode, int backdrop, int source) => mode switch
    {
        BlendMode.Normal => source,
        BlendMode.Multiply => backdrop * source / 255,
        BlendMode.Screen => 255 - (255 - backdrop) * (255 - source) / 255,
        BlendMode.Overlay => backdrop < 128
            ? 2 * backdrop * source / 255
            : 255 - 2 * (255 - backdrop) * (255 - source) / 255,
        BlendMode.Darken => Math.Min(backdrop, source),
        BlendMode.Lighten => Math.Max(backdrop, source),
        BlendMode.Add => Math.Min(255, backdrop + source),
        BlendMode.Difference => Math.Abs(backdrop - source),
        _ => source
    };
}
