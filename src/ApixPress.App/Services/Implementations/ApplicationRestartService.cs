using System.Diagnostics;
using ApixPress.App.Services.Interfaces;
using Azrng.Core.DependencyInjection;
using Azrng.Core.Results;

namespace ApixPress.App.Services.Implementations;

public sealed class ApplicationRestartService : IApplicationRestartService, ISingletonDependency
{
    private readonly Func<ProcessStartInfo, Process?> _processStarter;
    private readonly Func<string?> _processPathProvider;

    public ApplicationRestartService()
        : this(
            startInfo => Process.Start(startInfo),
            () => Environment.ProcessPath)
    {
    }

    public ApplicationRestartService(
        Func<ProcessStartInfo, Process?> processStarter,
        Func<string?> processPathProvider)
    {
        _processStarter = processStarter;
        _processPathProvider = processPathProvider;
    }

    public Task<IResultModel<bool>> RestartAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentProcessPath = _processPathProvider();
            if (string.IsNullOrWhiteSpace(currentProcessPath))
            {
                return Task.FromResult<IResultModel<bool>>(
                    ResultModel<bool>.Failure("无法识别当前主程序路径。", "app_restart_path_missing"));
            }

            if (!File.Exists(currentProcessPath))
            {
                return Task.FromResult<IResultModel<bool>>(
                    ResultModel<bool>.Failure($"未找到主程序：{currentProcessPath}", "app_restart_executable_missing"));
            }

            var process = _processStarter(new ProcessStartInfo
            {
                FileName = currentProcessPath,
                WorkingDirectory = Path.GetDirectoryName(currentProcessPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true
            });

            return Task.FromResult<IResultModel<bool>>(process is null
                ? ResultModel<bool>.Failure("启动新应用进程失败。", "app_restart_start_failed")
                : ResultModel<bool>.Success(true));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult<IResultModel<bool>>(
                ResultModel<bool>.Failure("重启已取消。", "app_restart_cancelled"));
        }
        catch (Exception exception)
        {
            return Task.FromResult<IResultModel<bool>>(
                ResultModel<bool>.Failure($"重启失败：{exception.Message}", "app_restart_failed"));
        }
    }
}
