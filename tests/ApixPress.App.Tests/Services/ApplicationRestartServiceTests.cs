using System.Diagnostics;
using ApixPress.App.Services.Implementations;

namespace ApixPress.App.Tests.Services;

public sealed class ApplicationRestartServiceTests
{
    [Fact]
    public async Task RestartAsync_ShouldStartCurrentExecutable()
    {
        var executablePath = Path.Combine(Path.GetTempPath(), $"ApixPress-restart-test-{Guid.NewGuid():N}.exe");
        await File.WriteAllTextAsync(executablePath, string.Empty);
        ProcessStartInfo? capturedStartInfo = null;
        var service = new ApplicationRestartService(
            startInfo =>
            {
                capturedStartInfo = startInfo;
                return new Process();
            },
            () => executablePath);

        try
        {
            var result = await service.RestartAsync(CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(capturedStartInfo);
            Assert.Equal(executablePath, capturedStartInfo!.FileName);
            Assert.Equal(Path.GetDirectoryName(executablePath), capturedStartInfo.WorkingDirectory);
            Assert.True(capturedStartInfo.UseShellExecute);
        }
        finally
        {
            File.Delete(executablePath);
        }
    }

    [Fact]
    public async Task RestartAsync_ShouldReturnFailureWhenExecutableMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe");
        var service = new ApplicationRestartService(
            _ => new Process(),
            () => missingPath);

        var result = await service.RestartAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("未找到主程序", result.Message, StringComparison.Ordinal);
    }
}
