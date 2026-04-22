namespace ApixPress.Updater;

internal sealed class UpdateLaunchRequest
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
