using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PharmazapPlus.API.Services.Instagram;

internal static class InstagramImagePreprocessor
{
    private static readonly JpegEncoder JpegEncoder = new()
    {
        Quality = 90
    };

    public static async Task NormalizeToJpegAsync(Stream input, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("O caminho de saída da imagem é obrigatório.", nameof(outputPath));

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (input.CanSeek)
            input.Position = 0;

        try
        {
            using var image = await Image.LoadAsync<Rgba32>(input, cancellationToken);
            image.Mutate(ctx => ctx.AutoOrient());

            // JPEG doesn't support transparency, so flatten transparent PNG/WEBP inputs
            // onto a white background before re-encoding for Instagram upload.
            using var flattened = new Image<Rgba32>(image.Width, image.Height, Color.White);
            flattened.Mutate(ctx => ctx.DrawImage(image, 1f));

            await flattened.SaveAsJpegAsync(outputPath, JpegEncoder, cancellationToken);
        }
        catch (UnknownImageFormatException ex)
        {
            throw new InvalidOperationException("Não foi possível processar a imagem enviada. Envie um arquivo JPEG, PNG ou WEBP válido.", ex);
        }
    }
}
