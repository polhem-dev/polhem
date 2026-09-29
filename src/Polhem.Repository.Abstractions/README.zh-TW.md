# Polhem.Repository.Abstractions

> 資料存取層的抽象介面庫，定義 Repository 合約與 Repository 工廠。

[English](README.md)

## 架構定位

- **層級**：資料存取層（合約）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

## 主要功能

### 工廠合約

- `IRepositoryFactory` -- 取得 Repository 的唯一入口，涵蓋兩個軸向：
  progId 軸用 `CreateFormRepository<T>(accessToken, progId)`（型別依 progId 而異），
  框架軸用 `Create<T>(accessToken)`（以介面指名的固定型別）。
  兩者都是泛型，新增 Repository 不會擴大介面。

### 表單 Repository 合約（`Form/`）

- `IDataFormRepository` -- 資料表單 CRUD 操作的 Repository 介面；`DataFormListResult` 承載一頁清單

### 系統 Repository 合約（`System/`）

- `ISessionRepository` -- session 種子的持久化：`GetSession`、`InsertSession`、`UpdateSession`、
  `DeleteSession`、`DeleteExpiredSessions`
- `IDatabaseRepository` -- 連線測試（`TestConnection`）與結構描述遷移（`UpgradeTableSchema`）
- `IUserRepository`、`IUserCompanyRepository`、`ICompanyRepository`、`IDepartmentRepository`、
  `IEmployeeRepository`、`IRolePermissionRepository`、`IApiKeyRepository` -- 框架的使用者、公司、組織、權限與
  API 金鑰資料表

### 稽核紀錄合約（`AuditLog/`）

- `IAuditLogRepository` -- 稽核與異常紀錄的查詢，搭配各 `*LogQuery` 型別與 `AuditLogPage`
- `IAuditLogWriteRepository` -- 寫入稽核與異常紀錄
- `IAuditRuleRepository` -- 讀取稽核規則，並通知規則已變更

### 資料庫路由合約

- `IRepositoryDatabaseRouter` -- 依邏輯 `DbScope`（`Common` / `Log` / `Company`）與當前 Session 的存取權杖，解析 Repository 應使用的實體 databaseId

## 主要公開 API

| 介面 / 類別 | 用途 |
|-------------|------|
| `IRepositoryFactory` | 兩個軸向上所有 Repository 的唯一入口 |
| `IDataFormRepository` | 資料表單資料存取合約 |
| `ISessionRepository` | Session 種子持久化 |
| `IDatabaseRepository` | 連線測試與結構描述遷移 |
| `IAuditLogRepository` / `IAuditLogWriteRepository` | 稽核紀錄查詢與寫入 |
| `IRepositoryDatabaseRouter` | 依邏輯 `DbScope` 與存取權杖解析實體 databaseId |

## 設計慣例

- **Repository 模式** -- 每個領域關注點各有專屬的 Repository 介面。
- **一個工廠、兩個軸向** -- `IRepositoryFactory` 以註冊表解析 progId 綁定的 Repository，以介面解析框架 Repository。它取代了三個工廠，其中一個每多一張系統表就多一個方法。
- **被動合約、經 DI 注入** -- 本專案只定義合約；沒有靜態持有者或 service locator。具體實作註冊到 DI 容器並在需要處注入，而不是從靜態進入點解析、或讀取靜態 `BackendConfiguration`。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- `AuditLog/` -- `IAuditLogRepository`、`IAuditLogWriteRepository`、`IAuditRuleRepository` 與查詢型別
- `Factories/` -- `IRepositoryFactory`
- `Form/` -- `IDataFormRepository`、`DataFormListResult`
- `System/` -- 系統 Repository 合約
- 專案根目錄 -- `IRepositoryDatabaseRouter`（`DbScope` -> databaseId）
