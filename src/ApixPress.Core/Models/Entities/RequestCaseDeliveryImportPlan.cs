namespace ApixPress.App.Models.Entities;

/// <summary>请求用例交付包的一次性事务写入计划。</summary>
public sealed class RequestCaseDeliveryImportPlan
{
    public string ProjectId { get; init; } = string.Empty;
    public ApiDocumentEntity? Document { get; init; }
    public IReadOnlyList<ApiEndpointEntity> Endpoints { get; init; } = [];
    public IReadOnlyList<RequestParameterEntity> Parameters { get; init; } = [];
    public IReadOnlyList<RequestCaseEntity> RequestCases { get; init; } = [];
    public IReadOnlyList<ProjectEnvironmentEntity> Environments { get; init; } = [];
    public IReadOnlyList<EnvironmentVariableEntity> Variables { get; init; } = [];
    public IReadOnlyList<string> EndpointIdsToDelete { get; init; } = [];
    public IReadOnlyList<string> DocumentIdsToDelete { get; init; } = [];
    public IReadOnlyList<string> EnvironmentIdsToClearVariables { get; init; } = [];
}
