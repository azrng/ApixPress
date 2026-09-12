# ApixPress.TestApi

本地全格式 HTTP 测试服务器（.NET 10 Minimal API），用于在 ApixPress 中做真实请求联调。
端点风格参考 httpbin，覆盖 ApixPress 调试功能涉及的各种请求/响应格式。

## 启动

```bash
dotnet run --project tools/ApixPress.TestApi
```

默认监听 `http://127.0.0.1:5180`，可通过 `--urls` 或环境变量 `ASPNETCORE_URLS` 覆盖：
需要测试 HTTPS/SSL 忽略选项时，先执行 `dotnet dev-certs https --trust`，再用
`--urls https://127.0.0.1:5181` 启动。

在 ApixPress 中可新建环境变量 `baseURL = http://127.0.0.1:5180` 配合 `{{baseURL}}` 使用。

## 端点速查

### 请求回显（验证方法、参数、头、Cookie、请求体）
| 端点 | 说明 |
| --- | --- |
| `/get` `/post` `/put` `/patch` `/delete` | 任意方法回显请求详情（真实方法、query、headers、cookies、body、JSON 解析、表单字段） |
| `/anything`、`/anything/{**path}` | 任意路径回显 |
| `/headers` `/ip` `/user-agent` `/uuid` | 单项信息 |

### 请求体格式
| 端点 | 说明 |
| --- | --- |
| `/forms/post` | x-www-form-urlencoded 解析回显 |
| `/upload` | multipart/form-data，回显字段与文件元信息（大小、SHA-256） |
| `/binary-echo` | 原始字节回显（二进制上传） |

### 响应格式
| 端点 | 说明 |
| --- | --- |
| `/json` `/xml` `/html` `/text?lines=n` | JSON / XML / HTML / 纯文本示例 |
| `/encoding/utf8` | 多语言 + Emoji 的 UTF-8 文本 |
| `/gzip` `/brotli` | 压缩响应（验证自动解压与中文显示） |
| `/problem/{code}` | RFC 9457 ProblemDetails 错误体 |

### 状态与网络行为
| 端点 | 说明 |
| --- | --- |
| `/status/{code}` | 任意状态码（200~599），418 有响应体彩蛋 |
| `/delay/{seconds}` | 延迟 0~60 秒后回显（验证超时与响应计时） |
| `/redirect/{n}?status_code=302` | 连续 n 次重定向；支持 301/302/303/307/308 |
| `/relative-redirect/{n}` `/absolute-redirect/{n}` `/redirect-to?url=` | 其他重定向形式 |
| `/unstable?rate=0.5` | 按比例随机 500（验证重试） |
| `/cache` | ETag 协商，命中 `If-None-Match` 返回 304 |
| `/stream/{n}` | 分块流式返回 n 行 JSON |
| `/bytes/{n}[/{seed}]` | 随机二进制下载（1B~10MB，可带种子复现） |
| `/image/png` `/image/gif` `/image/svg` | 示例图片下载 |
| `/links/{n}` | 含 n 个链接的 HTML |
| `/sse?count=&interval=` | Server-Sent Events 推送 |

### Cookie 与认证
| 端点 | 说明 |
| --- | --- |
| `/cookies` | 查看当前 Cookie |
| `/cookies/set?k=v` | 写入 Cookie（Set-Cookie）后跳回 |
| `/cookies/delete?k=` | 删除 Cookie 后跳回 |
| `/basic-auth/{user}/{password}` | Basic 认证，失败返回 401 + `WWW-Authenticate` |
| `/hidden-basic-auth/{user}/{password}` | Basic 认证失败返回 404 |
| `/bearer` | Bearer Token 校验 |

### 文档
| 端点 | 说明 |
| --- | --- |
| `/openapi/v1.json` | 内置 OpenAPI 3.1 文档，可测 ApixPress 的 Swagger URL 导入 |
| `/` | 全部端点目录（JSON） |

## 与 ApixPress 功能的对应验证场景

1. Swagger URL 导入：导入 `http://127.0.0.1:5180/openapi/v1.json`
2. Raw 请求体（JSON/XML/HTML/Text）：向 `/post`、`/put` 发送对应 Content-Type，回显校验
3. form-data 文件上传：`/upload`，比对返回的 SHA-256
4. x-www-form-urlencoded：`/forms/post`
5. 二进制上传：`/binary-echo` 比对字节
6. 响应格式化：`/json` `/xml` `/html`；Cookie 自动携带：`/cookies/set` 后任意请求
7. 重定向信息：`/redirect/3`；响应时间：`/delay/1.5`；下载文件：`/bytes/1048576` 或 `/image/png`
8. 环境变量与认证头：`/bearer`、`/basic-auth/admin/admin`

## 已知注意点

- `.NET 10` 下 `MapMethods` 搭配 `async (...) => Results.Json(...)` 表达式体 lambda 会
  出现 200 空响应体（已实测），本项目统一使用块体 lambda 规避。
