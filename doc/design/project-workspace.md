---
doc_id: DES-005
doc_type: design
module: project
status: approved
last_updated: 2026-11-08
source: 原 doc/design/设计文档.md §5（阶段 0 草案 → 已实施，状态修正）
related_docs:
  requirement: ../requirements/index.md
  architecture: ../architecture.md
---

# 多项目与多环境扩展（已实施）

> 原文标注「阶段 0 草案，用户确认前仅作基线草案」；项目 Tab、环境抽屉、项目隔离视图均已落地（MainWindowHomeView / EnvironmentDrawerView / CreateProjectDialogView 等），状态修正为已实施。原「阶段 0 草案」全文见 archive/design-doc-v1.md §5。

## 模块增量

| 模块 | 职责 | 设计说明 |
| ---- | ---- | ---- |
| ProjectWorkspace | 项目列表、当前项目选择、默认项目记忆 | 延续单窗口 Shell |
| EnvironmentScope | 项目下环境列表、激活环境、BaseUrl 和变量 | 环境面板从「单环境变量列表」升级为「环境列表 + 环境详情」 |
| WorkspaceIsolation | 文档、用例、历史按项目隔离 | 减少跨项目串用 |
| Persistence | SQLite 迁移脚本扩展 | projects / project_environments 等表 |

## 数据模型

`projects`（id/name/description/is_default/…）、`project_environments`（project_id/name/base_url/is_active/sort_order）、`environment_variables` 改挂 environment_id；`api_documents` / `request_cases` / `request_history` 均加 `project_id`。

## 关键技术决策

| 决策点 | 方案 | 原因 |
| ---- | ---- | ---- |
| 项目承载 | 首页项目浏览 + 项目管理对话框 | 与工作台兼容 |
| 环境承载 | 顶部环境下拉 + 环境管理抽屉 | 切换成本最低 |
| BaseUrl 建模 | 环境一级字段单独存储 | 便于校验展示 |
| 变量解析顺序 | BaseUrl 先参与 URL 组装，再解析自定义变量 | 拼装规则稳定可预测 |
| 数据迁移 | 新增迁移脚本兼容旧版 Default 环境数据 | 降低升级成本 |

## 业务规则（现行口径）

1. 启动优先进入默认项目；无项目展示创建引导
2. 每项目至少一个环境，BaseUrl 必填可编辑
3. 删除激活环境前先切换或自动指定新激活
4. 请求仅使用当前项目激活环境，禁止跨项目读变量
5. 相对路径按「当前环境 BaseUrl + Path」组装
