---
doc_id: DES-ARCH
doc_type: design
module: architecture
status: approved
last_updated: 2026-11-08
source: 原 doc/design/设计文档.md §1-4（迁移重组）
related_docs:
  requirement: ../requirements/index.md
  index: index.md
---

# ApixPress 架构总览

> 原 `doc/design/设计文档.md`（622 行）于 2026-11-08 按 doc-AGENTS 迁移规则拆分：现行内容迁至本文件与各模块设计档案；原文整体归档 `archive/design-doc-v1.md`。

## 技术形态

Avalonia 桌面应用（Avalonia 11 + MVVM，CommunityToolkit.Mvvm），双项目结构：`ApixPress.App`（视图/控件/服务宿主）+ `ApixPress.Core`（DTO/持久化/业务接口）。SQLite 持久化（按序号迁移脚本），HttpClient 执行请求。UI 组件优先复用 Ursa.Avalonia（NavMenu / Drawer / OverlayDialogHost / TreeComboBox / Form）。

## 模块与数据流

- 视图层：`MainWindow`（首页 + 项目 Tab 容器）+ Views/Controls 下按工作区拆分的控件（请求编辑、响应、用例抽屉、环境抽屉、历史详情等，单控件单文件）
- 请求链路：`RequestEditorWorkspaceView → ViewModel → IRequestExecutionService → EnvironmentResolver → HttpClient`；发送时生成不可变快照并锁定编辑区
- 持久化链路：ViewModel → Service（`IProjectWorkspaceService` / `IProjectEnvironmentService` 等）→ Repository → SQLite
- 项目隔离：文档、用例、历史、环境均按 `ProjectId` 过滤加载

## 设计档案索引

| 档案 | 内容 | 原章节 |
|------|------|--------|
| [project-workspace.md](project-workspace.md) | 多项目 + 多环境扩展（已实施） | §5 |
| [main-window-tabs.md](main-window-tabs.md) | 首页主窗口与项目 Tab 化（已实施） | §6 |
| [update-and-release.md](update-and-release.md) | 关于页检查更新与发布链路 | §7 |
| [request-fidelity.md](request-fidelity.md) | 请求调用保真与多文件上传 | §8 |

MVP 基线（架构图、页面清单、业务规则、交互说明）见原文档归档；页面与业务规则的现状口径以 `doc/requirement.md`（兼容入口，225 行未超限）为准。
