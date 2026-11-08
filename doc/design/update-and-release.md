---
doc_id: DES-007
doc_type: design
module: update
status: approved
last_updated: 2026-11-08
source: 原 doc/design/设计文档.md §7（修复章节错位：原正文被 §8 隔断于文末）
related_docs:
  requirement: ../requirements/index.md
  architecture: ../architecture.md
---

# 关于页检查更新与发布链路

> 原文档中本章节正文（7.1-7.3）被 §8 整段隔断在文件末尾，本次迁移归位。

## 功能目标

- 设置中心「关于」页提供真实可用的「检查更新」，不再 mock
- 更新由主程序进程内完成下载与安装准备（不派生更新子进程），进度与失败直接呈现在设置中心，避免静默下载让用户误以为程序退出
- 发布由 GitHub Actions 自动生成更新包与 `versions.json`，客户端固定从 GitHub Releases 最新资产拉取

## 客户端链路

1. 关于页点击「检查更新」→ `MainWindowViewModel` 调 `IApplicationUpdateService` 拉取 `versions.json` 解析
2. 已是最新：关于页展示结果，不启动更新
3. 发现新版本：主窗口保持打开，设置中心进入「更新中」——`PrepareUpdatePackageAsync` 主进程内流式下载 zip（百分比进度）、校验 SHA256、解压 `%TEMP%`，展示进度条/阶段文本，允许取消；取消或失败清理工作区并提示
4. 准备完成 `ApplyStagedUpdateAsync` 生成替换脚本并隐藏启动：等主进程退出后 robocopy 覆盖安装目录，再重启主程序；主窗口展示「即将自动重启」后关闭

## 发布与版本清单

- GitHub Actions 发布 win-x64 自包含主程序包 + 压缩 zip
- 每次发布生成 `versions.json`：`PacketName` / `Hash(SHA256)` / `Version` / `Url` / `PubTime`
- 清单默认地址 `https://github.com/azrng/ApixPress/releases/latest/download/versions.json`；更新包默认地址同 Releases 下 `ApixPress-win-x64-portable.zip`
- 版本号默认构建/发布按 UTC 时间生成（`yyyy.M.d.HH`）；根目录 `VERSION` 显式版本号优先；`auto`/空/缺失回退 UTC 日期小时版本
- 发布目录默认仅保留主程序 `ApixPress.exe`
