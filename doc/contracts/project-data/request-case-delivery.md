---
doc_id: CON-T077
doc_type: contract
module: project-data
status: implemented
last_updated: 2026-09-24
design_doc: ../../design/project-data/request-case-delivery.md
requirement_doc: ../../requirements/project-data/request-case-delivery.md
---

# 请求用例交付契约草案

本契约定义已确认的稳定边界。完整字段已落地到 `src/ApixPress.Core/Models/DTOs/`，代码成为最终事实来源；本文保留格式、版本和业务规则说明。

## 1. 包顶层

交付包格式标识固定为 `apixpress.request-case-delivery`，当前版本固定为 `apixpress.request-case-delivery.v1`。顶层字段含义如下：

| 字段 | 含义 | 规则 |
| --- | --- | --- |
| `format` | 包用途标识 | 必须是请求用例交付格式，不能被项目备份导入误接收 |
| `schemaVersion` | 格式版本 | 不支持的版本预览即失败 |
| `exportedAt` | 导出时间 | 使用 UTC |
| `source` | 来源项目元信息 | 至少包含项目来源 ID、名称和产品标识 |
| `interfaces` | 接口定义集合 | 至少包含被选接口和自动附带父接口 |
| `requestCases` | 请求用例集合 | 每条必须引用同包接口来源 ID |
| `environments` | 环境与变量集合 | 包含前置 URL、环境名、变量名和值、启用和活动标记 |

不提供响应历史、测试结果、报告或目标数据库 ID 字段。

## 2. 稳定标识和引用

- `source.projectId` 与实体 `sourceId` 组成来源键，作为跨项目导入和重复导入识别依据。
- `interfaces[].sourceId` 是接口来源 ID；接口改名不改变该值。
- `requestCases[].sourceId` 是请求用例来源 ID；`parentInterfaceSourceId` 必须匹配 `interfaces[].sourceId`。
- `environments[].sourceId` 是环境来源 ID；变量通过所属环境来源 ID和变量名关联。
- 导入阶段生成新的目标数据库 ID；包内来源 ID不直接写入目标数据库的主键或外键。

## 3. 请求快照边界

请求用例必须携带当前可加载和发送所需的请求快照：方法、URL、查询参数、路径参数、请求头、Body 模式、Body 内容、描述、忽略 SSL 等已有请求配置。Body 不新增环境变量插值能力；URL、Params、Headers 的环境变量引用保持现有语义。

## 4. 导入预览契约

预览结果至少提供：

- 来源项目和格式版本。
- 接口、请求用例、环境、变量总数。
- 按对象统计的 `新增`、`更新`、`跳过`、`另存`、`孤儿` 数量。
- 接口、请求用例、环境的逐项冲突记录及当前决策。
- 需要用户处理的孤儿项和未决冲突。

预览是只读操作，不产生持久化副作用。确认请求必须携带用户逐项决策，不能仅依赖预览时的默认值。

## 5. 导入提交契约

导入服务按一个事务处理环境、接口、参数和请求用例。提交前必须重新检查格式版本、来源引用、冲突决策和孤儿项；取消、校验失败或写入异常必须回滚整个批次。提交成功后返回各对象的新增、更新、跳过和另存计数，以及目标 ID 映射的刷新提示，不向调用方暴露来源 ID作为目标主键。

## 6. 冲突默认值（已确认）

阶段 0 推荐默认值：

1. 相同来源项目和来源 ID：`更新`。
2. 不同来源但接口方法路径、请求用例父接口名称或环境名称变量名冲突：`跳过`。
3. 用户选择`另存`：生成新目标 ID，名称沿用 `_copy` 规则，仍冲突时追加数字后缀。
4. 目标项目已有活动环境：保留目标活动环境；没有活动环境时才采用包内活动标记。

以上默认值已作为实现契约落地。

## 7. 现有契约关系

项目完整备份继续使用 `CON-T063` 及现有 `ProjectDataExportPackageDto`；请求用例交付包使用本契约独立的 DTO、预览 DTO、决策 DTO 和导入服务入口。两种包的格式标识和导入入口必须可区分。
