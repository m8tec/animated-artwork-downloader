# Animated Artwork Downloader

Download animated artworks from Apple Music straight to your local music library as WebP images, using the [Apple Music Animated Artworks](https://github.com/m8tec/apple-music-animated-artworks) API.

The files can be used by Navidrome, a self-hosted music server, which supports showing animated artworks when they are stored as `cover.webp` in the album folder.

## Docker

The default Docker setup pulls published images from GHCR and does not require a local build:

```bash
docker compose up -d
```

For local development with source builds, use the dev override:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

That setup expects the API repository to be cloned next to this one as `../apple-music-animated-artworks`.

The default compose expects the API service to be reachable as `http://animated-artworks:8080` inside the Docker network.

## Features
- Download animated artworks for albums in your music library
- MP4 to WebP conversion using ffmpeg
- Supported by Navidrome
- Configurable minimum resolution for artworks
- Configurable WebP quality for artwork conversion