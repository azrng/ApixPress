using CommunityToolkit.Mvvm.Input;
using ApixPress.App.ViewModels.Base;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace ApixPress.App.ViewModels;

/// <summary>用例删除确认弹窗的独立关闭上下文，避免误删并明确展示待删除用例。</summary>
public sealed partial class RequestCaseDeleteConfirmDialogViewModel : ViewModelBase, IDialogContext
{
    public RequestCaseDeleteConfirmDialogViewModel(string caseName)
    {
        CaseName = caseName;
    }

    public string CaseName { get; }

    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    [RelayCommand]
    private void Confirm() => RequestClose?.Invoke(this, DialogResult.OK);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(this, DialogResult.Cancel);
}
