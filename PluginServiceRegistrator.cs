using Jellyfin.Plugin.TopPosters.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TopPosters;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<MovieImageProvider>();
        serviceCollection.AddSingleton<SeriesImageProvider>();
        serviceCollection.AddSingleton<EpisodeImageProvider>();
    }
}
