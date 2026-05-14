namespace ApixPress.App.Models.DTOs;

public sealed class ProjectHttpAuthSettingsDto
{
    public const string ModeNone = "none";
    public const string ModeBearer = "bearer";

    public string ProjectId { get; init; } = string.Empty;
    public string AuthMode { get; init; } = ModeNone;
    public string BearerToken { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }

    public bool IsBearerEnabled =>
        string.Equals(AuthMode, ModeBearer, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(BearerToken);
}
