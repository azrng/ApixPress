# 任务清单（TASK）

> 本文件只维护当前仍需关注的任务与最近 5 条已完成任务。
> 规则解释与阶段门控仍以 `AGENTS.md` 为准。

---

## 活跃任务

| ID | 任务名称 | 任务目标 | 当前阶段 | 负责人 AI | 状态 | 优先级 | 最近更新 |
|----|----------|----------|----------|-----------|------|--------|----------|
| T082 | UI 交互 P1 修复：右键菜单缺口（P1-10） | 基于 2026-09-26 审查遗留 P1-10 为五处补右键菜单，全部复用既有命令（零新增业务逻辑）：① 侧栏历史行（重新发送=ResendHistoryRequestCommand 注入 / 删除=面板 DeleteHistoryItemCommand）；② 用例卡（加载/改名/复制/删除，与行内按钮同命令）；③ 首页项目卡（打开项目 / 打开项目设置=新 OpenProjectSettingsAsync 打开工作区后直达设置页）；④ 标题栏项目标签（关闭当前/其它/全部，新增 CloseOtherProjectTabs/CloseAllProjectTabs 快照枚举）；⑤ 响应区（工作台×2 复制三项、历史详情 两项，与 T081 头部菜单同源）。绑定模式：行项 VM 注入式 ICommand（RequestHistoryItem/RequestCaseItem/ProjectWorkspaceItem/ProjectTabViewModel 四组属性，跨 ContextMenu PopupRoot 保持绑定，接口树同模式）+ 响应区纯路径绑定；新增 ContextMenu 内零 $parent（脚本复核 7 视图）。原型同步：shared.css/js 右键菜单组件（六类 data-context-menu + 视口翻转 + ESC 收起）+ 01/02/03/04/05/06/08 页标记；DOM 六类全验证 + 截图自查 pass。验证：WorkbenchLayoutTests 新增 5 守卫、完整测试 406/406、App 项目构建 0 警告 0 错误；顺手修复 WorkspaceDetailStyles.axaml T081 遗留 512 个裸 LF；reviewer/图片子智能体撞 5h 用量限额（22:45 重置）改自查验收（绑定断链/注入时序/快照枚举/CRLF 四项全过），独立验收待恢复后可补跑；GUI 真机待用户重启应用后验证 | 阶段 3 | ZCode | DONE | P1 | 2026-09-30 |
| T081 | UI 交互 P1 修复：响应区复制 + 错误恢复 | 基于 2026-09-26 交互审查遗留项落地两组能力：P1-8 响应区复制（工作台/历史详情响应头部 MenuFlyout 复制菜单：复制响应 Body/复制 Headers/复制 cURL，命令在 Editor VM 带 ResponseSection 参数复用 ClipboardService 与 BuildCurlSnippet，经可选 IMessenger 发状态栏反馈，CopyableBodyText 排除格式化占位符）；P1-7 错误恢复（① ResponseSection.CanRetryRequest：transport 失败与 5xx 横幅显示重试按钮，工作台绑 SendRequestCommand、历史详情绑 ResendHistoryRequestCommand，BeginLoading/Reset 清除；② 历史列表加载失败与空态区分：RequestHistoryPanelViewModel 加 HasHistoryLoadError/文案 + LoadHistoryCommand 重试 + ShowHistoryEmptyText 互斥，侧栏错误块 SelectableTextBlock+重试按钮；③ 三视图失败横幅 ResponseNoticeText 改 SelectableTextBlock 可选中复制）。原型同步：shared.css/js 复制菜单/横幅重试/历史错误块组件（5 个 remixicon 码点经官方码表核对）、02/03/04 复制菜单、04 横幅重试、05 复制菜单+错误块。验证：原型 DOM 交互三例 + 截图图片子智能体三组件全 pass；新增测试 18 例（ResponseCopyCommandTests 7 + ResponseSectionViewModelTests 4 + RequestHistoryPanelViewModelTests 3 + WorkbenchLayoutTests 结构守卫 4）；完整测试 399/399、App 项目与全解决方案仓库内独立输出目录构建 0 错误（2 警告均为既有：NETSDK1194/xUnit2013）；reviewer 独立验收 8/8 PASS（note-1 空态互斥已顺手修复复跑通过）；GUI 真机待用户重启应用后验证 | 阶段 3 | ZCode | DONE | P1 | 2026-09-30 |
| T080 | UI 交互 P0/P1 批量修复：状态栏宿主 + 弹层键盘交互 + 发送快捷键 | 基于全面 UI 交互审查（状态栏反馈通道无宿主、13+ 弹层无 ESC/初始焦点/回车提交、无 Ctrl+Enter 发送、菜单标注 Ctrl+W 未实现、环境/用例保存失败静默）落地六项：① MainWindow 底部状态栏绑定 StatusMessage（OverlayDialogHost 扩为三行）；② 新增 Helpers/DialogInteractionAssistant 统一挂载 ESC 关闭（IDialogContext 通道）/打开聚焦第一个输入框全选/Enter 触发 DialogPrimaryButton，接入 DialogService 与三抽屉，覆盖确认弹窗标 DialogNoDefaultSubmit 禁 Enter；③ 窗口键盘改 Tunnel 注册补 Ctrl+Enter 发送（发送中防重发）与 Ctrl+W 关当前标签；④ URL 栏回车发送；⑤ 环境保存（单卡/全部）与用例保存失败经 StatusMessageRequest 报原因、成功轻提示；⑥ 原型同步：shared.js/css 注入底部状态栏（与 toast 联动+右侧环境名）、ESC 关弹层、弹层初始焦点、Enter 提交（.no-default-submit 排除），02/03/04/13 发送按钮加 Ctrl+Enter kbd 标注。审查修正：保存按钮“连点重复入库”不成立（[RelayCommand] 默认禁并发），实际缺陷为失败静默。验证：原型 DOM 交互三例通过+截图图片子智能体判定 pass；新增 6 项测试（4 状态消息 VM 测试+2 结构守卫）、完整测试 380/380、App 项目与全解决方案仓库内独立输出目录构建 0 警告 0 错误；GUI 真机待用户重启应用后验证 | 阶段 3 | ZCode | DONE | P0 | 2026-09-27 |
| T079 | 导出卡片格式切换改胶囊样式 + 卡片标题文案对齐原型 | 响应用户截图红框反馈（项目设置→导出数据）：T063 实现的三格式切换误用导入对话框下划线大选项卡样式（ProjectImportModeTab，横向零内边距 + Auto 列宽无间距），三个格式项渲染成粘连的带下划线链接；改为对齐原型 09 `.body-mode` 胶囊形态——新增 ToggleButton.ExportFormatChip 样式（pill 圆角、12 号字、边框胶囊、选中浅底蓝字加粗），导出区换为水平 StackPanel 6px 间距。用户确认后再将卡片标题「导出项目数据」→「导出数据」、描述改为「支持导出 ApixPress 备份用于迁移备份，或导出 HTML / Markdown 接口文档用于分享与归档。」，消除与操作按钮文字重复（按钮文案与测试断言未动）。验证：完整测试 374/374、App 项目仓库内独立输出目录构建 0 警告 0 错误；GUI 真机待用户重启应用后验证（当前实例锁定主输出目录） | 阶段 3 | ZCode | DONE | P2 | 2026-09-26 |
| T078 | 调试标签一键关闭 | 响应“调试界面 tab 仍需点击两次才能关闭”：移除单标签关闭（×、右键/更多菜单“关闭当前”、Ctrl+W）对未保存修改的二段式确认，脏标签直接关闭并以状态栏提示“未保存修改已放弃”兜底；固定标签仍拒绝关闭；关闭其它/全部保留原二段式。验证：定向测试改写为一键关闭断言、完整测试 357/357、App 项目独立输出目录构建 0 警告 0 错误；GUI 真机待用户重启应用后验证（当前实例运行旧二进制且锁定输出目录） | 阶段 3 | ZCode | DONE | P1 | 2026-09-25 |
| T077 | 请求用例交付包（UC-07～UC-09） | 实现独立交付包的范围选择、自动携带父接口、环境配置、稳定来源关联、导入预览、冲突决策与事务回滚，并补充回归验证；验证：完整测试 357/357、构建 0 警告 0 错误、GUI 真机导出侧已验（项目设置导出/导入区双入口、对话框统计与数据库一致、勾选用例自动附带父接口、混合选择统计、原生保存对话框建议文件名 `<项目名>-<时间戳>.apixdelivery.json`），导出成功经用户确认；导入预览/决策/回滚由用户自行手动测试 | 阶段 3 | Codex | DONE | P0 | 2026-09-25 |
| T075 | 生成代码弹框补充关闭按钮 | 启用 Ursa 对话框右上角关闭按钮，保留内容区关闭按钮，并补充回归断言；验证：RequestCodeDialog 定向测试 3/3、Release win-x64 构建 0 警告 0 错误 | 阶段 3 | Codex | DONE | P2 | 2026-09-20 |
| T074 | 全面性能问题修复 | 修复请求快照内存峰值、历史/用例列表大 JSON 加载、批量导入查找、环境变量 N+1 查询、导入缓存内存预算与用例列表索引；验证完整测试与 Release 构建 | 阶段 3 | Codex | DONE | P1 | 2026-09-18 |
| T073 | Postman 导入剥离开头 {{baseUrl}} 变量为环境相对路径 | 导入时请求 URL 开头的 `{{baseUrl}}`（含 https:// 前缀合成形式、大小写不敏感）剥离为相对路径，发送时由环境 BaseUrl 拼接；非开头变量与绝对 URL 原样保留。验证：PostmanJsonParserTests 新增 4 变体用例、完整测试 345/345、Release 构建 0 错误；已导入数据需重新导入才生效 | 阶段 3 | ZCode | DONE | P2 | 2026-09-17 |
| T072 | 导入卡片文案与自动识别交互对齐 | 「导入 API 数据」卡片描述由「请选择要导入的数据源格式」改为「选择 JSON 文件或输入 URL，格式自动识别」，消除"选择格式"与自动识别实际的矛盾；评估结论：三种格式均可自动识别，不拆分三选项。验证：完整测试 344/344、Release 构建 0 警告 0 错误 | 阶段 3 | ZCode | DONE | P3 | 2026-09-17 |
| T071 | 接口编辑上下文标签移至接口名后 | 响应用户截图反馈：把「接口模式 / 用例模式 · 用例名」从接口名输入框下方一行，改为接口名右侧同行状态 chip，上下文栏回落单行高度；新增 chip 样式，超长用例名省略并带 ToolTip；顺带删除无引用死样式 HttpEditorContextLabel。验证：Release 构建 0 警告 0 错误、WorkbenchLayoutTests 44/44、完整测试 344/344；GUI 真机自动化验证通过（接口模式默认 chip、保存用例即切换用例模式 chip、调试/文档预览双模式保持、切接口复位、超长用例名省略截断 + ToolTip 全文） | 阶段 3 | ZCode | DONE | P3 | 2026-09-17 |
| T068 | JSON 请求体增加格式化按钮 | 在 Raw JSON Body 模式提供一键缩进；合法内容原位格式化并保留中文，非法内容保持不变并显示提示。验证：Release 构建成功（1 条既有 xUnit2013 警告）、定向测试 35/35、完整测试 332/332；GUI 真机未验（当前运行实例未重启） | 阶段 3 | Codex | DONE | P3 | 2026-09-17 |
| T067 | 保存动作区去 caret 下拉改平铺「保存+保存用例」两按钮 | 根据用户反馈取消 caret Flyout，改为并列「保存」与「保存用例」按钮；结构守卫、原型检查通过，323 项测试全过。GUI 真机未验（当前应用实例运行中） | 阶段 3 | ZCode | DONE | P2 | 2026-09-17 |
| T066 | 用例改名、计数刷新与原型细节收敛 | 支持从用例抽屉改名并即时刷新名称、更新时间和接口用例计数；完成第三轮差异审查与取舍。自动化测试与 GUI 真机验证通过 | 阶段 3 | ZCode | DONE | P2 | 2026-09-17 |
| T062 | form-data 文件字段上传与 Binary Body 模式 | form-data 支持文本/多文件字段，新增 Binary Body 模式，并兼容既有请求快照与文本编码 | 阶段 3 | ZCode | DONE | P1 | 2026-09-17 |
| T063 | 导出格式补齐：HTML/Markdown 文档导出 + 备份范围勾选（对齐原型 09） | 已实现三格式导出卡片（备份/HTML/Markdown 切换）、备份四项范围勾选与按项目持久化（project_export_settings，迁移 008）、HTML 单文件自包含文档、Markdown 目录输出（README 索引 + 每目录一个 .md，含请求用例示例）、孤儿用例不导出、临时文件原子写入与 Markdown 非空目录拒绝、旧备份 v1 兼容。验证：完整测试 374/374（新增服务/渲染器/VM/结构守卫/初始化器 17 项）、App 项目独立输出目录构建 0 警告 0 错误；主输出目录构建因用户实例运行锁定未执行（等价路径已覆盖），GUI 真机待用户重启应用后验证 | 阶段 3 | ZCode | DONE | P2 | 2026-09-25 |
| T064 | 中级原型对标差距收敛 | 完成 9 项中级差异修复，并吸收 T066 遗留形态项决策；结构与导入分支回归测试通过 | 阶段 3 | ZCode | DONE | P3 | 2026-09-17 |
暂不处理（待用户决策）：M4 抽屉/快捷请求树虚拟化重构（需视觉重设计）、M7 历史保留策略（自动删数据的产品决策）、ObservableCollectionExtensions.ReplaceWith 死代码删除（删文件需确认）；遗留观察：UseCasesPanel_ShouldShowOnlyCurrentInterfaceCasesAndLoadSelectedCase 既有偶发"Collection was modified"测试竞态（后台续体 vs 测试枚举，非本次引入）；UIA 备注：CUA 合成点击打不到 Flyout 菜单项（挂 PopupRoot 独立窗口），须走真实鼠标 click-rect（uia.ps1） | 阶段 3 | ZCode | DONE | P1 | 2026-09-15 |

---

## 最近完成

| 任务 ID | 任务名称 | 任务状态 | 最近更新时间 |
| ------- | -------- | -------- | ------------ |
| T076 | 请求用例基础链路（UC-01～UC-06） | 已完成：统一请求用例文案与范围；新接口保存请求用例自动保存父接口；编辑器显示接口/用例模式和即时保存状态；接口树下一级请求用例默认收缩；请求用例加载、改名、复制、删除；响应仅进入请求历史。定向回归 123/123、完整测试 349/349、`dotnet build ApixPress.slnx --no-restore` 0 警告 0 错误。桌面真机因 CUA 认证令牌不可用未执行 | DONE | 2026-09-24 |
| T070 | Postman Collection 导入 | 已支持自动识别 Postman Collection v2/v2.1 JSON，导入文件夹、请求、查询/路径参数、请求头、Bearer/API Key/Basic 认证、raw/form-data/urlencoded/file/graphql 请求体；已用 `AIProvder.postman.json` 验证 3 个请求。定向测试 3/3、完整测试 344/344、Release win-x64 应用项目构建 0 警告 0 错误；GUI 真机未验 | DONE | 2026-09-17 |
| T069 | 用例上下文保存、实际请求失败展示、导入请求示例生成与用例复制删除修复 | 已支持加载用例后 Ctrl+S 更新当前用例；失败请求保留实际请求页签与快照；导入 JSON/XML 请求体支持主动生成示例；复制名称改为 `_copy`；删除改为可观察的危险确认弹窗并同步移除持久化与列表条目。完整测试 341/341、Release win-x64 构建 0 警告 0 错误；GUI 真机因当前实例运行中暂缓 | DONE | 2026-09-17 |
---
