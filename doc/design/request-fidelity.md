---
doc_id: DES-008
doc_type: design
module: request
status: approved
last_updated: 2026-11-08
source: 原 doc/design/设计文档.md §8（迁移重组）
related_docs:
  requirement: ../requirements/index.md
  architecture: ../architecture.md
---

# 请求调用保真与多文件上传（已实施）

## 架构设计

| 模块 | 职责 | 变更 |
| ---- | ---- | ---- |
| `RequestConfigTabViewModel` | 参数/Header/Form-data 编辑状态 | Form-data 字段增加文本/文件类型、文件路径集合、发送时只读 |
| `RequestSnapshotDto` | 可重放请求快照 | 新增结构化 Form-data 字段，兼容旧版 `BodyContent` 文本数据 |
| `IFilePickerService` | 系统文件选择器 | 新增多选方法，不新增窗口依赖 |
| `RequestExecutionService` | 构造 URL/Header/HttpContent | 保留原 query 追加表格 query；过滤停用 Header；文件字段创建 multipart 文件流 |
| `HttpInterfaceBodyTabView` | Form-data 编辑器 | 文件行显示选择/摘要/清除；文本行键值输入 |

发送流程：编辑请求 → 生成不可变快照 → 锁定编辑区 → 执行（URL 保留原 query 按序追加启用参数；Header 仅发启用项；Form-data 文本字段 + 同名多文件字段）→ 完成/失败/取消 → 应用响应状态 → 恢复编辑区。

## 技术决策

- URL query 用保序列表而非字典：不覆盖同名参数，不改写 `+`、无值参数等已有语义
- 文件只记录本地路径，发送时才开流：不复制进 SQLite、不长期占用句柄
- Form-data 新增字段随既有 JSON 快照持久化，不引入数据库迁移
- 文件选择复用主窗口 `StorageProvider`，code-behind 不写业务逻辑

## 业务规则

1. 原 URL query 原样保留；参数页启用行仅追加，不按参数名去重
2. Header 表格带启用选择，与 Params/Form-data 语义一致；`IsEnabled=false` 不参与请求、不阻止全局 Auth 注入
3. 文件字段可多选，同字段名逐个追加；清除只清选择不删字段；选择器取消保留原选择；无已选文件不参与请求
4. 发送中禁用方法/URL/Params/Body/Headers/保存/发送，响应区保留取消；结束无论如何恢复编辑

## 测试与验收

- `BuildUrl`：重复 query、`+` 编码、无值 query、表格参数追加顺序
- 请求执行：停用 Header 不发送、同名多文件 multipart 顺序、缺失文件 fail fast
- ViewModel：文件选择取消保留原值、清除、发送锁定与恢复
- Avalonia smoke：文件/文本字段切换、选择摘要、发送中禁用

## 范围假设

不调整已文档化的本地凭据明文持久化策略；本期不做文件大小限制、类型白名单、上传进度、断点续传；OS 不返回本地路径时字段保持未选择并提示平台不支持。
