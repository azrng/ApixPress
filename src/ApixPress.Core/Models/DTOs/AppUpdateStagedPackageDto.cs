namespace ApixPress.App.Models.DTOs;

/// <summary>已下载、校验并解压就绪的更新包信息，供应用更新阶段生成替换脚本。</summary>
public sealed class AppUpdateStagedPackageDto
{
    /// <summary>临时工作区根目录（含安装包与解压产物）。</summary>
    public string WorkspaceDirectory { get; init; } = string.Empty;

    /// <summary>安装包 zip 文件完整路径。</summary>
    public string PackageFilePath { get; init; } = string.Empty;

    /// <summary>解压后的安装目录内容。</summary>
    public string ExtractDirectory { get; init; } = string.Empty;

    /// <summary>更新目标目录（当前安装目录）。</summary>
    public string TargetDirectory { get; init; } = string.Empty;

    /// <summary>完成后重启的主程序完整路径。</summary>
    public string RestartExecutablePath { get; init; } = string.Empty;

    public string PackageName { get; init; } = string.Empty;

    public string TargetVersion { get; init; } = string.Empty;
}
