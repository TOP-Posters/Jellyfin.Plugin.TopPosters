using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TopPosters.Providers;

public class EpisodeImageProvider(IHttpClientFactory httpClientFactory) : TopPostersImageProvider(httpClientFactory)
{
    public override bool Supports(BaseItem item) => item is Episode;
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item) => Plugin.Instance?.Configuration.EnableEpisodeThumbnails != false ? [ImageType.Primary] : [];
}
