using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TopPosters.Refresh;

public class RefreshHistory(IApplicationPaths paths, ILogger<RefreshHistory> logger)
{
    private readonly string _path = Path.Combine(paths.PluginConfigurationsPath, "topposters-refresh.json");

    public async Task<Dictionary<Guid, DateTime>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return [];
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<Dictionary<Guid, DateTime>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogWarning("TOP Posters refresh history could not be read ({ErrorType}); starting a new rotation.", ex.GetType().Name);
            return [];
        }
    }

    public async Task SaveAsync(Dictionary<Guid, DateTime> history)
    {
        var temp = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(history)).ConfigureAwait(false);
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("TOP Posters refresh history could not be saved ({ErrorType}).", ex.GetType().Name);
        }
    }
}
