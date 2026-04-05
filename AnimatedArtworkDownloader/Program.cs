using AnimatedArtworkDownloader;
using AnimatedArtworkDownloader.Configuration;
using AnimatedArtworkDownloader.Services;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
	.ReadFrom.Configuration(builder.Configuration)
	.ReadFrom.Services(services)
	.Enrich.FromLogContext());

builder.Services.Configure<SyncConfig>(builder.Configuration.GetSection("SyncConfig"));

builder.Services.AddHttpClient();
builder.Services.AddSingleton<LibraryScanner>();
builder.Services.AddSingleton<ArtworkApiClient>();
builder.Services.AddSingleton<IAnimatedCoverConverter, FfmpegAnimatedCoverConverter>();
builder.Services.AddSingleton<INegativeCoverCache, FileNegativeCoverCache>();
builder.Services.AddSingleton<CoverSyncOrchestrator>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();