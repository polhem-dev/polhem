# Web.Js.Demo

[English](README.md) | **繁體中文**

示範如何用純 JavaScript（瀏覽器原生）呼叫 Polhem 的 JSON-RPC API ——
不需要 `npm`、不需要 build、不需要任何框架。JS 前端走
`PayloadFormat.Plain`（見 [ADR-014](../../maintainers/adr/adr-014-jsonrpc-plain-public-default.md)），
所有請求都是純 JSON。

它呼叫的每個方法都宣告為 `Public`，Plain 請求才進得去。頁面也會依 FormSchema /
FormLayout 定義渲染出表單：

| 區塊 | 對應方法 |
|------|---------|
| Login | `System.Login` |
| Ping | `System.Ping`（不需 auth） |
| Enter Company | `System.EnterCompany` / `System.LeaveCompany`（Staff CRUD 之前必須先做；未知的代碼可示範錯誤路徑） |
| Staff CRUD | `Staff.GetList` / `GetData` / `GetNewData` / `Save` / `Delete` |
| FormDefinition-driven 渲染 | `System.GetFormSchema` / `System.GetFormLayout` → 定義 XML → 動態 form |
| Logout | `System.Logout` |

## 怎麼跑

1. 啟動後端（另開一個 terminal）：

   ```sh
   cd samples/QuickStart.Server
   dotnet run
   ```

   Server 監聽 `http://localhost:5050`，已開 CORS 允許任何 origin
   （demo-only — production host 必須明確限制 origin）。

2. 用瀏覽器開啟 demo，兩種方式：

   - **直接開檔** — `open samples/Web.Js.Demo/index.html`（Mac），
     Windows / Linux 用對應工具。現代瀏覽器在 `AllowAnyOrigin` CORS 政策下
     接受 `file://` origin。

   - **靜態檔案 server** — 如果瀏覽器擋 `file://` 的 `fetch`：

     ```sh
     # Python 3
     cd samples/Web.Js.Demo
     python3 -m http.server 8080
     # 或裝了 dotnet serve
     dotnet serve -p 8080
     ```

     然後開 `http://localhost:8080/index.html`。

3. 點 **Login**（預設帳密 `demo` / `demo`），再點 **EnterCompany**（公司代碼已預填 `DEMO`），
   再點 **GetList** —— 結果會出現在下方的輸出區。人員（Staff）表單屬於公司範圍，session 進入公司之前，
   CRUD 與表單按鈕都會失敗。

## 檔案

| 檔案 | 用途 |
|------|------|
| `index.html` | 最小 UI — Login 表單、CRUD 按鈕、動態 form 區、結果輸出區。Vanilla CSS，無外部依賴。 |
| `polhem-api-client.js` | ES module：匯出 `systemApi.*`、`formApi(progId)`、`RpcError` 與 access token 輔助函式。所有呼叫都經過模組內部的 `rpcCall(method, value)`，HTTP 錯誤回應會先讀出其中的 JSON-RPC 錯誤，讀不到才退回狀態列；定義 XML 也在這裡解析成一般物件。 |
| `form-renderer.js` | ES module：吃解析後的 `FormLayout` 物件、產生可操作的 HTML 表單（CSS Grid、controlType dispatch）。匯出 `bindDataSet` / `collectDataSet` 做雙向資料綁定。 |
| `app.js` | UI 事件綁定；依賴 `polhem-api-client.js` 與 `form-renderer.js`。 |
| `.smoke.yaml` | `demo-smoke` skill 的設定檔 — 啟動兩個 prerequisite server 並驗證頁面載入後出現預期的區塊文字。檔案開頭註解說明瀏覽器 tier 限制（無法點擊 → 只能 load-level smoke）。 |

## 使用的 Headers

| Header | 值 | 說明 |
|--------|-----|------|
| `Content-Type` | `application/json` | JSON-RPC body |
| `X-Api-Key` | `quickstart-demo`（寫死於 client） | 識別呼叫端應用程式。預設的 `ApiAuthorizationValidator` 只在部署尚未發放任何 API 金鑰時接受任意非空值；發放金鑰後，這個值必須是該金鑰。正式部署應發放金鑰。 |
| `Authorization` | `Bearer <accessToken>` | 只在 `Login` 取得 AccessToken 後才送。所有 `[ApiAccessControl]` 為 `Authenticated` 的方法都需要。 |

## FormDefinition-driven 渲染

UI 區塊 5 示範 schema-driven 表單的端到端路徑 —— React / Vue / Angular
app 通常在這個 pattern 上加自己的元件層。定義以 XML 字串傳輸（回應的 `xml`
成員），由 `polhem-api-client.js` 解析成一般物件；資料則以 JSON 傳輸：

```
GetFormSchema + GetFormLayout（並行）→ 定義 XML → 解析後的 FormLayout
        ↓
renderFormLayout(layout, container)     ← 產生可操作的 HTML 表單
        ↓
GetNewData / GetData                     → DataSet JSON
        ↓
controller.bindDataSet(dataSet)          ← 灌進表單
        ↓
controller.collectDataSet()              ← 收回表單狀態，標 RowState=Modified
        ↓
Save                                     → server 回傳 refreshed DataSet
```

**設計取捨：**

- **不做客戶端驗證**。renderer 信任 `Save` 失敗會回 `RpcError`，
  錯誤訊息顯示在輸出區。實務 app 可加一層由 `FormSchema` 欄位 metadata
  （`allowNull`、`maxLength` 等）驅動的小型驗證 —— 不在本 demo 範圍內。
- **Detail 表唯讀**。把 `LayoutGrid` 渲染為純 `<table>`；加 / 改 / 刪明細列
  需要更複雜的 UI。
- **CSS Grid 排版**。`LayoutField.rowSpan` / `columnSpan` 直接對應
  `grid-row: span N` / `grid-column: span N`，多欄表單不需要 layout library。

**Port 到 React / Vue / Angular：**

- 把 `renderFormLayout` 直接操作 DOM 的部分換成框架的元件樹
  （一個 `<FormLayout layout={…} />` 元件把每個 section / field 對應到子元件）。
- 保留 control-type → 元件 map（`TextEdit` → `<TextField>` 等）——
  這部分是各框架差異最大的地方。
- 把 `bindDataSet` / `collectDataSet` 當純資料 helper 重用；它們除了
  fieldName → control 查找外沒有 DOM 依賴，在框架內可改用 ref 或 state binding。

## 為什麼純 JavaScript

開放 Plain wire format 的目的就是讓 JS 前端能直接和 Polhem 後端通訊，
不必引入 `Microsoft.JSInterop`、MessagePack、或 AES-CBC-HMAC 加密管線。
保持 demo 零依賴，可以清楚示範真實 JS 框架整合（React / Vue / Angular）
需要的最小 surface。

如果你的專案已經用 TypeScript toolchain，與其移植這個檔案，不如看
TypeScript client [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js)。
它的型別來自 [`wire-contracts/`](../../wire-contracts/README.md) 產生的宣告，
因此會跟著 server 的 message 型別走。

## 相關文件

- 整合指引：[docs/zh-TW/jsonrpc-frontend-integration.md](../../docs/zh-TW/jsonrpc-frontend-integration.md)
- 後端 host：[samples/QuickStart.Server](../QuickStart.Server/)
- Demo 帳密：[samples/Polhem.Samples.Shared/DemoCredentials.cs](../Polhem.Samples.Shared/DemoCredentials.cs)
