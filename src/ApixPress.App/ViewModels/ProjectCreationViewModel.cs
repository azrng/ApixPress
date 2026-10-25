using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectCreationViewModel : ViewModelBase
{
    private readonly Func<string> _buildNextProjectName;
    private readonly Func<string, string, Task<string?>> _createProjectAsync;

    public ProjectCreationViewModel(
        Func<string> buildNextProjectName,
        Func<string, string, Task<string?>> createProjectAsync)
    {
        _buildNextProjectName = buildNextProjectName;
        _createProjectAsync = createProjectAsync;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private string draftProjectName = string.Empty;

    [ObservableProperty]
    private string draftProjectDescription = string.Empty;

    /// <summary>创建失败时显示的原因；修改项目名称后清除。</summary>
    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// <summary>使用输入的名称和备注创建项目。</summary>
    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private async Task CreateProjectAsync()
    {
        var projectName = DraftProjectName.Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return;
        }

        var error = await _createProjectAsync(projectName, DraftProjectDescription.Trim());
        if (error is not null)
        {
            ErrorMessage = error;
            return;
        }

        DraftProjectName = string.Empty;
        DraftProjectDescription = string.Empty;
    }

    partial void OnDraftProjectNameChanged(string value)
    {
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void UseProjectTemplate()
    {
        DraftProjectName = _buildNextProjectName();
    }

    private bool CanCreateProject()
    {
        return !string.IsNullOrWhiteSpace(DraftProjectName);
    }
}
