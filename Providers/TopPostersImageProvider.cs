using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TopPosters.Providers;

public abstract class TopPostersImageProvider : IRemoteImageProvider, IHasOrder
{
    private readonly IHttpClientFactory _httpClientFactory;

    protected TopPostersImageProvider(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory;

    public string Name => "TOP Posters";
    public int Order => 0;

    public abstract bool Supports(BaseItem item);
    public abstract IEnumerable<ImageType> GetSupportedImages(BaseItem item);

    public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = Plugin.Instance?.Configuration;
        return Task.FromResult(config is null || !Supports(item)
            ? Enumerable.Empty<RemoteImageInfo>()
            : ArtworkUrls.ForItem(item, config));
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);

    protected static IEnumerable<ImageType> TitleImageTypes()
    {
        var config = Plugin.Instance?.Configuration;
        if (config?.EnablePosters != false) yield return ImageType.Primary;
        if (config?.EnableLogos == true) yield return ImageType.Logo;
        if (config?.EnableBackdrops == true) yield return ImageType.Backdrop;
    }
}
