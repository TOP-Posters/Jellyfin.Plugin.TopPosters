using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TopPosters.Providers;

public class MovieImageProvider(IHttpClientFactory httpClientFactory) : TopPostersImageProvider(httpClientFactory)
{
    public override bool Supports(BaseItem item) => item is Movie;
    public override IEnumerable<ImageType> GetSupportedImages(BaseItem item) => TitleImageTypes();
}
