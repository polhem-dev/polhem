# Blazor.Server.Demo

[English](README.md) | **繁體中文**

Blazor Server 宿主，示範如何把 `Polhem.Web.Blazor.Server` 元件庫掛到 ASP.NET Core，並透過 **in-process client**（`PolhemApiClient.CreateLocal`）直連 Polhem 後端（同一個程序內、無 HTTP round-trip）。

## 跑起來

```bash
cd samples/Blazor.Server.Demo
dotnet run
# 瀏覽器自動開 http://localhost:5055
```

第一次執行時：

1. 從 `POLHEM_MASTER_KEY` 環境變數讀 master key；`DemoBackend.AddPolhemBackend` 在變數未設時自動注入硬編碼的 demo 值(production host 必須覆寫,見 [`samples/README.zh-TW.md`](../README.zh-TW.md#master-key))
2. 自動建立 `samples/Blazor.Server.Demo/quickstart.db`（SQLite），內含 [`Define/DbCategorySettings.xml`](../Define/DbCategorySettings.xml) 登記的每張表：demo 自己的 `ft_*` 表與 backend 需要的框架表（登入、進入公司、cache-notify poller），定義都在 [`samples/Define/TableSchema/`](../Define/TableSchema/)
3. 寫入登入時讀取語系設定的 `demo` 使用者列、demo 公司 `DEMO` 與使用者的公司授權，以及 demo 人員與團隊

## 預期畫面

1. 進入首頁先看到 **Sign in** 區塊，預填提示 `demo / demo`
2. 按下 Sign in（送 `SystemApiConnector.LoginAsync`，由 `DemoAuthenticatingSystemBusinessObject` 接住）；接著頁面呼叫 `EnterCompanyAsync("DEMO")`，因為人員（Staff）表單屬於公司範圍，兩次呼叫都成功後才顯示表單（[`Components/Pages/Home.razor`](Components/Pages/Home.razor)）
3. 登入成功後顯示 `<FormPage ProgId="Staff" />`：
   - 上方 toolbar：`New` / `Save` / `Delete`
   - 中段：人員列表（`DynamicGrid` 依 `FormSchema.ListFields` 動態渲染）
   - 下段：選中 row 後出現的編輯表單（`DynamicForm`）
4. 點 `New` → 改欄位 → `Save`，新人員會出現在列表

## 對應到 library

| Demo 行為 | Library 元件 |
|----------|--------------|
| Login 表單 | `PolhemLoginPanel` |
| 進入公司 | `SystemApiConnector.EnterCompanyAsync` |
| 頁面上的登入狀態 | `PolhemAccessTokenProvider` |
| 人員列表渲染 | `DynamicGrid` + `FormSchema.GetListLayout()` |
| 人員編輯表單 | `DynamicForm` + 已存檔的 `FormLayout` 定義（`Define/FormLayout/Staff.FormLayout.xml`） |
| 列表 + 表單整合 | `FormPage` |
| CRUD 走 Polhem | `FormDataObject.LoadAsync / SaveAsync / NewAsync / DeleteAsync` |
| Local 模式 in-process 派遣 | `PolhemBlazorOptions.UseLocalProvider()` |
| In-process JSON-RPC | `PolhemApiClient.CreateLocal` → `JsonRpcDispatcher` → `FormBusinessObject` |

## 簡化措施（與 production 不同）

- **Local 模式信任每一位瀏覽器使用者。** `UseLocalProvider()` 讓每個呼叫都成為受信任的 in-process 呼叫：backend 對它略過 access token 檢查與 `LocalOnly` 限制。這適合使用者全都可被信任、能存取整個 backend 的網站，例如這個單人 demo 或內部管理工具。使用者必須受限於自身權限的網站，改用 `UseRemoteProvider(endpoint, apiKey)`（見 `PolhemBlazorOptions` 的 remarks）
- **`DemoAuthenticatingSystemBusinessObject`** 只替換帳密比對：它接受寫死的 `demo/demo`，而不驗證存在 `st_user` 的密碼。登入的其餘流程仍是框架的，所以 `st_user`（使用者的時區與語系）、`st_session`（session 種子）、`st_company` 與 `st_user_company`（進入公司）照樣會建立並寫入資料
- **只有一間公司，不詢問直接進入**：有多間公司的部署會在 `Login` 與 `EnterCompany` 之間放一個公司選擇畫面
- SQLite 為單一檔（`quickstart.db`），跟 `QuickStart.Server` 一樣

Session 狀態不屬於簡化：`AddPolhemBlazor` 為每個 circuit 註冊一個 `PolhemApiClient`，所以同時登入的使用者各自保有自己的 access token、傳輸金鑰與時區。
