using ApixPress.App.Helpers;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    partial void OnRequestTimeoutMillisecondsChanged(decimal value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnValidateSslCertificateChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnAutoFollowRedirectsChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnSendNoCacheHeaderChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnEnableVerboseLoggingChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    partial void OnEnableUpdateReminderChanged(bool value)
    {
        TriggerShellSettingsSave();
    }

    private async Task LoadShellSettingsAsync()
    {
        _isApplyingShellSettings = true;
        var result = await _appShellSettingsService.LoadAsync(CancellationToken.None);
        var settings = result.Data ?? new AppShellSettingsDto();

        RequestTimeoutMilliseconds = settings.RequestTimeoutMilliseconds;
        ValidateSslCertificate = settings.ValidateSslCertificate;
        AutoFollowRedirects = settings.AutoFollowRedirects;
        SendNoCacheHeader = settings.SendNoCacheHeader;
        EnableVerboseLogging = settings.EnableVerboseLogging;
        EnableUpdateReminder = settings.EnableUpdateReminder;
        GeneralSettingsSaveStatus = result.IsSuccess
            ? "设置会自动保存到本地工作目录。"
            : "设置读取失败，已回退默认值。";
        _isApplyingShellSettings = false;
    }

    private void TriggerShellSettingsSave()
    {
        if (_isApplyingShellSettings || !_initialized)
        {
            return;
        }

        var cancellationToken = CancellationTokenSourceHelper.Refresh(ref _shellSettingsSaveCancellationTokenSource).Token;
        _ = SaveShellSettingsDeferredAsync(cancellationToken);
    }

    private async Task SaveShellSettingsDeferredAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            await SaveShellSettingsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task SaveShellSettingsAsync(CancellationToken cancellationToken)
    {
        await _shellSettingsSaveSemaphore.WaitAsync(cancellationToken);
        try
        {
            var result = await _appShellSettingsService.SaveAsync(new AppShellSettingsDto
            {
                RequestTimeoutMilliseconds = (int)RequestTimeoutMilliseconds,
                ValidateSslCertificate = ValidateSslCertificate,
                AutoFollowRedirects = AutoFollowRedirects,
                SendNoCacheHeader = SendNoCacheHeader,
                EnableVerboseLogging = EnableVerboseLogging,
                EnableUpdateReminder = EnableUpdateReminder
            }, cancellationToken);

            GeneralSettingsSaveStatus = result.IsSuccess
                ? $"已自动保存 {DateTime.Now:HH:mm:ss}"
                : $"保存失败：{result.Message}";
            NotifyShellState();
        }
        finally
        {
            _shellSettingsSaveSemaphore.Release();
        }
    }
}
