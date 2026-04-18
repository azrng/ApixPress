# 2026-04-18 导入接口唯一键冲突修复

## 背景

导入 Swagger 后，导入结果已落库，但刷新左侧接口列表时会触发 `request_cases` 唯一约束冲突，界面出现数据库错误提示。

## 本次处理

- 调整导入生成的 `http-interface` 记录作用域，将 `ParentId` 固定为导入端点键 `method + path`，避免同目录下同名接口因导入来源不同而撞到唯一索引。
- 为 `RequestCaseService.SaveAsync` 增加 SQLite 唯一键冲突与通用异常收敛，避免仓储异常直接向上冒泡。
- 在导入接口同步链路中显式检查保存结果，失败时返回可控错误信息。

## 影响文件

- `Services/Implementations/RequestCaseService.cs`
- `TASK.md`

## 验证

- 已执行 `dotnet build .\\Apifox.App.csproj -c Release`
- 结果：通过，0 Warning，0 Error
- 已执行临时 SQLite 控制台验证，按唯一索引 `project_id + entry_type + group_name + folder_path + parent_id + name` 连续插入两条同名接口记录，分别使用 `swagger-import:GET /users` 与 `swagger-import:POST /users` 作为 `ParentId`
- 结果：通过，输出 `INSERTED:2`

## 风险

- 已存在的导入接口会在下次导入同步时被更新到新的作用域；若用户依赖旧数据的排序或名称冲突表现，需再做一次真实导入验证。
