using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApixPress.App.Models.DTOs;

namespace ApixPress.App.Services.Implementations;

internal static class AppUpdateRunner
{
    public const string ApplyUpdateArgument = "--apply-update";
    public const string RequestFileArgument = "--request-file";

    public static bool IsUpdateMode(IReadOnlyList<string> args)
    {
        return args.Any(argument => string.Equals(argument, ApplyUpdateArgument, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var requestFilePath = ParseRequestFilePath(args);
        if (string.IsNullOrWhiteSpace(requestFilePath) || !File.Exists(requestFilePath))
        {
            Console.Error.WriteLine("缺少更新请求文件。");
            return 1;
        }

        var request = await ReadRequestAsync(requestFilePath);
        if (request is null)
        {
            Console.Error.WriteLine("更新请求解析失败。");
            return 1;
        }

        ValidateRequest(request);

        var workspacePath = Path.GetDirectoryName(requestFilePath) ?? Path.GetTempPath();
        var packageFilePath = Path.Combine(
            workspacePath,
            string.IsNullOrWhiteSpace(request.PackageName) ? "update-package.zip" : $"{SanitizeFileName(request.PackageName)}.zip");
        var extractPath = Path.Combine(workspacePath, "package");

        await DownloadPackageAsync(request.PackageUrl, packageFilePath);
        await VerifyPackageHashAsync(packageFilePath, request.PackageHash);

        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, recursive: true);
        }

        ZipFile.ExtractToDirectory(packageFilePath, extractPath, overwriteFiles: true);

        var scriptPath = CreateApplyScript(workspacePath, extractPath, packageFilePath, request, Environment.ProcessId);
        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            WorkingDirectory = request.TargetDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        return 0;
    }

    private static string ParseRequestFilePath(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], RequestFileArgument, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    private static async Task<AppUpdateLaunchRequestDto?> ReadRequestAsync(string requestFilePath)
    {
        var content = await File.ReadAllTextAsync(requestFilePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<AppUpdateLaunchRequestDto>(content);
    }

    private static void ValidateRequest(AppUpdateLaunchRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.PackageUrl))
        {
            throw new InvalidOperationException("更新包地址为空。");
        }

        if (string.IsNullOrWhiteSpace(request.PackageHash))
        {
            throw new InvalidOperationException("更新包校验信息为空。");
        }

        if (string.IsNullOrWhiteSpace(request.TargetDirectory))
        {
            throw new InvalidOperationException("目标目录为空。");
        }

        if (string.IsNullOrWhiteSpace(request.RestartExecutablePath))
        {
            throw new InvalidOperationException("重启程序路径为空。");
        }
    }

    private static async Task DownloadPackageAsync(string packageUrl, string packageFilePath)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        await using var packageStream = await httpClient.GetStreamAsync(packageUrl);
        await using var outputStream = File.Create(packageFilePath);
        await packageStream.CopyToAsync(outputStream);
    }

    private static async Task VerifyPackageHashAsync(string packageFilePath, string expectedHash)
    {
        await using var fileStream = File.OpenRead(packageFilePath);
        var hashBytes = await SHA256.HashDataAsync(fileStream);
        var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("更新包哈希校验失败。");
        }
    }

    private static string CreateApplyScript(
        string workspacePath,
        string extractPath,
        string packageFilePath,
        AppUpdateLaunchRequestDto request,
        int updateProcessId)
    {
        var scriptPath = Path.Combine(workspacePath, "apply-update.cmd");

        var psScript = new StringBuilder()
            .AppendLine("$waitPid = " + request.CurrentProcessId)
            .AppendLine("$updatePid = " + updateProcessId)
            .AppendLine("$extractDir = '" + EscapeForPowerShell(extractPath) + "'")
            .AppendLine("$targetDir = '" + EscapeForPowerShell(request.TargetDirectory) + "'")
            .AppendLine("$restartExe = '" + EscapeForPowerShell(request.RestartExecutablePath) + "'")
            .AppendLine("$packageFile = '" + EscapeForPowerShell(packageFilePath) + "'")
            .AppendLine()
            .AppendLine("while (Get-Process -Id $waitPid -ErrorAction SilentlyContinue) {")
            .AppendLine("    Start-Sleep -Seconds 1")
            .AppendLine("}")
            .AppendLine("while (Get-Process -Id $updatePid -ErrorAction SilentlyContinue) {")
            .AppendLine("    Start-Sleep -Seconds 1")
            .AppendLine("}")
            .AppendLine()
            .AppendLine("robocopy $extractDir $targetDir /E /R:3 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null")
            .AppendLine("if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }")
            .AppendLine()
            .AppendLine("Start-Process $restartExe")
            .AppendLine("Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue")
            .AppendLine("Remove-Item $packageFile -Force -ErrorAction SilentlyContinue")
            .AppendLine("Remove-Item $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue")
            .ToString();

        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));
        var cmdContent = $"@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}\r\ndel /q \"%~f0\" 2>nul\r\n";
        File.WriteAllText(scriptPath, cmdContent, new UTF8Encoding(false));
        return scriptPath;
    }

    private static string EscapeForPowerShell(string value)
    {
        return value.Replace("'", "''");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }
}
