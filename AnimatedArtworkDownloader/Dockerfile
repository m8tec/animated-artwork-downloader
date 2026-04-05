FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AnimatedArtworkDownloader/AnimatedArtworkDownloader.csproj AnimatedArtworkDownloader/
RUN dotnet restore AnimatedArtworkDownloader/AnimatedArtworkDownloader.csproj

COPY . .
RUN dotnet publish AnimatedArtworkDownloader/AnimatedArtworkDownloader.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish/ .

ENTRYPOINT ["dotnet", "AnimatedArtworkDownloader.dll"]
