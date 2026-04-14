namespace ApixPress.App.Models.DTOs;

public sealed class EnvironmentVariableDto
{
    public string Id { get; init; } = string.Empty;
    public string EnvironmentId { get; init; } = string.Empty;
    public string EnvironmentName { get; init; } = "Default";
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
}
