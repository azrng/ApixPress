namespace ApixPress.App.Models.DTOs;

public sealed class AppUpdateLaunchRequestDto
{
    public string PackageName { get; init; } = string.Empty;

    public string PackageUrl { get; init; } = string.Empty;

    public string PackageHash { get; init; } = string.Empty;

    public string CurrentVersion { get; init; } = string.Empty;

    public string TargetVersion { get; init; } = string.Empty;

    public int CurrentProcessId { get; init; }

    public string RestartExecutablePath { get; init; } = string.Empty;

    public string TargetDirectory { get; init; } = string.Empty;
}
