using ApixPress.App.Helpers;

namespace ApixPress.App.Tests.Helpers;

public sealed class CancellationTokenSourceHelperTests
{
    [Fact]
    public void CancelAndDispose_ShouldTolerateCallbackClearingField()
    {
        CancellationTokenSource? source = new();
        source.Token.Register(() => source = null);
        CancellationTokenSourceHelper.CancelAndDispose(ref source);
        Assert.Null(source);
    }

    [Fact]
    public void CancelAndDispose_ShouldNotDisposeReplacementFromCallback()
    {
        using var replacement = new CancellationTokenSource();
        CancellationTokenSource? source = new();
        source.Token.Register(() => source = replacement);
        CancellationTokenSourceHelper.CancelAndDispose(ref source);
        Assert.Same(replacement, source);
        Assert.False(replacement.IsCancellationRequested);
        replacement.Cancel();
    }
}
