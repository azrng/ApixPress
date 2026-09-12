# ApixPress + TestApi 全流程联调测试手册

> 文档类型：测试流程 / 验证手册（SOP）
> 适用场景：ApixPress 每次功能改动、发布前回归，需要用真实 HTTP 请求验证"新建项目 → 导入配置 → 调试请求 → 保存用例 → 历史记录"完整链路时，按本文档逐步执行。
> 配套服务：`tools/ApixPress.TestApi`（本地全格式 HTTP 测试服务器），端点清单见 `tools/ApixPress.TestApi/README.md`。
> 最近实测：2026-09-12，全部步骤在 Windows 实机跑通。

---

## 1. 前置条件

### 1.1 启动测试服务器（TestApi）

```bash
dotnet run --project tools/ApixPress.TestApi
```

- 默认监听 `http://127.0.0.1:5180`，可用 `--urls` 覆盖。
- 自检：浏览器或 curl 访问 `http://127.0.0.1:5180/`，应返回端点目录 JSON。

### 1.2 启动 ApixPress

- 开发态：运行 `src/ApixPress.App`（Debug 构建产物 `ApixPress.exe`），或直接 `dotnet run --project src/ApixPress.App`。
- 确认主窗口打开并显示项目首页。

---

## 2. 流程总览

```text
① 新建项目 → ② 导入 OpenAPI 配置 → ③ 逐类发送请求验证 → ④ 保存测试用例 → ⑤ 使用请求历史
```

---

## 3. 详细步骤与验收标准

### 步骤 ① 新建项目

1. 项目首页点击「+ 新建项目」。
2. 填写项目名称（如 `testapi-联调`）与备注信息；创建后应用自动生成该项目默认环境（如「开发」）。
3. 点击「创建项目」。

**验收**：项目列表出现新卡片，项目计数 +1。

### 步骤 ② 导入 OpenAPI 配置

1. 点击新项目卡片上的「打开项目」，进入工作台。
2. 空状态页点击「导入文档」（或侧栏「项目设置 → 数据管理 → 导入数据」）。
3. 数据源格式选择「OpenAPI / Swagger」。
4. 切换到「URL 导入」标签，地址填：

   ```text
   http://127.0.0.1:5180/openapi/v1.json
   ```

5. 点击「导入 URL」。

**验收（实测数据）**：
- 设置页显示「已导入：1 份文档」。
- 侧栏「接口管理」树出现 `ApixPress.TestApi (151)` 分组（路径 × 方法全量导入）。
- 根目录节点带出 Auth 配置（Bearer Token / Basic Auth 生效规则说明）。
- 快捷请求编辑器自动带出 base URL `http://127.0.0.1:5180`，只需填路径即可发请求。

### 步骤 ③ 逐类发送请求验证

入口：侧栏「快捷请求 → 新建」（或双击接口树节点打开已导入接口）。每条请求按"方法 + URL + Body/Headers → 发送 → 核对响应"执行。

#### 3.1 核心请求矩阵（全部实测通过，实测日期 2026-09-12）

**参数与路由类（GET 为主）**

| # | 验证类别 | 方法 | URL / Body | 预期结果（实测值） |
| --- | --- | --- | --- | --- |
| 1 | GET + URL 查询参数 + 中文 | GET | `/get?name=apixpress&lang=中文` | 200；回显 `query.name=apixpress`、`query.lang=中文`（12ms） |
| 2 | GET + Params 面板查询参数 | GET | URL `/get`，Params 面板添加 `page=3` | 200；应用自动拼接为 `/get?page=3`，服务端回显 `query.page=3` |
| 3 | GET + 路由参数（单段） | GET | `/status/418` | 418；客户端错误提示 + 茶壶响应体正确显示（43B） |
| 4 | GET + 路由参数（深层路径） | GET | `/anything/v1/users/42` | 200；`path` 完整回显深层路径 |
| 5 | GET + 路由参数（带占位符接口） | GET | `/basic-auth/admin/admin` | 200/401；路径段即用户名密码（配合 Basic 头） |

**写方法 × 请求体类型类**

| # | 验证类别 | 方法 | URL / Body | 预期结果（实测值） |
| --- | --- | --- | --- | --- |
| 6 | POST + JSON | POST | `/post`，JSON `{"project":"testapi-联调","tags":["json","中文"]}` | 200；`Content-Type: application/json`，响应 `json` 字段完整解析中文 |
| 7 | PUT + JSON | PUT | `/put`，同上 JSON | 200；`method=PUT` |
| 8 | PATCH + JSON | PATCH | `/patch`，JSON `{"op":"replace","path":"/users/42/email"}` | 200；`method=PATCH` + JSON 解析（1ms） |
| 9 | DELETE + JSON 请求体 | DELETE | `/delete`，JSON `{"op":"replace",...}` | 200；DELETE 携带请求体的边界场景正常 |
| 10 | POST + XML | POST | `/post`，Body=XML `<user id="42"><name>中文用户</name></user>` | 200；`Content-Type: application/xml`，中文 XML 完整回显 |
| 11 | POST + Text（raw） | POST | `/post`，Body=Text `这是一段纯文本 raw body 测试` | 200；`Content-Type: text/plain`，中文回显正常 |
| 12 | PUT + x-www-form-urlencoded | PUT/POST | `/forms/post`，表单字段 `username=apixpress` | 200；`form.username` 正确解析 |
| 13 | POST + multipart/form-data | POST | `/upload`，form-data 字段 `file=<本地路径>` | 200；multipart boundary 编码正确；⚠️ 路径按普通文本字段发送（见已知问题 3） |

**响应与网络行为类**

| # | 验证类别 | 方法 | URL / Body | 预期结果（实测值） |
| --- | --- | --- | --- | --- |
| 14 | XML 响应渲染 | GET | `/xml` | 200；XML 中文内容正常（285B） |
| 15 | gzip 压缩响应 | GET | `/gzip` | 200；⚠️ 见已知问题 2：未自动解压，显示乱码 |
| 16 | 错误状态码 | GET | `/status/500` | 500；应用显示「服务端错误 HTTP 500」友好提示 |
| 17 | 延迟与计时 | GET | `/delay/1` | 200；耗时 ≈1004ms，响应计时准确 |
| 18 | 重定向跟随 | GET | `/redirect/2` | 200；自动跟随 2 跳后落地 `/get` |
| 19 | JWT 登录 | POST | `/jwt/login`，表单 `username=admin`、`password=admin123` | 200；返回 `access_token` / `refresh_token` / `expires_in=3600` |
| 20 | JWT 受保护接口 | GET | `/jwt/protected`，Header `Authorization: Bearer <token>` | 200；`authenticated=true` + 完整 claims（role=admin） |

#### 3.2 补充可选场景（TestApi 已内置，按需执行）

- `POST /upload` 真实文件上传（等文件上传 UI 落地后）：响应返回文件名、大小、SHA-256 供比对。
- `POST /binary-echo`：二进制请求体原样回显。
- `/html`、`/text?lines=n`、`/encoding/utf8`：HTML / 纯文本 / 多语言编码响应。
- `/status/418`、`/unstable?rate=0.5`：特殊状态码与随机失败重试。
- `/cookies/set?k=v` → 任意请求：Cookie 自动携带验证。
- `/basic-auth/{user}/{password}`：HTTP Basic；`/hidden-basic-auth/...` 失败返回 404。
- `/apikey`（Header `X-Api-Key: apixpress-dev-key`）：自定义请求头认证。
- `/jwt/mint?expires_in=-1` 等：铸造过期/错签/错误受众令牌，验证 401 错误码解析。
- `/bytes/1048576`、`/image/png`：二进制下载与图片预览。
- `/sse?count=3`：Server-Sent Events。

### 步骤 ④ 保存测试用例

1. 请求成功后，在发送按钮右侧的用例名输入框填写名称（如 `GET-查询参数回显`、`JWT-受保护接口访问`）。
2. 点击「保存测试用例」。

**验收**：接口树对应节点下出现用例子节点（带计数与标记）；重新打开可还原全部请求配置（方法、URL、Body、Headers）。

### 步骤 ⑤ 使用请求历史

1. 点击左侧栏「请求历史」。
2. 核对历史列表：每条记录包含方法徽章、URL、状态码（200 绿 / 500 红）、时间戳、耗时。
3. 点击任一记录，右侧显示「请求历史详情」（参数 / Headers / Body 标签 + 完整响应），可点「继续请求」基于该历史快速重发。
4. 需要「一键清空」时使用页面清空按钮（有二次确认则确认）。

**验收（实测）**：11 条请求全部按序入列，500 状态码以红色徽章区分；历史详情响应与实时响应一致。

---

## 4. 已知问题与注意事项

1. **编辑器状态跨请求保留**：快捷请求编辑器内添加的 Query 参数（如 `page=3`）、Body、Headers 在切换方法或 URL 后不会自动清理，后续请求会继续携带。这是编辑器设计行为（与主流工具一致），但连续验证不同接口时发送前应人工核对参数区，避免脏参数干扰判断。
2. **gzip 响应未自动解压**：TestApi 的 `/gzip` 返回 `Content-Encoding: gzip`，ApixPress 当前版本响应区显示二进制乱码（压缩后字节数）。建议应用层在收到 `Content-Encoding: gzip/br/deflate` 时自动解压后再渲染。
3. **文件上传 UI 缺口（对应任务 T014）**：form-data 字段行目前只有"字段名/字段值/说明"文本输入，无文件选择器与文件字段类型；填本地路径会作为普通文本字段发送（服务端 `files=[]`）。multipart 编码本身正常，真实文件上传已通过 curl `curl -F "file=@路径" http://127.0.0.1:5180/upload` 验证（返回 SHA-256 可比对）。待 T014 落地文件选择后补充 UI 路径实测。

---

## 5. TestApi 端点速查

完整端点清单、内置账号、JWT 参数与 curl 示例见 `tools/ApixPress.TestApi/README.md`；应用内也可访问 `http://127.0.0.1:5180/` 获取 JSON 目录，或把 `http://127.0.0.1:5180/openapi/v1.json` 重新导入新项目做冒烟。

## 6. 回归检查清单（快速版）

- [ ] TestApi 启动且 `/` 返回目录
- [ ] 新建项目成功
- [ ] URL 导入 OpenAPI：接口树出现 151 个接口 + Auth 配置 + base URL 自动带出
- [ ] GET URL 查询参数（含中文）200
- [ ] GET Params 面板参数自动拼接到 URL 200
- [ ] GET 路由参数（`/status/418`、深层路径）200/418
- [ ] POST JSON（`json` 字段解析）200
- [ ] PATCH JSON 200（`method=PATCH`）
- [ ] DELETE + JSON 请求体 200
- [ ] POST XML（`application/xml` + 中文）200
- [ ] POST Text（`text/plain` + 中文）200
- [ ] urlencoded 表单解析 200
- [ ] multipart form-data 请求发送成功（boundary 正常）
- [ ] `/status/500` 显示服务端错误提示
- [ ] `/delay/1` 耗时 ≈1s
- [ ] `/redirect/2` 自动跟随
- [ ] JWT：login 拿 token → protected 200 返回 claims
- [ ] 保存用例后接口树出现用例节点
- [ ] 请求历史完整、可查看详情、可继续请求
