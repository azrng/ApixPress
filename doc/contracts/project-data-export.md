---
doc_id: CON-T063
doc_type: contract
module: project-data
status: draft
last_updated: 2026-09-17
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
| `RequestCaseDto` / `RequestSnapshotDto` | `src/ApixPress.Core/Models/DTOs/RequestCaseDto.cs`、`src/ApixPress.App/Models/DTOs/RequestSnapshotDto.cs` | 接口、目录、测试用例和请求快照 |
| `ApiDocumentDto` / `ApiEndpointDto` | `src/ApixPress.Core/Models/DTOs/ApiDocumentDto.cs`、`src/ApixPress.Core/Models/DTOs/ApiEndpointDto.cs` | 导入文档、接口目录和接口定义来源 |
| `ProjectEnvironmentDto` / `EnvironmentVariableDto` | `src/ApixPress.Core/Models/DTOs/ProjectEnvironmentDto.cs`、`src/ApixPress.Core/Models/DTOs/EnvironmentVariableDto.cs` | 环境变量范围 |
| `RequestHistoryItemDto` | `src/ApixPress.Core/Services/Interfaces/RequestHistoryItemDto.cs` | 请求历史范围 |

## 阶段 0 草案

阶段 1 预计新增或扩展以下边界，具体字段在用户确认后才落地：

- `ProjectDataExportFormat`：`backup`、`html`、`markdown`。
- `ProjectDataExportScope`：接口与目录、测试用例、环境变量、请求历史四个布尔范围。
- `ProjectDataExportSettingsDto`：当前项目的四项范围默认值及最近一次输出位置/格式；不保存敏感数据。
- `IProjectDataDocumentRenderer`：接收已筛选的项目导出模型，输出 HTML 字符串或 Markdown 文件计划，不访问数据库、不写 UI 状态。
- `ProjectDataExportResultDto`：保留现有结果兼容字段，并增加格式、输出路径/目录和各范围实际计数。

## 兼容约束

- 现有 `ProjectDataExportPackageDto.CurrentSchemaVersion` 继续作为备份导入版本入口。
- 旧备份缺少新增范围数据时按空集合兼容，不因反序列化缺字段失败。
- HTML/Markdown 是发布文档，不作为可导入备份格式；不新增对应导入契约。
