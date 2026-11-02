using AnimatedArtworkDownloader.Models;

namespace AnimatedArtworkDownloader.Configuration;

public class SyncConfig
{
    public string LibraryPath { get; set; } = string.Empty;
    public int RescanIntervalMinutes { get; set; } = 24 * 60;

    public string ArtworkApiUrl { get; set; } = string.Empty;
    public int DelayBetweenApiRequestsMs { get; set; } = 2000;

    public ArtworkVariant ArtworkVariants { get; set; } = ArtworkVariant.Square;

    public string FfmpegBinaryPath { get; set; } = "ffmpeg";

    public int MinVariantResolution { get; set; } = 1000;
    public int WebpQuality { get; set; } = 50;

    public string OutputFileName { get; set; } = "cover.webp";
    public string TallOutputFileName { get; set; } = "cover-tall.webp";

    public string GetOutputFileName(ArtworkVariant variant) => variant switch
    {
        ArtworkVariant.Square => OutputFileName,
        ArtworkVariant.Tall => TallOutputFileName,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Only single artwork variants have an output file name.")
    };

    public static bool IsValidOutputFileName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
               && fileName is not ("." or "..")
               && fileName == Path.GetFileName(fileName)
               && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}