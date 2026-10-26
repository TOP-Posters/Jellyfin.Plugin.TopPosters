using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TopPosters;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public bool EnablePosters { get; set; } = true;
    public bool EnableSeasonPosters { get; set; } = true;
    public bool EnableEpisodeThumbnails { get; set; } = true;
    public bool EnableLogos { get; set; }
    public bool EnableBackdrops { get; set; }

    // Replacing existing artwork is opt-in, including when upgrading the plugin.
    public bool EnableArtworkRefresh { get; set; }
    public int RefreshBatchSize { get; set; } = 100;
}
