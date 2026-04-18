# 2026-04-18 导入更新保留用例

## 背景

用户反馈 Swagger 重复导入时，确认弹窗使用“覆盖”表述容易造成误解，且旧接口在同步阶段可能连带删除已保存用例，不符合“接口更新但用例保留”的预期。

## 本次处理

- 调整导入接口同步策略：当旧导入接口已关联 `http-case` 用例且新导入中已不存在该接口时，不再直接删除，而是转为保留状态。
- 保留状态下会移除该接口的导入标记，避免后续导入再次把这类接口当作可自动清理的导入项。
- 更新导入确认弹窗与状态提示文案，将“覆盖”统一改为“更新”，并明确提示“已保存用例会保留”。

## 影响文件

- `Services/Implementations/RequestCaseService.cs`
- `ViewModels/ProjectTabViewModel.Import.cs`
- `ViewModels/ProjectTabViewModel.ProjectSettingsState.cs`
- `Views/Controls/ImportOverwriteConfirmDialogView.axaml`
- `TASK.md`

## 验证

- 已执行 `dotnet build .\\Apifox.App.csproj -c Release`
- 结果：通过，0 Warning，0 Error

## 风险

- 当前仅完成编译验证，尚未在真实 UI 中回归“重复导入同一 Swagger 且接口下已有用例”的完整流程。
