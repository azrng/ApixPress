namespace ApixPress.App.Models.DTOs;

/// <summary>更新包准备进度所处的阶段。</summary>
public enum AppUpdateProgressStage
{
    /// <summary>正在下载更新包。</summary>
    Downloading,

    /// <summary>正在校验安装包哈希。</summary>
    Verifying,

    /// <summary>正在解压安装包。</summary>
    Extracting
}
