namespace ApixPress.App.Models.DTOs;

/// <summary>更新包准备进度数据（下载/校验/解压），供界面渲染进度条与阶段文本。</summary>
public sealed class AppUpdateProgressDto
{
    /// <summary>当前所处阶段。</summary>
    public AppUpdateProgressStage Stage { get; init; }

    /// <summary>已接收字节数（仅下载阶段递增）。</summary>
    public long ReceivedBytes { get; init; }

    /// <summary>更新包总字节数；服务端未返回 Content-Length 时为 null，进度退化为不确定态。</summary>
    public long? TotalBytes { get; init; }
}
