using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ApixPress.App.Messages;
using ApixPress.App.ViewModels.Base;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace ApixPress.App.ViewModels;

public partial class ProjectQuickRequestSaveViewModel : ViewModelBase, IDialogContext
{
    private readonly ProjectTabWorkspaceContext _workspaceContext;
    private readonly Func<RequestWorkspaceTabViewModel, string?, Task<bool>> _saveQuickRequestAsync;
    private readonly ProjectTabHostContext _hostContext;

    internal ProjectQuickRequestSaveViewModel(
        ProjectTabWorkspaceContext workspaceContext,
        Func<RequestWorkspaceTabViewModel, string?, Task<bool>> saveQuickRequestAsync,
        ProjectTabHostContext hostContext)
    {
        _workspaceContext = workspaceContext;
        _saveQuickRequestAsync = saveQuickRequestAsync;
        _hostContext = hostContext;
    }

    [ObservableProperty]
    private string draftName = string.Empty;

    [ObservableProperty]
    private string draftDescription = string.Empty;

    /// <summary>弹窗异步关闭通道：取消/保存成功都会经此关闭 Ursa 弹窗。</summary>
    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    /// <summary>标签切换 / 数据清理时的防御性收口：弹窗未打开时无订阅者，为空操作。</summary>
    public void Dismiss() => RequestClose?.Invoke(this, DialogResult.None);

    /// <summary>预填草稿并打开「保存快捷请求」弹窗；调用方为同步上下文，弹窗生命周期异步等待。</summary>
    public void OpenDialogFor(RequestWorkspaceTabViewModel workspaceTab)
    {
        var fallbackName = string.IsNullOrWhiteSpace(workspaceTab.ConfigTab.RequestName)
            ? workspaceTab.ResolveRequestName()
            : workspaceTab.ConfigTab.RequestName.Trim();
        DraftName = fallbackName;
        DraftDescription = workspaceTab.ConfigTab.RequestDescription;
        _hostContext.Messenger.Send(new StatusMessageRequest("请输入快捷请求名称后再保存。"));
        _ = ShowDialogAsync();
    }

    private async Task ShowDialogAsync()
    {
        await _hostContext.DialogService.ShowDialogAsync<Views.Controls.QuickRequestSaveDialogView>(this, "保存快捷请求");
    }

    [RelayCommand]
    private void CloseDialog()
    {
        RequestClose?.Invoke(this, DialogResult.None);
        _hostContext.Messenger.Send(new StatusMessageRequest("已取消保存快捷请求。"));
    }

    [RelayCommand]
    private async Task ConfirmSaveAsync()
    {
        var workspaceTab = _workspaceContext.GetActiveWorkspaceTab();
        if (workspaceTab is null || !workspaceTab.IsQuickRequestTab)
        {
            RequestClose?.Invoke(this, DialogResult.None);
            return;
        }

        if (string.IsNullOrWhiteSpace(DraftName))
        {
            _hostContext.Messenger.Send(new StatusMessageRequest("请输入快捷请求名称。"));
            return;
        }

        workspaceTab.ConfigTab.RequestName = DraftName.Trim();
        workspaceTab.ConfigTab.RequestDescription = DraftDescription.Trim();
        var isSaved = await _saveQuickRequestAsync(workspaceTab, workspaceTab.ConfigTab.RequestName);
        if (isSaved)
        {
            RequestClose?.Invoke(this, DialogResult.OK);
        }
    }
}
