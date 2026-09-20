namespace ApixPress.App.ViewModels.Base;

public abstract class DisposableObject : IDisposable
{
    private int _isDisposed;

    protected bool IsDisposed => _isDisposed != 0;

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0)
        {
            return;
        }

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
