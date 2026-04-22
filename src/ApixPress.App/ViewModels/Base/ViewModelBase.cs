using CommunityToolkit.Mvvm.ComponentModel;

namespace ApixPress.App.ViewModels.Base;

public abstract partial class ViewModelBase : ObservableObject, IDisposable
{
    protected bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        DisposeManaged();
        GC.SuppressFinalize(this);
    }

    protected virtual void DisposeManaged()
    {
    }
}
