using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TopPosters.Providers;

public class EpisodeImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string Name => "TOP Posters";

    public int Order => 0;

    public EpisodeImageProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool Supports(BaseItem item) => item is Episode;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        => new[] { ImageType.Primary };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var apiKey = Plugin.Instance?.Configuration.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return Enumerable.Empty<RemoteImageInfo>();

        if (item is not Episode episode)
            return Enumerable.Empty<RemoteImageInfo>();

        var seasonNumber = episode.ParentIndexNumber;
        var episodeNumber = episode.IndexNumber;

        if (seasonNumber is null || episodeNumber is null)
            return Enumerable.Empty<RemoteImageInfo>();

        var url = BuildUrl(episode, apiKey, seasonNumber.Value, episodeNumber.Value);
        if (url is null)
            return Enumerable.Empty<RemoteImageInfo>();

        return new[]
        {
            new RemoteImageInfo
            {
                ProviderName = Name,
                Url = url,
                Type = ImageType.Primary
            }
        };
    }

    private static string? BuildUrl(Episode episode, string apiKey, int season, int episodeNum)
    {
        var s = season.ToString("D2");
        var e = episodeNum.ToString("D2");

        // TMDB series ID from the parent series
        var tmdbId = episode.Series?.GetProviderId(MetadataProvider.Tmdb);
        if (!string.IsNullOrEmpty(tmdbId))
            return $"{Plugin.BaseUrl}/{apiKey}/tmdb/thumbnail/series-{tmdbId}/S{s}E{e}.jpg";

        // Fallback: IMDB ID of the series
        var imdbId = episode.Series?.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdbId))
            return $"{Plugin.BaseUrl}/{apiKey}/imdb/thumbnail/{imdbId}/S{s}E{e}.jpg";

        return null;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient().GetAsync(url, cancellationToken);
}
