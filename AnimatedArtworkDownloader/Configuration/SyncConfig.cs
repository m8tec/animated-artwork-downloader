namespace AnimatedArtworkDownloader.Configuration;

public class SyncConfig
{
    public string LibraryPath { get; set; } = string.Empty;
    public string ArtworkApiUrl { get; set; } = string.Empty;
    public int DelayBetweenApiRequestsMs { get; set; } = 2000;
    public string FfmpegBinaryPath { get; set; } = "ffmpeg";
    public int MinVariantResolution { get; set; } = 1000;
    public int WebpQuality { get; set; } = 50;
}