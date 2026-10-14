using Avalonia.Controls;
using Avalonia.Input;
using ApixPress.App.ViewModels;

namespace ApixPress.App.Views.Controls;

public partial class MainWindowHomeView : UserControl
{
    private static readonly DataFormat<string> ProjectIdDragFormat =
        DataFormat.CreateInProcessFormat<string>("apixpress/project-id");

    public MainWindowHomeView()
    {
        InitializeComponent();
    }

    private async void OnProjectDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control handle
            || handle.DataContext is not ProjectWorkspaceItemViewModel project
            || DataContext is not MainWindowViewModel viewModel
            || !viewModel.ProjectPanel.CanReorderProjects
            || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.Create(ProjectIdDragFormat, project.Id));
        e.Handled = true;
        await DragDrop.DoDragDropAsync(e, dataTransfer, DragDropEffects.Move);
    }

    private void OnProjectTileDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Control tile
            || tile.DataContext is not ProjectWorkspaceItemViewModel target
            || DataContext is not MainWindowViewModel viewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var sourceProjectId = e.DataTransfer.TryGetValue(ProjectIdDragFormat);
        if (!viewModel.ProjectPanel.CanReorderProjects
            || string.IsNullOrWhiteSpace(sourceProjectId)
            || string.Equals(sourceProjectId, target.Id, StringComparison.OrdinalIgnoreCase))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        tile.Classes.Add("HomeProjectTileDropTarget");
        e.Handled = true;
    }

    private void OnProjectTileDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Control target)
        {
            target.Classes.Remove("HomeProjectTileDropTarget");
        }
    }

    private async void OnProjectTileDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Control tile
            || tile.DataContext is not ProjectWorkspaceItemViewModel target
            || DataContext is not MainWindowViewModel viewModel
            )
        {
            return;
        }

        var sourceProjectId = e.DataTransfer.TryGetValue(ProjectIdDragFormat);
        if (string.IsNullOrWhiteSpace(sourceProjectId))
        {
            return;
        }

        tile.Classes.Remove("HomeProjectTileDropTarget");
        e.Handled = true;
        await viewModel.ProjectPanel.MoveProjectAsync(sourceProjectId, target.Id);
    }
}
