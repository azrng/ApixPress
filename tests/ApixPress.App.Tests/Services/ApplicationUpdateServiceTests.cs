using System.Net;
using System.Net.Http;
using System.Text;
using ApixPress.App.Services.Implementations;
using Microsoft.Extensions.Configuration;

namespace ApixPress.App.Tests.Services;

public sealed class ApplicationUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_ShouldDetectNewerManifestVersion()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            [
              {
                "PacketName": "ApixPress-win-x64-portable",
                "Hash": "abc123",
                "Version": "1.1.0.0",
                "Url": "https://github.com/azrng/ApixPress/releases/latest/download/ApixPress-win-x64-portable.zip",
                "PubTime": "2026-04-16T03:00:00Z"
              }
            ]
            """));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.CheckForUpdatesAsync("1.0.0.0", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.HasUpdate);
        Assert.Equal("1.1.0.0", result.Data.LatestVersion);
        Assert.Equal("1.0.0.0", result.Data.CurrentVersion);
    }

    [Fact]
    public async Task StartUpdateAsync_ShouldFailWhenUpdaterExecutableMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Update:ChannelName"] = "GitHub Releases",
                ["Update:VersionManifestUrl"] = "https://github.com/azrng/ApixPress/releases/latest/download/versions.json",
                ["Update:VersionFileName"] = "versions.json",
                ["Update:UpgradeAppName"] = "MissingUpdater.exe"
            })
            .Build();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("[]"));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.StartUpdateAsync("1.0.0.0", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("MissingUpdater.exe", result.Message);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Update:ChannelName"] = "GitHub Releases",
                ["Update:VersionManifestUrl"] = "https://github.com/azrng/ApixPress/releases/latest/download/versions.json",
                ["Update:VersionFileName"] = "versions.json",
                ["Update:UpgradeAppName"] = "ApixPress.Updater.exe"
            })
            .Build();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public FakeHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
