namespace AnimatedArtworkDownloader.Models;

public record ArtworkSearchResult(bool HasAnimatedCover, string? PlaylistUrl, string? Reason)
{
    public static ArtworkSearchResult Found(string playlistUrl) => new(true, playlistUrl, null);

    public static ArtworkSearchResult NotFound(string reason) => new(false, null, reason);
}