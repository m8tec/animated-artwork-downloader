namespace AnimatedArtworkDownloader.Models;

// Variant defaults to Square so cache files written before tall artwork support stay valid.
public record NegativeCacheEntry(string ArtistName, string AlbumName, string Reason, DateTimeOffset LastCheckedUtc, ArtworkVariant Variant = ArtworkVariant.Square);