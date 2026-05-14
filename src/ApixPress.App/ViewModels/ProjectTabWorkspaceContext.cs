namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabWorkspaceContext
{
    public required Func<RequestWorkspaceTabViewModel?> GetActiveWorkspaceTab { get; init; }
    public required Func<RequestWorkspaceTabViewModel> GetFallbackWorkspaceTab { get; init; }
    public required Func<string> GetCurrentBaseUrl { get; init; }
    public required Func<IReadOnlyDictionary<string, string>> GetActiveVariables { get; init; }
    public required Func<bool> IsInterfaceRootWorkspaceActive { get; init; }
    public required Func<bool> HasHistory { get; init; }
}
