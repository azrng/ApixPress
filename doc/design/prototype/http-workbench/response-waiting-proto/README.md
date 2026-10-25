# HTTP 工作台响应等待态原生原型

本原型只展示 ApixPress HTTP 工作台的等待响应状态，使用静态模拟数据，不连接应用服务、数据库或网络。

运行方式（Windows）：

```powershell
dotnet run --project .\ResponseWaitingPrototype.csproj
```

颜色、字体和间距资源链接到正式应用的 `src/ApixPress.App/Assets/Styles/`，用于保持视觉基线一致。原型确认后，再决定是否同步正式界面。
