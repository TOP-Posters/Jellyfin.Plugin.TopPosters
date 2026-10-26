using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using MediaBrowser.Common.Net;

namespace Jellyfin.Plugin.TopPosters.Refresh;

public record ArtworkDownload(HttpStatusCode Status, byte[]? Body = null, string? MediaType = null);

public class ArtworkDownloader(IHttpClientFactory httpClientFactory)
{
    private const int MaxBytes = 32 * 1024 * 1024;

    public async Task<ArtworkDownload> DownloadAsync(string url, string? currentPath, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        cancellationToken = timeout.Token;
        string? localHash = null;
        if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
        {
            await using var local = File.OpenRead(currentPath);
            localHash = Convert.ToHexStringLower(await MD5.HashDataAsync(local, cancellationToken).ConfigureAwait(false));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // A dashboard change keeps the URL stable: ask intermediary caches to revalidate.
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        if (localHash is not null) request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{localHash}\""));
        var client = httpClientFactory.CreateClient(NamedClient.Default);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            if (localHash is null) throw new InvalidDataException("Received 304 without a local image.");
            return new(HttpStatusCode.NotModified);
        }
        if (!response.IsSuccessStatusCode) return new(response.StatusCode);

        if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException("Artwork is too large.");
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("image/jpeg" or "image/png")) throw new InvalidDataException("Unexpected artwork content type.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + read > MaxBytes) throw new InvalidDataException("Artwork is too large.");
            buffer.Write(chunk, 0, read);
        }
        var body = buffer.ToArray();
        var valid = mediaType == "image/png"
            ? body.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : body.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
        if (!valid) throw new InvalidDataException("Invalid artwork signature.");

        // Some intermediaries ignore conditional requests. Do not rewrite an identical file.
        if (localHash == Convert.ToHexStringLower(MD5.HashData(body))) return new(HttpStatusCode.NotModified);
        return new(response.StatusCode, body, mediaType);
    }
}
