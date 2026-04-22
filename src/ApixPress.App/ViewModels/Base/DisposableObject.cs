namespace ApixPress.App.ViewModels.Base;

public abstract class DisposableObject : IDisposable
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

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    protected virtual void DisposeManaged()
    {
    }
}
