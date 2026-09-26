namespace ApixPress.App.Models.Entities;

public sealed class ProjectExportSettingsEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string Format { get; set; } = "backup";
    public bool IncludeInterfaces { get; set; } = true;
    public bool IncludeRequestCases { get; set; } = true;
    public bool IncludeEnvironments { get; set; } = true;
    public bool IncludeRequestHistory { get; set; }
    public string LastOutputDirectory { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
