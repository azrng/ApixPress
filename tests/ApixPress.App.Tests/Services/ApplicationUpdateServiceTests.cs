using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
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
    public async Task CheckForUpdatesAsync_ShouldPreferSelfContainedEntryInDualPackageManifest()
    {
        // 双包清单：同一版本两条记录（Version、PubTime 相同），必须显式选中自包含包
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            [
              {
                "PacketName": "ApixPress-framework-win-x64",
                "Hash": "framework-hash",
                "Version": "1.1.16",
                "Url": "https://example.com/ApixPress-framework-win-x64.zip",
                "PubTime": "2026-09-15T08:00:00Z"
              },
              {
                "PacketName": "ApixPress-selfcontained-win-x64",
                "Hash": "selfcontained-hash",
                "Version": "1.1.16",
                "Url": "https://example.com/ApixPress-selfcontained-win-x64.zip",
                "PubTime": "2026-09-15T08:00:00Z"
              }
            ]
            """));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.CheckForUpdatesAsync("1.1.15", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.HasUpdate);
        Assert.Equal("selfcontained-hash", result.Data.PackageHash);
        Assert.Equal("ApixPress-selfcontained-win-x64", result.Data.PackageName);
        Assert.Equal("https://example.com/ApixPress-selfcontained-win-x64.zip", result.Data.DownloadUrl);
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
    public async Task CheckForUpdatesAsync_ShouldNotifyLegacyTimestampVersionAboutCalendarUpdate()
    {
        // 版本策略回归：旧时间戳版本（2026.9.15.10，四段）必须能感知日历版本 2026.9.16+ 的更新
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("""
            [
              {
                "PacketName": "ApixPress-selfcontained-win-x64",
                "Hash": "abc123",
                "Version": "2026.9.16",
                "Url": "https://github.com/azrng/ApixPress/releases/latest/download/ApixPress-selfcontained-win-x64.zip",
                "PubTime": "2026-09-15T08:00:00Z"
              }
            ]
            """));
        var service = new ApplicationUpdateService(configuration, httpClient);

        var result = await service.CheckForUpdatesAsync("2026.9.15.10", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.HasUpdate);
        Assert.Equal("2026.9.16", result.Data.LatestVersion);
    }

    [Fact]
    public async Task PrepareUpdatePackageAsync_ShouldFailWhenCurrentExecutableMissing()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("[]"));
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ApixPress-update-tests-{Guid.NewGuid():N}");
        var missingExecutablePath = Path.Combine(tempRoot, "MissingApixPress.exe");
        var service = new ApplicationUpdateService(
            configuration,
            httpClient,
            startInfo => Process.GetCurrentProcess(),
            () => missingExecutablePath,
            () => 9527,
            () => tempRoot,
            () => tempRoot);

        var result = await service.PrepareUpdatePackageAsync(new AppUpdateCheckResultDto
        {
            CurrentVersion = "1.0.0.0",
            LatestVersion = "1.1.0.0",
            HasUpdate = true,
            DownloadUrl = "https://example.com/update.zip",
            PackageHash = "abc123"
        }, progress: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("MissingApixPress.exe", result.Message);
    }

    [Fact]
    public async Task PrepareUpdatePackageAsync_ShouldDownloadVerifyAndExtractStagedPackage()
    {
        var configuration = CreateConfiguration();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ApixPress-update-tests-{Guid.NewGuid():N}");
        var baseDirectory = Path.Combine(tempRoot, "app");
        var executablePath = Path.Combine(baseDirectory, "ApixPress.exe");
        Directory.CreateDirectory(baseDirectory);
        await File.WriteAllTextAsync(executablePath, "stub", Encoding.UTF8);

        // 用真实 zip + 真实 SHA256 走完整链路：下载（字节流）→ 哈希校验 → 解压
        var zipBytes = CreateZipBytes("ApixPress.exe", "new stub content");
        var service = new ApplicationUpdateService(
            configuration,
            new HttpClient(new FakeHttpMessageHandler("[]")),
            startInfo => Process.GetCurrentProcess(),
            () => executablePath,
            () => 9527,
            () => baseDirectory,
            () => tempRoot,
            downloadHttpClientFactory: () => new HttpClient(new FakeHttpMessageHandler(zipBytes, "application/zip")));
        var progress = new SynchronousProgressCollector();

        var result = await service.PrepareUpdatePackageAsync(new AppUpdateCheckResultDto
        {
            PackageName = "ApixPress-selfcontained-win-x64",
            PackageHash = Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant(),
            CurrentVersion = "1.0.0.0",
            LatestVersion = "1.1.0.0",
            HasUpdate = true,
            DownloadUrl = "https://example.com/update.zip"
        }, progress, CancellationToken.None);

        try
        {
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.True(Directory.Exists(result.Data!.ExtractDirectory));

            var extractedExecutable = Path.Combine(result.Data.ExtractDirectory, "ApixPress.exe");
            Assert.True(File.Exists(extractedExecutable));
            Assert.Equal("new stub content", await File.ReadAllTextAsync(extractedExecutable, Encoding.UTF8));
            Assert.True(File.Exists(result.Data.PackageFilePath));
            Assert.Equal(executablePath, result.Data.RestartExecutablePath);
            Assert.Equal(baseDirectory, result.Data.TargetDirectory);
            Assert.StartsWith(tempRoot, result.Data.WorkspaceDirectory);

            // 进度应依次覆盖下载（末次达 100%）、校验、解压三个阶段
            Assert.Contains(progress.Reports, item => item.Stage == AppUpdateProgressStage.Verifying);
            Assert.Contains(progress.Reports, item => item.Stage == AppUpdateProgressStage.Extracting);
            var lastDownload = progress.Reports.Last(item => item.Stage == AppUpdateProgressStage.Downloading);
            Assert.Equal(zipBytes.Length, lastDownload.ReceivedBytes);
            Assert.Equal(zipBytes.Length, lastDownload.TotalBytes);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareUpdatePackageAsync_ShouldFailAndCleanupWhenPackageHashMismatched()
    {
        var configuration = CreateConfiguration();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ApixPress-update-tests-{Guid.NewGuid():N}");
        var baseDirectory = Path.Combine(tempRoot, "app");
        var executablePath = Path.Combine(baseDirectory, "ApixPress.exe");
        Directory.CreateDirectory(baseDirectory);
        await File.WriteAllTextAsync(executablePath, "stub", Encoding.UTF8);

        var zipBytes = CreateZipBytes("ApixPress.exe", "new stub content");
        var service = new ApplicationUpdateService(
            configuration,
            new HttpClient(new FakeHttpMessageHandler("[]")),
            startInfo => Process.GetCurrentProcess(),
            () => executablePath,
            () => 9527,
            () => baseDirectory,
            () => tempRoot,
            downloadHttpClientFactory: () => new HttpClient(new FakeHttpMessageHandler(zipBytes, "application/zip")));

        var result = await service.PrepareUpdatePackageAsync(new AppUpdateCheckResultDto
        {
            PackageName = "ApixPress-selfcontained-win-x64",
            PackageHash = "deadbeef",
            DownloadUrl = "https://example.com/update.zip"
        }, progress: null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("哈希", result.Message);
        // 失败后 guid 工作区应被整体清理（父目录 ApixPress-Update 只是壳，允许保留）
        Assert.Empty(Directory.GetDirectories(Path.Combine(tempRoot, "ApixPress-Update")));
        Directory.Delete(tempRoot, recursive: true);
    }

    [Fact]
    public async Task PrepareUpdatePackageAsync_ShouldReportCancelledWhenTokenAlreadyCancelled()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("[]"));
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ApixPress-update-tests-{Guid.NewGuid():N}");
        var baseDirectory = Path.Combine(tempRoot, "app");
        var executablePath = Path.Combine(baseDirectory, "ApixPress.exe");
        Directory.CreateDirectory(baseDirectory);
        await File.WriteAllTextAsync(executablePath, "stub", Encoding.UTF8);
        var service = new ApplicationUpdateService(
            configuration,
            httpClient,
            startInfo => Process.GetCurrentProcess(),
            () => executablePath,
            () => 9527,
            () => baseDirectory,
            () => tempRoot);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var result = await service.PrepareUpdatePackageAsync(new AppUpdateCheckResultDto
        {
            PackageHash = "abc123",
            DownloadUrl = "https://example.com/update.zip"
        }, progress: null, cancellationTokenSource.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal("更新已取消。", result.Message);
        Directory.Delete(tempRoot, recursive: true);
    }

    [Fact]
    public async Task ApplyStagedUpdateAsync_ShouldGenerateScriptThatWaitsCurrentProcessOnly()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("[]"));
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ApixPress-update-script-tests-{Guid.NewGuid():N}");
        var extractDirectory = Path.Combine(tempRoot, "extract '目录'");
        var targetDirectory = Path.Combine(tempRoot, "target '目录'");
        Directory.CreateDirectory(extractDirectory);
        Directory.CreateDirectory(targetDirectory);
        var packageFilePath = Path.Combine(tempRoot, "update '包'.zip");
        await File.WriteAllTextAsync(packageFilePath, "zip", Encoding.UTF8);

        ProcessStartInfo? capturedStartInfo = null;
        var service = new ApplicationUpdateService(
            configuration,
            httpClient,
            startInfo =>
            {
                capturedStartInfo = startInfo;
                return Process.GetCurrentProcess();
            },
            () => null,
            () => 9527,
            () => tempRoot,
            () => tempRoot);

        var result = await service.ApplyStagedUpdateAsync(new AppUpdateStagedPackageDto
        {
            WorkspaceDirectory = tempRoot,
            PackageFilePath = packageFilePath,
            ExtractDirectory = extractDirectory,
            TargetDirectory = targetDirectory,
            RestartExecutablePath = Path.Combine(targetDirectory, "ApixPress.exe"),
            PackageName = "ApixPress-selfcontained-win-x64"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedStartInfo);
        Assert.True(capturedStartInfo!.UseShellExecute);
        Assert.Equal(ProcessWindowStyle.Hidden, capturedStartInfo.WindowStyle);
        Assert.EndsWith("apply-update.cmd", capturedStartInfo.FileName);
        Assert.StartsWith(tempRoot, capturedStartInfo.FileName);
        Assert.True(File.Exists(capturedStartInfo.FileName));

        var cmdContent = await File.ReadAllTextAsync(capturedStartInfo.FileName, Encoding.UTF8);
        var powerShellScript = DecodeEncodedCommand(cmdContent);

        // 只等待主进程退出（子进程更新模式已删除），robocopy 覆盖后重启主程序
        Assert.Contains("$waitPid = 9527", powerShellScript);
        Assert.DoesNotContain("$updatePid", powerShellScript);
        Assert.Contains("robocopy $extractDir $targetDir", powerShellScript);
        Assert.Contains("Start-Process $restartExe", powerShellScript);
        Assert.Contains("extract ''目录''", powerShellScript);
        Assert.Contains("update ''包''.zip", powerShellScript);

        Directory.Delete(tempRoot, recursive: true);
    }

    [Fact]
    public async Task ApplyStagedUpdateAsync_ShouldFailWhenExtractDirectoryMissing()
    {
        var configuration = CreateConfiguration();
        using var httpClient = new HttpClient(new FakeHttpMessageHandler("[]"));
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ApixPress-update-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var service = new ApplicationUpdateService(
            configuration,
            httpClient,
            startInfo => Process.GetCurrentProcess(),
            () => null,
            () => 9527,
            () => tempRoot,
            () => tempRoot);

        var result = await service.ApplyStagedUpdateAsync(new AppUpdateStagedPackageDto
        {
            WorkspaceDirectory = tempRoot,
            PackageFilePath = Path.Combine(tempRoot, "package.zip"),
            ExtractDirectory = Path.Combine(tempRoot, "missing-extract"),
            TargetDirectory = tempRoot,
            RestartExecutablePath = Path.Combine(tempRoot, "ApixPress.exe")
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("解压目录不存在", result.Message);
        Directory.Delete(tempRoot, recursive: true);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Update:ChannelName"] = "GitHub Releases",
                ["Update:VersionManifestUrl"] = "https://github.com/azrng/ApixPress/releases/latest/download/versions.json"
            })
            .Build();
    }

    private static string DecodeEncodedCommand(string cmdContent)
    {
        const string marker = " -EncodedCommand ";
        var markerIndex = cmdContent.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0);

        var encodedStart = markerIndex + marker.Length;
        var encodedEnd = cmdContent.IndexOf("\r\n", encodedStart, StringComparison.Ordinal);
        Assert.True(encodedEnd > encodedStart);

        return Encoding.Unicode.GetString(Convert.FromBase64String(cmdContent[encodedStart..encodedEnd]));
    }

    private static byte[] CreateZipBytes(string entryName, string content)
    {
        using var packageStream = new MemoryStream();
        using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            var entryBytes = Encoding.UTF8.GetBytes(content);
            entryStream.Write(entryBytes);
        }

        return packageStream.ToArray();
    }

    private sealed class SynchronousProgressCollector : IProgress<AppUpdateProgressDto>
    {
        public List<AppUpdateProgressDto> Reports { get; } = [];

        public void Report(AppUpdateProgressDto value)
        {
            Reports.Add(value);
        }
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(string responseBody)
            : this(Encoding.UTF8.GetBytes(responseBody), "application/json")
        {
        }

        public FakeHttpMessageHandler(byte[] responseBody, string contentType)
        {
            _response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseBody)
            };
            _response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
