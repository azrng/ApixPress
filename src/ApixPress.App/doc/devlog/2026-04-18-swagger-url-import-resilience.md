# 2026-04-18 Swagger URL 导入稳定性修复

## 背景

用户反馈导入 `http://synyi-manhattan-sea-api-2440-develop.sy/swagger/v1/swagger.json` 时，应用会提示无响应并直接退出。

## 本次处理

- 为 Swagger URL 下载补充 20 秒超时控制，避免长时间等待造成“卡死”感知。
- 为 URL 导入补充 HTTP 状态码错误信息，失败时直接反馈远程服务返回结果。
- 为导入预检、确认导入和导入后刷新链路补充兜底异常处理，避免异常直接冒到 UI 线程导致应用退出。
- 保留现有导入预检与覆盖确认流程，不调整持久化契约。

## 影响文件

- `Services/Implementations/ApiWorkspaceService.cs`
- `ViewModels/ProjectTabViewModel.Import.cs`
- `TASK.md`

## 验证

- 已执行 `dotnet build Apifox.App.csproj -v minimal`
- 结果：通过，0 Warning，0 Error

## 风险

- 当前 URL 导入仍按文本读取远端内容，若远端返回 YAML，后续仍需补充 YAML 解析支持。
