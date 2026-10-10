# Polhem Framework

[English](README.md)

[![Build CI](https://github.com/polhem-dev/polhem/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem)

Polhem Framework 是一套採用 **N-Tier + Clean Architecture + MVVM** 混合模式的企業資訊系統開發框架，以**定義導向架構（Definition-Driven Architecture）**為核心，以 `FormSchema` 作為系統唯一定義來源（Single Source of Truth），統一驅動 UI 配置、資料表結構與業務驗證規則。

> 📌 *N-Tier* 指超過三層的邏輯分層架構，在 Polhem 中實際拆分為五層（表現層、API 呼叫層、業務邏輯層、資料存取層、資料庫層），各層職責明確分離，更能因應複雜企業需求。

所有套件目標框架為 **`net10.0`**。

## ✨ 特色

- **定義導向架構（Definition-Driven Architecture）**：以 `FormSchema` 作為系統唯一定義來源，自動推導 UI 配置（`FormLayout`）、資料表結構（`TableSchema`）與驗證規則，定義一處即全層同步。
- **N-Tier + Clean Architecture + MVVM**：支援表現層、API 層、業務邏輯層（BO）與資料存取層的清晰分離，針對企業資訊系統從各模式取用最適合的概念。
- **跨平台支援**：所有套件採用 `net10.0`，支援現代 .NET 執行環境。
- **多資料庫支援**：內建 SQL Server、PostgreSQL、SQLite、MySQL、Oracle 五種 dialect，由 host 應用程式按需註冊。
- **模組化組件**：根據職責切分為多個元件，靈活組合、降低耦合。
- **開發加速器**：透過可重用基底類別與 FormSchema 驅動的 CRUD，大幅減少重複程式碼。
- **慣例於建置期把關**：Roslyn analyzer 隨套件自動註冊，把框架慣例——資料庫 scope 選擇、定義檔跨檔一致性、wire 合約形狀——變成同時指出原因與修法的建置期診斷。詳見 [Analyzer 規則](docs/zh-TW/reference/analyzer-rules.md)。

## 📐 架構總覽

關於 Polhem 的分層架構、資料流與設計決策，請參閱[架構總覽文件](docs/zh-TW/architecture/architecture-overview.md)。

關於 API 合約與 BO 參數的設計原則（Request/Response 與 Args/Result 的使用方式），請參閱 [API/BO 合約設計原則](docs/zh-TW/api/api-bo-contract-design.md)。所有對外公開 API 方法的清單（含每方法 `[ApiAccessControl]` 設定）見 [API 方法參考](docs/zh-TW/api/api-method-reference.md)。

從 JavaScript / TypeScript 前端（React、Vue、Angular、vanilla — 前端無 .NET）呼叫 JSON-RPC API，請參閱 [JSON-RPC 前端整合指引](docs/zh-TW/api/jsonrpc-frontend-integration.md)。

完整開發者文件索引請見 [docs/zh-TW/README.md](docs/zh-TW/README.md)。

## 📦 組件說明

下列每個組件都以同名的 NuGet 套件發佈。JSON-RPC 伺服端從其中兩個開始，再加上提供 HTTP 端點的 `Polhem.JsonRpc.AspNetCore`：

```bash
dotnet add package Polhem.Hosting
dotnet add package Polhem.JsonRpc.AspNetCore
dotnet add package Polhem.Db
```

接下來的步驟見[快速上手](docs/zh-TW/getting-started/getting-started.md)。

> **1.x 的 minor 版可能含破壞性變更**，每一項都會連同遷移步驟列在[變更記錄](CHANGELOG.zh-TW.md)。請把套件鎖定在
> 一個 minor 版內，例如 `Version="[1.5,1.6)"`。自 2.0.0 起依循語意化版本。

### 共用（前端 / 後端）

| 組件名稱 | 說明 |
|---|---|
| **Polhem.Core.dll** | 提供基礎函式與工具（序列化、加密等），作為共通基礎模組。 |
| **Polhem.Definition.dll** | 定義系統結構化資料，包含 FormSchema、欄位結構描述與版面配置。 |
| **Polhem.Expressions.dll** | 可攜、沙箱化的運算式求值引擎（DynamicExpresso 封裝），供計算欄與驗證規則使用；後端存檔與 Avalonia 前端即時預覽共用，兩端算法一致。 |
| **Polhem.Api.Contracts.dll** | 前後端共用的資料契約（請求 / 回應模型）。 |
| **Polhem.Api.Core.dll** | 提供 API 核心支援，包含資料模型、Payload 加解密與序列化管線。 |

### 後端

| 組件名稱 | 說明 |
|---|---|
| **Polhem.Repository.Abstractions.dll** | 定義業務層存取資料層的介面契約，作為 Business Object 與 Repository 之間的邊界。 |
| **Polhem.ObjectCaching.dll** | 執行階段快取 FormSchema 定義資料與衍生資料，提升系統效能。 |
| **Polhem.Db.dll** | 封裝資料庫操作邏輯，支援動態 SQL 命令產生與連線綁定；內建 SQL Server、PostgreSQL、SQLite、MySQL、Oracle 五種 dialect。 |
| **Polhem.Repository.dll** | 提供共用的 Repository 基底類別與 FormSchema 驅動的資料存取機制。 |
| **Polhem.Business.dll** | 實作業務邏輯核心（Business Object / BO），負責 Use Case 工作流程。 |
| **Polhem.Hosting.dll** | Composition root — 提供 `AddPolhemFramework` 擴充方法，將所有後端服務註冊至 `IServiceCollection`（不依賴 ASP.NET Core），可用於 ASP.NET Core、WinForms、Console、Worker Service 等各種宿主。 |

### 前端

| 組件名稱 | 說明 |
|---|---|
| **Polhem.Api.Client.dll** | 提供 API client，支援以同程序或 HTTP 呼叫後端 Business Object（`PolhemApiClient.CreateLocal` / `CreateRemote`）。 |
| **Polhem.UI.Core.dll** | 跨平台 UI 共通層（`ClientInfo` / `IEndpointStorage` / `FileEndpointStorage` / `IUIViewService`），供原生 UI 宿主共用 client-side 連線狀態與 endpoint 持久化邏輯。 |
| **Polhem.UI.Avalonia.dll** | Avalonia 控制項套件，適用桌面（Windows / macOS / Linux）、瀏覽器（WebAssembly）、iOS 與 Android head，提供 FormSchema 驅動控制項（`FormView` / `ListView` / `GridControl` 加上一組 field editor 與 `FormScope` ambient 綁定，皆以 `FormDataObject` 為資料中樞）。單一 `net10.0` TFM；下限版本鎖在 Avalonia 12.0.0 + DataGrid 12.0.0。 |
| **Polhem.Web.Blazor.Server.dll** | Blazor Server 宿主用的 Razor Class Library（RCL），提供 DI scope 連接器與 Blazor 元件（`DynamicForm`、`FormDataObject`）。 |

### Tooling（dotnet tool）

| 套件 | 安裝 | 說明 |
|---|---|---|
| **Polhem.Cli** | `dotnet tool install -g Polhem.Cli` <br/>升版：`dotnet tool update -g Polhem.Cli` | 框架 CLI，命令名 `dotnet polhem`。`defines` 系列命令用來 materialize 與列出 `Polhem.Definition.dll` 內嵌的框架預設定義檔（供新消費者 bootstrap `DefinePath`）；`keys` 系列命令產生 `SystemSettings.xml` 所用的受保護金鑰。見 [Polhem.Cli README](tools/Polhem.Cli/README.zh-TW.md)；各選項以 `dotnet polhem --help` 列出。 |

## 🚀 Quick Start

30 秒看到 Polhem 跑起來：

```bash
# Terminal 1 — 啟動 JSON-RPC API host
cd samples/QuickStart.Server
dotnet run

# Terminal 2 — 連線並呼叫 Echo BO
cd samples/QuickStart.Console
dotnet run
```

Console 會列出 `System.Ping` 狀態與自訂 BO 回應的訊息。完整 demo 清單與每個 demo 對應到哪些 library 功能，見 [`samples/README.zh-TW.md`](samples/README.zh-TW.md)。

想建自己的專案？[快速上手](docs/zh-TW/getting-started/getting-started.md)從一個空資料夾走完同一件事 —— 套件、`DefinePath`、DI 接線、第一個商業物件，再由用戶端呼叫。

## 🌟 完整示範應用 — Polhem.Northwind

[`apps/Polhem.Northwind`](apps/Polhem.Northwind/README.zh-TW.md) 是最完整的示範應用：經典 Northwind 進銷存案例，**幾乎完全由定義組成**（主檔表單、含 lookup 的主從訂單、僅一個手寫商業物件，其餘皆 XML）。同一套共用 `Polhem.Northwind.UI` 跑在 **四個 Avalonia head** —— Desktop、Browser（WASM）、iOS、Android —— 連同一個 JSON-RPC 後端。

四個 head 渲染同一張訂單表單 —— 同一份定義、同一套控件，差別只在最外層的平台殼：

| 桌面 | Browser（WASM） |
|---|---|
| <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/desktop-order-detail.png" alt="桌面 — 訂單單筆" width="420"> | <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/browser-order-detail.png" alt="Browser — 訂單單筆" width="420"> |

| iOS | Android |
|---|---|
| <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/ios-order-detail.png" alt="iOS — 訂單單筆" width="200"> | <img src="https://raw.githubusercontent.com/polhem-dev/polhem/main/apps/Polhem.Northwind/docs/images/android-order-detail.png" alt="Android — 訂單單筆" width="200"> |

更多畫面、表單清單與執行方式：[`apps/Polhem.Northwind/README.zh-TW.md`](apps/Polhem.Northwind/README.zh-TW.md)。

## 💡 範例程式

所有 demo 都集中在 repo 內 [`samples/`](samples/README.zh-TW.md) 目錄底下，最小、聚焦、跟著框架同步演進。以 `dotnet build samples/Polhem.Samples.slnx` 建置（獨立於主 `Polhem.slnx`，不影響主 CI / build 時間）。

| 類別 | Demo | 重點 |
|------|------|------|
| QuickStart | [`QuickStart.Server`](samples/QuickStart.Server/README.zh-TW.md) + [`QuickStart.Console`](samples/QuickStart.Console/README.zh-TW.md) | 最小 JSON-RPC 端到端，含一個 anonymous 自訂 BO |
| Blazor Server | [`Blazor.Server.Demo`](samples/Blazor.Server.Demo/README.zh-TW.md) | `PolhemLoginPanel` + `FormPage` + Staff CRUD,走 `PolhemApiClient.CreateLocal` in-process 派遣 |
| Avalonia | [`Avalonia.DemoCenter`](samples/Avalonia.DemoCenter/README.zh-TW.md) | 主題導向控件 demo center（DevExpress 風格）：導覽樹（主題 → 案例）+ Demo/Source 分頁 + 主題/FormMode 工具列；涵蓋資料繫結、唯讀必填、FormMode、Layout、Grid、原生 vs 繼承比對（Semi.Avalonia，無後端） |

## 從 Bee.NET 遷移

Polhem 以新名稱延續 [Bee.NET](https://github.com/jeff377/bee-library) 框架（`Bee.*` 套件），沒有相容層。
升級時要改的地方見[從 Bee.NET 遷移](docs/zh-TW/guides/migrating-from-bee-net.md)。

## 設計決策

設計背後的理由記錄在[架構決策紀錄](maintainers/adr/README.md)。

## 參與貢獻

見 [CONTRIBUTING.md](CONTRIBUTING.md)（英文）。

## 授權

[MIT](LICENSE.txt)。Copyright (c) Polhem contributors。

## 📬 聯絡與關注

Polhem 由 GitHub 上的 [polhem-dev](https://github.com/polhem-dev) 組織維護。

- 提問、想法與作品分享：[GitHub Discussions](https://github.com/polhem-dev/polhem/discussions)
- 錯誤回報與功能需求：[GitHub Issues](https://github.com/polhem-dev/polhem/issues)
