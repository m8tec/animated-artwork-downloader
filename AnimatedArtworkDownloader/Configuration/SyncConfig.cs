namespace AnimatedArtworkDownloader.Configuration;

public class SyncConfig
{
    public string LibraryPath { get; set; } = string.Empty;
    public int RescanIntervalMinutes { get; set; } = 24 * 60;

    public string ArtworkApiUrl { get; set; } = string.Empty;
    public int DelayBetweenApiRequestsMs { get; set; } = 2000;

    public string FfmpegBinaryPath { get; set; } = "ffmpeg";

    public int MinVariantResolution { get; set; } = 1000;
    public int WebpQuality { get; set; } = 50;

    public string OutputFileName { get; set; } = "cover.webp";

    public static bool IsValidOutputFileName(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
               && fileName is not ("." or "..")
               && fileName == Path.GetFileName(fileName)
               && fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
}