---
rule_id: ui-agents
version: 1.60.0
last_updated: 2026-09-07
dependencies: [agents-root]
---

# UI 层规则

## 适用范围

- 作用域：界面、布局、导航、样式、状态、交互与界面测试
- 触发场景：涉及页面、窗口、布局、组件、交互、样式、UI smoke test 时阅读

### 常见任务入口
- 新增页面、窗口或布局：先看组件规则与推荐目录结构
- 改表单、对话框、导航、交互：先看状态管理规则与导航规则
- 改样式、主题、视觉展示：先看样式规则与设计系统
- 补界面回归：先看 `提交前最小回归` 与测试规则

---

## 技术栈

### UI
- .NET + Avalonia 11+ + Ursa.Avalonia + CommunityToolkit.Mvvm；版本以仓库现有 `TargetFramework` 为准，不主动升级，新项目先联网检索当前最新正式版
- 设计风格：Fluent Design
- 消息传递：`IMessenger`

### 设计系统
#### 设计系统基线与定制
- `design-system.yaml` 中的颜色、主题、字体、风格关键词默认是内置基线，按产品品牌定制后才是本项目的规范
- `meta.status` 为「基线待定制」时，首次界面任务前必须先完成定制，并把 `meta.status` 改为「已定制」：
  - 已有产品资料（VI / 品牌色 / 设计稿）：按资料更新 token 与主题策略
  - 仓库已有样式实现：从现有代码反向提炼产品 token 回填 `design-system.yaml`，不强行套用基线值
  - 资料缺失：先向用户询问品牌色、明暗主题、密度等偏好，禁止把基线色默认当作产品主色
- `meta.status` 为「已定制」后，`use-ai-rule` 重新生成会保留本地 `design-system.yaml` 不覆盖；定制后忘记改状态，下次生成会被基线覆盖
- 调整 token 的固定顺序：先改 `design-system.yaml`，再同步文件中声明的实现资源（样式资源 / 主题配置 / 图表主题），最后改界面
- 语义 token 的命名、分层与「token → 实现资源 → 组件」引用管线属于结构契约：可以改值、增删 token，不得另建平行 token 体系或在界面绕过 token 硬编码
- 使用 `design-system.yaml` 中定义的设计 token
- 样式必须使用 Avalonia 样式表（Styles / Classes），禁止把颜色、间距、圆角直接硬编码在控件属性中
- 颜色、间距、圆角必须来自样式资源，使用 `{StaticResource}` 或 `{DynamicResource}` 引用

### 技术选择原则
如果仓库已经有真实实现，以现有代码为准，不要强行重构或替换技术栈。
技术债务与重构判断遵循 `collaboration-AGENTS.md` 的「纠错、回退与重构」。

---

## 主动建议规则
- 发现窗口结构、页面布局、控件拆分、状态展示、样式资源、可访问性或交互一致性问题时，应主动提醒，并说明是否影响当前任务
- 发现设计稿、截图反馈或用户描述与 `design-system.yaml`、Avalonia 样式资源或 Fluent Design 约束冲突时，应先说明冲突点，再给出最小调整方案
- 涉及共享控件、全局样式、导航方案或跨 ViewModel 状态传递的建议改动，未经用户确认不得扩大到本次任务之外
- 发现可以复用已有视图、控件、样式资源或交互模式时，应优先建议复用
- 不确定交互规则、视觉规范或界面数据来源时，应按根 `AGENTS.md` 的查证优先级处理，禁止凭经验补写规则

---

## 推荐目录结构

- UI 目录建议聚焦在 `src/AppName/Views/`、`src/AppName/ViewModels/`、`src/AppName/Assets/Styles/` 下组织，优先复用现有结构，不强制迁移。

```text
src/AppName/
├── Views/
│   ├── Controls/             # 自定义控件
│   ├── Converters/           # 值转换器
│   ├── Dialogs/              # 对话框视图
│   └── Pages/                # 页面视图（按业务域拆分）
├── ViewModels/
│   ├── Base/                 # ViewModel 基类
│   ├── Dialogs/              # 对话框 ViewModel
│   └── Pages/                # 页面 ViewModel（按业务域拆分）
├── Assets/
│   ├── Styles/
│   │   ├── Fluent.axaml      # Fluent 主题
│   │   ├── Colors.axaml      # 颜色资源
│   │   ├── Fonts.axaml       # 字体资源
│   │   └── Global.axaml      # 全局样式
│   └── Images/               # 图片资源
└── App.axaml                 # 应用级资源
```

---

## 阶段 1 — 视图实现（视图实现角色主导）

**触发条件**：用户发出「开始视图开发」指令

**入场要求**：阶段 0 设计文档已由用户确认

**工作内容**：
1. 按设计文档实现页面和组件，遵循 `design-system.yaml` 和 Avalonia 样式规范。
2. 数据层使用 mock（静态 mock 数据），不依赖真实服务。
3. 同步输出接口契约文件 `src/AppName/Models/DTOs/`，定义所有数据传输对象。

**产物**：
- 可运行的 Avalonia 视图页面
- `src/AppName/Models/DTOs/` 契约类

**门控规则**：
- 用户确认视图页面符合设计文档预期。
- DTO 类中的类型已初步定稳；阶段 2 若发现契约缺口，按 `collaboration-AGENTS.md`「纠错、回退与重构」先回到契约变更流程。
- 满足以上两点后，才允许进入阶段 2。

---

## UI 规则

### 样式规则
- 所有样式使用 Avalonia Styles（`.axaml` 文件），禁止在控件中直接设置 `Background`、`Margin` 等样式属性。
- 所有颜色来自 `design-system.yaml` 中定义的语义化 token，通过样式资源引用。
- 所有间距使用统一的资源或样式类，禁止硬编码数值。
- 响应式设计遵循窗口大小变化，必须在不同窗口尺寸下测试。

### 组件规则
- 优先复用 `src/AppName/Views/Controls/` 下已有组件，禁止重复创建
- 只有确实有复用价值时才新增共享控件，避免为单次需求过度抽象
- 页面状态必须完整：`loading`、`empty`、`error`、`no-permission`
- 使用 Ursa.Avalonia 控件库优先，必要时使用 Avalonia 官方控件
- 图标统一使用 Avalonia 官方图形能力、Ursa 组件能力或仓库既有素材

### MVVM 模式规则
- 所有 ViewModel 最终都必须继承 `ObservableObject`；若项目已有 ViewModel 基类（如 `ViewModelBase`），应由基类继承 `ObservableObject` 后统一复用
- 属性通知使用 `[ObservableProperty]` 特性自动生成，禁止手写重复样板
- 命令定义使用 `[RelayCommand]` 特性生成，禁止手动拼装重复命令逻辑
- ViewModel 依赖通过构造函数注入
- 禁止在 Code-behind 中编写业务逻辑

### 导航规则
- 使用项目既有导航方案或 Ursa 导航能力进行页面切换。
- 导航逻辑封装在导航服务中，禁止在 ViewModel 中直接操作 View。
- 页面参数通过导航消息或显式参数对象传递，禁止使用静态全局状态。
- 需要历史记录时，应支持前进 / 后退。

### 对话框规则
- 使用项目既有对话框方案或 Ursa 的 Dialog / Overlay 组件实现对话框。
- 对话框内容必须使用 ViewModel，禁止在 Code-behind 编写业务逻辑。
- 对话框结果通过异步返回，禁止依赖隐式全局状态。
- 危险操作需提供明确的二次确认。

### 数据绑定规则
- 列表数据使用 `ObservableCollection<T>` 或适合当前项目的可观察集合
- 复杂集合变更优先使用批量更新策略，而不是简单清空后重添
- 异步数据加载必须支持取消（`CancellationToken`）
- 绑定路径必须可维护，禁止依赖脆弱的控件查找方式

### 状态管理规则
- 本地状态使用 `[ObservableProperty]` 管理
- 全局共享状态使用 `IMessenger` 传递跨 ViewModel 消息
- 禁止使用静态全局类存储业务状态
- 主题、语言等低频全局配置才放入应用级资源或全局上下文

---

## 测试规则

### 提交前最小回归
- 默认执行：项目现有的编译、静态检查或等价校验
- 页面、窗口、导航、表单交互改动：至少补一次受影响界面或组件的 smoke test / 等价验证
- **微小调整**（文案修改、颜色调整、间距优化等不影响逻辑的改动，修改内容少于10行）：可不补自动化测试，但仍应确认受影响界面的关键状态、布局与主要交互未回退
- 仅样式或视觉改动：至少确认受影响界面的关键状态、布局与主要交互未回退
- 若影响共享组件、布局或状态流转，优先验证影响范围最大的界面，而不是只看局部组件

### 总体要求
- 影响行为的改动应优先补充或更新测试
- 若本次改动未补测试，必须在最终说明中写明原因和风险
- 测试应覆盖真实业务行为，不要只验证静态渲染

### 视图层测试
- 页面交互、表单校验、列表行为、状态展示、异常状态发生变化时，应补充对应测试
- 至少关注以下关键状态：`loading`、`empty`、`error`、`no-permission`
- 若涉及数据请求、筛选、分页、提交等用户关键路径，应验证主要交互结果
- 推荐使用 Avalonia Headless 或项目既有 UI 测试方案

### 外部依赖与数据
- 测试中不要真实调用外部服务，统一使用 mock、stub 或测试替身
- 测试数据应尽量最小化、可读、可重复执行
- 不要让测试依赖本地人工状态或不可控外部环境

### 无法执行测试时
- 必须说明未执行的测试类型、原因、潜在影响范围和风险

---
