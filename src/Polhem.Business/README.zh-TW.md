# Polhem.Business

> 商業邏輯層：系統、表單與稽核紀錄商業物件，驗證與 Session 處理，以及自訂函式執行框架。

[English](README.md)

## 架構定位

- **層級**：商業邏輯層
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

## 主要功能

### 自訂函式執行

- `IBusinessObject` -- 基底介面，提供 `ExecFunc`（需驗證）與 `ExecFuncAnonymous`（匿名）進入點
- `ExecFuncArgs` / `ExecFuncResult` -- 自訂函式分派的輸入/輸出契約
- `IExecFuncHandler` -- 以公開方法作為各函式的類別；`ExecFuncHandlerExtensions` 透過反射呼叫函式 id 指名的方法
- `ExecFuncAccessControlAttribute` -- 方法層級屬性，宣告每個函式的存取需求

### 系統操作

- `ISystemBusinessObject` -- 跨 BO 契約：`Login`、`CreateSession`、`EnterCompany`、`LeaveCompany`、`Logout`。僅供 API 的方法（`Ping`、`GetFormSchema`、`GetFormLayout`、`GetLanguage` 等）以 `[ApiAccessControl]` 公開在具體的 `SystemBusinessObject` 上，刻意不放進此介面
- 每個操作在 `System/` 中都有一組參數/結果（`LoginArgs` / `LoginResult`、`GetDefineArgs` / `GetDefineResult` 等）

### 表單操作

- `IFormBusinessObject` / `FormBusinessObject` -- 以 FormSchema 驅動的 CRUD（`GetList`、`GetData`、`GetNewData`、`Save`、`Delete`、`GetLookup`），含資料範圍、權限、稽核與外掛
- `FormBusinessPlugin` -- 外掛的基底類別，於程式的儲存與刪除管線固定節點執行，透過 `PluginSettings` 綁定

### 稽核紀錄

- `AuditLogBusinessObject` -- 異動、存取、登入與異常紀錄的查詢（保留 progId `AuditLog`）
- `AuditRuleBusinessObject` -- 稽核規則的維護（保留 progId `AuditRule`）

### 驗證與安全

- `LoginAttemptTracker` -- 記憶體內帳號鎖定；預設值為常數 `DefaultMaxFailedAttempts` 與 `DefaultLockoutMinutes`
- `AccessTokenValidator` -- 驗證需認證 API 呼叫的存取權杖
- `DerivedApiEncryptionKeyProvider`（預設）、`DynamicApiEncryptionKeyProvider`、`StaticApiEncryptionKeyProvider` -- API 酬載保護的加密金鑰策略
- `DeploymentAuthorizationService` -- 判定 session 的使用者是否為部署管理員

### 資料、Session 與快取

- `CacheDataSourceProvider` -- 載入資料庫相依的快取物件
- `SessionCompanyBinder` / `EmployeeContextResolver` -- 進入公司，以及 session 的員工上下文
- `BusinessArgs` / `BusinessResult` -- 商業操作共用的基底輸入/輸出型別

## 主要公開 API

| 類別 / 介面 | 用途 |
|-------------|------|
| `IBusinessObject` | 基底 BO 介面（`ExecFunc`、`ExecFuncAnonymous`） |
| `ISystemBusinessObject` | 跨 BO 系統操作（僅供 API 的方法留在具體類別上） |
| `IFormBusinessObject` | 表單層級商業邏輯介面 |
| `BusinessObjectFactory` | 為 progId 建立 BO 實例 |
| `IBoTypeResolver` / `ProgramSettingsBoTypeResolver` | 透過 `ProgramSettings` 將 progId 解析為 BO 型別 |
| `LoginAttemptTracker` | 連續失敗後的帳號鎖定 |
| `AccessTokenValidator` | 存取權杖驗證 |
| `DerivedApiEncryptionKeyProvider` | 預設的每 session 金鑰，由根金鑰與存取權杖推導 |
| `ExecFuncArgs` / `ExecFuncResult` | 自訂函式分派契約 |
| `ExecFuncAccessControlAttribute` | 方法層級存取需求宣告 |
| `BusinessArgs` / `BusinessResult` | 操作的基底輸入/輸出型別 |

## 設計慣例

- **命令模式（Command Pattern）** -- `ExecFunc` 透過反射依名稱呼叫 handler 方法，動態分派自訂商業邏輯。
- **工廠模式（Factory Pattern）** -- `BusinessObjectFactory` 建立 progId 綁定的商業物件，並帶入存取權杖與上下文。
- **樣板方法（Template Method）** -- `BusinessObject` 定義執行骨架；子類別覆寫 `DoExecFunc(ExecFuncArgs, ExecFuncResult)` 與 `DoExecFuncAnonymous(ExecFuncArgs, ExecFuncResult)` 實作特定邏輯。
- **策略模式（Strategy Pattern）** -- 各加密金鑰提供者是 `IApiEncryptionKeyProvider` 的可互換實作，以 `BackendComponents.ApiEncryptionKeyProvider` 選擇。
- **屬性驅動存取控制** -- `ExecFuncAccessControlAttribute` 宣告每個方法的存取需求，於分派時檢查。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- 專案根目錄 -- `BusinessObject`、`BusinessObjectFactory`、`IBusinessObject`、`IExecFuncHandler`、`ExecFuncArgs`、`ExecFuncResult`、`BusinessArgs`、`BusinessResult`、progId 解析相關型別
- `Attributes/` -- `ExecFuncAccessControlAttribute`
- `AuditLog/` -- `AuditLogBusinessObject`、`AuditRuleBusinessObject` 及其參數與結果
- `Form/` -- `IFormBusinessObject`、`FormBusinessObject`（依職責拆檔）、表單外掛，以及表單參數與結果
- `Permission/` -- `ScopeResolver`（資料範圍）
- `Providers/` -- 加密金鑰提供者、`CacheDataSourceProvider`
- `Security/` -- `LoginAttemptTracker`、`DeploymentAuthorizationService`
- `Session/` -- `SessionCompanyBinder`、`EmployeeContextResolver`
- `System/` -- `ISystemBusinessObject`、`SystemBusinessObject`（依職責拆檔）與系統參數與結果
- `Validator/` -- `AccessTokenValidator`
