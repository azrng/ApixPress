using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ApixPress.App.Models.DTOs;
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
        Assert.Equal("abc123", result.Data.PackageHash);
        Assert.Equal("ApixPress-win-x64-portable", result.Data.PackageName);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReadSingleObjectManifest()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            {
              "PacketName": "ApixPress-win-x64-portable",
              "Hash": "abc123",
              "Version": "1.1.0.0",
              "Url": "https://github.com/azrng/ApixPress/releases/latest/download/ApixPress-win-x64-portable.zip",
              "PubTime": "2026-04-17T10:39:19Z"
            }
            """));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.CheckForUpdatesAsync("1.0.0.0", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.HasUpdate);
        Assert.Equal("1.1.0.0", result.Data.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldTreatMissingVersionComponentsAsEqual()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            [
              {
                "PacketName": "ApixPress-win-x64-portable",
                "Hash": "abc123",
                "Version": "1.0.0",
                "Url": "https://github.com/azrng/ApixPress/releases/latest/download/ApixPress-win-x64-portable.zip",
                "PubTime": "2026-04-17T10:39:19Z"
              }
            ]
            """));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.CheckForUpdatesAsync("1.0", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.HasUpdate);
        Assert.Equal("1.0", result.Data.CurrentVersion);
        Assert.Equal("1.0.0", result.Data.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldSupportDateBasedVersionFormat()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            [
              {
                "PacketName": "ApixPress-win-x64-portable",
                "Hash": "abc123",
                "Version": "2026.4.21",
                "Url": "https://github.com/azrng/ApixPress/releases/latest/download/ApixPress-win-x64-portable.zip",
                "PubTime": "2026-04-21T03:00:00Z"
              }
            ]
            """));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.CheckForUpdatesAsync("2026.4.20", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.HasUpdate);
        Assert.Equal("2026.4.20", result.Data.CurrentVersion);
        Assert.Equal("2026.4.21", result.Data.LatestVersion);
    }

    [Fact]
    public async Task StartUpdateAsync_ShouldFailWhenUpdaterExecutableMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Update:ChannelName"] = "GitHub Releases",
                ["Update:VersionManifestUrl"] = "https://github.com/azrng/ApixPress/releases/latest/download/versions.json",
                ["Update:UpgradeAppName"] = "MissingUpdater.exe"
            })
            .Build();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("[]"));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.StartUpdateAsync(new AppUpdateCheckResultDto
        {
            CurrentVersion = "1.0.0.0",
            LatestVersion = "1.1.0.0",
            HasUpdate = true,
            DownloadUrl = "https://example.com/update.zip",
            PackageHash = "abc123"
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("MissingUpdater.exe", result.Message);
    }

    [Fact]
    public async Task StartUpdateAsync_ShouldWriteRequestFileAndLaunchUpdater()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("[]"));
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ApixPress-update-tests-{Guid.NewGuid():N}");
        var baseDirectory = Path.Combine(tempRoot, "app");
        Directory.CreateDirectory(baseDirectory);

        var updaterPath = Path.Combine(baseDirectory, "ApixPress.Updater.exe");
        await File.WriteAllTextAsync(updaterPath, "stub", Encoding.UTF8);

        ProcessStartInfo? capturedStartInfo = null;
        var service = new ApplicationUpdateService(
            configuration,
            httpClient,
            startInfo =>
            {
                capturedStartInfo = startInfo;
                return Process.GetCurrentProcess();
            },
            () => Path.Combine(baseDirectory, "ApixPress.exe"),
            () => 9527,
            () => baseDirectory,
            () => tempRoot);

        var result = await service.StartUpdateAsync(new AppUpdateCheckResultDto
        {
            PackageName = "ApixPress-win-x64-portable",
            PackageHash = "abc123",
            CurrentVersion = "1.0.0.0",
            LatestVersion = "1.1.0.0",
            HasUpdate = true,
            DownloadUrl = "https://example.com/update.zip"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal(updaterPath, capturedStartInfo!.FileName);

        var requestFilePath = ExtractRequestFilePath(capturedStartInfo.Arguments);
        Assert.True(File.Exists(requestFilePath));

        var requestJson = await File.ReadAllTextAsync(requestFilePath, Encoding.UTF8);
        var request = JsonSerializer.Deserialize<AppUpdateLaunchRequestDto>(requestJson);
        Assert.NotNull(request);
        Assert.Equal("https://example.com/update.zip", request!.PackageUrl);
        Assert.Equal("abc123", request.PackageHash);
        Assert.Equal("1.1.0.0", request.TargetVersion);
        Assert.Equal(9527, request.CurrentProcessId);
        Assert.Equal(Path.Combine(baseDirectory, "ApixPress.exe"), request.RestartExecutablePath);

        Directory.Delete(tempRoot, recursive: true);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Update:ChannelName"] = "GitHub Releases",
                ["Update:VersionManifestUrl"] = "https://github.com/azrng/ApixPress/releases/latest/download/versions.json",
                ["Update:UpgradeAppName"] = "ApixPress.Updater.exe"
            })
            .Build();
    }

    private static string ExtractRequestFilePath(string arguments)
    {
        const string prefix = "--request-file \"";
        Assert.StartsWith(prefix, arguments);
        Assert.EndsWith("\"", arguments);
        return arguments[prefix.Length..^1];
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
