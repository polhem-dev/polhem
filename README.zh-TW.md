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
- **慣例於建置期把關**：Roslyn analyzer 隨套件自動註冊，把框架慣例——資料庫 scope 選擇、定義檔跨檔一致性、wire 合約形狀——變成同時指出原因與修法的建置期診斷。詳見 [Analyzer 規則](docs/zh-TW/analyzer-rules.md)。

## 📐 架構總覽

關於 Polhem 的分層架構、資料流與設計決策，請參閱[架構總覽文件](docs/zh-TW/architecture-overview.md)。

關於 API 合約與 BO 參數的設計原則（Request/Response 與 Args/Result 的使用方式），請參閱 [API/BO 合約設計原則](docs/zh-TW/api-bo-contract-design.md)。所有對外公開 API 方法的清單（含每方法 `[ApiAccessControl]` 設定）見 [API 方法參考](docs/zh-TW/api-method-reference.md)。

從 JavaScript / TypeScript 前端（React、Vue、Angular、vanilla — 前端無 .NET）呼叫 JSON-RPC API，請參閱 [JSON-RPC 前端整合指引](docs/zh-TW/jsonrpc-frontend-integration.md)。

完整開發者文件索引請見 [docs/zh-TW/README.md](docs/zh-TW/README.md)。

## 📦 組件說明

下列每個組件都以同名的 NuGet 套件發佈。JSON-RPC 伺服端從其中兩個開始：

```bash
dotnet add package Polhem.Api.AspNetCore
dotnet add package Polhem.Db
```

接下來的步驟見[快速上手](docs/zh-TW/getting-started.md)。

### 共用（前端 / 後端）

| 組件名稱 | 說明 |
|---|---|
| **Polhem.Base.dll** | 提供基礎函式與工具（序列化、加密等），作為共通基礎模組。 |
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
| **Polhem.Api.AspNetCore.dll** | ASP.NET Core 的 JSON-RPC 2.0 API 整合（`UsePolhemFramework` middleware 與 `ApiServiceController`）。 |

### 前端

| 組件名稱 | 說明 |
|---|---|
| **Polhem.Api.Client.dll** | 提供連接器機制，支援近端與遠端呼叫後端 Business Object（`LocalApiProvider` / `RemoteApiProvider`）。 |
| **Polhem.UI.Core.dll** | 跨平台 UI 共通層（`ClientInfo` / `IEndpointStorage` / `IUIViewService` / `VersionInfo`），供原生 UI 宿主共用 client-side 連線狀態與 endpoint 持久化邏輯。 |
| **Polhem.UI.Avalonia.dll** | Avalonia 桌面控制項套件（Windows / macOS / Linux），提供 FormSchema 驅動控制項（`FormView` / `ListView` / `GridControl` 加上一組 field editor 與 `FormScope` ambient 綁定，皆以 `FormDataObject` 為資料中樞）與檔案後端 `FileEndpointStorage`。單一 `net10.0` TFM；下限版本鎖在 Avalonia 12.0.0 + DataGrid 12.0.0。 |
| **Polhem.Web.Blazor.Server.dll** | Blazor Server 宿主用的 Razor Class Library（RCL），提供 DI scope 連接器與 Blazor 元件（`DynamicForm`、`FormDataObject`）。 |

### Tooling（dotnet tool）

| 套件 | 安裝 | 說明 |
|---|---|---|
| **Polhem.Cli** | `dotnet tool install -g Polhem.Cli` <br/>升版：`dotnet tool update -g Polhem.Cli` | 框架 CLI，命令名 `dotnet polhem`。本版 ship 出 `defines` subcommand group，用於 materialize / list 框架預設定義檔（`st_*` TableSchema、框架預設 FormSchema / FormLayout / Language、SystemSettings / DatabaseSettings template）。用於新消費者從 `Polhem.Definition.dll` 內 embedded 資源 bootstrap `DefinePath`。 |

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

想建自己的專案？[快速上手](docs/zh-TW/getting-started.md)從一個空資料夾走完同一件事 —— 套件、`DefinePath`、DI 接線、第一個商業物件，再由用戶端呼叫。

## 🌟 完整示範應用 — Polhem.Northwind

[`apps/Polhem.Northwind`](apps/Polhem.Northwind/README.zh-TW.md) 是最完整的示範應用：經典 Northwind 進銷存案例，**幾乎完全由定義組成**（八張表單、含 lookup 的主從訂單、僅一個手寫商業物件，其餘皆 XML）。同一套共用 `Polhem.Northwind.UI` 跑在 **四個 Avalonia head** —— Desktop、Browser（WASM）、iOS、Android —— 連同一個 JSON-RPC 後端。

四個 head 渲染同一張訂單表單 —— 同一份定義、同一套控件，差別只在最外層的平台殼：

| 桌面 | Browser（WASM） |
|---|---|
| <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-desktop-order-detail.png" alt="桌面 — 訂單單筆" width="420"> | <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-browser-order-detail.png" alt="Browser — 訂單單筆" width="420"> |

| iOS | Android |
|---|---|
| <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-ios-order-detail.png" alt="iOS — 訂單單筆" width="200"> | <img src="https://raw.githubusercontent.com/jeff377/blog-images/main/avalonia-mobile-frontend-android-order-detail.png" alt="Android — 訂單單筆" width="200"> |

更多畫面、表單清單與執行方式：[`apps/Polhem.Northwind/README.zh-TW.md`](apps/Polhem.Northwind/README.zh-TW.md)。

## 💡 範例程式

所有 demo 都集中在 repo 內 [`samples/`](samples/README.zh-TW.md) 目錄底下，最小、聚焦、跟著框架同步演進。以 `dotnet build samples/Polhem.Samples.slnx` 建置（獨立於主 `Polhem.slnx`，不影響主 CI / build 時間）。

| 類別 | Demo | 重點 |
|------|------|------|
| QuickStart | [`QuickStart.Server`](samples/QuickStart.Server/README.zh-TW.md) + [`QuickStart.Console`](samples/QuickStart.Console/README.zh-TW.md) | 最小 JSON-RPC 端到端，含一個 anonymous 自訂 BO |
| Blazor Server | [`Blazor.Server.Demo`](samples/Blazor.Server.Demo/README.zh-TW.md) | `PolhemLoginPanel` + `FormPage` + Employee CRUD,走 `LocalApiProvider` in-process 派遣 |
| Avalonia | [`Avalonia.DemoCenter`](samples/Avalonia.DemoCenter/README.zh-TW.md) | 主題導向控件 demo center（DevExpress 風格）：導覽樹（主題 → 案例）+ Demo/Source 分頁 + 主題/FormMode 工具列；涵蓋資料繫結、唯讀必填、FormMode、Layout、Grid、原生 vs 繼承比對（Semi.Avalonia，無後端） |
| 純 JS | [`Web.Js.Demo`](samples/Web.Js.Demo/README.zh-TW.md) | 用瀏覽器原生 JavaScript 呼叫 JSON-RPC API — 前端無 .NET、無 npm |

## 從 Bee.NET 遷移

Polhem 以新名稱延續 [Bee.NET](https://github.com/jeff377/bee-library) 框架（`Bee.*` 套件，最後發佈的版本是 4.33.0）。除了改名，Polhem 1.0.0 的程式碼就是
Bee.NET 4.33.0 的程式碼。所有帶 `Bee` 的名稱都已改名，舊名稱不再被辨識：沒有相容層。

### 套件、命名空間與型別

- 每個 `Bee.<名稱>` 套件改為 `Polhem.<名稱>`，套件的切分不變：`Bee.Hosting` 改為 `Polhem.Hosting`，上方表格中的
  每個套件依此類推。命名空間照同一規則：`Bee.Definition.Forms` 改為 `Polhem.Definition.Forms`。
- 型別或成員名稱中的 `Bee` 改為 `Polhem`：`AddBeeFramework` 改為 `AddPolhemFramework`、`UseBeeFramework` 改為
  `UsePolhemFramework`、`IBeeContext` 改為 `IPolhemContext`、`BeeLoginPanel` 改為 `PolhemLoginPanel`。
- 命令列工具 `Bee.Cli`（`dotnet bee`）改為 `Polhem.Cli`（`dotnet polhem`）。先移除舊工具，再以
  `dotnet tool install -g Polhem.Cli` 安裝新工具。

程式碼中仍使用這些名稱的地方，編譯器都會報出來。

### 編譯器不會檢查的名稱

以下都是字串。沿用舊名稱時建置照樣成功，問題要到執行時才出現，或根本不會出現。

| 項目 | Bee.NET | Polhem | 沿用舊名稱時 |
|------|---------|--------|--------------|
| 定義檔中的型別名稱：`ProgramSettings.xml` 的 `BusinessObject` 與 `Repository`、`SystemSettings.xml` 的 `BackendComponents` 各元素，以及自家程式碼中的型別名稱 | `Bee.Business.AuditLog.LogBusinessObject, Bee.Business` | `Polhem.Business.AuditLog.LogBusinessObject, Polhem.Business` | 執行時找不到型別 |
| 主金鑰的預設環境變數 | `BEE_MASTER_KEY` | `POLHEM_MASTER_KEY` | 只影響沿用預設變數名稱的 `SystemSettings.xml`。`MasterKeySource` 的 `Value` 寫明變數名稱時，照樣使用該名稱；`dotnet bee defines materialize` 寫出的預設檔寫的是 `BEE_MASTER_KEY` |
| `.editorconfig`、`#pragma warning`、`NoWarn` 與 `[SuppressMessage]` 中的 analyzer 診斷代號 | `BEE1001` | `POLHEM1001`（數字不變） | 設定被靜默忽略 |
| 定義檔檢查用的 MSBuild 屬性 | `BeeDefinitionFilesGlob`、`BeeRequireDefinitionFiles`、`BeeAnalyzeDefinitionFiles` | `PolhemDefinitionFilesGlob`、`PolhemRequireDefinitionFiles`、`PolhemAnalyzeDefinitionFiles` | 設定被靜默忽略，改用預設值 |
| Blazor 元件的 CSS class | `bee-dynamic-form`、`bee-dynamic-grid`、`bee-form-page`、`bee-login-panel` | `polhem-dynamic-form`、`polhem-dynamic-grid`、`polhem-form-page`、`polhem-login-panel` | 自訂的樣式規則不再套用 |
| logging category，例如 `Logging:LogLevel` 底下的篩選 | `Bee.Api.AspNetCore` | `Polhem.Api.AspNetCore` | 篩選不再符合 |

在自家 repo 的根目錄執行下列指令即可找出這些地方：

```bash
grep -rnE "Bee\.|Bee[A-Z]|BEE_|BEE[0-9]{4}|dotnet[- ]bee|bee-(dynamic|form|login)" --include="*.cs" --include="*.razor" --include="*.css" --include="*.xml" --include="*.json" --include="*.csproj" --include="*.props" --include="*.targets" --include=".editorconfig" --include="*.yml" --include="*.yaml" --include="*.sh" --include="Dockerfile" .
```

### 切換時會發生的事

- **已登入的使用者要重新登入。** 預設的 `DerivedApiEncryptionKeyProvider` 以標籤衍生各 session 的加密金鑰，而標籤已
  改名（`bee-api-*` 改為 `polhem-api-*`），所以 Bee.NET 建立的 session 在 Polhem 上無法再使用。衍生出的金鑰從不儲存，
  所以沒有任何儲存的資料依賴這些標籤。
- **用戶端與伺服端要一起升級。** payload 帶有型別名稱，例如 `Polhem.Definition.Collections.Parameter, Polhem.Definition`，
  而伺服端只接受允許的命名空間中的型別。Bee.NET 用戶端送出的是 `Bee.*` 名稱，Polhem 伺服端會拒絕，反之亦然。
- **Bee.NET 寫入的稽核紀錄保留舊標記。** `st_log_change` 的 `changes_xml` 欄以 `msprop:Bee.FieldDbType` 記錄每個欄位
  宣告的型別，Polhem 讀的是 `Polhem.FieldDbType`。舊紀錄讀出的值不變，只有 `GetDeclaredFieldDbType()` 對其欄位回傳
  `null`。
- **DefineEditor 以全新的設定啟動。** 它在使用者應用程式資料資料夾下的設定資料夾，由 `Bee.DefineEditor` 改為
  `Polhem.DefineEditor`。要保留最近開啟的檔案與偏好設定，請複製舊資料夾。
- **資料表與欄位不變。** 框架資料表維持 `st_` 開頭的名稱，不需要資料遷移。

## 設計決策

設計背後的理由記錄在[架構決策紀錄](docs/adr/README.zh-TW.md)。

## 參與貢獻

見 [CONTRIBUTING.zh-TW.md](CONTRIBUTING.zh-TW.md)。

## 授權

[MIT](LICENSE.txt)。Copyright (c) Polhem contributors。

## 📬 聯絡與關注
歡迎追蹤我的技術筆記與實戰經驗分享

[Facebook](https://www.facebook.com/profile.php?id=61574839666569) ｜ [HackMD](https://hackmd.io/@jeff377) ｜ [GitHub](https://github.com/jeff377) ｜ [NuGet](https://www.nuget.org/profiles/jeff377)
