using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;

namespace ApixPress.App.ViewModels;

public partial class ProjectCreationViewModel : ViewModelBase
{
    private readonly Func<string> _buildNextProjectName;
    private readonly Func<string, string, Task<bool>> _createProjectAsync;

    public ProjectCreationViewModel(
        Func<string> buildNextProjectName,
        Func<string, string, Task<bool>> createProjectAsync)
    {
        _buildNextProjectName = buildNextProjectName;
        _createProjectAsync = createProjectAsync;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private string draftProjectName = string.Empty;

    [ObservableProperty]
    private string draftProjectDescription = string.Empty;

    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private async Task CreateProjectAsync()
    {
        var projectName = DraftProjectName.Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return;
        }

        var isCreated = await _createProjectAsync(projectName, DraftProjectDescription.Trim());
        if (!isCreated)
        {
            return;
        }

        DraftProjectName = string.Empty;
        DraftProjectDescription = string.Empty;
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
