using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TopPosters.Providers;

public class SeriesImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string Name => "Top Posters";

    public int Order => 0;

    public SeriesImageProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool Supports(BaseItem item) => item is Series;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        => new[] { ImageType.Primary };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var apiKey = Plugin.Instance?.Configuration.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return Enumerable.Empty<RemoteImageInfo>();

        var url = BuildUrl(item, apiKey);
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

    private static string? BuildUrl(BaseItem item, string apiKey)
    {
        var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);
        if (!string.IsNullOrEmpty(tmdbId))
            return $"{Plugin.BaseUrl}/{apiKey}/tmdb/poster/series-{tmdbId}.jpg";

        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdbId))
            return $"{Plugin.BaseUrl}/{apiKey}/imdb/poster/{imdbId}.jpg";

        return null;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient().GetAsync(url, cancellationToken);
}
