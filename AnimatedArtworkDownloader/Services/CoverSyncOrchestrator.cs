using Microsoft.Extensions.Options;
using AnimatedArtworkDownloader.Configuration;
using AnimatedArtworkDownloader.Models;

namespace AnimatedArtworkDownloader.Services;

public class CoverSyncOrchestrator(
    ArtworkApiClient artworkApiClient,
    IAnimatedCoverConverter coverConverter,
    INegativeCoverCache negativeCoverCache,
    IOptions<SyncConfig> config,
    ILogger<CoverSyncOrchestrator> logger)
{
    public async Task ProcessMissingCoversAsync(IEnumerable<AlbumDirectory> albumsWithoutCover, CancellationToken cancellationToken)
    {
        var createdCount = 0;
        var processedCount = 0;

        var albumDirectories = albumsWithoutCover as AlbumDirectory[] ?? albumsWithoutCover.ToArray();
        foreach (var album in albumDirectories)
        {
            processedCount++;

            cancellationToken.ThrowIfCancellationRequested();

            if (negativeCoverCache.IsCachedAsMissing(album))
            {
                logger.LogDebug("Skipping {Artist} - {Album}: present in negative cache.", album.ArtistName, album.AlbumName);
                continue;
            }

            logger.LogInformation("[{current}/{total}] Searching animated cover for {Artist} - {Album}", processedCount, albumDirectories.Length, album.ArtistName, album.AlbumName);

            ArtworkSearchResult searchResult;
            try
            {
                searchResult = await artworkApiClient.SearchAnimatedCoverAsync(album.ArtistName, album.AlbumName, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Artwork API call failed for {Artist} - {Album}", album.ArtistName, album.AlbumName);
                continue;
            }

            if (!searchResult.HasAnimatedCover || string.IsNullOrWhiteSpace(searchResult.PlaylistUrl))
            {
                negativeCoverCache.StoreMissingCover(album, searchResult.Reason ?? "No animated cover returned");
                logger.LogInformation("No animated cover for {Artist} - {Album}. Cached as negative result.", album.ArtistName, album.AlbumName);
                await DelayBetweenRequestsAsync(cancellationToken);
                continue;
            }

            var outputPath = Path.Combine(album.Path, "cover.webp");

            try
            {
                await coverConverter.ConvertHlsToWebpAsync(searchResult.PlaylistUrl, outputPath, cancellationToken);
                negativeCoverCache.Remove(album);
                createdCount++;
                logger.LogInformation("Animated cover created at {OutputPath}", outputPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to convert HLS to WebP for {Artist} - {Album}", album.ArtistName, album.AlbumName);
            }

            await DelayBetweenRequestsAsync(cancellationToken);
        }

        logger.LogInformation("Cover processing completed. Created {Count} animated cover{Plural}.", createdCount, createdCount != 1 ? "s" : "");
    }

    private async Task DelayBetweenRequestsAsync(CancellationToken cancellationToken)
    {
        var delay = Math.Max(0, config.Value.DelayBetweenApiRequestsMs);
        if (delay == 0)
        {
            return;
        }

        await Task.Delay(delay, cancellationToken);
    }
}