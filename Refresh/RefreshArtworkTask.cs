using System.Net;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TopPosters.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TopPosters.Refresh;

public class RefreshArtworkTask(
    ILibraryManager libraryManager,
    IProviderManager providerManager,
    IFileSystem fileSystem,
    ArtworkDownloader downloader,
    RefreshHistory historyStore,
    ILogger<RefreshArtworkTask> logger) : IScheduledTask
{
    public const string TaskKey = "TopPostersRefreshArtwork";
    public string Name => "Refresh TOP Posters artwork";
    public string Key => TaskKey;
    public string Description => "Refresh selected artwork in libraries with TOP Posters enabled. Existing images are replaced; locked items are skipped.";
    public string Category => "TOP Posters";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new() { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(24).Ticks }];

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config?.EnableArtworkRefresh != true || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            logger.LogInformation("TOP Posters refresh is disabled or has no API key. Enable it in the plugin settings.");
            progress.Report(100);
            return;
        }
        if (libraryManager.IsScanRunning)
        {
            logger.LogInformation("TOP Posters refresh deferred while a library scan is running.");
            progress.Report(100);
            return;
        }

        var history = await historyStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var ids = libraryManager.GetItemIds(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Season, BaseItemKind.Episode],
            IsVirtualItem = false,
            IsLocked = false,
            Recursive = true
        });
        var existing = ids.ToHashSet();
        foreach (var id in history.Keys.Where(id => !existing.Contains(id)).ToArray()) history.Remove(id);
        // Oldest checked first, so small batches eventually cover even very large libraries.
        var queue = ids.OrderBy(id => history.GetValueOrDefault(id)).ThenBy(id => id).ToArray();
        var limit = Math.Clamp(config.RefreshBatchSize, 1, 1000);
        var checkedItems = 0;
        var updatedImages = 0;
        var denied = new HashSet<string>();
        var refreshOptions = new ImageRefreshOptions(new DirectoryService(fileSystem))
        {
            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
            IsAutomated = true
        };
        try
        {
            foreach (var id in queue)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (checkedItems >= limit) break;
                var item = libraryManager.GetItemById(id);
                if (item is null || item.IsLocked) continue;
                // Use Jellyfin's own provider selection, including library enable/disable settings.
                if (!providerManager.GetImageProviders(item, refreshOptions).OfType<TopPostersImageProvider>().Any()) continue;
                var library = libraryManager.GetLibraryOptions(item);
                if (!library.Enabled) continue;
                var typeOptions = library.TypeOptions.FirstOrDefault(o => o.Type == item.GetType().Name);
                var images = ArtworkUrls.ForItem(item, config)
                    .Where(image => typeOptions?.ImageOptions.FirstOrDefault(o => o.Type == image.Type)?.Limit != 0)
                    .ToArray();
                if (images.Length == 0) continue;

                foreach (var image in images)
                {
                    var resource = new Uri(image.Url).AbsolutePath.Split('/')[3];
                    if (denied.Contains(resource)) continue;
                    try
                    {
                        var current = item.GetImageInfo(image.Type, 0);
                        var result = await downloader.DownloadAsync(image.Url, current?.Path, cancellationToken).ConfigureAwait(false);
                        if (result.Status == HttpStatusCode.Unauthorized)
                            throw new RefreshUnavailableException("TOP Posters rejected the API key. Check the key and plan in the plugin settings.");
                        if (result.Status == HttpStatusCode.TooManyRequests)
                            throw new RefreshUnavailableException("TOP Posters request limit reached. The rotation is saved; run the task again after the limit resets.");
                        if ((int)result.Status >= 500)
                            throw new RefreshUnavailableException("TOP Posters is temporarily unavailable. The rotation is saved; try again later.");
                        if (result.Status == HttpStatusCode.Forbidden)
                        {
                            denied.Add(resource);
                            logger.LogWarning("TOP Posters {Resource} is unavailable for this key; skipping it for this run.", resource);
                            continue;
                        }
                        if (result.Body is null) continue; // 304, missing artwork or an unavailable feature.
                        cancellationToken.ThrowIfCancellationRequested();
                        if (item.IsLocked) continue;
                        await using var body = new MemoryStream(result.Body, writable: false);
                        await providerManager.SaveImage(item, body, result.MediaType!, image.Type, 0, cancellationToken).ConfigureAwait(false);
                        await libraryManager.UpdateImagesAsync(item, true).ConfigureAwait(false);
                        updatedImages++;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                    {
                        // Never log the request URI: it contains the user's API key.
                        logger.LogWarning("TOP Posters could not refresh {ImageType} for {ItemId} ({ErrorType}); keeping the existing image.", image.Type, id, ex.GetType().Name);
                    }
                }
                history[id] = DateTime.UtcNow;
                checkedItems++;
                if (checkedItems % 10 == 0) await historyStore.SaveAsync(history).ConfigureAwait(false);
                progress.Report(100d * checkedItems / Math.Min(limit, queue.Length));
            }
            progress.Report(100);
        }
        finally
        {
            // Save even after cancellation or a quota error, so the next run resumes the rotation.
            await historyStore.SaveAsync(history).ConfigureAwait(false);
            logger.LogInformation("TOP Posters refresh checked {CheckedItems} items and updated {UpdatedImages} images.", checkedItems, updatedImages);
        }
    }

    private sealed class RefreshUnavailableException(string message) : InvalidOperationException(message);
}
