namespace AnimatedArtworkDownloader.Models;

public record NegativeCacheEntry(string ArtistName, string AlbumName, string Reason, DateTimeOffset LastCheckedUtc);