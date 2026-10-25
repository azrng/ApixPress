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
        // 先移交字段所有权：Cancel 会同步运行回调，回调可能清空字段或替换为新请求令牌。
        var previous = Interlocked.Exchange(ref cancellationTokenSource, null);
        if (previous is null)
        {
            return;
        }

        try
        {
            previous.Cancel();
        }
        finally
        {
            previous.Dispose();
        }
    }
}
