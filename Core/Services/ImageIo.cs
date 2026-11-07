using System.Runtime.InteropServices;
using SkiaSharp;

namespace Mphoto.Core;

public enum ImageFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp
}

public static class ImageIo
{
    public static readonly string[] OpenExtensions =
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".ico"
    };

    public static bool CanOpen(string path) =>
        OpenExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static (byte[] Pixels, int Width, int Height) Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidOperationException($"No se puede leer «{Path.GetFileName(path)}»: formato no compatible.");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
        {
            throw new InvalidOperationException($"No se puede decodificar «{Path.GetFileName(path)}» ({result}).");
        }

        return (Unpremultiply(bitmap.Bytes), info.Width, info.Height);
    }

    public static void Save(string path, byte[] pixels, int width, int height, ImageFormat format, int quality = 92)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        var premultiplied = Premultiply(pixels);
        Marshal.Copy(premultiplied, 0, bitmap.GetPixels(), premultiplied.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(MapFormat(format), Math.Clamp(quality, 1, 100))
            ?? throw new InvalidOperationException("No se pudo codificar la imagen en el formato elegido.");

        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    public static ImageFormat FormatFromPath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".webp" => ImageFormat.Webp,
            ".bmp" => ImageFormat.Bmp,
            _ => ImageFormat.Png
        };

    public static string ExtensionFor(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => ".jpg",
        ImageFormat.Webp => ".webp",
        ImageFormat.Bmp => ".bmp",
        _ => ".png"
    };

    private static SKEncodedImageFormat MapFormat(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
        ImageFormat.Webp => SKEncodedImageFormat.Webp,
        ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
        _ => SKEncodedImageFormat.Png
    };

    internal static SKBitmap ToPremultipliedBitmap(byte[] pixels, int width, int height)
    {
        var premultiplied = Premultiply(pixels);
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        Marshal.Copy(premultiplied, 0, bitmap.GetPixels(), premultiplied.Length);
        return bitmap;
    }

    internal static byte[] FromPremultipliedBitmap(SKBitmap bitmap) => Unpremultiply(bitmap.Bytes);

    internal static byte[] Premultiply(byte[] pixels)
    {
        var result = new byte[pixels.Length];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha == 255)
            {
                result[i] = pixels[i];
                result[i + 1] = pixels[i + 1];
                result[i + 2] = pixels[i + 2];
            }
            else if (alpha != 0)
            {
                result[i] = (byte)(pixels[i] * alpha / 255);
                result[i + 1] = (byte)(pixels[i + 1] * alpha / 255);
                result[i + 2] = (byte)(pixels[i + 2] * alpha / 255);
            }

            result[i + 3] = alpha;
        }

        return result;
    }

    internal static byte[] Unpremultiply(byte[] pixels)
    {
        var result = new byte[pixels.Length];
        Buffer.BlockCopy(pixels, 0, result, 0, pixels.Length);
        for (var i = 0; i < result.Length; i += 4)
        {
            var alpha = result[i + 3];
            if (alpha == 255 || alpha == 0)
            {
                continue;
            }

            result[i] = (byte)Math.Min(255, (result[i] * 255 + alpha / 2) / alpha);
            result[i + 1] = (byte)Math.Min(255, (result[i + 1] * 255 + alpha / 2) / alpha);
            result[i + 2] = (byte)Math.Min(255, (result[i + 2] * 255 + alpha / 2) / alpha);
        }

        return result;
    }
}
