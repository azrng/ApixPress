namespace ApixPress.App.Helpers;

internal static class CancellationTokenSourceHelper
{
    public static CancellationTokenSource Refresh(ref CancellationTokenSource? cancellationTokenSource)
    {
        CancelAndDispose(ref cancellationTokenSource);
        cancellationTokenSource = new CancellationTokenSource();
        return cancellationTokenSource;
    }

    public static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource is null)
        {
            return;
        }

        cancellationTokenSource.Cancel();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = null;
    }
}
