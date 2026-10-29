using Jellyfin.Plugin.TopPosters.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.TopPosters.Refresh;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.TopPosters;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IImageProvider, MovieImageProvider>();
        serviceCollection.AddSingleton<IImageProvider, SeriesImageProvider>();
        serviceCollection.AddSingleton<IImageProvider, EpisodeImageProvider>();
        serviceCollection.AddSingleton<IImageProvider, SeasonImageProvider>();
        serviceCollection.AddSingleton<RefreshHistory>();
        serviceCollection.AddSingleton<ArtworkDownloader>();
        serviceCollection.AddSingleton<IScheduledTask, RefreshArtworkTask>();
    }
}
