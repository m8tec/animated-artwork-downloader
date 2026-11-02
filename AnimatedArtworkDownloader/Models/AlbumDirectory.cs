namespace AnimatedArtworkDownloader.Models;

public record AlbumDirectory(
    string Path,
    string ArtistName,
    string AlbumName,
    IReadOnlyList<ArtworkVariant> MissingVariants
);