using Microsoft.Extensions.Options;
using AnimatedArtworkDownloader.Configuration;
using AnimatedArtworkDownloader.Models;

namespace AnimatedArtworkDownloader.Services;

public class LibraryScanner(IOptions<SyncConfig> config, ILogger<LibraryScanner> logger)
{
    private readonly string[] _supportedExtensions = [".mp3", ".flac", ".m4a", ".ogg", ".wma", ".aac", ".wav"];

    public IEnumerable<AlbumDirectory> ScanLibrary()
    {
        var basePath = config.Value.LibraryPath;

        if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
        {
            logger.LogError("Library path '{Path}' does not exist or is not configured.", basePath);
            yield break;
        }

        logger.LogInformation("Scanning library at '{Path}'...", basePath);

        var allDirectories = new List<string> { basePath };
        allDirectories.AddRange(Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories));
        var totalDirectories = allDirectories.Count;

        logger.LogInformation("Library scan will inspect {TotalDirectories} directories.", totalDirectories);

        for (var i = 0; i < totalDirectories; i++)
        {
            var dir = allDirectories[i];
            var current = i + 1;
            var progress = totalDirectories == 0 ? 1d : (double)current / totalDirectories;

            if (current == 1 || current == totalDirectories || current % 25 == 0)
            {
                logger.LogInformation(
                    "Library scan progress {ProgressBar} {Current}/{Total}",
                    RenderProgressBar(progress, 24),
                    current,
                    totalDirectories);
            }

            string? firstAudioFile;

            try
            {
                if (File.Exists(Path.Combine(dir, "cover.webp")))
                {
                    logger.LogDebug("Skipping dir '{Dir}': cover.webp already exists.", dir);
                    continue;
                }

                firstAudioFile = Directory.EnumerateFiles(dir)
                    .FirstOrDefault(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
            }
            catch (DirectoryNotFoundException)
            {
                logger.LogWarning("Skipping dir '{Dir}': directory no longer exists.", dir);
                continue;
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Skipping dir '{Dir}': access denied.", dir);
                continue;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Skipping dir '{Dir}': IO error while scanning.", dir);
                continue;
            }

            if (firstAudioFile == null)
            {
                continue;
            }
            
            string artist;
            string album;

            try
            {
                using var file = TagLib.File.Create(firstAudioFile);
                
                album = file.Tag.Album;
                artist = file.Tag.FirstAlbumArtist ?? file.Tag.FirstPerformer;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read metadata from '{File}'. Skipping...", firstAudioFile);
                continue;
            }
            
            if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(album))
            {
                yield return new AlbumDirectory(dir, artist.Trim(), album.Trim());
            }
            else
            {
                logger.LogDebug("Skipping '{File}': Missing Artist or Album tags.", firstAudioFile);
            }
        }

        logger.LogInformation("Library scan finished. Processed {TotalDirectories} directories.", totalDirectories);
    }

    private static string RenderProgressBar(double progress, int width)
    {
        var clampedProgress = Math.Clamp(progress, 0, 1);
        var completed = (int)Math.Round(clampedProgress * width, MidpointRounding.AwayFromZero);

        return $"[{new string('#', completed)}{new string('-', width - completed)}]";
    }
}