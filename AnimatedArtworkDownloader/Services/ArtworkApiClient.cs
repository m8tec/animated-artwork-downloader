using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using AnimatedArtworkDownloader.Configuration;
using AnimatedArtworkDownloader.Models;
using System.Text.Json;

namespace AnimatedArtworkDownloader.Services;

public class ArtworkApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SyncConfig> config,
    ILogger<ArtworkApiClient> logger)
{
    private const string ClientName = "ArtworkApi";

    public async Task<ArtworkSearchResult> SearchAnimatedCoverAsync(string artist, string album, CancellationToken cancellationToken)
    {
        var baseUrl = config.Value.ArtworkApiUrl.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return ArtworkSearchResult.NotFound("ArtworkApiUrl not configured");
        }

        var endpoint = BuildSearchEndpoint(baseUrl, artist, album);
        var client = httpClientFactory.CreateClient(ClientName);

        using var response = await client.GetAsync(endpoint, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return ArtworkSearchResult.NotFound("API returned 404");
        }

        if (!response.IsSuccessStatusCode)
        {
            var reason = $"API returned {(int)response.StatusCode} ({response.ReasonPhrase})";
            logger.LogWarning("Artwork search failed for {Artist} - {Album}: {Reason}", artist, album, reason);
            return ArtworkSearchResult.NotFound(reason);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("url", out var playlistUrlElement))
        {
            return ArtworkSearchResult.NotFound("No url property in API response");
        }

        var playlistUrl = playlistUrlElement.GetString();
        if (string.IsNullOrWhiteSpace(playlistUrl))
        {
            return ArtworkSearchResult.NotFound("No m3u8 URL in API response");
        }

        return ArtworkSearchResult.Found(playlistUrl);
    }

    private static string BuildSearchEndpoint(string baseUrl, string artist, string album)
    {
        var normalizedBase = baseUrl.EndsWith('/') ? baseUrl[..^1] : baseUrl;

        var builder = new StringBuilder();
        builder.Append(normalizedBase);
        builder.Append("/api/v1/artwork/search?artist=");
        builder.Append(Uri.EscapeDataString(artist));
        builder.Append("&album=");
        builder.Append(Uri.EscapeDataString(album));
        return builder.ToString();
    }
}