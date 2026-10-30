using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Services;

public sealed class OpenApiImportSourceReaderTests
{
    [Fact]
    public async Task ReadLimitedDocumentAsync_StopsBeforeReadingEntireOversizedStream()
    {
        using var stream = new TrackingStream(new byte[25 * 1024 * 1024]);
        using var content = new StreamContent(stream);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OpenApiImportSourceReader.ReadLimitedDocumentAsync(content, CancellationToken.None));
        Assert.True(stream.BytesRead < 25 * 1024 * 1024);
    }

    [Fact]
    public async Task ReadLimitedDocumentAsync_PreservesDeclaredEncoding()
    {
        using var content = new StringContent("测试", System.Text.Encoding.Unicode, "application/json");
        Assert.Equal("测试", await OpenApiImportSourceReader.ReadLimitedDocumentAsync(content, CancellationToken.None));
    }
    private sealed class TrackingStream : MemoryStream
    {
        public TrackingStream(byte[] bytes) : base(bytes) { }
        public long BytesRead { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }
    }
}
