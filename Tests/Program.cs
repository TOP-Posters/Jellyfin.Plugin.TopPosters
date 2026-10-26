using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using Jellyfin.Plugin.TopPosters;
using Jellyfin.Plugin.TopPosters.Providers;
using Jellyfin.Plugin.TopPosters.Refresh;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging.Abstractions;

var root = Path.Combine(Path.GetTempPath(), "topposters-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var passed = 0;
try
{
    var config = new PluginConfiguration { ApiKey = "TP-test" };
    Check(config.EnablePosters && config.EnableSeasonPosters && config.EnableEpisodeThumbnails && !config.EnableLogos && !config.EnableBackdrops && !config.EnableArtworkRefresh, "upgrade defaults preserve existing providers and require refresh opt-in");
    var movie = new Movie { Id = Guid.NewGuid(), Name = "Movie" };
    movie.SetProviderId(MetadataProvider.Tmdb, "550");
    movie.SetProviderId(MetadataProvider.Imdb, "tt0137523");
    var images = ArtworkUrls.ForItem(movie, config).ToArray();
    Check(images.Length == 1 && images[0].Url.EndsWith("/tmdb/poster/movie-550.jpg?user_agent=jellyfin_client"), "movie uses typed TMDB ID and the Jellyfin device profile");
    config.EnableLogos = config.EnableBackdrops = true;
    images = ArtworkUrls.ForItem(movie, config).ToArray();
    Check(images.Select(i => i.Type).SequenceEqual(new[] { ImageType.Primary, ImageType.Logo, ImageType.Backdrop }) && images[1].Url.Contains("/logo/movie-550.png") && images[2].Url.Contains("/background/movie-550.jpg"), "optional artwork uses the matching PNG and JPEG endpoints");
    movie.ProviderIds.Remove("Tmdb");
    Check(ArtworkUrls.ForItem(movie, config).First().Url.Contains("/imdb/poster/tt0137523.jpg"), "IMDb fallback works without TMDB");
    movie.ProviderIds.Clear();
    Check(!ArtworkUrls.ForItem(movie, config).Any(), "missing identifiers produce no unusable image URLs");

    var series = new Series { Id = Guid.NewGuid(), Name = "Series" };
    series.SetProviderId(MetadataProvider.Tmdb, "1396");
    BaseItem.LibraryManager = Stub.Create<ILibraryManager>((method, args) => method.Name == "GetItemById" ? series : null);
    var season = new Season { Id = Guid.NewGuid(), SeriesId = series.Id, IndexNumber = 0 };
    season.SetParent(series);
    var episode = new Episode { Id = Guid.NewGuid(), SeriesId = series.Id, ParentIndexNumber = 0, IndexNumber = 1 };
    episode.SetParent(season);
    Check(ArtworkUrls.ForItem(season, config).Single().Url.Contains("/poster/series-1396-s0.jpg"), "season zero uses the parent series ID and the season suffix");
    Check(ArtworkUrls.ForItem(episode, config).Single().Url.Contains("/thumbnail/series-1396/S00E01.jpg"), "specials use valid season-zero episode numbering");
    episode.IndexNumber = null;
    Check(!ArtworkUrls.ForItem(episode, config).Any(), "episodes with missing numbers are skipped");
    config.EnableSeasonPosters = false;
    Check(!ArtworkUrls.ForItem(season, config).Any(), "season artwork respects its switch");
    config.ApiKey = " ";
    Check(!ArtworkUrls.ForItem(series, config).Any(), "blank keys never produce image candidates");

    var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    var localPath = Path.Combine(root, "current.png");
    await File.WriteAllBytesAsync(localPath, png);
    var http = new FakeHttp(request => new HttpResponseMessage(HttpStatusCode.NotModified));
    var downloader = new ArtworkDownloader(http);
    var reply = await downloader.DownloadAsync("https://api.top-posters.com/TP-test/tmdb/poster/movie-1.jpg", localPath, default);
    var expectedEtag = "\"" + Convert.ToHexStringLower(MD5.HashData(png)) + "\"";
    Check(reply.Status == HttpStatusCode.NotModified && http.LastEtag == expectedEtag && http.NoCache, "refresh sends the local content ETag and revalidates intermediary caches");
    http.Reply = _ => ImageResponse(png);
    reply = await downloader.DownloadAsync("https://api.top-posters.com/image", localPath, default);
    Check(reply.Status == HttpStatusCode.NotModified && reply.Body is null, "identical 200 responses do not rewrite files");
    reply = await downloader.DownloadAsync("https://api.top-posters.com/image", null, default);
    Check(reply.Body?.SequenceEqual(png) == true && http.LastEtag is null, "missing local images use an unconditional request");
    http.Reply = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not an image") };
    await Throws<InvalidDataException>(() => downloader.DownloadAsync("https://api.top-posters.com/image", localPath, default), "non-image responses are rejected");
    http.Reply = _ => ImageResponse([1, 2, 3]);
    await Throws<InvalidDataException>(() => downloader.DownloadAsync("https://api.top-posters.com/image", localPath, default), "invalid image signatures are rejected");
    http.Reply = _ => { var r = ImageResponse(png); r.Content.Headers.ContentLength = 33L * 1024 * 1024; return r; };
    await Throws<InvalidDataException>(() => downloader.DownloadAsync("https://api.top-posters.com/image", localPath, default), "oversized downloads are rejected before reading the body");
    Check((await File.ReadAllBytesAsync(localPath)).SequenceEqual(png), "failed downloads preserve the existing file");
    http.Reply = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    Check((await downloader.DownloadAsync("https://api.top-posters.com/image", localPath, default)).Status == HttpStatusCode.TooManyRequests, "quota responses reach the task without retrying");
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        await Throws<OperationCanceledException>(() => downloader.DownloadAsync("https://api.top-posters.com/image", localPath, cancelled.Token), "cancellation stops refresh I/O");
    }

    var paths = Stub.Create<IApplicationPaths>((method, _) => method.ReturnType == typeof(string) ? root : null);
    var serializer = Stub.Create<IXmlSerializer>((method, _) => method.Name.StartsWith("Deserialize") ? new PluginConfiguration() : null);
    var plugin = new Plugin(paths, serializer);
    config = plugin.Configuration;
    config.ApiKey = "TP-test";
    config.RefreshBatchSize = 1;
    var a = new Movie { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "A" };
    var b = new Movie { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "B" };
    var locked = new Movie { Id = Guid.NewGuid(), IsLocked = true };
    var disabled = new Movie { Id = Guid.NewGuid() };
    foreach (var item in new[] { a, b, locked, disabled }) item.SetProviderId(MetadataProvider.Tmdb, "1");
    var items = new[] { a, b, locked, disabled }.ToDictionary(i => i.Id);
    var saved = new List<Guid>();
    var imageUpdates = 0;
    var libraryOptions = new LibraryOptions();
    var library = Stub.Create<ILibraryManager>((method, args) => method.Name switch
    {
        "get_IsScanRunning" => false,
        "GetItemIds" => items.Keys.ToArray(),
        "GetItemById" => items.GetValueOrDefault((Guid)args![0]!),
        "GetLibraryOptions" => libraryOptions,
        "UpdateImagesAsync" => Updated(),
        _ => null
    });
    BaseItem.LibraryManager = library;
    http.Reply = _ => ImageResponse(png);
    var provider = new MovieImageProvider(http);
    var manager = Stub.Create<IProviderManager>((method, args) =>
    {
        if (method.Name == "GetImageProviders") return ((BaseItem)args![0]!).Id == disabled.Id ? Array.Empty<IImageProvider>() : new IImageProvider[] { provider };
        if (method.Name == "SaveImage")
        {
            var item = (BaseItem)args![0]!;
            var stream = (Stream)args[1]!;
            var type = (ImageType)args[3]!;
            var path = Path.Combine(root, item.Id + "-" + type + ".png");
            using var file = File.Create(path);
            stream.CopyTo(file);
            item.ImageInfos = item.ImageInfos.Where(i => i.Type != type).Append(new ItemImageInfo { Path = path, Type = type }).ToArray();
            saved.Add(item.Id);
            return Task.CompletedTask;
        }
        return null;
    });
    var history = new RefreshHistory(paths, NullLogger<RefreshHistory>.Instance);
    var task = new RefreshArtworkTask(library, manager, Stub.Create<IFileSystem>((_, _) => null), downloader, history, NullLogger<RefreshArtworkTask>.Instance);
    var progress = new QuietProgress();
    await task.ExecuteAsync(progress, default);
    Check(saved.Count == 0, "scheduled refresh is inactive until explicitly enabled");
    config.EnableArtworkRefresh = true;
    await task.ExecuteAsync(progress, default);
    Check(saved.SequenceEqual(new[] { a.Id }) && imageUpdates == 1, "one-title batches update images through Jellyfin without a metadata refresh");
    var resumed = new RefreshArtworkTask(library, manager, Stub.Create<IFileSystem>((_, _) => null), downloader, new RefreshHistory(paths, NullLogger<RefreshHistory>.Instance), NullLogger<RefreshArtworkTask>.Instance);
    await resumed.ExecuteAsync(progress, default);
    Check(saved.SequenceEqual(new[] { a.Id, b.Id }), "persisted rotation resumes with the next title and skips locked or disabled items");
    await resumed.ExecuteAsync(progress, default);
    Check(saved.Count == 2, "unchanged artwork does not call SaveImage or invalidate Jellyfin image caches");
    http.Reply = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests);
    await Throws<InvalidOperationException>(() => resumed.ExecuteAsync(progress, default), "quota errors stop the task visibly instead of scanning the entire library");
    Check((await history.LoadAsync(default)).Count == 2, "completed progress survives quota failures");

    config.RefreshBatchSize = 2;
    config.EnableLogos = config.EnableBackdrops = true;
    var logoRequests = 0;
    http.Reply = request =>
    {
        if (request.RequestUri!.AbsolutePath.Contains("/logo/")) { logoRequests++; return new(HttpStatusCode.Forbidden); }
        return ImageResponse(png);
    };
    await resumed.ExecuteAsync(progress, default);
    Check(logoRequests == 1 && saved.Count == 4 && a.GetImageInfo(ImageType.Backdrop, 0) is not null && b.GetImageInfo(ImageType.Backdrop, 0) is not null, "a denied artwork feature is skipped for the rest of the run without blocking other types");
    libraryOptions.TypeOptions = [new TypeOptions { Type = nameof(Movie), ImageOptions = [new ImageOption { Type = ImageType.Logo, Limit = 0 }] }];
    config.EnablePosters = config.EnableBackdrops = false;
    await resumed.ExecuteAsync(progress, default);
    Check(logoRequests == 1 && saved.Count == 4, "library image limits prevent requests for disabled image types");
    config.EnableLogos = false;
    config.EnablePosters = true;
    http.Reply = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);
    await Throws<InvalidOperationException>(() => resumed.ExecuteAsync(progress, default), "authentication errors stop the task without replacing existing artwork");
    http.Reply = _ => ImageResponse([1, 2, 3]);
    await resumed.ExecuteAsync(progress, default);
    Check(saved.Count == 4, "invalid image data is skipped without replacing artwork or aborting the batch");

    Task Updated() { imageUpdates++; return Task.CompletedTask; }
    Console.WriteLine($"Passed {passed} checks.");
}
finally
{
    Directory.Delete(root, recursive: true);
}

void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

async Task Throws<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); }
    catch (T) { Check(true, name); return; }
    throw new Exception("FAIL: " + name);
}

static HttpResponseMessage ImageResponse(byte[] png)
{
    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
    response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
    return response;
}

public class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler, IHttpClientFactory
{
    public Func<HttpRequestMessage, HttpResponseMessage> Reply { get; set; } = reply;
    public string? LastEtag { get; private set; }
    public bool NoCache { get; private set; }
    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastEtag = request.Headers.IfNoneMatch.FirstOrDefault()?.Tag;
        NoCache = request.Headers.CacheControl?.NoCache == true;
        return Task.FromResult(Reply(request));
    }
}

public class Stub : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var result = Handler(method!, args);
        if (result is not null || method!.ReturnType == typeof(void)) return result;
        if (method.ReturnType == typeof(Task)) return Task.CompletedTask;
        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
    public static T Create<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = Create<T, Stub>();
        ((Stub)(object)proxy).Handler = handler;
        return proxy;
    }
}

public class QuietProgress : IProgress<double>
{
    public void Report(double value) { }
}
