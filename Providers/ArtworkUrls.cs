using System.Globalization;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TopPosters.Providers;

public static class ArtworkUrls
{
    public static IEnumerable<RemoteImageInfo> ForItem(BaseItem item, PluginConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey)) yield break;

        var owner = item switch
        {
            Season season => season.Series,
            Episode episode => episode.Series,
            Movie or Series => item,
            _ => null
        };
        if (owner is null) yield break;

        var tmdb = owner.GetProviderId(MetadataProvider.Tmdb);
        var imdb = owner.GetProviderId(MetadataProvider.Imdb);
        var idType = !string.IsNullOrWhiteSpace(tmdb) ? "tmdb" : "imdb";
        var id = idType == "tmdb" ? $"{(owner is Movie ? "movie" : "series")}-{tmdb}" : imdb;
        if (string.IsNullOrWhiteSpace(id)) yield break;

        var key = Uri.EscapeDataString(config.ApiKey.Trim());
        if (item is Episode ep)
        {
            if (config.EnableEpisodeThumbnails && ep.ParentIndexNumber is >= 0 && ep.IndexNumber is >= 1)
            {
                var numbers = string.Create(CultureInfo.InvariantCulture, $"S{ep.ParentIndexNumber:D2}E{ep.IndexNumber:D2}");
                yield return Image(ImageType.Primary, $"thumbnail/{Uri.EscapeDataString(id)}/{numbers}.jpg");
            }
            yield break;
        }

        if (item is Season s)
        {
            if (!config.EnableSeasonPosters || s.IndexNumber is not >= 0) yield break;
            id += "-s" + s.IndexNumber.Value.ToString(CultureInfo.InvariantCulture);
            yield return Image(ImageType.Primary, $"poster/{Uri.EscapeDataString(id)}.jpg");
            yield break;
        }

        if (config.EnablePosters) yield return Image(ImageType.Primary, $"poster/{Uri.EscapeDataString(id)}.jpg");
        if (config.EnableLogos) yield return Image(ImageType.Logo, $"logo/{Uri.EscapeDataString(id)}.png");
        if (config.EnableBackdrops) yield return Image(ImageType.Backdrop, $"background/{Uri.EscapeDataString(id)}.jpg");

        RemoteImageInfo Image(ImageType type, string path) => new()
        {
            ProviderName = "TOP Posters",
            Type = type,
            Url = $"{Plugin.BaseUrl}/{key}/{idType}/{path}?user_agent=jellyfin_client"
        };
    }
}
