namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabHostContext
{
    public required Func<RequestWorkspaceTabViewModel?> GetActiveWorkspaceTab { get; init; }
    public required Action<string> SetStatusMessage { get; init; }
    public required Action NotifyShellState { get; init; }
    public required Action NotifyWorkspaceEditorState { get; init; }
    public required Action NotifyWorkspaceBindingsChanged { get; init; }
    public required Action NotifyActiveWorkspaceTabChanged { get; init; }
    public required Action NotifyWorkspaceTabMenuChanged { get; init; }
    public required Action<bool> SetBusyState { get; init; }
}
