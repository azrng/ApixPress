using System.Text;
using System.Text.Json;
using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class AppShellSettingsService : IAppShellSettingsService, ISingletonDependency
{
    private readonly string _settingsFilePath;

    public AppShellSettingsService()
        : this(WorkspacePaths.ResolveFromBaseDirectory(Path.Combine("Data", "app-shell-settings.json")))
    {
    }

    public AppShellSettingsService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public async Task<IResultModel<AppShellSettingsDto>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return ResultModel<AppShellSettingsDto>.Success(new AppShellSettingsDto());
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath, Encoding.UTF8, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return ResultModel<AppShellSettingsDto>.Success(new AppShellSettingsDto());
            }

            var settings = JsonSerializer.Deserialize<AppShellSettingsDto>(json) ?? new AppShellSettingsDto();
            return ResultModel<AppShellSettingsDto>.Success(settings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<AppShellSettingsDto>.Failure("设置读取已取消。", "app_shell_settings_load_cancelled");
        }
        catch (JsonException exception)
        {
            return ResultModel<AppShellSettingsDto>.Failure($"设置文件格式无效：{exception.Message}", "app_shell_settings_invalid_json");
        }
        catch (Exception exception)
        {
            return ResultModel<AppShellSettingsDto>.Failure($"设置读取失败：{exception.Message}", "app_shell_settings_load_failed");
        }
    }

    public async Task<IResultModel<AppShellSettingsDto>> SaveAsync(AppShellSettingsDto settings, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_settingsFilePath, json, Encoding.UTF8, cancellationToken);
            return ResultModel<AppShellSettingsDto>.Success(settings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ResultModel<AppShellSettingsDto>.Failure("设置保存已取消。", "app_shell_settings_save_cancelled");
        }
        catch (Exception exception)
        {
            return ResultModel<AppShellSettingsDto>.Failure($"设置保存失败：{exception.Message}", "app_shell_settings_save_failed");
        }
    }
}
