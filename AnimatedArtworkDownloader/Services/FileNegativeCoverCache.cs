using System.Text.Json;
using Microsoft.Extensions.Options;
using AnimatedArtworkDownloader.Configuration;
using AnimatedArtworkDownloader.Models;

namespace AnimatedArtworkDownloader.Services;

public class FileNegativeCoverCache(IOptions<SyncConfig> config, ILogger<FileNegativeCoverCache> logger) : INegativeCoverCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _syncRoot = new();
    private Dictionary<string, NegativeCacheEntry>? _entries;

    private const string NegativeCacheFileName = "negative-cache.json";

    public bool IsCachedAsMissing(AlbumDirectory album)
    {
        lock (_syncRoot)
        {
            EnsureLoaded();

            var key = BuildKey(album.ArtistName, album.AlbumName);
            if (_entries == null || !_entries.TryGetValue(key, out var entry))
            {
                return false;
            }

            return true;
        }
    }

    public void StoreMissingCover(AlbumDirectory album, string reason)
    {
        lock (_syncRoot)
        {
            EnsureLoaded();

            var key = BuildKey(album.ArtistName, album.AlbumName);
            _entries![key] = new NegativeCacheEntry(album.ArtistName, album.AlbumName, reason, DateTimeOffset.UtcNow);
            Persist();
        }
    }

    public void Remove(AlbumDirectory album)
    {
        lock (_syncRoot)
        {
            EnsureLoaded();

            var key = BuildKey(album.ArtistName, album.AlbumName);
            if (_entries!.Remove(key))
            {
                Persist();
            }
        }
    }

    private void EnsureLoaded()
    {
        if (_entries != null)
        {
            return;
        }

        var cacheFilePath = GetCacheFilePath();

        if (!File.Exists(cacheFilePath))
        {
            _entries = new Dictionary<string, NegativeCacheEntry>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var json = File.ReadAllText(cacheFilePath);
            var list = JsonSerializer.Deserialize<List<NegativeCacheEntry>>(json) ?? [];
            _entries = list.ToDictionary(e => BuildKey(e.ArtistName, e.AlbumName), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load negative cover cache. Starting with an empty cache.");
            _entries = new Dictionary<string, NegativeCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist()
    {
        var cacheFilePath = GetCacheFilePath();
        var cacheDirectory = Path.GetDirectoryName(cacheFilePath)!;
        Directory.CreateDirectory(cacheDirectory);

        var payload = _entries!.Values
            .OrderBy(e => e.ArtistName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.AlbumName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(cacheFilePath, json);
    }

    private string GetCacheFilePath()
    {
        var basePath = config.Value.LibraryPath;
        if (string.IsNullOrWhiteSpace(basePath))
        {
            basePath = AppContext.BaseDirectory;
        }

        return Path.Combine(basePath, ".animatedartworkdownloader", NegativeCacheFileName);
    }

    private static string BuildKey(string artist, string album)
        => $"{artist.Trim().ToLowerInvariant()}|{album.Trim().ToLowerInvariant()}";
}