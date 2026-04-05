using AnimatedArtworkDownloader.Models;

namespace AnimatedArtworkDownloader.Services;

public interface INegativeCoverCache
{
    bool IsCachedAsMissing(AlbumDirectory album);
    void StoreMissingCover(AlbumDirectory album, string reason);
    void Remove(AlbumDirectory album);
}