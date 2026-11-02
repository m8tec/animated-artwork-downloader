namespace AnimatedArtworkDownloader.Models;

public record ArtworkSearchResult(IReadOnlyDictionary<ArtworkVariant, string> PlaylistUrls, string? Reason)
{
    public static ArtworkSearchResult Found(IReadOnlyDictionary<ArtworkVariant, string> playlistUrls) => new(playlistUrls, null);

    public static ArtworkSearchResult NotFound(string reason) => new(new Dictionary<ArtworkVariant, string>(), reason);
}