using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class HttpRequestUrlRowView : UserControl
{
    public HttpRequestUrlRowView()
    {
        InitializeComponent();
        RequestUrlInput.AddHandler(KeyDownEvent, OnRequestUrlKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnRequestUrlKeyDown(object? sender, KeyEventArgs e)
    {
        // URL 栏回车直接发送（单行输入框，Enter 不承担换行职责）；发送中不重发
        if (e.Key is not (Key.Enter or Key.Return) || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (DataContext is not ProjectTabViewModel viewModel
            || viewModel.ResponseSection.IsLoading
            || !viewModel.SendRequestCommand.CanExecute(null))
        {
            return;
        }

        e.Handled = true;
        viewModel.SendRequestCommand.Execute(null);
    }
}
