using CommunityToolkit.Mvvm.Messaging;

namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabHostContext
{
    public required Func<RequestWorkspaceTabViewModel?> GetActiveWorkspaceTab { get; init; }
    public required Func<bool> IsInterfaceRootWorkspaceActive { get; init; }
    public required IMessenger Messenger { get; init; }
}
