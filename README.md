# Animated Artwork Downloader

Automated downloader for Apple Music animated artworks in your local music library.
The tool regularly scans a specified directory for music albums and downloads the missing animated artworks as WebP images, using the [Apple Music Animated Artworks](https://github.com/m8tec/apple-music-animated-artworks) API for fetching the artworks.

The downloaded artworks are saved as `cover.webp` in the respective album folders and can be used by music servers like Navidrome for displaying.

> [!TIP]
> For manually downloading animated artworks as WebP or MP4, head over to the [public instance](https://artwork.m8tec.top) of the [Apple Music Animated Artworks](https://github.com/m8tec/apple-music-animated-artworks) API, which also has a simple search interface.

## Features
- Download animated artworks for albums in your music library
- MP4 to WebP conversion using ffmpeg
- Supported by Navidrome
- Configurable minimum resolution for artworks
- Configurable WebP quality for artwork conversion

## Getting Started (Docker)

```bash
# Clone the repository
git clone https://github.com/m8tec/animated-artwork-downloader.git
cd animated-artwork-downloader

# Configure
cp .env.example .env
nano .env  # Edit with your settings

# Start
docker-compose up -d

# Watch logs
docker-compose logs -f
```

This will start two containers: the downloader and the API. The downloader will periodically check your music library for albums without `cover.webp` and attempt to download the animated artwork for them. The API container runs the [Apple Music Animated Artworks API](https://github.com/m8tec/apple-music-animated-artworks), which the downloader uses to fetch the artworks. It can also be used at `http://localhost:8080` to manually query for artworks.

## How It Works

1. The downloader scans the specified music library directory for folders containing music files which do not have a `cover.webp` file.
2. For each album folder, it extracts the artist and album name from the metadata of a music file.
3. It queries the API for an animated artwork matching the artist and album.
4. If an artwork is found, it downloads the MP4 file, converts it to WebP format using ffmpeg, and saves it as `cover.webp` in the album folder.

## Development

For local development with source builds, use the dev override:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

That setup expects the API repository to be cloned next to this one as `../apple-music-animated-artworks`.

The default compose expects the API service to be reachable as `http://animated-artworks:8080` inside the Docker network.
