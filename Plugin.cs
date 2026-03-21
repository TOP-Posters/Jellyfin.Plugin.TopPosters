using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TopPosters;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string BaseUrl = "https://test.top-streaming.stream";

    public static Plugin? Instance { get; private set; }

    public override string Name => "TOP Posters";

    public override Guid Id => Guid.Parse("a3f2e1d4-5b6c-7d8e-9f0a-1b2c3d4e5f6a");

    public override string Description => "Generates movie and TV posters with multi-source rating badges via TOP Posters API.";

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "topposters",
                EmbeddedResourcePath = $"{GetType().Namespace}.Web.topposters.html"
            },
            new PluginPageInfo
            {
                Name = "toppostersjs",
                EmbeddedResourcePath = $"{GetType().Namespace}.Web.topposters.js"
            }
        };
    }
}
