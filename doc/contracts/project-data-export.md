---
doc_id: CON-T063
doc_type: contract
module: project-data
status: implemented
last_updated: 2026-09-25
design_doc: ../design/project-data-export.md
requirement_doc: ../requirements/project-data-export.md
---

# 项目数据导出契约草案

本文件只登记契约边界和稳定引用，不复制完整字段结构。最终字段以代码中的 DTO 和接口为准。

## 已有契约

| 契约 | 最终事实来源 | 本次使用范围 |
| --- | --- | --- |
| `IProjectDataExportService` | `src/ApixPress.Core/Services/Interfaces/IProjectDataExportService.cs` | 备份导出、现有备份预览与导入入口 |
| `ProjectDataExportRequestDto` | `src/ApixPress.App/Models/DTOs/ProjectDataExportRequestDto.cs` | 当前项目标识、项目元信息、目标路径；需阶段 1 扩展格式与范围字段 |
| `ProjectDataExportPackageDto` | `src/ApixPress.App/Models/DTOs/ProjectDataExportPackageDto.cs` | 现有 ApixPress 备份包结构和版本兼容入口 |
| `RequestCaseDto` / `RequestSnapshotDto` | `src/ApixPress.Core/Models/DTOs/RequestCaseDto.cs`、`src/ApixPress.App/Models/DTOs/RequestSnapshotDto.cs` | 接口、目录、请求用例和请求快照 |
| `ApiDocumentDto` / `ApiEndpointDto` | `src/ApixPress.Core/Models/DTOs/ApiDocumentDto.cs`、`src/ApixPress.Core/Models/DTOs/ApiEndpointDto.cs` | 导入文档、接口目录和接口定义来源 |
| `ProjectEnvironmentDto` / `EnvironmentVariableDto` | `src/ApixPress.Core/Models/DTOs/ProjectEnvironmentDto.cs`、`src/ApixPress.Core/Models/DTOs/EnvironmentVariableDto.cs` | 环境变量范围 |
| `RequestHistoryItemDto` | `src/ApixPress.Core/Services/Interfaces/RequestHistoryItemDto.cs` | 请求历史范围 |

## 阶段 0 草案（已落地）

以下边界已按草案落地，最终事实来源为代码：

- `ProjectDataExportFormat` / `ProjectDataExportFormats`：`backup`、`html`、`markdown` 枚举与 token 转换（`src/ApixPress.Core/Models/DTOs/ProjectDataExportFormat.cs`）。
- 备份四项范围：`ProjectDataExportRequestDto.IncludeInterfaces / IncludeRequestCases / IncludeEnvironments / IncludeRequestHistory`。
- `ProjectDataExportSettingsDto`：项目级导出偏好（格式、四项范围、最近输出目录），持久化于 `project_export_settings` 表（迁移 008）。
- 渲染器：`IProjectDataHtmlRenderer`（单个自包含 HTML）、`IProjectDataMarkdownRenderer`（`README.md` 索引 + 每目录一个 `.md` 的文件计划），输入均为 `ProjectDataDocumentModelDto`。
- `ProjectDataExportResultDto`：`OutputPath`（文件或目录）、`Format`、各范围计数与 `FileCount`。

## 兼容约束

- 现有 `ProjectDataExportPackageDto.CurrentSchemaVersion` 继续作为备份导入版本入口。
- 旧备份缺少新增范围数据时按空集合兼容，不因反序列化缺字段失败。
- HTML/Markdown 是发布文档，不作为可导入备份格式；不新增对应导入契约。
