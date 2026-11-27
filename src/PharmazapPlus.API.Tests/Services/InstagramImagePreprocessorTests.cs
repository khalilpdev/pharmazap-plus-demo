using System.Text;
using FluentAssertions;
using PharmazapPlus.API.Services.Instagram;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PharmazapPlus.API.Tests.Services;

public class InstagramImagePreprocessorTests
{
    [Theory]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task NormalizeToJpegAsync_ConvertsSupportedImagesToJpeg(string sourceFormat)
    {
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(32, 24, new Rgba32(32, 64, 128, 180)))
        {
            if (string.Equals(sourceFormat, "webp", StringComparison.OrdinalIgnoreCase))
                await image.SaveAsWebpAsync(input);
            else
                await image.SaveAsPngAsync(input);
        }

        input.Position = 0;

        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        try
        {
            await InstagramImagePreprocessor.NormalizeToJpegAsync(input, outputPath);

            var bytes = await File.ReadAllBytesAsync(outputPath);
            bytes.Should().NotBeEmpty();
            bytes[0].Should().Be(0xFF);
            bytes[1].Should().Be(0xD8);

            using var normalized = await Image.LoadAsync<Rgba32>(outputPath);
            normalized.Width.Should().Be(32);
            normalized.Height.Should().Be(24);
        }
        finally
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task NormalizeToJpegAsync_InvalidImage_ThrowsFriendlyException()
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("not-an-image"));
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        Func<Task> act = async () => await InstagramImagePreprocessor.NormalizeToJpegAsync(input, outputPath);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Não foi possível processar a imagem enviada. Envie um arquivo JPEG, PNG ou WEBP válido.");
    }
}
