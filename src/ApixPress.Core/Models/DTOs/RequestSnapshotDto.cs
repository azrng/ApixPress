namespace ApixPress.App.Models.DTOs;

public sealed class RequestSnapshotDto
{
    public string EndpointId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>发送本次请求使用的环境 Id，仅发送写入历史时回填，供"继续请求"恢复环境；用例快照不绑定环境。旧快照无此字段。</summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>发送本次请求使用的环境名称，仅用于展示；旧快照无此字段。</summary>
    public string EnvironmentName { get; set; } = string.Empty;
    public string BodyMode { get; init; } = BodyModes.None;
    public string BodyContent { get; init; } = string.Empty;
    public bool IgnoreSslErrors { get; init; }
    public List<RequestKeyValueDto> QueryParameters { get; init; } = [];
    public List<RequestKeyValueDto> PathParameters { get; init; } = [];
    public List<RequestKeyValueDto> Headers { get; init; } = [];

    /// <summary>form-data 字段完整状态（含禁用字段与文件引用）；旧快照无此字段时按 BodyContent 兼容解析。</summary>
    public List<FormFieldDto> FormFields { get; init; } = [];
}
