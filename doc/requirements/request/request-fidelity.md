---
doc_id: REQ-003
doc_type: requirement
module: request
status: approved
last_updated: 2026-11-08
source: 原 doc/requirement.md §13（迁移）
related_docs:
  design: ../design/request-fidelity.md
  index: index.md
---

# 请求调用保真与多文件上传（已实施）

## 13.1 目标与范围

- URL 已有 query 按原顺序与编码语义发送，支持重复参数名、`+` 空格编码、无值参数
- 参数编辑区 query 允许同名重复，按列表顺序追加，不覆盖 URL 已有同名参数
- Header 支持启停；停用不发送，状态在编辑/保存/加载/发送间一致
- `multipart/form-data` 支持文本与文件字段；单文件字段可多选，同名按选择顺序发送
- 发送期间锁定影响快照的编辑与保存入口，仅保留取消；结束后恢复

## 13.2 用户与流程

选 Form-data → 字段设为文件类型 → 多选文件（显示数量与摘要，可清除）→ 发送（固定快照、禁用编辑）→ 按字段顺序组装 multipart（文本用内容、文件逐个读取）→ 结束显示响应并恢复；文件路径仅存请求用例快照，不复制源文件。

## 13.3 边界与约束

- 文件发送前不存在/不可读/读取失败 → 整个请求 fail fast，显示文件名与原因
- 取消文件选择不改已选；清除文件保留字段但本次不发送
- 不支持目录上传、拖拽上传、远程文件 URL、文件副本存入项目目录
- OpenAPI 导入的 `string`+`binary`/`byte` 字段初始化为文件字段；无法判定时文本字段
- 旧快照无文件字段信息时按原文本表单逻辑兼容加载
