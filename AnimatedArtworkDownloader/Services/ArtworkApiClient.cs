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
    private static readonly TimeSpan StartupProbeInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StartupProbeTimeout = TimeSpan.FromSeconds(3);

    public async Task WaitUntilApiIsReachableAsync(CancellationToken cancellationToken)
    {
        var baseUrl = config.Value.ArtworkApiUrl.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("ArtworkApiUrl is not configured.");
        }

        var client = httpClientFactory.CreateClient(ClientName);
        var attempts = 0;

        while (true)
        {
            attempts++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(StartupProbeTimeout);

                using var response = await client.GetAsync(baseUrl, timeoutCts.Token);
                logger.LogInformation("Artwork API is reachable at {BaseUrl} (status {StatusCode}) after {Attempts} attempt{Plural}.", baseUrl, (int)response.StatusCode, attempts, attempts == 1 ? string.Empty : "s");
                return;
            }
            catch (HttpRequestException ex)
            {
                logger.LogInformation("Waiting for Artwork API at {BaseUrl} (attempt {Attempt}): {Reason}", baseUrl, attempts, ex.Message);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Waiting for Artwork API at {BaseUrl} (attempt {Attempt} timed out after {TimeoutSeconds}s)...", baseUrl, attempts, (int)StartupProbeTimeout.TotalSeconds);
            }

            await Task.Delay(StartupProbeInterval, cancellationToken);
        }
    }

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