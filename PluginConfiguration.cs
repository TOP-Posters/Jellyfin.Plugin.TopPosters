using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TopPosters;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
}
