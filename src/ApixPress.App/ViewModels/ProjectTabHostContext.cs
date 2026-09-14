using ApixPress.App.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;

namespace ApixPress.App.ViewModels;

internal sealed class ProjectTabHostContext
{
    public required Func<RequestWorkspaceTabViewModel?> GetActiveWorkspaceTab { get; init; }
    public required Func<bool> IsInterfaceRootWorkspaceActive { get; init; }
    public required IMessenger Messenger { get; init; }

    /// <summary>弹层入口：项目内保存/生成代码/导入/目录弹窗经此打开。</summary>
    public required IDialogService DialogService { get; init; }
}
