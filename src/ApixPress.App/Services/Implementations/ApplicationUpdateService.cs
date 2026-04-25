using System.Diagnostics;
using System.Net.Http;
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
        Func<string> tempDirectoryProvider)
    {
        _httpClient = httpClient;
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

            var latestRelease = manifest
                .Where(item => TryParseComparableVersion(item.Version, out _))
                .OrderByDescending(item => item.PubTime)
                .ThenByDescending(item => ParseComparableVersion(item.Version))
                .FirstOrDefault();

            if (latestRelease is null)
            {
                return ResultModel<AppUpdateCheckResultDto>.Failure("更新清单中未找到有效版本。", "app_update_invalid_manifest");
            }

            var latestVersion = ParseComparableVersion(latestRelease.Version);
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

    public async Task<IResultModel<bool>> StartUpdateAsync(AppUpdateCheckResultDto updateInfo, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return ResultModel<bool>.Failure("尚未配置更新清单地址。", "app_update_not_configured");
        }

        if (updateInfo is null)
        {
            return ResultModel<bool>.Failure("更新信息不能为空。", "app_update_info_missing");
        }

        var currentProcessPath = _processPathProvider();
        if (string.IsNullOrWhiteSpace(currentProcessPath))
        {
            return ResultModel<bool>.Failure("无法识别当前主程序名称。", "app_update_app_name_missing");
        }

        if (!File.Exists(currentProcessPath))
        {
            return ResultModel<bool>.Failure($"未找到主程序：{currentProcessPath}", "app_update_launcher_missing");
        }

        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            return ResultModel<bool>.Failure("更新包地址为空。", "app_update_package_url_missing");
        }

        if (string.IsNullOrWhiteSpace(updateInfo.PackageHash))
        {
            return ResultModel<bool>.Failure("更新包校验信息为空。", "app_update_package_hash_missing");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requestFilePath = await WriteLaunchRequestAsync(
                new AppUpdateLaunchRequestDto
                {
                    PackageName = updateInfo.PackageName,
                    PackageUrl = updateInfo.DownloadUrl,
                    PackageHash = updateInfo.PackageHash,
                    CurrentVersion = updateInfo.CurrentVersion,
                    TargetVersion = updateInfo.LatestVersion,
                    CurrentProcessId = _processIdProvider(),
                    RestartExecutablePath = currentProcessPath,
                    TargetDirectory = Path.GetDirectoryName(currentProcessPath) ?? _baseDirectoryProvider()
                },
                cancellationToken);
            var targetDirectory = Path.GetDirectoryName(currentProcessPath) ?? _baseDirectoryProvider();

            var startInfo = new ProcessStartInfo
            {
                FileName = currentProcessPath,
                Arguments = $"{AppUpdateRunner.ApplyUpdateArgument} {AppUpdateRunner.RequestFileArgument} \"{requestFilePath}\"",
                WorkingDirectory = targetDirectory,
                UseShellExecute = true
            };

            var process = _processStarter(startInfo);
            if (process is null)
            {
                return ResultModel<bool>.Failure("启动更新程序失败。", "app_update_start_process_failed");
            }

            return ResultModel<bool>.Success(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<bool>.Failure("启动更新已取消。", "app_update_start_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<bool>.Failure($"启动更新失败：{exception.Message}", "app_update_start_failed");
        }
    }

    private async Task<string> WriteLaunchRequestAsync(AppUpdateLaunchRequestDto request, CancellationToken cancellationToken)
    {
        var workspacePath = Path.Combine(_tempDirectoryProvider(), "ApixPress-Update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspacePath);

        var requestFilePath = Path.Combine(workspacePath, "request.json");
        var requestContent = JsonSerializer.Serialize(request);
        await File.WriteAllTextAsync(requestFilePath, requestContent, Encoding.UTF8, cancellationToken);
        return requestFilePath;
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
