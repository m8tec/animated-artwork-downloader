using AnimatedArtworkDownloader.Models;

namespace AnimatedArtworkDownloader.Services;

public interface INegativeCoverCache
{
    bool IsCachedAsMissing(AlbumDirectory album, ArtworkVariant variant);
    void StoreMissingCover(AlbumDirectory album, ArtworkVariant variant, string reason);
    void Remove(AlbumDirectory album, ArtworkVariant variant);
}