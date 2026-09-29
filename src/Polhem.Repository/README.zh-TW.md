# Polhem.Repository

> Repository 抽象的預設實作：表單資料存取、框架系統資料表，以及 Repository 工廠。

[English](README.md)

## 架構定位

- **層級**：資料存取層（實作）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/architecture/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。
- 應用程式透過 `Polhem.Repository.Abstractions` 的 `I*Repository` 介面使用；Repository 由 `RepositoryFactory` 建立。

## 目標框架

- `net10.0` -- 存取現代執行階段 API 與效能改進

## 主要功能

### 表單資料存取

- `DataFormRepository` -- `IDataFormRepository` 的預設實作，負責以 FormSchema 驅動的 CRUD，依 ProgId 解析。
  程式可以透過 `ProgramSettings` 的 `ProgramItem.Repository` 綁定自己的子類別。

### 框架 Repository（internal）

框架資料表（session、使用者、公司、部門、員工、角色、API 金鑰、稽核紀錄與稽核規則，以及資料庫管理）的
Repository 皆為 `internal`。Host 透過 `Polhem.Repository.Abstractions` 中的介面，以
`IRepositoryFactory.Create<T>(accessToken)` 取得。

- Session -- `st_session` 的資料列保存 session 種子：序列化為 XML 的 `SessionUser`，以存取權杖的雜湊值
  （`AccessTokenHasher`）為鍵，因此權杖本身不會被儲存。存取權杖由商業層在登入時核發，而不是由 Repository
  產生。讀取時會篩掉過期的資料列但不刪除；過期資料列由 host 的過期 session 清理服務（`Polhem.Hosting`）移除。
- 資料庫管理 -- 連線測試透過 `ConnectionStringTemplate` 解析 `{@DbName}`、`{@UserId}`、`{@Password}` 佔位符；
  結構升級透過 `TableSchemaBuilder` 進行。

### 工廠實作

- `RepositoryFactory` -- 預設的 `IRepositoryFactory`：以單一共用的 `IRepositoryContext` 建出兩個軸向上的
  所有 Repository。框架軸是一張型別對照表，而不是一個 Repository 一個方法，因此不會每多一張系統表就多一個成員。
- `IRepositoryTypeResolver` / `ProgramSettingsRepositoryTypeResolver` -- 解析 progId 綁定的 Repository 型別。

## 主要公開 API

| 類別 | 用途 |
|------|------|
| `DataFormRepository` | 資料表單的資料存取實作 |
| `RepositoryFactory` | 預設的 `IRepositoryFactory` 實作 |
| `RepositoryBase` | Repository 的基底類別（上下文、存取權杖、progId） |
| `IRepositoryContext` / `RepositoryContext` | 交給每個 Repository 的共用應用程式生命週期服務 |
| `RepositoryDatabaseRouter` | 預設的 `IRepositoryDatabaseRouter` |

## 設計慣例

- **參數化查詢** -- Repository 透過 `DbCommandSpec` 佔位符傳值，由框架轉成命令參數。
- **一個工廠、兩個軸向** -- progId 綁定的 Repository 與框架 Repository 都由 `RepositoryFactory` 建立。
- 啟用 **Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

- 專案根目錄 -- `RepositoryBase`、`IRepositoryContext`、`RepositoryContext`、`RepositoryDatabaseRouter`
- `AuditLog/` -- 稽核紀錄與稽核規則 Repository（internal）
- `Factories/` -- `RepositoryFactory`、`IRepositoryTypeResolver`、`ProgramSettingsRepositoryTypeResolver`
- `Form/` -- `DataFormRepository`
- `System/` -- 框架系統資料表 Repository（internal）
