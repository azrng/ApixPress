namespace ApixPress.App.Models.DTOs;

public sealed class ImportedHttpInterfaceSyncResultDto
{
    public IReadOnlyList<RequestCaseDto> UpsertedCases { get; init; } = [];

    public IReadOnlyList<string> DeletedCaseIds { get; init; } = [];
}
