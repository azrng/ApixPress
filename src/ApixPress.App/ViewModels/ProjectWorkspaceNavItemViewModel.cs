using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApixPress.App.ViewModels;

public partial class ProjectWorkspaceNavItemViewModel : ObservableObject
{
    public ProjectWorkspaceNavItemViewModel(string sectionKey, string title, string iconData, ICommand command)
    {
        SectionKey = sectionKey;
        Title = title;
        IconData = iconData;
        Command = command;
    }

    public string SectionKey { get; }

    public string Title { get; }

    public string IconData { get; }

    public ICommand Command { get; }

    [ObservableProperty]
    private bool isSelected;
}
