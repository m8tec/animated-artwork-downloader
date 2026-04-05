using AnimatedArtworkDownloader.Services;

namespace AnimatedArtworkDownloader;

public class Worker(ILogger<Worker> logger, LibraryScanner scanner, CoverSyncOrchestrator coverSyncOrchestrator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var albumsWithoutCover = scanner.ScanLibrary().ToList();

        logger.LogInformation("Found {Count} albums missing an animated cover.", albumsWithoutCover.Count);

        foreach (var album in albumsWithoutCover)
        {
            logger.LogDebug("Needs Cover: {Artist} - {Album} ({Path})", album.ArtistName, album.AlbumName, album.Path);
        }

        await coverSyncOrchestrator.ProcessMissingCoversAsync(albumsWithoutCover, stoppingToken);
    }
}