# AGENTS.md

## 适用范围

- 作用域：Avalonia 视图、样式、资源、导航、对话框、数据绑定、页面状态与 UI 测试
- 触发场景：涉及页面实现、样式资源、控件交互、ViewModel 绑定、UI smoke test 时阅读

---

## 技术栈

### UI
- .NET 10 + Avalonia 11+ + Ursa.Avalonia + CommunityToolkit.Mvvm
- 设计风格：Fluent Design
- 消息传递：`IMessenger`

### 设计系统
- 使用 `design-system.yaml` 中定义的设计 token
- 样式必须使用 Avalonia 样式表（Styles / Classes），禁止把颜色、间距、圆角直接硬编码在控件属性中
- 颜色、间距、圆角必须来自样式资源，使用 `{StaticResource}` 或 `{DynamicResource}` 引用

如果仓库已经有真实实现，以现有代码为准，不要强行重构或替换技术栈。

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

## 阶段 1 — 视图实现（Claude Code 主导）

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
- DTO 类中的类型已定稳，不再变动。
- 满足以上两点后，才允许进入阶段 2。

---

## UI 规则

### 样式规则
- 所有样式使用 Avalonia Styles（`.axaml` 文件），禁止在控件中直接设置 `Background`、`Margin` 等样式属性。
- 所有颜色来自 `design-system.yaml` 中定义的语义化 token，通过样式资源引用。
- 所有间距使用统一的资源或样式类，禁止硬编码数值。
- 响应式设计遵循窗口大小变化，必须在不同窗口尺寸下测试。

### 组件规则
- 优先复用 `src/AppName/Views/Controls/` 下已有组件，禁止重复创建。
- 若 `Irihi.Ursa` / `Ursa.Avalonia` 组件库已有可满足需求的现成组件，优先直接使用或轻量封装现有组件，避免重复实现同类基础控件与交互。
- 只有确实有复用价值时才新增共享组件，避免为单次需求过度抽象。
- 页面状态必须完整：`loading`、`empty`、`error`、`no-permission`。
- 使用 Ursa.Avalonia 控件库优先，必要时使用 Avalonia 官方控件。
- 图标统一使用 Avalonia 官方图形能力、Ursa 组件能力或仓库既有素材。

### MVVM 模式规则
- 所有 ViewModel 必须继承 `ObservableObject`。
- 属性通知使用 `[ObservableProperty]` 特性自动生成，禁止手写重复样板。
- 命令定义使用 `[RelayCommand]` 特性生成，禁止手动拼装重复命令逻辑。
- ViewModel 依赖通过构造函数注入。
- 禁止在 Code-behind（`.axaml.cs`）中编写业务逻辑。

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
- 列表数据使用 `ObservableCollection<T>` 或适合当前项目的可观察集合。
- 复杂集合变更优先使用批量更新策略，而不是简单清空后重添。
- 异步数据加载必须支持取消（`CancellationToken`）。
- 绑定路径必须可维护，禁止依赖脆弱的控件查找方式。

### 状态管理规则
- 本地状态使用 `[ObservableProperty]` 管理。
- 全局共享状态使用 `IMessenger` 传递跨 ViewModel 消息。
- 禁止使用静态全局类存储业务状态。
- 主题、语言等低频全局配置才放入应用级资源或全局上下文。

---

## 测试规则

### 总体要求
- 影响行为的改动应优先补充或更新测试。
- 若本次改动未补测试，必须在最终说明中写明原因和风险。
- 测试应覆盖真实业务行为，不要只验证静态渲染。

### 视图层测试
- 页面交互、表单校验、列表行为、状态展示、异常状态变化时，应补充对应测试。
- 至少关注以下关键状态：`loading`、`empty`、`error`、`no-permission`。
- 若涉及数据请求、筛选、提交等关键路径，应验证主要交互结果。
- 推荐使用 Avalonia Headless 或项目既有 UI 测试方案。

### 外部依赖与数据
- 测试中不要真实调用外部服务，统一使用 mock、stub 或测试替身。
- 测试数据应尽量最小化、可读、可重复执行。
- 不要让测试依赖本地人工状态或不可控外部环境。

### 无法执行测试时
- 必须说明未执行的测试类型。
- 必须说明未执行原因。
- 必须说明潜在影响范围和风险。

---

文件结束。
