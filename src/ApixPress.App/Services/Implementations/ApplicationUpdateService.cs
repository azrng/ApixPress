using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;
using Microsoft.Extensions.Configuration;

namespace ApixPress.App.Services.Implementations;

public sealed class ApplicationUpdateService : IApplicationUpdateService, ISingletonDependency
{
    private readonly HttpClient _httpClient;
    private readonly Func<HttpClient> _downloadHttpClientFactory;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;
    private readonly Func<string?> _processPathProvider;
    private readonly Func<int> _processIdProvider;
    private readonly Func<string> _baseDirectoryProvider;
    private readonly Func<string> _tempDirectoryProvider;
    private readonly string _versionManifestUrl;

    public ApplicationUpdateService(IConfiguration configuration)
        : this(configuration, new HttpClient())
    {
    }

    public ApplicationUpdateService(IConfiguration configuration, HttpClient httpClient)
        : this(
            configuration,
            httpClient,
            startInfo => Process.Start(startInfo),
            () => Environment.ProcessPath,
            () => Environment.ProcessId,
            () => AppContext.BaseDirectory,
            Path.GetTempPath)
    {
    }

    public ApplicationUpdateService(
        IConfiguration configuration,
        HttpClient httpClient,
        Func<ProcessStartInfo, Process?> processStarter,
        Func<string?> processPathProvider,
        Func<int> processIdProvider,
        Func<string> baseDirectoryProvider,
        Func<string> tempDirectoryProvider,
        Func<HttpClient>? downloadHttpClientFactory = null)
    {
        _httpClient = httpClient;
        // 下载客户端独立于清单客户端：15 秒清单超时不适用于几十 MB 安装包
        _downloadHttpClientFactory = downloadHttpClientFactory ?? CreateDefaultDownloadClient;
        _processStarter = processStarter;
        _processPathProvider = processPathProvider;
        _processIdProvider = processIdProvider;
        _baseDirectoryProvider = baseDirectoryProvider;
        _tempDirectoryProvider = tempDirectoryProvider;
        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        ChannelName = ReadValue(configuration, "Update:ChannelName", "未配置更新通道");
        _versionManifestUrl = ReadValue(configuration, "Update:VersionManifestUrl");
        IsConfigured = !string.IsNullOrWhiteSpace(_versionManifestUrl);
    }

    private static HttpClient CreateDefaultDownloadClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    public string ChannelName { get; }

    public bool IsConfigured { get; }

    public async Task<IResultModel<AppUpdateCheckResultDto>> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return ResultModel<AppUpdateCheckResultDto>.Failure("尚未配置更新清单地址。", "app_update_not_configured");
        }

        var normalizedCurrentVersion = NormalizeVersion(currentVersion);
        if (!TryParseComparableVersion(normalizedCurrentVersion, out var parsedCurrentVersion))
        {
            return ResultModel<AppUpdateCheckResultDto>.Failure($"当前版本号无效：{currentVersion}", "app_update_invalid_current_version");
        }

        try
        {
            await using var manifestStream = await _httpClient.GetStreamAsync(_versionManifestUrl, cancellationToken);
            var manifest = await ReadManifestAsync(manifestStream, cancellationToken);

            if (manifest is null || manifest.Count == 0)
            {
                return ResultModel<AppUpdateCheckResultDto>.Failure("更新清单为空。", "app_update_empty_manifest");
            }

            var manifestVersions = manifest
                .Where(item => TryParseComparableVersion(item.Version, out _))
                .Select(item => ParseComparableVersion(item.Version))
                .ToList();
            if (manifestVersions.Count == 0)
            {
                return ResultModel<AppUpdateCheckResultDto>.Failure("更新清单中未找到有效版本。", "app_update_invalid_manifest");
            }

            // 先取清单最高版本，再在该版本的记录里显式优先自包含包：
            // 同一版本有自包含/非自包含两条记录（Version、PubTime 相同），依赖排序顺序选中包不可靠
            var highestVersion = manifestVersions.Max();
            var latestRelease = manifest
                .Where(item => TryParseComparableVersion(item.Version, out _)
                    && ParseComparableVersion(item.Version) == highestVersion)
                .OrderByDescending(item => item.PacketName?.Contains("selfcontained", StringComparison.OrdinalIgnoreCase) == true)
                .ThenByDescending(item => item.PubTime)
                .First();

            var latestVersion = highestVersion;
            var latestDisplayVersion = NormalizeVersion(latestRelease.Version);
            return ResultModel<AppUpdateCheckResultDto>.Success(new AppUpdateCheckResultDto
            {
                PackageName = latestRelease.PacketName,
                PackageHash = latestRelease.Hash,
                CurrentVersion = normalizedCurrentVersion,
                LatestVersion = latestDisplayVersion,
                HasUpdate = parsedCurrentVersion < latestVersion,
                PublishedAt = latestRelease.PubTime,
                DownloadUrl = latestRelease.Url
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<AppUpdateCheckResultDto>.Failure("检查更新已取消。", "app_update_check_cancelled");
        }
        catch (HttpRequestException exception)
        {
            return ResultModel<AppUpdateCheckResultDto>.Failure($"更新清单请求失败：{exception.Message}", "app_update_http_failed");
        }
        catch (JsonException exception)
        {
            return ResultModel<AppUpdateCheckResultDto>.Failure($"更新清单解析失败：{exception.Message}", "app_update_manifest_invalid_json");
        }
        catch (Exception exception)
        {
            return ResultModel<AppUpdateCheckResultDto>.Failure($"检查更新失败：{exception.Message}", "app_update_check_failed");
        }
    }

    public async Task<IResultModel<AppUpdateStagedPackageDto>> PrepareUpdatePackageAsync(
        AppUpdateCheckResultDto updateInfo,
        IProgress<AppUpdateProgressDto>? progress,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return ResultModel<AppUpdateStagedPackageDto>.Failure("尚未配置更新清单地址。", "app_update_not_configured");
        }

        if (updateInfo is null)
        {
            return ResultModel<AppUpdateStagedPackageDto>.Failure("更新信息不能为空。", "app_update_info_missing");
        }

        var currentProcessPath = _processPathProvider();
        if (string.IsNullOrWhiteSpace(currentProcessPath))
        {
            return ResultModel<AppUpdateStagedPackageDto>.Failure("无法识别当前主程序名称。", "app_update_app_name_missing");
        }

        if (!File.Exists(currentProcessPath))
        {
            return ResultModel<AppUpdateStagedPackageDto>.Failure($"未找到主程序：{currentProcessPath}", "app_update_launcher_missing");
        }

        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            return ResultModel<AppUpdateStagedPackageDto>.Failure("更新包地址为空。", "app_update_package_url_missing");
        }

        if (string.IsNullOrWhiteSpace(updateInfo.PackageHash))
        {
            return ResultModel<AppUpdateStagedPackageDto>.Failure("更新包校验信息为空。", "app_update_package_hash_missing");
        }

        var workspacePath = Path.Combine(_tempDirectoryProvider(), "ApixPress-Update", Guid.NewGuid().ToString("N"));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(workspacePath);
            var packageFilePath = Path.Combine(
                workspacePath,
                string.IsNullOrWhiteSpace(updateInfo.PackageName) ? "update-package.zip" : $"{SanitizeFileName(updateInfo.PackageName)}.zip");

            await DownloadPackageAsync(updateInfo.DownloadUrl, packageFilePath, progress, cancellationToken);

            progress?.Report(new AppUpdateProgressDto { Stage = AppUpdateProgressStage.Verifying });
            if (!await VerifyPackageHashAsync(packageFilePath, updateInfo.PackageHash, cancellationToken))
            {
                // 哈希不匹配的包已确认损坏，连同工作区一起清掉
                DeleteDirectoryIfExists(workspacePath);
                return ResultModel<AppUpdateStagedPackageDto>.Failure("更新包哈希校验失败，安装包可能已损坏，请重试。", "app_update_hash_mismatch");
            }

            progress?.Report(new AppUpdateProgressDto { Stage = AppUpdateProgressStage.Extracting });
            var extractPath = Path.Combine(workspacePath, "package");
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, recursive: true);
            }

            // 解压是纯 CPU+IO 密集操作，放后台线程避免冻结 UI
            await Task.Run(() => ZipFile.ExtractToDirectory(packageFilePath, extractPath, overwriteFiles: true), cancellationToken);

            return ResultModel<AppUpdateStagedPackageDto>.Success(new AppUpdateStagedPackageDto
            {
                WorkspaceDirectory = workspacePath,
                PackageFilePath = packageFilePath,
                ExtractDirectory = extractPath,
                TargetDirectory = Path.GetDirectoryName(currentProcessPath) ?? _baseDirectoryProvider(),
                RestartExecutablePath = currentProcessPath,
                PackageName = updateInfo.PackageName,
                TargetVersion = updateInfo.LatestVersion
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 取消或失败都清掉半成品工作区，避免 %TEMP% 残留几十 MB 的半个安装包
            DeleteDirectoryIfExists(workspacePath);
            return ResultModel<AppUpdateStagedPackageDto>.Failure("更新已取消。", "app_update_cancelled");
        }
        catch (Exception exception)
        {
            DeleteDirectoryIfExists(workspacePath);
            return ResultModel<AppUpdateStagedPackageDto>.Failure($"准备更新包失败：{exception.Message}", "app_update_prepare_failed");
        }
    }

    public Task<IResultModel<bool>> ApplyStagedUpdateAsync(AppUpdateStagedPackageDto stagedPackage, CancellationToken cancellationToken)
    {
        if (stagedPackage is null)
        {
            return Task.FromResult(ResultModel<bool>.Failure("更新包信息不能为空。", "app_update_info_missing"));
        }

        if (string.IsNullOrWhiteSpace(stagedPackage.ExtractDirectory) || !Directory.Exists(stagedPackage.ExtractDirectory))
        {
            return Task.FromResult(ResultModel<bool>.Failure("更新包解压目录不存在。", "app_update_extract_missing"));
        }

        if (string.IsNullOrWhiteSpace(stagedPackage.TargetDirectory))
        {
            return Task.FromResult(ResultModel<bool>.Failure("目标目录为空。", "app_update_target_missing"));
        }

        if (string.IsNullOrWhiteSpace(stagedPackage.RestartExecutablePath))
        {
            return Task.FromResult(ResultModel<bool>.Failure("重启程序路径为空。", "app_update_restart_missing"));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scriptPath = CreateApplyScript(stagedPackage, _processIdProvider());
            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                WorkingDirectory = stagedPackage.TargetDirectory,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var process = _processStarter(startInfo);
            if (process is null)
            {
                return Task.FromResult(ResultModel<bool>.Failure("启动更新脚本失败。", "app_update_start_process_failed"));
            }

            return Task.FromResult(ResultModel<bool>.Success(true));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultModel<bool>.Failure("更新已取消。", "app_update_cancelled"));
        }
        catch (Exception exception)
        {
            return Task.FromResult(ResultModel<bool>.Failure($"应用更新失败：{exception.Message}", "app_update_apply_failed"));
        }
    }

    private async Task DownloadPackageAsync(
        string packageUrl,
        string packageFilePath,
        IProgress<AppUpdateProgressDto>? progress,
        CancellationToken cancellationToken)
    {
        // ResponseHeadersRead：拿到 Content-Length 就开始流式落盘，而不是把 68MB 缓冲进内存
        using var httpClient = _downloadHttpClientFactory();
        using var response = await httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(packageFilePath);

        var buffer = new byte[81920];
        var receivedBytes = 0L;
        var lastReportedPercent = -1L;
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            receivedBytes += bytesRead;

            // 按百分比变化上报：高速网络下每 80KB 回调一次会造成无意义的 UI 刷新
            if (totalBytes is > 0)
            {
                var percent = receivedBytes * 100 / totalBytes.Value;
                if (percent != lastReportedPercent)
                {
                    lastReportedPercent = percent;
                    progress?.Report(new AppUpdateProgressDto
                    {
                        Stage = AppUpdateProgressStage.Downloading,
                        ReceivedBytes = receivedBytes,
                        TotalBytes = totalBytes
                    });
                }
            }
        }

        // 收尾补一次上报，保证界面能收到 100% 与实际总字节数
        progress?.Report(new AppUpdateProgressDto
        {
            Stage = AppUpdateProgressStage.Downloading,
            ReceivedBytes = receivedBytes,
            TotalBytes = totalBytes
        });
    }

    private static async Task<bool> VerifyPackageHashAsync(string packageFilePath, string expectedHash, CancellationToken cancellationToken)
    {
        // 68MB 级别的哈希计算约需数百毫秒，放后台线程避免冻结 UI
        var actualHash = await Task.Run(() =>
        {
            using var fileStream = File.OpenRead(packageFilePath);
            return Convert.ToHexString(SHA256.HashData(fileStream)).ToLowerInvariant();
        }, cancellationToken);
        return string.Equals(actualHash, expectedHash.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }

    private string CreateApplyScript(AppUpdateStagedPackageDto stagedPackage, int currentProcessId)
    {
        var scriptPath = Path.Combine(stagedPackage.WorkspaceDirectory, "apply-update.cmd");

        var psScript = new StringBuilder()
            .AppendLine("$waitPid = " + currentProcessId)
            .AppendLine("$extractDir = '" + EscapeForPowerShell(stagedPackage.ExtractDirectory) + "'")
            .AppendLine("$targetDir = '" + EscapeForPowerShell(stagedPackage.TargetDirectory) + "'")
            .AppendLine("$restartExe = '" + EscapeForPowerShell(stagedPackage.RestartExecutablePath) + "'")
            .AppendLine("$packageFile = '" + EscapeForPowerShell(stagedPackage.PackageFilePath) + "'")
            .AppendLine()
            .AppendLine("while (Get-Process -Id $waitPid -ErrorAction SilentlyContinue) {")
            .AppendLine("    Start-Sleep -Seconds 1")
            .AppendLine("}")
            .AppendLine()
            .AppendLine("robocopy $extractDir $targetDir /E /R:3 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null")
            .AppendLine("if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }")
            .AppendLine()
            .AppendLine("Start-Process $restartExe")
            .AppendLine("Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue")
            .AppendLine("Remove-Item $packageFile -Force -ErrorAction SilentlyContinue")
            .AppendLine("Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue")
            .ToString();

        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));
        var cmdContent = $"@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}\r\ndel /q \"%~f0\" 2>nul\r\n";
        File.WriteAllText(scriptPath, cmdContent, new UTF8Encoding(false));
        return scriptPath;
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // 清理失败只影响 %TEMP% 残留，不值得让更新流程失败
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string EscapeForPowerShell(string value)
    {
        return value.Replace("'", "''");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private static string ReadValue(IConfiguration configuration, string key, string defaultValue = "")
    {
        return configuration[key] ?? defaultValue;
    }

    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = version.Trim();
        var metadataSeparatorIndex = normalized.IndexOf('+');
        if (metadataSeparatorIndex >= 0)
        {
            normalized = normalized[..metadataSeparatorIndex];
        }

        return normalized.Trim();
    }

    private static Version ParseComparableVersion(string version)
    {
        return TryParseComparableVersion(version, out var parsedVersion)
            ? parsedVersion
            : new Version(0, 0, 0, 0);
    }

    private static bool TryParseComparableVersion(string version, out Version parsedVersion)
    {
        if (!Version.TryParse(NormalizeVersion(version), out var rawVersion))
        {
            parsedVersion = new Version(0, 0, 0, 0);
            return false;
        }

        parsedVersion = new Version(
            rawVersion.Major,
            rawVersion.Minor,
            rawVersion.Build < 0 ? 0 : rawVersion.Build,
            rawVersion.Revision < 0 ? 0 : rawVersion.Revision);
        return true;
    }

    private static async Task<List<AppUpdateManifestItemDto>?> ReadManifestAsync(
        Stream manifestStream,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.Deserialize<List<AppUpdateManifestItemDto>>(),
            JsonValueKind.Object => document.RootElement.Deserialize<AppUpdateManifestItemDto>() is { } item
                ? [item]
                : null,
            _ => null
        };
    }
}
