---
rule_id: application-agents
version: 1.60.0
last_updated: 2026-09-07
dependencies: [agents-root]
---

# 应用层规则

## 适用范围

- 作用域：ViewModel、Service、命令、业务编排、结果包装与应用层测试
- 触发场景：涉及业务流程、命令逻辑、状态流转、应用服务或应用层测试时阅读

### 常见任务入口
- 改命令流程或状态切换：先看 ViewModel / 命令规则与结果处理约束
- 改 Service 或业务规则：先看应用层职责边界与异常处理规范
- 改 DTO、映射、结果包装：先看 DTO 与模型规则与统一结果包装
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
技术债务与重构判断遵循 `collaboration-AGENTS.md` 的「纠错、回退与重构」。

---

## 主动建议规则
- 发现业务逻辑放错层、ViewModel 与 Service 职责混杂、DTO 契约不稳定、结果包装不一致或异常处理不统一时，应主动提醒
- 发现状态流转、命令执行、消息传递或业务规则可能影响既有流程、用户可见结果或数据一致性时，必须先说明风险，不得直接扩大修改
- 发现可以复用既有 ViewModel、Service、命令封装、DTO 映射或结果包装时，应优先建议复用
- 不确定状态流转、命令边界或业务规则时，应按根 `AGENTS.md` 的查证优先级处理，禁止按通用经验补写规则

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

### 目录职责映射

按职责将类放到对应目录，文件命名与类名保持一致；View（`.axaml`）的组织见 `ui-AGENTS.md`。

| 目录 | 放什么类 | 命名约定 |
| --- | --- | --- |
| `ViewModels/Base/` | ViewModel 基类 | `*ViewModelBase.cs` |
| `ViewModels/Pages/`、`ViewModels/Dialogs/` | 页面 / 对话框 ViewModel（状态 + 命令编排） | `{名称}ViewModel.cs` |
| `Services/Interfaces/` | 服务接口 | `I{业务域}Service.cs` |
| `Services/Implementations/` | 服务实现 | `{业务域}Service.cs` |
| `Models/Entities/` | 数据实体，一类一文件 | `{实体名}.cs` |
| `Models/DTOs/` | DTO（视图与业务层契约），一类一文件 | `{名称}Dto.cs` |

---

## 阶段 2 — 业务逻辑实现（业务实现角色主导）

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
- View 只负责展示，不承载业务逻辑
- ViewModel 负责状态管理、命令触发、调用 Service 与处理用户可见结果
- Service 负责业务逻辑、流程编排和规则校验，不直接操作 View
- Repository 是数据访问边界，具体实现细则见 `infrastructure-AGENTS.md`
- Model / DTO 负责承载业务数据，不在其中夹带 UI 行为

### ViewModel 规则
- ViewModel 中可以组织用户操作流程，但禁止直接写 SQL 或直接依赖存储细节
- 命令执行后的成功、失败、空状态必须显式反馈到界面状态
- 异常在 ViewModel 层转换为用户友好的提示信息，不把底层异常原样暴露给用户
- 跨 ViewModel 协作优先使用 `IMessenger` 或显式服务，不依赖静态全局状态
- 禁止在 ViewModel、Service、Repository、Model、DTO 等 C# 类中使用主构造函数（Primary Constructor），统一使用显式构造函数

### Service 层规则
- 服务类必须实现接口，接口定义在 `Services/Interfaces/` 目录。
- 服务方法必须优先采用异步形式（返回 `Task<T>` 或 `ValueTask<T>`）。
- 服务层处理所有业务逻辑，不直接访问 Repository 以外的数据依赖。
- 服务层异常必须统一封装，抛出 Azrng 异常体系中的业务异常类型。
- 服务类实现 `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` 接口表达生命周期，按对应生命周期显式注册（`RegisterBusinessServices` 属 `Azrng.AspNetCore.Core`，桌面项目不使用）。

### DTO 与模型规则
- DTO 是视图层与业务逻辑层之间的稳定契约，变更时必须同步更新相关映射和调用方
- 若仓库已有真实实体或 DTO 结构，优先沿用现状，不为模板强行改名或重组
- 数据转换规则应集中放在 Service 或明确的映射层，不散落在 View 或 Repository 调用点

### 代码组织规范
- 一个文件只放一个主类型：ViewModel、Service 接口与实现、Entity、DTO 等，默认一个类一个文件，文件名与类名一致。
- DTO 目录组织：DTO 放 `Models/DTOs/`，一类一文件，不合并到 `Dtos.cs`。
- 触发拆分的信号：职责混杂、同文件出现多个主类型、参数或字段持续堆叠、方法跨多个不相关业务时，应主动拆分。
- 允许例外：`private` / `internal` 且只服务当前文件的小辅助类型、与主类型强绑定的局部 mapping extension、测试文件中只服务当前测试类的小型 fixture。
- 反模式：`Dtos.cs` 长期堆放多个 DTO；把 ViewModel 专用模型内联写在 ViewModel 文件里；业务逻辑塞进 View 的 code-behind。

### 核心逻辑可读性（KISS）
- 核心逻辑优先直白写法；聚合、分组排序、失败边界和稳定顺序等场景，不为形式简洁压成难读的链。
- 只有真正复用、降低认知负担或隔离复杂边界时才抽取 helper；单调用点且无特殊职责的方法直接内联。
- 提交前确认读者能看懂核心逻辑、抽象没有增加跳转成本，并选择当前需求的最简单可行实现。

### 统一结果包装
- 桌面 / 移动应用没有 Web 那样的全局结果包装过滤器：Service 方法默认返回裸业务对象（`Task<T>`），参数校验等可预期错误抛 Azrng 业务异常，由 ViewModel 捕获并转为用户提示
- 仅当调用方需要「有结果但失败」的流程型返回时使用 `IResultModel<T>`，构造用 `ResultModelFactory.Success(data)` / `ResultModelFactory.Failure(message)`（`Azrng.Core` 提供；`Failure` 默认 code 为 `"ERROR"`）
- `IResultModel<T>` 仅暴露 `Data`；成败标志与错误信息（`IsSuccess` / `Message` / `Errors`）在非泛型 `IResultModel` 上，实现类 `ResultModel<T>` 同时实现两个接口，消费侧按 `is IResultModel` 判别
- 对于仅表示操作结果的方法，也应保持统一的结果语义，不返回随意结构

### 异常处理规范
- 业务异常继承 `BaseException` 或其子类：
  - `LogicBusinessException`：业务逻辑异常
  - `ParameterException`：参数校验异常
  - `NotFoundException`：资源不存在
  - `ForbiddenException`：禁止访问
  - `InternalServerException`：服务器内部错误
- 禁止抛出非 Azrng 体系的随意自定义异常。
- 异常处理应尽量保留可定位信息，同时对用户输出友好、可理解的提示。

### 代码注释规范
- 必须补 XML 注释的位置：
  - ViewModel 公共命令与关键属性：用 `<summary>` 说明用户操作意图与可观察状态。
  - Service 接口方法：说明该能力做什么，不写实现细节。
  - 不来自接口的实现类自有方法（接口未约束的核心方法）：补 `<summary>` 说明该能力做什么。
- 接口已注释时，实现方法不重复注释，也不加 `/// <inheritdoc />`；只有实现类自有的、接口未定义的方法才需要补注释。
- 默认不写 `<remarks>`：实现类、实现方法上的 `<remarks>` 多为实现细节，按“不写实现细节”原则省略。
  - 例外：ViewModel 公共命令需要向使用者说明绑定约定、关联属性、触发条件等对外信息时，可保留精简后的 `<remarks>`，但不写内部实现说明。
- 优先补行内注释的位置：复杂状态流转、命令分支、外部调用编排，说明“为什么这样做”——包括 fail fast 的原因、为什么跳过某类输入、为什么保留某一步骤。
- 不要补的注释：普通属性的 get / set、简单 `if` / `return`、“返回结果”这类复述代码的低价值注释。
- 注释统一使用中文，不使用纯英文注释；不在注释里泄露密钥、token、连接串或真实生产地址。

### 反模式与修根因清单
- 这是根 `AGENTS.md`「修 Bug 必须定位根因」在 .NET 中的具体禁止项，禁止用下列方式掩盖问题：
  - 空 `catch` 或 `catch (Exception)` 吞异常后静默继续
  - 用 `!`（null-forgiving）压制可空告警而非真正处理 null
  - 在异步上下文用 `.Result` / `.Wait()` 同步阻塞
  - `#pragma warning disable` 关闭编译 / 分析器告警掩盖问题
  - 删除或注释测试让构建通过
  - 用一层抽象重写掩盖原始复杂度，而不是简化它
- 出现这些倾向时立刻回退一步：复杂 LINQ 比业务逻辑本身更难读；helper 数量增加但理解成本没下降；核心逻辑缺注释只能靠猜。

### 可维护性自检（提交前）
提交前对核心逻辑至少问一遍：
- 维护者能否一遍看懂这段核心逻辑？
- 是否必须跳转多个 helper 才知道发生了什么？
- 关键方法和关键逻辑有没有注释？
- 这是不是当前需求的最简单可行实现？
- 我是不是为了“写得像高手”而牺牲了可读性？
- 犹豫写“抽象版”还是“直白版”时，默认选直白版。

---

## 测试规则

### 提交前最小回归
- 默认执行：项目现有的编译、静态检查或单元测试
- ViewModel、命令、Service 改动：至少验证一项业务流程、状态流转或错误分支
- DTO、映射、结果包装改动：至少验证输入输出结构和边界条件
- 与数据访问、集成或配置相关的改动：至少补一项联调或等价验证，证明真实链路生效

### 总体要求
- 影响行为的改动应优先补充或更新测试，而不是只修改实现代码
- 若本次改动未补测试，必须在最终说明中写明原因和风险
- 测试应覆盖真实业务行为，不要只覆盖静态渲染或无意义分支

### 应用层测试
- ViewModel 命令执行、属性变更、消息发送发生变化时，应补充对应测试
- Service 层业务逻辑、数据转换、异常处理发生变化时，应补充对应测试
- 推荐使用 xUnit 作为测试框架

### 外部依赖与数据
- 测试中不要真实调用外部服务，统一使用 mock、stub 或测试替身
- 测试数据应尽量最小化、可读、可重复执行
- 不要让测试依赖本地人工状态或不可控外部环境

### 无法执行测试时
- 必须说明未执行的测试类型、原因、潜在影响范围和风险

---
