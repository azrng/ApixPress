---
doc_id: DES-006
doc_type: design
module: shell
status: approved
last_updated: 2026-11-08
source: 原 doc/design/设计文档.md §6（阶段 0 草案 → 已实施，状态修正）
related_docs:
  requirement: ../requirements/index.md
  architecture: ../architecture.md
---

# 首页主窗口与项目 Tab 化（已实施）

> 原文标注「阶段 0 草案」；ShellTabs / 首页 / 项目 Tab / 快捷请求工作区 / 环境管理均已落地（MainWindowHomeView / ProjectInnerTabBarView / QuickRequestWorkbenchView / EnvironmentDrawerView 等），状态修正为已实施。

## 模块重构

| 模块 | 职责 |
| ---- | ---- |
| ShellTabs | 首页 Tab + 多项目 Tab 的顶部主导航 |
| HomeWorkspace | 首页项目列表与新建入口（固定主 Tab） |
| ProjectWorkspaceTab | 单项目工作台壳层（环境、接口入口、快捷请求入口） |
| ProjectExplorer | 左侧接口管理导航 |
| QuickRequestWorkspace | 中央快捷请求工作区（默认直接可编辑可发送） |
| EnvironmentWorkspace | 环境选择（右上窄选择器）+ 环境管理界面 |

## 数据流

首页打开项目 `HomeWorkspace → MainWindowViewModel.OpenProjectTab → ProjectTabCollection`；Tab 切换经 `ActiveProjectTab`；环境切换/保存走 `EnvironmentPanelViewModel → Service → Repository`。

## 业务规则（现行口径）

1. 启动默认激活首页 Tab；首页 Tab 不可关闭，项目 Tab 可关闭
2. 同项目已开 Tab 时再次点击直接切到已有 Tab，不重复开
3. 每个项目 Tab 维持独立环境与工作状态；环境选择仅影响当前 Tab
4. 项目 Tab 中央默认直接展示快捷请求编辑器（无入口卡片中转）
5. 环境管理（前置 URL、变量、激活环境）修改真实落库，保存后当前 Tab 上下文即时刷新
6. 快捷请求支持当前环境变量参与 URL / Headers / Params 解析

## 布局

顶部 `[Logo] + Tab 列 + 刷新/设置/通知/窗控`；项目 Tab 内左窄接口导航 + 中央快捷请求（GET URL 发送/保存 + Params/Body/Headers/Cookies/Auth/设置 页签 + 响应区）+ 右上环境选择。ASCII 原型与「待确认与当前假设」（首版只放快捷请求、只做项目级环境等）见 archive/design-doc-v1.md §6。
