using System.Globalization;
using System.Windows.Media;

namespace Mphoto;

public static class ColorUtil
{
    public static readonly string[] Palette =
    {
        "#000000", "#1F2937", "#374151", "#6B7280", "#9CA3AF", "#D1D5DB", "#F3F4F6", "#FFFFFF", "#7F1D1D", "#B91C1C",
        "#EF4444", "#F97316", "#F59E0B", "#FACC15", "#EAB308", "#84CC16", "#22C55E", "#10B981", "#14B8A6", "#06B6D4",
        "#0EA5E9", "#3B82F6", "#6366F1", "#8B5CF6", "#A855F7", "#D946EF", "#EC4899", "#F43F5E", "#78350F", "#92400E",
        "#A16207", "#854D0E", "#166534", "#164E63", "#1E3A8A", "#312E81", "#4C1D95", "#581C87", "#831843", "#FDA4AF"
    };

    public static uint ToUint(Color color) =>
        ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    public static Color FromUint(uint value) =>
        Color.FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static bool TryParseHex(string? text, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim().TrimStart('#');
        if (value.Length == 8)
        {
            value = value[2..];
        }

        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }
}
