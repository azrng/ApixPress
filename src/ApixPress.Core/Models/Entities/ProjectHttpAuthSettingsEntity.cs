namespace ApixPress.App.Models.Entities;

public sealed class ProjectHttpAuthSettingsEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string AuthMode { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public string BasicUsername { get; set; } = string.Empty;
    public string BasicPassword { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
