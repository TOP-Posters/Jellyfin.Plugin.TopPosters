using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TopPosters.Providers;

public class SeriesImageProvider(IHttpClientFactory httpClientFactory) : TopPostersImageProvider(httpClientFactory)
{
    public override bool Supports(BaseItem item) => item is Series;
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item) => TitleImageTypes();
}
