namespace AnimatedArtworkDownloader.Services;

public interface IAnimatedCoverConverter
{
    Task ConvertHlsToWebpAsync(string playlistUrl, string outputFilePath, CancellationToken cancellationToken);
}