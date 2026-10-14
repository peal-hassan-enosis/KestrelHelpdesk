using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using SkiaSharp;

namespace KestrelHelpdesk.Infrastructure.ImageProcessing;

/// <summary>
/// Validates and proportionally resizes supported still images before they are stored.
/// The original upload is never written to disk alongside the processed output.
/// </summary>
public sealed class SkiaSharpImageAttachmentProcessor : IImageAttachmentProcessor
{
    private const int JpegQuality = 88;
    private const int WebpQuality = 88;

    public async Task<ProcessedAttachmentImage?> ProcessAsync(
        Stream source,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (!TicketAttachmentPolicy.IsImageExtension(extension))
            return null;

        var bytes = await ReadSourceAsync(source, cancellationToken);
        using var metadataStream = new MemoryStream(bytes, writable: false);
        using var codec = CreateValidatedCodec(metadataStream, extension);

        var info = codec.Info;
        var origin = codec.EncodedOrigin;
        var swapDimensions = SwapsDimensions(origin);
        var displayWidth = swapDimensions ? info.Height : info.Width;
        var displayHeight = swapDimensions ? info.Width : info.Height;

        if (displayWidth <= 0 || displayHeight <= 0)
            throw InvalidImage();

        var sourceFormat = GetFormat(codec.EncodedFormat, extension);

        // Small, valid images are stored byte-for-byte as the single stored output.
        // Decode them to validate the actual pixel data before accepting the upload.
        var longestSide = Math.Max(displayWidth, displayHeight);
        if (longestSide <= TicketAttachmentPolicy.MaxImageLongestSide)
        {
            using var decoded = DecodeBitmap(bytes);
            return new ProcessedAttachmentImage(
                bytes,
                sourceFormat.Extension,
                sourceFormat.ContentType,
                displayWidth,
                displayHeight,
                WasResized: false);
        }

        try
        {
            // Decode large sources directly to the target raw dimensions to avoid loading
            // a full-resolution bitmap into memory for very large compressed images.
            var scale = TicketAttachmentPolicy.MaxImageLongestSide / (double)longestSide;
            var targetRawWidth = Math.Max(1, (int)Math.Round(info.Width * scale));
            var targetRawHeight = Math.Max(1, (int)Math.Round(info.Height * scale));

            using var resizeStream = new MemoryStream(bytes, writable: false);
            using var resizeCodec = SKCodec.Create(resizeStream) ?? throw InvalidImage();
            var targetInfo = new SKImageInfo(targetRawWidth, targetRawHeight);

            // Some codecs (including some JPEG dimension/scale combinations) cannot decode
            // directly into arbitrary target dimensions. Try the lower-memory scaled decode
            // first, then fall back to a normal decode and explicit resize for valid images.
            var decodedBitmap = SKBitmap.Decode(resizeCodec, targetInfo);
            if (decodedBitmap is null)
            {
                using var fullResolution = SKBitmap.Decode(bytes) ?? throw InvalidImage();
                decodedBitmap = fullResolution.Resize(
                    targetInfo,
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            }

            using var decoded = decodedBitmap ?? throw InvalidImage();
            using var resized = ApplyOrientation(decoded, origin);

            // SkiaSharp does not encode GIF animation. A static GIF that needs resizing
            // becomes a static PNG, with a matching filename extension and content type.
            var outputFormat = codec.EncodedFormat == SKEncodedImageFormat.Gif
                ? SKEncodedImageFormat.Png
                : codec.EncodedFormat;

            var output = GetOutputFormat(outputFormat, sourceFormat.Extension);
            using var encoded = resized.Encode(outputFormat, GetQuality(outputFormat));
            if (encoded is null)
                throw new InvalidDataException("The processed image could not be encoded. Try uploading a PNG, JPG, or WebP image.");

            return new ProcessedAttachmentImage(
                encoded.ToArray(),
                output.Extension,
                output.ContentType,
                resized.Width,
                resized.Height,
                WasResized: true);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("The image could not be processed. Upload a valid, non-animated image.", ex);
        }
    }

    private static async Task<byte[]> ReadSourceAsync(Stream source, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static SKCodec CreateValidatedCodec(Stream source, string extension)
    {
        try
        {
            var codec = SKCodec.Create(source) ?? throw InvalidImage();
            EnsureSupportedStillImage(codec, extension);
            return codec;
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException(
                "The image could not be inspected. Upload a valid, non-animated PNG, JPG/JPEG, GIF, or WebP image.",
                ex);
        }
    }

    private static SKBitmap DecodeBitmap(byte[] bytes)
    {
        try
        {
            return SKBitmap.Decode(bytes) ?? throw InvalidImage();
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException(
                "The image could not be decoded. Upload a valid, non-animated PNG, JPG/JPEG, GIF, or WebP image.",
                ex);
        }
    }

    private static void EnsureSupportedStillImage(SKCodec codec, string extension)
    {
        if (!TicketAttachmentPolicy.IsImageExtension(extension))
            throw InvalidImage();

        if (codec.Info.Width <= 0 || codec.Info.Height <= 0)
            throw InvalidImage();

        if (codec.FrameCount > 1)
            throw new InvalidDataException("Animated images are not supported. Please upload a still image instead.");

        _ = GetFormat(codec.EncodedFormat, extension);
    }

    private static (string Extension, string ContentType) GetFormat(
        SKEncodedImageFormat format,
        string requestedExtension) =>
        format switch
        {
            SKEncodedImageFormat.Jpeg => (
                requestedExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpeg" : ".jpg",
                "image/jpeg"),
            SKEncodedImageFormat.Png => (".png", "image/png"),
            SKEncodedImageFormat.Webp => (".webp", "image/webp"),
            SKEncodedImageFormat.Gif => (".gif", "image/gif"),
            _ => throw new InvalidDataException("Supported image formats are PNG, JPG/JPEG, GIF, and WebP.")
        };

    private static (string Extension, string ContentType) GetOutputFormat(
        SKEncodedImageFormat format,
        string fallbackExtension) =>
        format switch
        {
            SKEncodedImageFormat.Jpeg => (
                fallbackExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpeg" : ".jpg",
                "image/jpeg"),
            SKEncodedImageFormat.Png => (".png", "image/png"),
            SKEncodedImageFormat.Webp => (".webp", "image/webp"),
            _ => throw new InvalidDataException("The processed image format is not supported.")
        };

    private static int GetQuality(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Jpeg => JpegQuality,
        SKEncodedImageFormat.Webp => WebpQuality,
        _ => 100
    };

    private static bool SwapsDimensions(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var swapDimensions = SwapsDimensions(origin);
        var targetWidth = swapDimensions ? source.Height : source.Width;
        var targetHeight = swapDimensions ? source.Width : source.Height;
        var result = new SKBitmap(
            new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul));

        var width = source.Width;
        var height = source.Height;
        var matrix = origin switch
        {
            SKEncodedOrigin.TopLeft => new SKMatrix(1, 0, 0, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity
        };

        using (var canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(source, 0, 0);
            canvas.Flush();
        }

        return result;
    }

    private static InvalidDataException InvalidImage() =>
        new("This file is not a valid supported image. Upload a PNG, JPG/JPEG, GIF, or WebP image.");
}
