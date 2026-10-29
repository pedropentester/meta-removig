using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MetaRemovig;

public sealed class CleanResult
{
    public string OriginalName { get; init; } = "";
    public string OutputName { get; init; } = "";
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public bool Success { get; init; }
    public string? Error { get; init; }

    /// <summary>El archivo original contenía marcas de procedencia/IA (C2PA, JUMBF o IPTC DigitalSourceType).</summary>
    public bool HadAiMarkers { get; init; }

    /// <summary>Tras limpiar, se verificó que el archivo nuevo ya no contiene esas marcas.</summary>
    public bool Verified { get; init; }

    public string Display =>
        Success
            ? $"✓  {OriginalName}  →  {OutputName}" + (HadAiMarkers ? "   (marcas de IA eliminadas)" : "")
            : $"✕  {OriginalName}  —  {Error}";
}

public static class ImageCleaner
{
    /// <summary>
    /// Decodifica la imagen, copia únicamente los píxeles a un bitmap nuevo y lo codifica
    /// sin ningún bloque de metadatos (EXIF, IPTC, XMP, C2PA/JUMBF...).
    /// </summary>
    public static CleanResult Clean(string path)
    {
        var originalName = Path.GetFileName(path);
        try
        {
            var originalBytes = File.ReadAllBytes(path);
            bool hadMarkers = HasAiMarkers(originalBytes);

            BitmapFrame frame;
            using (var ms = new MemoryStream(originalBytes))
            {
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
                frame.Freeze();
            }

            // 1) Leer la orientación EXIF ANTES de perder los metadatos y aplicarla a los píxeles.
            int orientation = ReadOrientation(frame);
            BitmapSource source = ApplyOrientation(frame, orientation);

            // 2) Decidir formato de salida.
            var ext = Path.GetExtension(path).ToLowerInvariant();
            bool asJpeg = ext is ".jpg" or ".jpeg" or ".jfif";
            bool hasAlpha = HasAlpha(source.Format);
            var targetFormat = (!asJpeg && hasAlpha) ? PixelFormats.Bgra32 : PixelFormats.Bgr24;

            BitmapSource converted = source.Format == targetFormat
                ? source
                : new FormatConvertedBitmap(source, targetFormat, null, 0);

            // 3) Copiar píxeles a un bitmap completamente nuevo.
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = (width * converted.Format.BitsPerPixel + 7) / 8;
            var pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            var clean = BitmapSource.Create(
                width, height,
                frame.DpiX > 0 ? frame.DpiX : 96,
                frame.DpiY > 0 ? frame.DpiY : 96,
                targetFormat, null, pixels, stride);
            clean.Freeze();

            // 4) Conservar solo el perfil de color (no es un metadato de procedencia) si es RGB.
            BitmapFrame outFrame;
            var contexts = frame.ColorContexts;
            bool rgbSource = frame.Format.ToString().Contains("Bgr") || frame.Format.ToString().Contains("Rgb");
            if (contexts is { Count: > 0 } && rgbSource)
                outFrame = BitmapFrame.Create(clean, null, null, contexts);
            else
                outFrame = BitmapFrame.Create(clean);

            // 5) Codificar SIN metadatos.
            BitmapEncoder encoder = asJpeg
                ? new JpegBitmapEncoder { QualityLevel = 95 }
                : new PngBitmapEncoder();
            encoder.Frames.Add(outFrame);

            byte[] output;
            using (var outMs = new MemoryStream())
            {
                encoder.Save(outMs);
                output = outMs.ToArray();
            }

            // 6) Verificación: el archivo nuevo no debe contener marcas.
            bool verified = !HasAiMarkers(output);

            var baseName = Path.GetFileNameWithoutExtension(path);
            var outName = baseName + "_limpia" + (asJpeg ? ".jpg" : ".png");

            return new CleanResult
            {
                OriginalName = originalName,
                OutputName = outName,
                Data = output,
                Success = true,
                HadAiMarkers = hadMarkers,
                Verified = verified
            };
        }
        catch (Exception ex)
        {
            return new CleanResult
            {
                OriginalName = originalName,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static int ReadOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is BitmapMetadata md && md.ContainsQuery("System.Photo.Orientation"))
            {
                var value = md.GetQuery("System.Photo.Orientation");
                if (value != null) return Convert.ToInt32(value);
            }
        }
        catch { /* sin orientación */ }
        return 1;
    }

    private static BitmapSource ApplyOrientation(BitmapSource src, int orientation)
    {
        // Espejo horizontal (2, 5, 7) y/o vertical (4), y luego rotación.
        BitmapSource result = src;

        if (orientation is 2 or 5 or 7)
            result = new TransformedBitmap(result, new ScaleTransform(-1, 1));
        else if (orientation == 4)
            result = new TransformedBitmap(result, new ScaleTransform(1, -1));

        double angle = orientation switch
        {
            3 => 180,
            5 => 270,
            6 => 90,
            7 => 90,
            8 => 270,
            _ => 0
        };

        if (angle != 0)
            result = new TransformedBitmap(result, new RotateTransform(angle));

        result.Freeze();
        return result;
    }

    private static bool HasAlpha(PixelFormat f) =>
        f == PixelFormats.Bgra32 || f == PixelFormats.Pbgra32 ||
        f == PixelFormats.Rgba64 || f == PixelFormats.Prgba64 ||
        f == PixelFormats.Rgba128Float || f == PixelFormats.Prgba128Float;

    /// <summary>Busca firmas de Content Credentials (C2PA/JUMBF) y del campo IPTC de origen digital por IA.</summary>
    private static bool HasAiMarkers(byte[] data)
    {
        var span = data.AsSpan();
        return span.IndexOf("c2pa"u8) >= 0
            || span.IndexOf("jumb"u8) >= 0
            || span.IndexOf("AlgorithmicMedia"u8) >= 0
            || span.IndexOf("contentauth"u8) >= 0;
    }
}
