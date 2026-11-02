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

            var variantsToSearch = album.MissingVariants
                .Where(v => !negativeCoverCache.IsCachedAsMissing(album, v))
                .ToArray();

            if (variantsToSearch.Length == 0)
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

            foreach (var variant in variantsToSearch)
            {
                if (!searchResult.PlaylistUrls.TryGetValue(variant, out var playlistUrl))
                {
                    negativeCoverCache.StoreMissingCover(album, variant, searchResult.Reason ?? "No animated cover returned");
                    logger.LogInformation("No {Variant} animated cover for {Artist} - {Album}. Cached as negative result.", variant, album.ArtistName, album.AlbumName);
                    continue;
                }

                if (await TryCreateCoverAsync(album, variant, playlistUrl, cancellationToken))
                {
                    createdCount++;
                }
            }

            await DelayBetweenRequestsAsync(cancellationToken);
        }

        logger.LogInformation("Cover processing completed. Created {Count} animated cover{Plural}.", createdCount, createdCount != 1 ? "s" : "");
    }

    private async Task<bool> TryCreateCoverAsync(AlbumDirectory album, ArtworkVariant variant, string playlistUrl, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(album.Path, config.Value.GetOutputFileName(variant));

        try
        {
            await coverConverter.ConvertHlsToWebpAsync(playlistUrl, outputPath, cancellationToken);
            negativeCoverCache.Remove(album, variant);
            logger.LogInformation("Animated {Variant} cover created at {OutputPath}", variant, outputPath);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to convert HLS to WebP for {Variant} cover of {Artist} - {Album}", variant, album.ArtistName, album.AlbumName);
            return false;
        }
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