namespace ApixPress.App.Models.DTOs;

public sealed class AppShellSettingsDto
{
    public string StorageDirectoryPath { get; init; } = string.Empty;

    public int RequestTimeoutMilliseconds { get; init; } = 30000;

    public bool ValidateSslCertificate { get; init; } = true;

    public bool AutoFollowRedirects { get; init; } = true;

    public bool SendNoCacheHeader { get; init; }

    public bool EnableVerboseLogging { get; init; }

    public bool EnableUpdateReminder { get; init; } = true;

    /// <summary>强调色预设标识（teal/blue/purple），对应 AccentPaletteService 预设表（T123）</summary>
    public string AccentTheme { get; init; } = "teal";
}
