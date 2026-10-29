# TOP Posters for Jellyfin

Artwork provider for Jellyfin 12 / .NET 10. Movie and series posters, season
covers, episode thumbnails, transparent title logos and metadata backgrounds
come from your TOP Posters API key. Appearance follows your TOP Posters dashboard.
The server appears there as the **Jellyfin Server** device: its settings apply to
everyone who watches through it, because Jellyfin stores the images and serves the
same ones to every client.

## Set up artwork

1. Add the repository below in **Dashboard → Plugins → Repositories**, install
   TOP Posters from the plugin catalog, and restart Jellyfin. Check that the
   selected release supports your server version. Artwork options and scheduled
   refresh described here require plugin **1.2.0 or later for Jellyfin 12**.
   Jellyfin 10.9–10.11 servers get 1.0.10, with movie and series posters and
   episode thumbnails.
2. Open **Dashboard → Plugins → TOP Posters**, enter the API key and choose the
   artwork types. Movie/series posters, seasons and episode thumbnails are enabled
   by default. Logos and backgrounds are optional and disabled by default.
3. Enable TOP Posters as an image fetcher in each relevant Jellyfin library for
   Movies, Series, **Seasons** and Episodes. Move it above other image fetchers
   when TOP Posters should supply the artwork during normal library scans.
4. Premium is required for episode thumbnails, logos and backgrounds. Enable logos
   and backgrounds in the TOP Posters dashboard as well as in the plugin.

Repository URL:

```text
https://raw.githubusercontent.com/TOP-Posters/Jellyfin.Plugin.TopPosters/main/manifest.json
```

Season covers use the series provider ID with a `-sN` suffix. Episodes use the
series ID and their season/episode numbers, including season zero for specials.
TMDB is preferred; IMDb is used when the item has no TMDB ID.

## Refresh images already in Jellyfin

Enable **Artwork refresh** in the plugin settings. This is off by default, also
on upgrades. The task **Refresh TOP Posters artwork** appears in **Dashboard →
Scheduled Tasks → TOP Posters**, with a default interval of 24 hours. Jellyfin's
task screen controls the schedule, cancellation, progress and last result.
**Save and refresh now** saves your settings and starts the next batch immediately.

- Refresh **replaces the selected images**, including images manually chosen or
  supplied by another provider, in libraries with TOP Posters enabled. Lock any
  items you want to preserve. Metadata, media files and watch progress are not
  refreshed. Only backdrop index zero is replaced; other backdrops remain.
- Each run checks up to **100 titles** by default (configurable from 1 to 1,000).
  Films, series, seasons and episodes each count as a title. A film or series can
  make up to three image requests when posters, logos and backgrounds are all on.
  Requests count against your API quota even when an image has not changed.
- A rotation persisted in the Jellyfin plugin configuration directory checks the
  least recently checked items first. Larger libraries take multiple runs to
  update completely. After changing your dashboard appearance, run another batch
  or increase the batch size to cover the desired library sooner.
- The plugin compares local image content with the API response, uses conditional
  requests, and revalidates intermediary caches. Identical images are not saved
  again. Updated images go through Jellyfin's image storage and cache update APIs.
  Reopen or reload a library screen if a client is still displaying an old image.
- A library scan in progress defers the task. Locked items, disabled providers,
  disabled artwork and image types whose library limit is zero are skipped.
- Missing or unavailable artwork keeps the current image. Authentication errors,
  rate limits and server errors stop the run, preserving its completed rotation.
  Network failures and timeouts skip that image. Requests have a 30-second timeout
  and a 32 MB download limit. API keys are not written to the rotation file or logs.

## Build and verify

```sh
dotnet build Jellyfin.Plugin.TopPosters.csproj -c Release
dotnet run --project Tests/TopPosters.Tests.csproj
node Tests/settings.test.mjs
```

The .NET test executable uses the Jellyfin interfaces and an in-memory HTTP
handler, with no running server or real API requests. It covers artwork URLs,
conditional requests, download failures, opt-in, batch limits and persisted
rotation. Installation and client image refresh should also be verified on a
Jellyfin 12 test server before publishing a release.
