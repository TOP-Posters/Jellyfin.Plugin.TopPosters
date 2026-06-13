using Jellyfin.Plugin.TopPosters.Providers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TopPosters;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IImageProvider, MovieImageProvider>();
        serviceCollection.AddSingleton<IImageProvider, SeriesImageProvider>();
        serviceCollection.AddSingleton<IImageProvider, EpisodeImageProvider>();
    }
}
