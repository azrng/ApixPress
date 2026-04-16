namespace ApixPress.App.Models.DTOs;

public sealed class AppUpdateCheckResultDto
{
    public string CurrentVersion { get; init; } = string.Empty;

    public string LatestVersion { get; init; } = string.Empty;

    public bool HasUpdate { get; init; }

    public DateTime? PublishedAt { get; init; }

    public string DownloadUrl { get; init; } = string.Empty;
}
