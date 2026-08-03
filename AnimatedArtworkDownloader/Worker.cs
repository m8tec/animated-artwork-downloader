using Microsoft.Extensions.Options;
using AnimatedArtworkDownloader.Configuration;
using AnimatedArtworkDownloader.Services;

namespace AnimatedArtworkDownloader;

public class Worker(
    ILogger<Worker> logger,
    LibraryScanner scanner,
    CoverSyncOrchestrator coverSyncOrchestrator,
    IOptions<SyncConfig> config,
    ArtworkApiClient artworkApiClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Waiting for artwork API before starting library scan...");
        await artworkApiClient.WaitUntilApiIsReachableAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Starting library sync run...");
                var albumsWithoutCover = scanner.ScanLibrary().ToList();

                logger.LogInformation("Found {Count} albums missing an animated cover.", albumsWithoutCover.Count);
                
                foreach (var album in albumsWithoutCover)
                {
                    logger.LogDebug("Needs Cover: {Artist} - {Album} ({Path})", album.ArtistName, album.AlbumName, album.Path);
                }

                await coverSyncOrchestrator.ProcessMissingCoversAsync(albumsWithoutCover, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "An error occurred during the sync run.");
            }

            var intervalMinutes = config.Value.RescanIntervalMinutes;

            if (intervalMinutes <= 0)
            {
                logger.LogInformation("Periodic rescanning is disabled (RescanIntervalMinutes <= 0). Stopping...");
                break;
            }
            else
            {
                logger.LogInformation("Sleeping for {IntervalMinutes} minutes until the next scan...", intervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }
    }
}