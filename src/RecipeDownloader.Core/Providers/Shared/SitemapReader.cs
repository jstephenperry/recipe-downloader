using System.IO.Compression;
using System.Xml;

namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// Reads sitemaps.org XML documents. Handles both <c>&lt;urlset&gt;</c> documents and
/// <c>&lt;sitemapindex&gt;</c> documents, and transparently decompresses <c>.gz</c> sitemaps.
/// </summary>
public static class SitemapReader
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true
    };

    /// <summary>
    /// Streams every <c>&lt;loc&gt;</c> value out of a sitemap document. Streaming avoids
    /// materializing large sitemaps (some providers publish tens of thousands of URLs).
    /// </summary>
    public static List<string> ReadLocations(Stream xmlStream)
    {
        var locations = new List<string>();

        using var reader = XmlReader.Create(xmlStream, ReaderSettings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "loc")
                continue;

            var loc = reader.ReadElementContentAsString().Trim();
            if (loc.Length > 0)
                locations.Add(loc);
        }

        return locations;
    }

    /// <summary>
    /// Fetches a sitemap and returns its locations. When the sitemap is an index, the child
    /// sitemaps matching <paramref name="childSitemapFilter"/> are fetched and flattened.
    /// </summary>
    public static async Task<List<string>> FetchLocationsAsync(
        HttpClient httpClient,
        string sitemapUrl,
        Func<string, bool>? childSitemapFilter = null,
        CancellationToken ct = default)
    {
        var isIndex = await IsSitemapIndexAsync(httpClient, sitemapUrl, ct);
        var locations = await FetchLocationsCoreAsync(httpClient, sitemapUrl, ct);

        if (!isIndex)
            return locations;

        var children = childSitemapFilter is null
            ? locations
            : locations.Where(childSitemapFilter).ToList();

        var all = new List<string>();
        foreach (var child in children)
        {
            ct.ThrowIfCancellationRequested();
            all.AddRange(await FetchLocationsCoreAsync(httpClient, child, ct));
        }

        return all;
    }

    private static async Task<List<string>> FetchLocationsCoreAsync(
        HttpClient httpClient, string url, CancellationToken ct)
    {
        await using var stream = await OpenSitemapStreamAsync(httpClient, url, ct);
        return ReadLocations(stream);
    }

    private static async Task<bool> IsSitemapIndexAsync(
        HttpClient httpClient, string url, CancellationToken ct)
    {
        await using var stream = await OpenSitemapStreamAsync(httpClient, url, ct);
        using var reader = XmlReader.Create(stream, ReaderSettings);

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
                return reader.LocalName == "sitemapindex";
        }

        return false;
    }

    /// <summary>
    /// Opens a sitemap as a seek-free stream, decompressing when the URL or response indicates gzip.
    /// The response is buffered because the stream is read more than once (index detection, then parse).
    /// </summary>
    private static async Task<Stream> OpenSitemapStreamAsync(
        HttpClient httpClient, string url, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);

        // HttpClient transparently decompresses Content-Encoding: gzip, but a ".xml.gz" file is
        // gzip *content*, which arrives intact. Detect it by magic number rather than by extension.
        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            using var compressed = new MemoryStream(bytes);
            await using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            var decompressed = new MemoryStream();
            await gzip.CopyToAsync(decompressed, ct);
            decompressed.Position = 0;
            return decompressed;
        }

        return new MemoryStream(bytes);
    }
}
