using ApixPress.App.ViewModels;
using Avalonia.Input;
using Avalonia.Controls;

namespace ApixPress.App.Views.Controls;

public partial class QuickRequestWorkbenchView : UserControl
{
    public QuickRequestWorkbenchView()
    {
        InitializeComponent();
    }
    private void OnResponseSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ProjectTabViewModel viewModel) return;
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? viewModel.ResponseSection.PreviousBodySearchMatchCommand
            : viewModel.ResponseSection.NextBodySearchMatchCommand;
        if (command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }
}
