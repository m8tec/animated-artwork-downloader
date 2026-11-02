namespace AnimatedArtworkDownloader.Models;

[Flags]
public enum ArtworkVariant
{
    None = 0,
    Square = 1,
    Tall = 2
}

public static class ArtworkVariantExtensions
{
    public static IReadOnlyList<ArtworkVariant> GetSingleVariants(this ArtworkVariant variants)
        => Enum.GetValues<ArtworkVariant>()
            .Where(v => v != ArtworkVariant.None && variants.HasFlag(v))
            .ToArray();
}
