namespace ApixPress.App.ViewModels;

public partial class MainWindowViewModel
{
    private void NotifyActiveProjectTabBindings()
    {
        OnPropertyChanged(nameof(ConfigTab));
        OnPropertyChanged(nameof(ResponseSection));
        OnPropertyChanged(nameof(EnvironmentPanel));
        OnPropertyChanged(nameof(UseCasesPanel));
        OnPropertyChanged(nameof(HistoryPanel));
        OnPropertyChanged(nameof(RequestHistory));
    }
}
