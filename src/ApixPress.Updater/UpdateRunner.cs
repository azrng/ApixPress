using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApixPress.Updater;

internal static class UpdateRunner
{
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

        var scriptPath = CreateApplyScript(workspacePath, extractPath, packageFilePath, request);
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
            if (string.Equals(args[index], "--request-file", StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    private static async Task<UpdateLaunchRequest?> ReadRequestAsync(string requestFilePath)
    {
        var content = await File.ReadAllTextAsync(requestFilePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<UpdateLaunchRequest>(content);
    }

    private static void ValidateRequest(UpdateLaunchRequest request)
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
        UpdateLaunchRequest request)
    {
        var scriptPath = Path.Combine(workspacePath, "apply-update.cmd");
        var scriptContent = $$"""
@echo off
setlocal
set "WAIT_PID={{request.CurrentProcessId}}"
set "EXTRACT_DIR={{EscapeForBatch(extractPath)}}"
set "TARGET_DIR={{EscapeForBatch(request.TargetDirectory)}}"
set "RESTART_EXE={{EscapeForBatch(request.RestartExecutablePath)}}"
set "PACKAGE_FILE={{EscapeForBatch(packageFilePath)}}"

:wait_loop
tasklist /FI "PID eq %WAIT_PID%" | find "%WAIT_PID%" >nul
if not errorlevel 1 (
  timeout /t 1 /nobreak >nul
  goto wait_loop
)

robocopy "%EXTRACT_DIR%" "%TARGET_DIR%" /E /R:3 /W:1 /NFL /NDL /NJH /NJS /NP >nul
set "ROBOCOPY_EXIT=%ERRORLEVEL%"
if %ROBOCOPY_EXIT% GEQ 8 exit /b %ROBOCOPY_EXIT%

start "" "%RESTART_EXE%"
rd /s /q "%EXTRACT_DIR%" 2>nul
del /q "%PACKAGE_FILE%" 2>nul
(goto) 2>nul & del "%~f0"
""";

        File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(false));
        return scriptPath;
    }

    private static string EscapeForBatch(string value)
    {
        return value.Replace("^", "^^").Replace("%", "%%");
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
