namespace ApixPress.App.Models.Entities;

public sealed class EnvironmentVariableEntity
{
    public string Id { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Default";
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
