using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectQuickRequestSaveViewModel : ViewModelBase
{
    private readonly Func<RequestWorkspaceTabViewModel?> _getActiveWorkspaceTab;
    private readonly Func<RequestWorkspaceTabViewModel, string?, Task<bool>> _saveQuickRequestAsync;
    private readonly Action<string> _setStatusMessage;

    public ProjectQuickRequestSaveViewModel(
        Func<RequestWorkspaceTabViewModel?> getActiveWorkspaceTab,
        Func<RequestWorkspaceTabViewModel, string?, Task<bool>> saveQuickRequestAsync,
        Action<string> setStatusMessage)
    {
        _getActiveWorkspaceTab = getActiveWorkspaceTab;
        _saveQuickRequestAsync = saveQuickRequestAsync;
        _setStatusMessage = setStatusMessage;
    }

    [ObservableProperty]
    private bool isDialogOpen;

    [ObservableProperty]
    private string draftName = string.Empty;

    [ObservableProperty]
    private string draftDescription = string.Empty;

    public void OpenDialogFor(RequestWorkspaceTabViewModel workspaceTab)
    {
        var fallbackName = string.IsNullOrWhiteSpace(workspaceTab.ConfigTab.RequestName)
            ? workspaceTab.ResolveRequestName()
            : workspaceTab.ConfigTab.RequestName.Trim();
        DraftName = fallbackName;
        DraftDescription = workspaceTab.ConfigTab.RequestDescription;
        IsDialogOpen = true;
        _setStatusMessage("请输入快捷请求名称后再保存。");
    }

    public void Dismiss()
    {
        IsDialogOpen = false;
    }

    [RelayCommand]
    private void CloseDialog()
    {
        Dismiss();
        _setStatusMessage("已取消保存快捷请求。");
    }

    [RelayCommand]
    private async Task ConfirmSaveAsync()
    {
        var workspaceTab = _getActiveWorkspaceTab();
        if (workspaceTab is null || !workspaceTab.IsQuickRequestTab)
        {
            Dismiss();
            return;
        }

        if (string.IsNullOrWhiteSpace(DraftName))
        {
            _setStatusMessage("请输入快捷请求名称。");
            return;
        }

        workspaceTab.ConfigTab.RequestName = DraftName.Trim();
        workspaceTab.ConfigTab.RequestDescription = DraftDescription.Trim();
        var isSaved = await _saveQuickRequestAsync(workspaceTab, workspaceTab.ConfigTab.RequestName);
        if (isSaved)
        {
            Dismiss();
        }
    }
}
