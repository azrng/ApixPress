using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ApixPress.App.Services.Implementations;

internal sealed class OpenApiImportSourceReader
{
    private const long MaxSwaggerFileSizeBytes = 20 * 1024 * 1024;
    private static readonly TimeSpan SwaggerUrlDownloadTimeout = TimeSpan.FromSeconds(20);
    private static readonly HttpClient SharedHttpClient = CreateSwaggerImportHttpClient();

    private readonly Func<Uri, CancellationToken, Task<string>> _downloadOpenApiDocumentAsync;
    private readonly Func<string, CancellationToken, Task<string>> _readOpenApiFileAsync;

    public OpenApiImportSourceReader()
        : this(DownloadOpenApiDocumentAsync, ReadOpenApiFileAsync)
    {
    }

    public OpenApiImportSourceReader(
        Func<Uri, CancellationToken, Task<string>> downloadOpenApiDocumentAsync,
        Func<string, CancellationToken, Task<string>> readOpenApiFileAsync)
    {
        _downloadOpenApiDocumentAsync = downloadOpenApiDocumentAsync;
        _readOpenApiFileAsync = readOpenApiFileAsync;
    }

    public Task<string> ReadFromUrlAsync(Uri targetUri, CancellationToken cancellationToken)
    {
        return _downloadOpenApiDocumentAsync(targetUri, cancellationToken);
    }

    public async Task<string> ReadFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new OpenApiImportSourceException("未找到指定的本地文件。", "swagger_file_not_found");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxSwaggerFileSizeBytes)
        {
            throw new OpenApiImportSourceException("Swagger 文件超过 20MB 限制。", "swagger_file_too_large");
        }

        return await _readOpenApiFileAsync(filePath, cancellationToken);
    }

    private static Task<string> ReadOpenApiFileAsync(string filePath, CancellationToken cancellationToken)
    {
        return File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private static async Task<string> DownloadOpenApiDocumentAsync(Uri targetUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/yaml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/yaml"));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(SwaggerUrlDownloadTimeout);

        using var response = await SharedHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"远程服务返回 {(int)response.StatusCode} {response.ReasonPhrase}".Trim(),
                null,
                response.StatusCode);
        }

        if (response.Content.Headers.ContentLength is > MaxSwaggerFileSizeBytes)
        {
            throw new InvalidOperationException("Swagger 文档超过 20MB 限制。");
        }

        var document = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        if (Encoding.UTF8.GetByteCount(document) > MaxSwaggerFileSizeBytes)
        {
            throw new InvalidOperationException("Swagger 文档超过 20MB 限制。");
        }

        return document;
    }

    private static HttpClient CreateSwaggerImportHttpClient()
    {
        var httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ApixPress/1.0");
        return httpClient;
    }
}
