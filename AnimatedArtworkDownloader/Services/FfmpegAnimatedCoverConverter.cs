using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using AnimatedArtworkDownloader.Configuration;

namespace AnimatedArtworkDownloader.Services;

public class FfmpegAnimatedCoverConverter(
    IHttpClientFactory httpClientFactory,
    IOptions<SyncConfig> config,
    ILogger<FfmpegAnimatedCoverConverter> logger) : IAnimatedCoverConverter
{
    private sealed record VariantCandidate(Uri Uri, int Width, int Height);

    public async Task ConvertHlsToWebpAsync(string playlistUrl, string outputFilePath, CancellationToken cancellationToken)
    {
        var ffmpegBinary = string.IsNullOrWhiteSpace(config.Value.FfmpegBinaryPath)
            ? "ffmpeg"
            : config.Value.FfmpegBinaryPath;
        var webpQuality = Math.Clamp(config.Value.WebpQuality, 0, 100);

        var selectedVariant = await ResolvePreferredVariantAsync(playlistUrl, cancellationToken);

        var outputDirectory = Path.GetDirectoryName(outputFilePath) ?? Path.GetTempPath();
        Directory.CreateDirectory(outputDirectory);

        var tempVideoPath = Path.Combine(outputDirectory, $".{Path.GetFileNameWithoutExtension(outputFilePath)}.{Guid.NewGuid():N}.source.mp4");
        var tempWebpPath = Path.Combine(outputDirectory, $".{Path.GetFileNameWithoutExtension(outputFilePath)}.{Guid.NewGuid():N}.tmp.webp");

        try
        {
            var remuxArgs =
                $"-y -i \"{selectedVariant.Uri}\" -c copy -bsf:a aac_adtstoasc \"{tempVideoPath}\"";
            await RunFfmpegAsync(ffmpegBinary, remuxArgs, "remux", cancellationToken);

            if (!File.Exists(tempVideoPath))
            {
                throw new InvalidOperationException($"ffmpeg remux completed without producing {tempVideoPath}");
            }

            var convertArgs =
                $"-y -i \"{tempVideoPath}\" -vf \"scale=-1:-1:flags=lanczos\" -c:v libwebp -compression_level 6 -q:v {webpQuality} -loop 0 -an \"{tempWebpPath}\"";
            await RunFfmpegAsync(ffmpegBinary, convertArgs, "webp-convert", cancellationToken);

            if (!File.Exists(tempWebpPath))
            {
                throw new InvalidOperationException($"ffmpeg conversion completed without producing {tempWebpPath}");
            }

            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            File.Move(tempWebpPath, outputFilePath);
            logger.LogDebug("Moved temp webp from {TempPath} to {FinalPath}", tempWebpPath, outputFilePath);
        }
        finally
        {
            CleanupWorkingFile(tempVideoPath);
            CleanupWorkingFile(tempWebpPath);
        }
    }

    private async Task<VariantCandidate> ResolvePreferredVariantAsync(string playlistUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(playlistUrl, UriKind.Absolute, out var playlistUri))
        {
            return new VariantCandidate(new Uri(playlistUrl, UriKind.RelativeOrAbsolute), 0, 0);
        }

        if (!playlistUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return new VariantCandidate(playlistUri, 0, 0);
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var playlistBody = await httpClient.GetStringAsync(playlistUri, cancellationToken);
            var lines = playlistBody.Split('\n', StringSplitOptions.TrimEntries);
            var variants = new List<VariantCandidate>();

            for (var i = 0; i < lines.Length; i++)
            {
                var streamInfoLine = lines[i];
                if (!streamInfoLine.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var (width, height) = ParseResolution(streamInfoLine);
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var candidate = lines[j];
                    if (string.IsNullOrWhiteSpace(candidate) || candidate.StartsWith('#'))
                    {
                        continue;
                    }

                    if (!Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteVariantUri))
                    {
                        absoluteVariantUri = new Uri(playlistUri, candidate);
                    }

                    variants.Add(new VariantCandidate(absoluteVariantUri, width, height));
                    break;
                }
            }

            if (variants.Count == 0)
            {
                return new VariantCandidate(playlistUri, 0, 0);
            }

            var minResolution = Math.Max(0, config.Value.MinVariantResolution);
            var matchingByResolution = variants
                .Where(v => minResolution <= 0 || (v.Width >= minResolution && v.Height >= minResolution))
                .ToList();

            VariantCandidate selectedVariant;
            if (matchingByResolution.Count > 0)
            {
                selectedVariant = matchingByResolution.
                    OrderBy(v => v.Width)
                    .First();
            }
            else
            {
                selectedVariant = variants.
                    OrderByDescending(v => v.Width)
                    .First();
            }

            logger.LogDebug(
                "Resolved master playlist to preferred variant: {VariantUrl} ({Width}x{Height}), min resolution {MinResolution}",
                selectedVariant.Uri.ToString(),
                selectedVariant.Width,
                selectedVariant.Height,
                minResolution);

            return selectedVariant;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not resolve master playlist variant. Falling back to original URL.");
        }

        return new VariantCandidate(playlistUri, 0, 0);
    }

    private async Task RunFfmpegAsync(string ffmpegBinary, string arguments, string phaseName, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting ffmpeg phase {Phase}.", phaseName);

        var effectiveArguments = $"-progress pipe:1 -nostats {arguments}";

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegBinary,
            Arguments = effectiveArguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var standardOutput = process.StandardOutput;
        var standardError = process.StandardError;

        var stdOutLines = new List<string>();
        var stdErrLines = new List<string>();
        var progressLock = new object();
        var totalDuration = (TimeSpan?)null;
        var lastProgressSummary = string.Empty;

        var stdOutTask = Task.Run(async () =>
        {
            long outTimeMicroseconds = 0;
            var speed = "?";
            var fps = "?";
            var frame = "?";

            while (true)
            {
                var line = await standardOutput.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    break;
                }

                stdOutLines.Add(line);

                if (line.StartsWith("out_time_ms=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line["out_time_ms=".Length..].Trim();
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOutTime))
                    {
                        outTimeMicroseconds = parsedOutTime;
                    }

                    continue;
                }

                if (line.StartsWith("speed=", StringComparison.OrdinalIgnoreCase))
                {
                    speed = line["speed=".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("fps=", StringComparison.OrdinalIgnoreCase))
                {
                    fps = line["fps=".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("frame=", StringComparison.OrdinalIgnoreCase))
                {
                    frame = line["frame=".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("progress=", StringComparison.OrdinalIgnoreCase))
                {
                    lock (progressLock)
                    {
                        lastProgressSummary = BuildProgressSummary(outTimeMicroseconds, totalDuration, speed, fps, frame);
                    }
                }
            }
        }, cancellationToken);

        var stdErrTask = Task.Run(async () =>
        {
            while (true)
            {
                var line = await standardError.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    break;
                }

                stdErrLines.Add(line);

                if (TryParseDuration(line, out var parsedDuration))
                {
                    lock (progressLock)
                    {
                        totalDuration = parsedDuration;
                    }
                }
            }
        }, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var nextHeartbeat = TimeSpan.FromSeconds(5);

        while (!process.HasExited)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            if (process.HasExited)
            {
                break;
            }

            if (stopwatch.Elapsed >= nextHeartbeat)
            {
                string progressSummary;
                lock (progressLock)
                {
                    progressSummary = lastProgressSummary;
                }

                if (!string.IsNullOrWhiteSpace(progressSummary))
                {
                    logger.LogInformation("ffmpeg phase {Phase} running for {ElapsedSeconds}s. {Progress}", phaseName, (int)stopwatch.Elapsed.TotalSeconds, progressSummary);
                }
                else
                {
                    logger.LogInformation("ffmpeg phase {Phase} running for {ElapsedSeconds}s.", phaseName, (int)stopwatch.Elapsed.TotalSeconds);
                }

                nextHeartbeat += TimeSpan.FromSeconds(5);
            }
        }

        await process.WaitForExitAsync(cancellationToken);

        await stdOutTask;
        await stdErrTask;

        logger.LogInformation("ffmpeg phase {Phase} finished in {ElapsedSeconds}s with exit code {ExitCode}.", phaseName, (int)stopwatch.Elapsed.TotalSeconds, process.ExitCode);

        if (process.ExitCode != 0)
        {
            var details = string.Join(Environment.NewLine, [TailLines(stdOutLines, 120), TailLines(stdErrLines, 120)]).Trim();
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}. {details}");
        }
    }

    private static bool TryParseDuration(string stderrLine, out TimeSpan duration)
    {
        const string marker = "Duration:";
        var markerIndex = stderrLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            duration = default;
            return false;
        }

        var durationPart = stderrLine[(markerIndex + marker.Length)..].TrimStart();
        var commaIndex = durationPart.IndexOf(',');
        if (commaIndex >= 0)
        {
            durationPart = durationPart[..commaIndex];
        }

        return TimeSpan.TryParse(durationPart, CultureInfo.InvariantCulture, out duration);
    }

    private static string BuildProgressSummary(long outTimeMicroseconds, TimeSpan? totalDuration, string speed, string fps, string frame)
    {
        var elapsedMediaTime = TimeSpan.FromMilliseconds(outTimeMicroseconds / 1000d);
        var normalizedSpeed = string.IsNullOrWhiteSpace(speed) ? "?" : speed;
        var normalizedFps = string.IsNullOrWhiteSpace(fps) ? "?" : fps;
        var normalizedFrame = string.IsNullOrWhiteSpace(frame) ? "?" : frame;

        if (!totalDuration.HasValue || totalDuration.Value <= TimeSpan.Zero)
        {
            return $"time={elapsedMediaTime:hh\\:mm\\:ss} frame={normalizedFrame} fps={normalizedFps} speed={normalizedSpeed}";
        }

        var progress = elapsedMediaTime.TotalSeconds / totalDuration.Value.TotalSeconds;
        progress = Math.Clamp(progress, 0, 1);

        return $"{RenderProgressBar(progress, 24)} {(progress * 100):0.0}% time={elapsedMediaTime:hh\\:mm\\:ss}/{totalDuration:hh\\:mm\\:ss} frame={normalizedFrame} fps={normalizedFps} speed={normalizedSpeed}";
    }

    private static string RenderProgressBar(double progress, int width)
    {
        var completed = (int)Math.Round(progress * width, MidpointRounding.AwayFromZero);
        completed = Math.Clamp(completed, 0, width);

        return $"[{new string('#', completed)}{new string('-', width - completed)}]";
    }

    private static string TailLines(IReadOnlyList<string> lines, int maxLines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var start = Math.Max(0, lines.Count - maxLines);
        return string.Join(Environment.NewLine, lines.Skip(start));
    }

    private static void CleanupWorkingFile(string workingOutputPath)
    {
        if (File.Exists(workingOutputPath))
        {
            File.Delete(workingOutputPath);
        }
    }

    private static (int Width, int Height) ParseResolution(string streamInfoLine)
    {
        var marker = "RESOLUTION=";
        var markerIndex = streamInfoLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return (0, 0);
        }

        var startIndex = markerIndex + marker.Length;
        var endIndex = streamInfoLine.IndexOf(',', startIndex);
        var resolutionPart = endIndex >= 0
            ? streamInfoLine[startIndex..endIndex]
            : streamInfoLine[startIndex..];

        var parts = resolutionPart.Split('x', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return (0, 0);
        }

        return int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height)
            ? (width, height)
            : (0, 0);
    }

}