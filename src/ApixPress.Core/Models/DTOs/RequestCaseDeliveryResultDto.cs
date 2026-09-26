namespace ApixPress.App.Models.DTOs;

public sealed class RequestCaseDeliveryResultDto
{
    public string FilePath { get; init; } = string.Empty;
    public int InterfaceCount { get; init; }
    public int RequestCaseCount { get; init; }
    public int EnvironmentCount { get; init; }
    public int VariableCount { get; init; }
    public int AddedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int SkippedCount { get; init; }
    public int DuplicatedCount { get; init; }
}
