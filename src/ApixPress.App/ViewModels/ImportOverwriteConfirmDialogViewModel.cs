using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace ApixPress.App.ViewModels;

/// <summary>
/// 导入覆盖确认弹窗的独立关闭上下文：覆盖确认叠加在导入主弹窗之上，
/// 若与主弹窗共用 ProjectImportViewModel 的关闭通道，确认/取消会连带关闭底层导入弹窗。
/// </summary>
public sealed partial class ImportOverwriteConfirmDialogViewModel : ViewModelBase, IDialogContext
{
    public ProjectImportViewModel Import { get; }

    public ImportOverwriteConfirmDialogViewModel(ProjectImportViewModel import)
    {
        Import = import;
    }

    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    [RelayCommand]
    private void Confirm() => RequestClose?.Invoke(this, DialogResult.OK);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, DialogResult.Cancel);
}
