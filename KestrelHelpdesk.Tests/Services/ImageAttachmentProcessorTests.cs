using KestrelHelpdesk.Infrastructure.ImageProcessing;
using SkiaSharp;

namespace KestrelHelpdesk.Tests.Services;

public sealed class ImageAttachmentProcessorTests
{
    private readonly SkiaSharpImageAttachmentProcessor _processor = new();

    [Fact]
    public async Task ResizesImageWhenLongestSideExceedsLimitAndPreservesAspectRatio()
    {
        var bytes = CreateJpeg(2400, 1200);

        await using var input = new MemoryStream(bytes);
        var processed = await _processor.ProcessAsync(input, ".jpg");

        Assert.NotNull(processed);
        Assert.True(processed.WasResized);
        Assert.Equal(1600, processed.Width);
        Assert.Equal(800, processed.Height);
        Assert.Equal(".jpg", processed.Extension);
        Assert.Equal("image/jpeg", processed.ContentType);

        using var outputStream = new MemoryStream(processed.Content, writable: false);
        using var codec = SKCodec.Create(outputStream);
        Assert.NotNull(codec);
        Assert.Equal(1600, codec.Info.Width);
        Assert.Equal(800, codec.Info.Height);
    }

    [Fact]
    public async Task DoesNotEnlargeAnImageThatIsAlreadyWithinLimit()
    {
        var bytes = CreateJpeg(800, 600);

        await using var input = new MemoryStream(bytes);
        var processed = await _processor.ProcessAsync(input, ".jpg");

        Assert.NotNull(processed);
        Assert.False(processed.WasResized);
        Assert.Equal(800, processed.Width);
        Assert.Equal(600, processed.Height);
        Assert.Equal(bytes, processed.Content);
    }

    [Fact]
    public async Task RejectsInvalidImageDataForImageExtension()
    {
        await using var input = new MemoryStream("not an image"u8.ToArray());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => _processor.ProcessAsync(input, ".png"));

        Assert.Contains("valid supported image", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 92);
        Assert.NotNull(encoded);
        return encoded.ToArray();
    }
}
