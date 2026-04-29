---
rule_id: application-agents
version: 1.2.0
last_updated: 2026-04-16
dependencies: [agents-root]
---

# AGENTS.md

## 适用范围

- 作用域：ViewModel、Service、命令、业务编排、结果包装与应用层测试
- 触发场景：涉及业务流程、命令逻辑、状态流转、应用服务或应用层测试时阅读

### 阅读摘要
- 建议阅读：改 ViewModel、改命令、改 Service、改业务规则、补应用层验证
- 可先跳过：纯界面样式、纯文档整理、仅发布配置调整
- 优先查看：应用层规则、业务流程 / 状态流转规则、测试规则

### 常见任务入口
- 改命令流程或状态切换：先看 ViewModel / 命令规则与结果处理约束
- 改 Service 或业务规则：先看应用层职责边界与异常处理规范
- 改 DTO、映射、结果包装：先看输入输出结构与边界约定
- 补应用层回归：先看 `提交前最小回归` 与测试规则

---

## 技术栈

### 分层架构
- `View → ViewModel → Service → Repository → Model`
- View 负责展示，ViewModel 负责状态与命令编排，Service 负责业务逻辑，Repository 负责数据访问边界，Model 负责领域数据表达

### 核心库
- `CommunityToolkit.Mvvm`：`[ObservableProperty]`、`[RelayCommand]`、`IMessenger`
- `Azrng.Core`：实体基类、扩展方法、工具类、结果包装、异常体系
- `Azrng.Core.Json`：基于 `System.Text.Json` 的 JSON 序列化

### 技术选择原则
如果仓库已经有真实实现，以现有代码为准，不要强行重构或替换技术栈。

**技术债务评估框架**：
- **沿用现有实现**：代码可正常运行、无明显性能问题、维护成本可接受、团队熟悉度高
- **必须重构**：存在安全漏洞、严重影响性能、阻碍新功能开发、维护成本过高
- **可选重构**：代码风格不一致、存在更好的替代方案、但不紧急
- **禁止重构**：仅为个人偏好、追赶技术潮流、非关键路径的过度优化

**重构决策流程**：
1. 先评估现有代码的技术债务等级（高 / 中 / 低）。
2. 判断重构的紧急程度和业务价值。
3. 评估重构风险和工作量。
4. 高风险或大规模重构需先与团队确认。

---

## 推荐目录结构

- 应用层目录建议聚焦在 `src/AppName/ViewModels/`、`src/AppName/Services/`、`src/AppName/Models/` 下组织，优先复用现有结构，不强制迁移。

```text
src/AppName/
├── ViewModels/
│   ├── Base/
│   ├── Dialogs/
│   └── Pages/
├── Services/
│   ├── Interfaces/           # 服务接口定义
│   └── Implementations/      # 服务实现
└── Models/
    ├── Entities/             # 数据实体
    └── DTOs/                 # 数据传输对象
```

---

## 阶段 2 — 业务逻辑实现（Codex 主导）

**触发条件**：用户发出「开始业务逻辑开发」指令

**入场要求**：前端视图已完成，`src/AppName/Models/DTOs/` 契约文件已明确

**工作内容**：
1. 严格按照 DTO 中的类型定义实现 ViewModel 命令编排与 Service 层逻辑。
2. 遵循 `ViewModel → Service → Repository → Model` 完整分层。
3. 涉及数据访问或数据库结构变更时，结合 `infrastructure-AGENTS.md` 同步补齐仓储实现与迁移脚本。
4. 每个关键服务方法和关键 ViewModel 命令都应能通过测试独立验证。

**字段命名约定**：
- C# 类型 / 属性：PascalCase
- DTO / 序列化字段：遵循当前协议约定并保持一致
- 数据库存储字段和映射规则见 `infrastructure-AGENTS.md`

**门控规则**：
- 核心业务逻辑测试通过后，才允许进入阶段 3。

---

## 应用层规则

### 分层边界规则
- View 只负责展示，不承载业务逻辑。
- ViewModel 负责状态管理、命令触发、调用 Service 与处理用户可见结果。
- Service 负责业务逻辑、流程编排和规则校验，不直接操作 View。
- Repository 是数据访问边界，具体实现细则见 `infrastructure-AGENTS.md`。
- Model / DTO 负责承载业务数据，不在其中夹带 UI 行为。

### ViewModel 规则
- ViewModel 中可以组织用户操作流程，但禁止直接写 SQL 或直接依赖存储细节。
- 命令执行后的成功、失败、空状态必须显式反馈到界面状态。
- 异常在 ViewModel 层转换为用户友好的提示信息，不把底层异常原样暴露给用户。
- 供绑定直接读取的属性 getter、预览文本计算和 `Build*Snapshot` / `Resolve*` 一类派生方法必须保持无副作用；禁止为“临时补默认值”而回写 `ObservableProperty`，避免绑定求值触发递归通知。
- 跨 ViewModel 协作优先使用 `IMessenger` 或显式服务，不依赖静态全局状态。
- 禁止在 ViewModel、Service、Repository、Model、DTO 等 C# 类中使用主构造函数（Primary Constructor），统一使用显式构造函数。

### Service 层规则
- 服务类必须实现接口，接口定义在 `Services/Interfaces/` 目录。
- 服务方法必须优先采用异步形式（返回 `Task<T>` 或 `ValueTask<T>`）。
- 服务层处理所有业务逻辑，不直接访问 Repository 以外的数据依赖。
- 服务层异常必须统一封装，抛出 Azrng 异常体系中的业务异常类型。
- 服务类实现 `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` 接口，通过 `RegisterBusinessServices` 批量注册。

### DTO 与模型规则
- DTO 是视图层与业务逻辑层之间的稳定契约，变更时必须同步更新相关映射和调用方。
- 若仓库已有真实实体或 DTO 结构，优先沿用现状，不为模板强行改名或重组。
- 数据转换规则应集中放在 Service 或明确的映射层，不散落在 View 或 Repository 调用点。

### 统一结果包装
- 服务层方法返回值统一使用 `ResultModel<T>` 包装。
- 成功响应：`ResultModel<T>.Success(data)`。
- 错误响应：`ResultModel<T>.Failure(message, errorCode)`。
- 对于仅表示操作结果的方法，也应保持统一的结果语义，不返回随意结构。

### 异常处理规范
- 业务异常继承 `BaseException` 或其子类：
  - `LogicBusinessException`：业务逻辑异常
  - `ParameterException`：参数校验异常
  - `NotFoundException`：资源不存在
  - `ForbiddenException`：禁止访问
  - `InternalServerException`：服务器内部错误
- 禁止抛出非 Azrng 体系的随意自定义异常。
- 异常处理应尽量保留可定位信息，同时对用户输出友好、可理解的提示。

---

## 测试规则

### 提交前最小回归
- 默认执行：项目现有的编译、静态检查或单元测试
- ViewModel、命令、Service 改动：至少验证一项业务流程、状态流转或错误分支
- DTO、映射、结果包装改动：至少验证输入输出结构和边界条件
- 与数据访问、集成或配置相关的改动：至少补一项联调或等价验证，证明真实链路生效

### 总体要求
- 影响行为的改动应优先补充或更新测试。
- 若本次改动未补测试，必须在最终说明中写明原因和风险。
- 测试应覆盖真实业务行为，而不是只覆盖静态分支。

### 应用层测试
- ViewModel 命令执行、属性变更、消息发送发生变化时，应补充对应测试。
- Service 层业务逻辑、数据转换、异常处理发生变化时，应补充对应测试。
- 推荐使用 xUnit 作为测试框架。

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
