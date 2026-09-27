# QuickStart.Console

[English](README.md) | **繁體中文**

Polhem.Api.Client 最小消費端示範。連線到 [`QuickStart.Server`](../QuickStart.Server/README.zh-TW.md)，呼叫 `System.Ping` 與自訂的 `Echo.Echo` BO。

## 跑法

```bash
# 先在另一個 terminal 啟動 QuickStart.Server
cd samples/QuickStart.Console
dotnet run
```

預設連 `http://localhost:5050/api`。要換 endpoint：

```bash
dotnet run -- --endpoint http://other-host:5050/api --apikey app-id.secret
```

## 預期輸出

```
→ endpoint: http://localhost:5050/api

• System.Ping
  status: ok

• Echo.Echo (message="hello from QuickStart.Console")
  response : echo: hello from QuickStart.Console
  serverTime: 2026-05-23T13:00:00.0000000Z
```

## 對應到哪些 library 功能

| 程式段落 | library 功能 |
|----------|--------------|
| `ApiClientInfo.ApiKey = ParseApiKey(args) ?? DefaultApiKey` | `Polhem.Api.Client.ApiClientInfo` — Remote 模式下每個 request 會帶 `X-Api-Key`。伺服端發放正式金鑰後以 `--apikey <key>` 傳入；內建值只是 demo 預設，之所以能用是因為尚未發放金鑰的部署仍接受任何非空值 |
| `new SystemApiConnector(endpoint, Guid.Empty)` | `Polhem.Api.Client.Connectors.SystemApiConnector` — 內部用 `RemoteApiProvider` 走 HTTP |
| `await connector.PingAsync()` | 直接打 `System.Ping`，框架預設將其視為 anonymous，回傳 `status=ok` |
| `new FormApiConnector(endpoint, Guid.Empty, "Echo")` | `Polhem.Api.Client.Connectors.FormApiConnector` — 鎖定 progId="Echo"，呼叫 `Echo.<action>` |
| `connector.ExecuteAsync<EchoResponse>("Echo", req, PayloadFormat.Plain)` | 走 `Echo.Echo`；Plain format 因為 BO 標 `[ApiAccessControl(Public, Anonymous)]` 不需要加密 |

## Local vs Remote 模式

這個 console 用 **Remote 模式**（透過 HTTP 連到 Server）。底層 `Polhem.Api.Client` 同樣支援 **Local 模式**（in-process 直接呼叫 backend），呼叫端 API 完全相同。差別只在建構連線時：

```csharp
// Remote（本 demo）
var connector = new SystemApiConnector("http://localhost:5050/api", Guid.Empty);

// Local（在同一個 process 內）：傳入註冊了 backend 的 service provider
var services = new ServiceCollection();
services.AddPolhemFramework(settings.BackendConfiguration, paths);
using var provider = services.BuildServiceProvider();
var connector = new SystemApiConnector(provider, Guid.Empty);
```

Local 版的 connector 建構子接收註冊 backend 的那個 `IServiceProvider`；`BuildServiceProvider` 來自 `Microsoft.Extensions.DependencyInjection` 套件。完整的 backend bootstrap（master key、SQLite 註冊、載入設定、`AddPolhemFramework`）是 `QuickStart.Server` 呼叫的 [`DemoBackend.AddPolhemBackend`](../Polhem.Samples.Shared/DemoBackend.cs)；console host 在一般的 `ServiceCollection` 上照同樣步驟做即可。

> Local 呼叫是受信任的 in-process 呼叫：backend 對它略過 access token 檢查與 `LocalOnly` 限制。只在呼叫端可被信任、能存取整個 backend 的情境使用。
