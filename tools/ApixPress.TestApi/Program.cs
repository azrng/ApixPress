using ApixPress.TestApi;

var builder = WebApplication.CreateBuilder(args);

// 默认监听本地回环地址，可用 --urls 命令行参数或 ASPNETCORE_URLS 环境变量覆盖
var urls = builder.Configuration["urls"];
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(urls) ? "http://127.0.0.1:5180" : urls);

// 内置 OpenAPI 文档（/openapi/v1.json），用于测试 ApixPress 的 Swagger URL 导入
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

EchoEndpoints.Map(app);
FormatEndpoints.Map(app);
ControlEndpoints.Map(app);
SessionEndpoints.Map(app);
JwtEndpoints.Map(app);

// 根路径返回全部端点目录，便于浏览与发现
app.MapGet("/", () => Results.Json(new
{
    name = "ApixPress.TestApi",
    description = "本地全格式 HTTP 测试服务器，用于 ApixPress 真实请求联调",
    openapi = "/openapi/v1.json",
    endpoints = EndpointCatalog.Items.Select(item => new { item.Method, item.Path, item.Description })
}));

app.Run();
