using System.Windows.Input;

namespace ApixPress.App.ViewModels;

public sealed class ProjectWorkspaceNavItemViewModel
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
}
