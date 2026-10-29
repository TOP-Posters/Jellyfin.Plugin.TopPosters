using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TopPosters.Providers;

public class SeasonImageProvider(IHttpClientFactory httpClientFactory) : TopPostersImageProvider(httpClientFactory)
{
    public override bool Supports(BaseItem item) => item is Season;
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item) => Plugin.Instance?.Configuration.EnableSeasonPosters != false ? [ImageType.Primary] : [];
}
