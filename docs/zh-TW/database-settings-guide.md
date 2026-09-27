<!-- source: en/database-settings-guide.md blob: 42fe7243a4b97f87e12b0fa52d9e98061c1500c0 -->
# DatabaseSettings 與 DbCategorySettings 指引

[English](../en/database-settings-guide.md) · [← 文件索引](README.md)

> 本文件說明 Polhem 框架中兩個資料庫相關設定檔的結構、定位、存取方式與運作流程，協助開發者理解設定 → 連線 → 分類路由的完整串接。

## 目錄

1. [概覽](#1-概覽)
2. [DatabaseSettings](#2-databasesettings)
3. [DbCategorySettings](#3-dbcategorysettings)
4. [存取入口與快取](#4-存取入口與快取)
5. [CategoryId 串接](#5-categoryid-串接)
6. [檔案位置與範例](#6-檔案位置與範例)

---

## 1. 概覽

兩個設定檔共同支撐「FormSchema 定義 → 邏輯分類 → 實體連線」的串接。職責分工如下：

| 設定檔 | 回答的問題 | 對應實體 |
|--------|-----------|---------|
| **DatabaseSettings** | 系統有哪些「實體」資料庫連線？ | DatabaseServer（伺服器組態）+ DatabaseItem（連線項目） |
| **DbCategorySettings** | 系統有哪些「邏輯」資料庫分類？分類下登錄哪些表？ | DbCategory（分類）+ TableItem（表登錄） |

### 串接關係圖

```text
FormSchema.CategoryId ─────┐
                           │
                           ├──► DbCategory.Id  (in DbCategorySettings)
                           │       └─ Tables  (該分類登錄的表清單)
DatabaseItem.CategoryId ───┘

DatabaseItem.ServerId  ────► DatabaseServer.Id  (in DatabaseSettings.Servers)
```

關鍵概念：
- **FormSchema.CategoryId** 宣告表單的資料表屬於哪個邏輯分類。設計階段決定 TableSchema 落檔目錄；執行時決定表單的 Repository 經由哪個範圍（`DbScope`）路由（§5.4）
- **DatabaseItem.CategoryId** 用於部署階段：宣告該實體連線屬於哪個邏輯分類，藉此推導「該實體 DB 應建立哪些表」
- **DbCategory** 是兩者共同的對應目標，`Id` 為框架三個分類 `common`、`company`、`log` 之一（[`DbCategoryIds`](../../src/Polhem.Definition/Database/DbCategoryIds.cs) 常數）；其他值由什麼機制拒絕見 §3.4
- ⚠️ **執行時只用 `DatabaseItem.Id` 查找連線；`DatabaseItem.CategoryId` 與 `DbCategorySettings` 都不會被讀取**

### 邏輯 vs 實體：對應模型

`DbCategory` 是**純邏輯抽象**，只定義「這個分類包含哪些資料表」；它不對應實體資料庫的數量，也不指定要部署在哪。`DatabaseItem.CategoryId` 與 `DbCategory.Id` 是 **多對一** 關係 —— 同一分類可有多筆 DatabaseItem，常見觸發情境：

- **單一實體載體**（如 `common`）：1 筆 DatabaseItem
- **多租戶切分**（如 `company`）：N 家公司有多筆 DatabaseItem（`company001`、`company002`...），各對應一家公司的實體 DB
- **時間封存切分**（如 `log`）：依年份切分有多筆 DatabaseItem（`log_2024`、`log_2025`...），各對應該年份獨立的實體 DB
- 兩種切分維度可疊加（如某分類同時依公司 + 年份切分）

多筆 DatabaseItem 可指向同一個實體 DB（合併部署），也可各自指向獨立實體 DB（分散或多載體部署）。

四種典型部署狀況（皆以三個邏輯分類 common / company / log 為例）：

**狀況 1：合併部署（單一實體資料庫包含全部三類）**

```text
DatabaseSettings.Items (3 筆)                                     實體資料庫 (1 個)
─────────────────────────────                                     ────────────────
DatabaseItem  Id="common"   CategoryId="common"   DbName=erp ──┐
DatabaseItem  Id="company"  CategoryId="company"  DbName=erp ──┼──► erp（含 common 與 company 表、
DatabaseItem  Id="log"      CategoryId="log"      DbName=erp ──┘     ft_project 等業務表，
                                                                     以及 st_log_* log 表）
```

> `st_log_*` = 框架 opt-in 的 log 表 —— 稽核軌跡（`st_log_login`、`st_log_change`、`st_log_access`）加上執行異常記錄（`st_log_anomaly_api`、`st_log_anomaly_db`）；後續情境圖以「log tables」略稱。「common tables」與「company tables」同理，指登錄在該分類下的框架表。各分類的表列在 [`src/Polhem.Definition/Defaults/DbCategorySettings.xml`](../../src/Polhem.Definition/Defaults/DbCategorySettings.xml)；另見 [框架保留命名 §1](framework-reserved-names.md#1-系統表st_)。

**狀況 2：分散部署（三個實體資料庫，各對應一個邏輯分類）**

```text
DatabaseSettings.Items (3 筆)                       實體資料庫 (3 個)
─────────────────────────────                       ────────────────
DatabaseItem  Id="common"   CategoryId="common"   DbName=erp_common  ──► erp_common  (common tables)
DatabaseItem  Id="company"  CategoryId="company"  DbName=erp_company ──► erp_company (company tables、ft_project)
DatabaseItem  Id="log"      CategoryId="log"      DbName=erp_log     ──► erp_log     (log tables)
```

**狀況 3：多租戶部署（共用分類各 1 筆 + company 分類每家公司 1 筆）**

```text
DatabaseSettings.Items (2 + N 筆)                   實體資料庫 (2 + N 個)
─────────────────────────────                       ────────────────
DatabaseItem  Id="common"      CategoryId="common"   ──► erp_common
DatabaseItem  Id="company001"  CategoryId="company"  ──► company001  (company tables、ft_project)
DatabaseItem  Id="company002"  CategoryId="company"  ──► company002  (company tables、ft_project)
DatabaseItem  Id="company003"  CategoryId="company"  ──► company003  (company tables、ft_project)
   ⋮          (每家公司一筆)
DatabaseItem  Id="log"         CategoryId="log"      ──► erp_log
```

每家公司的 `companyXXX` 實體 DB 表結構完全相同（皆來自 `DbCategory["company"].Tables`），但資料各自獨立。公司使用哪一筆並不是由公司 Id 推導：每家公司在 `st_company` 的那一列，以 `company_database_id` 欄位指定它使用的項目。

**狀況 4：log 按年封存（log 分類每年一筆）**

```text
DatabaseSettings.Items (2 + Y 筆)                   實體資料庫 (2 + Y 個)
─────────────────────────────                       ────────────────
DatabaseItem  Id="common"     CategoryId="common"   ──► erp_common
DatabaseItem  Id="company"    CategoryId="company"  ──► erp_company
DatabaseItem  Id="log_2024"   CategoryId="log"      ──► log_2024  (log tables)
DatabaseItem  Id="log_2025"   CategoryId="log"      ──► log_2025  (log tables)
DatabaseItem  Id="log_2026"   CategoryId="log"      ──► log_2026  (log tables)
   ⋮          (每年新增一筆)
```

每年的 `log_YYYY` 實體 DB 表結構完全相同（皆來自 `DbCategory["log"].Tables`），業務寫入時用當前年份對應的 DatabaseId，查詢可跨多筆 DatabaseItem 聚合。狀況 3 與 4 可疊加（如業務上同時依公司 + 年份切分）。

> ⚠️ 按年的項目服務的是**應用程式自有**、由應用程式自行路由的 log 表（§5.4）。框架自己的 `st_log_*` 讀寫走 `DbScope.Log`，一律解析到 `Id="log"` 那一筆；開啟 `AuditLogOptions` 的部署除了 `log_YYYY` 項目之外，還需要這一筆。

業務程式對部署形態無感：永遠透過 `IDatabaseSettingsProvider.GetItem(databaseId)`（DI ctor 注入）取連線。差異只在業務層怎麼決定要傳哪個 `databaseId`：

- 狀況 1、2：固定對照（分類 → DatabaseId）
- 狀況 3：由 session 的公司查得：`SessionInfo.CompanyId` → `ICompanyInfoService` → `CompanyInfo.CompanyDatabaseId`（§5.4）
- 狀況 4：由應用程式依當前年份為自己的 log 表推導（如 `$"log_{DateTime.UtcNow.Year}"`），跨年查詢需聚合多筆

**狀況可任意組合**：以上四種僅為典型基本模式，實際部署可依成本 / 效能 / 維運考量任意組合。例如多租戶情境下，為避免「log 集中所有租戶」造成效能瓶頸，可改採「**每租戶的 company 與 log 共用同一實體 DB**」：

```text
DatabaseSettings.Items                                實體資料庫
─────────────────────────                             ────────
DatabaseItem  Id="common"           CategoryId="common"   ──► erp_common
DatabaseItem  Id="company001"       CategoryId="company"  ──┐
DatabaseItem  Id="log_company001"   CategoryId="log"      ──┴► company001  (含 ft_* 與 log_* 兩類表)
DatabaseItem  Id="company002"       CategoryId="company"  ──┐
DatabaseItem  Id="log_company002"   CategoryId="log"      ──┴► company002  (含 ft_* 與 log_* 兩類表)
   ⋮          (每家公司 2 筆 DatabaseItem，共用同一實體 DB)
```

每家公司 2 筆 DatabaseItem，分別宣告 company 與 log 分類，但 DbName 指向同一實體 DB —— 實體 DB 內同時包含 ft_* 與 log_* 兩類表。log 資料隨租戶分散，避免跨租戶集中。與狀況 4 相同，這只適用於應用程式自行路由的 log 表：框架的 `st_log_*` 表仍在唯一的 `Id="log"` 那一筆，因為 `CompanyInfo` 沒有各公司專屬的 log 資料庫 Id。

關鍵觀念：**邏輯分類與實體部署是兩個獨立維度，可任意組合**。設計時依資料量、查詢模式、維運成本決定切分策略。框架自己的路由只用 `common`、`log` 兩筆，以及各公司指定的 `company_database_id`（§5.4）；其他項目由應用程式自行路由。

---

## 2. DatabaseSettings

定義位置：[`src/Polhem.Definition/Settings/DatabaseSettings/`](../../src/Polhem.Definition/Settings/DatabaseSettings/)

### 2.1 階層結構

```text
DatabaseSettings
├── Servers : DatabaseServerCollection   共用伺服器組態（連線範本）
│     └── DatabaseServer
└── Items   : DatabaseItemCollection     實際資料庫連線項目
      └── DatabaseItem
```

### 2.2 DatabaseServer 欄位

定義「共用伺服器組態」，多個 DatabaseItem 可引用同一個 Server 共享連線範本與帳密。

| 欄位 | 型別 | 用途 |
|------|------|------|
| `Id` | string | 伺服器識別碼（Key） |
| `DisplayName` | string | 顯示名稱 |
| `DatabaseType` | DatabaseType | `SQLServer` / `PostgreSQL` 等 |
| `ConnectionString` | string | 連線字串範本，可含 `{@DbName}` / `{@UserId}` / `{@Password}` 佔位符 |
| `UserId` | string | 登入 ID，會替換 `{@UserId}` |
| `Password` | string | 登入密碼，會替換 `{@Password}`；存檔時加密（§2.6） |

### 2.3 DatabaseItem 欄位

定義「實體連線項目」，是執行時實際建立連線的單位。

| 欄位 | 型別 | 用途 |
|------|------|------|
| `Id` | string | 連線識別碼（Key），呼叫端用此 Id 取得連線 |
| `CategoryId` | string | 所屬邏輯分類 Id（對應 `DbCategory.Id`） |
| `DisplayName` | string | 顯示名稱 |
| `DatabaseType` | DatabaseType | `SQLServer` / `PostgreSQL` 等（設定 `ServerId` 時忽略，改用 Server 的值） |
| `ServerId` | string | 引用的 DatabaseServer Id（選用） |
| `ConnectionString` | string | 獨立連線字串（設定 `ServerId` 時忽略） |
| `DbName` | string | 資料庫名稱，會替換 `{@DbName}` |
| `UserId` | string | 登入 ID（覆蓋 Server 設定） |
| `Password` | string | 登入密碼（覆蓋 Server 設定）；存檔時加密（§2.6） |

> 📌 **DatabaseItem 與邏輯分類為多對一關係**：`CategoryId` 為單一字串而非集合，每筆 DatabaseItem 只屬於一個分類；但**同一分類可有多筆 DatabaseItem**，常見觸發情境包括多租戶切分（如 company 每家公司一筆）、時間封存切分（如 log 每年一筆）等。詳見 [§1 邏輯 vs 實體：對應模型](#邏輯-vs-實體對應模型)。

### 2.4 Server 與 Item 的選擇

兩種使用模式：

- **引用 Server**：`DatabaseItem.ServerId` 指定 Server，連線字串範本與 `DatabaseType` 來自 Server，Item 只提供 `DbName`、（必要時）覆寫 `UserId` / `Password`。適合多個 Item 共用同一台伺服器、多個 DB 的場景。`ServerId` 指向不存在的 Server 時，載入檔案不會發現；要到第一次為該 Item 建立連線時才失敗
- **獨立設定**：Item 的 `ServerId` 留空，直接在 Item 上指定 `ConnectionString`、`UserId`、`Password`。適合單一連線或連線設定差異大的場景

### 2.5 連線字串範本替換

連線字串中可使用三個佔位符，框架在建立連線時會替換（[`ConnectionStringTemplate.Resolve`](../../src/Polhem.Db/Manager/ConnectionStringTemplate.cs)）：

| 佔位符 | 替換來源 |
|--------|---------|
| `{@DbName}` | `DatabaseItem.DbName` |
| `{@UserId}` | `DatabaseItem.UserId`（為空則 fallback `DatabaseServer.UserId`） |
| `{@Password}` | `DatabaseItem.Password`（為空則 fallback `DatabaseServer.Password`） |

範本會先解析成連線字串，再以 `DbConnectionStringBuilder` 寫回，因此替換進去的值會依連線字串語法加上引號：含 `;` 或 `=` 的密碼仍是單一值，不會提前結束該組鍵值。佔位符比對不分大小寫，也可以出現在值的中間（例如 `Data Source=file:app_{@DbName}.db`）。值為空的佔位符會原樣保留。由於含佔位符時整個字串會被重寫，關鍵字可能變成小寫、值可能加上引號；兩者都是正常的連線字串語法。

範例：
```xml
<DatabaseServer Id="sql_main" DatabaseType="SQLServer"
                ConnectionString="Server=sql.example;Database={@DbName};User ID={@UserId};Password={@Password};" />
<DatabaseItem Id="company_main" CategoryId="company" ServerId="sql_main"
              DbName="erp_company" UserId="erp_user" Password="..." />
```

### 2.6 Password 加密

`DatabaseServer.Password` 與 `DatabaseItem.Password` 在 `DatabaseSettings.xml` 中以加密形式儲存。只有這兩個欄位會加密：檔案其餘內容（包括連線字串範本、`DbName`、`UserId`）都是明文，所以密碼不要寫進 `ConnectionString`，而是透過 `{@Password}` 提供。

- **加密方式**：AES-CBC-HMAC，金鑰為 `SystemSettings` 的 `ConfigEncryptionKey`（位於 `BackendConfiguration.SecurityKeySettings`，本身以主金鑰加密儲存）。`AddPolhemFramework` 將它解密後，經建構子傳給 `CacheDefineAccess`
- **儲存格式**：`enc:` + Base64 編碼的密文
- **時機**：由 `CacheDefineAccess` 呼叫 cryptor，而不是 XML 序列化器：
  - `SaveDatabaseSettings` 在副本上將所有明文 Password 加密（`EncryptInPlace`）並寫出該副本；傳入的執行個體仍保留明文密碼。已經以 `enc:` 開頭的值原樣通過
  - `GetDatabaseSettings` 將 `enc:` 開頭的 Password 解密（`DecryptInPlace`）。解密失敗的值（Base64 錯誤、HMAC 不符）會變成空字串。手動寫進檔案的明文密碼照原樣使用，直到透過 `SaveDatabaseSettings` 存檔前都維持明文
- **沒有金鑰時**：若 `ConfigEncryptionKey` 為空，跳過加解密：密碼以明文儲存，`enc:` 值也不會被解密。只要設定中有任何密碼，`CacheDefineAccess` 會在第一次載入時、以及每次存檔時記錄一筆警告。僅限開發環境使用

實作位置：[`DatabaseSettingsCryptor.cs`](../../src/Polhem.Definition/Settings/DatabaseSettings/DatabaseSettingsCryptor.cs) `EncryptInPlace` / `DecryptInPlace`。

---

## 3. DbCategorySettings

定義位置：[`src/Polhem.Definition/Settings/DbCategorySettings/`](../../src/Polhem.Definition/Settings/DbCategorySettings/)

### 3.1 階層結構

```text
DbCategorySettings
└── Categories : DbCategoryCollection
      └── DbCategory
            └── Tables : TableItemCollection
                  └── TableItem
```

### 3.2 DbCategory 欄位

| 欄位 | 型別 | 用途 |
|------|------|------|
| `Id` | string | 分類識別碼（Key），FormSchema / DatabaseItem 透過此 Id 對應 |
| `DisplayName` | string | 顯示名稱（如「Common database」） |
| `Tables` | TableItemCollection | 該分類下登錄的表清單 |

### 3.3 TableItem 欄位

| 欄位 | 型別 | 用途 |
|------|------|------|
| `TableName` | string | 資料表名稱（Key） |
| `DisplayName` | string | 顯示名稱（如「用戶」） |

`Tables` 子節點是**哪些表屬於該分類的登錄表**。部署步驟讀它來決定要在該分類的資料庫建立哪些表（§5.3）；表單的資料表沒有登錄在表單所屬分類下時，analyzer POLHEM2001 會發出警告（見 [Analyzer 規則](analyzer-rules.md)）。執行時不會讀取它，表結構本身則來自 `TableSchema` 與 `FormSchema` 檔案。

### 3.4 三個分類

框架只認得三個邏輯分類，即 [`DbCategoryIds`](../../src/Polhem.Definition/Database/DbCategoryIds.cs) 的常數：

| 分類 Id | 用途 | 範例 |
|---------|------|------|
| `common` | 共用資料庫 — 跨公司共用的系統表 | `st_user`、`st_session`、`st_company` |
| `company` | 公司資料庫 — 業務資料、各公司獨立 | `st_department`、`st_employee`、`ft_project` 等業務表 |
| `log` | 日誌資料庫 — 寫入頻繁的稽核軌跡與執行異常記錄 | `st_log_login`、`st_log_change`（opt-in）、應用程式的 log 表 |

框架自己的表及其所屬分類列在框架隨附的 [`src/Polhem.Definition/Defaults/DbCategorySettings.xml`](../../src/Polhem.Definition/Defaults/DbCategorySettings.xml)；請以它為起點，不要手寫框架的表。各表的用途見 [框架保留命名](framework-reserved-names.md)。

表單與 `DbCategory` 不接受其他分類 Id（`DatabaseItem.CategoryId` 則沒有任何機制檢查）：

- 建置時，analyzer POLHEM1001 與 POLHEM1002 會對建置看得到的定義檔中，不屬於這三者的 `FormSchema/@CategoryId` 或 `DbCategory/@Id` 回報錯誤（見 [Analyzer 規則 § 定義檔規則從哪裡讀取](analyzer-rules.md#定義檔規則從哪裡讀取)）。
- 執行時，為 `CategoryId` 是其他值的表單建立 Repository 會拋出 `InvalidOperationException`（「Unknown schema.CategoryId」）。

自訂的表，包括應用程式自己的 log 表，都登錄在這三個分類之一。

**`common` 為框架契約**：框架的系統服務（session、使用者、公司、API 金鑰、快取通知）連線到固定的 `databaseId = "common"`，所以部署需要一筆 `Id="common"` 的 `DatabaseItem`，慣例上它的 `CategoryId` 也是 `common`。啟動時沒有任何檢查：缺少這一筆時，要到其中某個服務第一次連線才失敗（查找會拋出 `KeyNotFoundException`）。想提早失敗的 host 可以在 service provider 建好後呼叫 `IDatabaseSettingsProvider.ValidateRequired()`；沒有 `common` 項目時它會拋出例外。

`company` 與 `log` 是框架的另外兩個分類。框架在 `log` 提供 opt-in 的 `st_log_*` 表（由 `AuditLogOptions` 控制、預設關閉），並透過 `Id="log"` 那一筆存取（§5.4）。單租戶只有在稽核與異常記錄都停用時，才可以不設 `log` 項目。

---

## 4. 存取入口與快取

### 4.1 統一入口

兩個 settings 都透過 `IDefineAccess`（DI ctor 注入）存取：

```csharp
public class MyService(IDefineAccess defineAccess)
{
    public void Demo()
    {
        // 讀取
        DatabaseSettings dbSettings = defineAccess.GetDatabaseSettings();
        DbCategorySettings catSettings = defineAccess.GetDbCategorySettings();

        // 寫入
        defineAccess.SaveDatabaseSettings(dbSettings);
        defineAccess.SaveDbCategorySettings(catSettings);
    }
}
```

`IDefineAccess` 由 `AddPolhemFramework` 註冊為 singleton，預設 `CacheDefineAccess`，其讀取經由 `IDefineStorage`。兩者皆可透過 XML `Components` 設定替換——把定義移出檔案系統的支援做法是改用資料庫版的 `IDefineStorage`。`DbCategorySettings` 跟著 storage 走；`DatabaseSettings` 不會：無論使用哪種 storage，`CacheDefineAccess` 都從 `<DefinePath>/DatabaseSettings.xml` 讀寫它（§6.1），因為資料庫版的 storage 本身就需要它才能連線。client 端不實作 `IDefineAccess`，而是透過 `ClientDefineAccess` 經 API 取得定義。

### 4.2 快取機制

兩個 settings 由 DI 註冊的 [`ICacheContainer`](../../src/Polhem.ObjectCaching/ICacheContainer.cs)（預設實作 `CacheContainerService`）集中持有；持有者即快取物件本身，載入為 cache-miss 時 per-key 惰性載入（底層 `ObjectCache<T>` 於某個 key 首次被要求時呼叫 `CreateInstance()`）：

| 快取 | 持有者 |
|------|--------|
| `DatabaseSettings` | `ICacheContainer.DatabaseSettings`（`DatabaseSettingsCache`） |
| `DbCategorySettings` | `ICacheContainer.DbCategorySettings`（`DbCategorySettingsCache`） |

行為：
- **20 分鐘 sliding expiration**：未存取超過 20 分鐘則重新載入
- **讀取時偵測變更**：讀取時會比對來源與載入時的狀態，有變更就重新載入。檔案來源比對的是檔案最後寫入時間，每筆項目每秒最多檢查一次；資料庫版 storage 中的 `DbCategorySettings` 比對的是該項目的 cache-notify 版本。因此在行程外做的修改，是由其後的下一次讀取發現，而不是主動推送
- **存檔即失效**：呼叫 `Save*` 後立即清除對應快取，下次 `Get*` 重新載入

### 4.3 常用查找運算

```csharp
// 取單一連線項目（DI 注入 IDatabaseSettingsProvider）
DatabaseItem item = dbSettingsProvider.GetItem("company_main");

// 取分類下所有表（透過索引器）
DbCategory company = catSettings.Categories!["company"];
foreach (var table in company.Tables!) { ... }
```

`IDatabaseSettingsProvider.GetItem` 在找不到 Id 時拋 `KeyNotFoundException`，呼叫端可依此判斷未知連線。

### 4.4 API 存取限制

| Settings | 可從 API 遠端存取？ |
|----------|-------------------|
| DatabaseSettings | ❌ 否。local call 取得的是檔案原本儲存的內容，密碼仍是 `enc:` 形式 |
| DbCategorySettings | ❌ 否 |

`SystemBusinessObject.GetDefine` 對遠端呼叫者只提供 client 繪製表單與選單所需的定義型別，其他型別一律拒絕；這兩個設定檔都不在清單內。local call（行程內的 `LocalApiProvider`，例如工具程式）可以讀取，`ClientDefineAccess.GetDbCategorySettingsAsync` 就是這樣運作。存檔經由 local-only 的 `SaveDefine`。允許清單本身寫在 `SystemBusinessObject.GetDefine` 的 XML 文件中。

---

## 5. CategoryId 串接

`CategoryId` 是這套設計的**核心關聯鍵**，貫穿三層：

### 5.1 FormSchema 定義階段

每個 FormSchema 必須宣告所屬分類：

```xml
<FormSchema ProgId="Project" CategoryId="company" ...>
  <FormTable TableName="Project" DbTableName="ft_project" ...>
    ...
  </FormTable>
</FormSchema>
```

落檔時由 [`CacheDefineAccess.SaveFormSchema`](../../src/Polhem.ObjectCaching/CacheDefineAccess.cs) 強制檢查 `CategoryId` 非空（透過 [`TableSchemaGenerator.GetCategoryId`](../../src/Polhem.Definition/Database/TableSchemaGenerator.cs)），否則拋 `InvalidOperationException`。

### 5.2 TableSchema 落檔路徑

FormSchema 衍生的 TableSchema 依 CategoryId 分目錄存放：

```text
<DefinePath>/TableSchema/
              ├── common/
              │     ├── st_user.TableSchema.xml
              │     └── ...
              ├── company/
              │     ├── st_employee.TableSchema.xml
              │     ├── ft_project.TableSchema.xml
              │     └── ...
              └── log/
                    ├── st_log_login.TableSchema.xml
                    └── ...
```

框架自己的 TableSchema 檔以相同結構隨附於 [`src/Polhem.Definition/Defaults/TableSchema/`](../../src/Polhem.Definition/Defaults/TableSchema/)。

路徑解析：[`PathOptions.GetTableSchemaFilePath(categoryId, tableName)`](../../src/Polhem.Definition/PathOptions.cs)（DI ctor 注入）。

### 5.3 部署階段：實體 DB 的表清單推導

`DatabaseItem.CategoryId` 在 schema 部署階段（建立或升級實體資料庫表結構時）使用。框架提供各個零件（分類登錄表、TableSchema 檔，以及 [資料庫 Schema 升級](database-schema-upgrade.md) 說明的 schema 比對與升級 API），但沒有逐筆走訪 DatabaseItem 的執行器：這一步屬於 host，例如 Northwind 示範的 schema seeder（`apps/Polhem.Northwind/Polhem.Northwind.Server/NorthwindSchemaSeeder.cs`）。**運算單位是 DatabaseItem**：對每筆 DatabaseItem 各自跑一次推導與建表流程，建到該 DatabaseItem 連線指向的實體 DB 上。

對單筆 DatabaseItem 的推導流程：

1. 讀 `DatabaseItem.CategoryId`（如 `"company"`）
2. 從 `DbCategorySettings.Categories["company"].Tables` 取得該分類下登錄的表清單
3. 對每個 `TableName`，從 `<DefinePath>/TableSchema/company/{TableName}.TableSchema.xml` 取得實體表結構
4. 透過該 DatabaseItem 的連線資訊在實體 DB 上執行 DDL（建表或 schema 升級）

```text
[DatabaseItem]            [DbCategorySettings]              [TableSchema 檔案]
 └─ CategoryId ─────────► DbCategory[id]                    └─ TableSchema/{cid}/{table}.xml
   ("company")             └─ Tables (該分類應有的表清單)        └─ 提供實體表結構
```

由於對每筆 DatabaseItem 獨立推導，兩種部署狀況（單一實體 DB / 多個實體 DB，見 §1）的處理流程完全相同：

- **狀況 1**（3 筆 DatabaseItem 都指向同一個實體 DB）：跑 3 次推導，3 次都連到同一個實體 DB，建出 common / company / log 三類表共存於該 DB
- **狀況 2**（3 筆 DatabaseItem 指向 3 個獨立實體 DB）：跑 3 次推導，分別建到 3 個實體 DB，每個實體 DB 只有對應分類的表

此設計讓 schema 定義（哪些表、表的結構）與實體部署（要不要切分、切幾個 DB）完全解耦。新增一個邏輯分類只需動 `DbCategorySettings.xml` 與對應 FormSchema；改變實體部署切分只需動 `DatabaseSettings.xml` 中各 DatabaseItem 的連線資訊，定義檔不必動。

### 5.4 執行時：資料庫存取

⚠️ **執行時取得連線完全透過 `DatabaseItem.Id`，與 `DbCategorySettings` 完全無關**：

```csharp
DatabaseItem item = dbSettingsProvider.GetItem(databaseId);
// 用 item.ConnectionString / DbName / UserId / Password 建立連線、執行 SQL
```

業務程式存取資料時依「資料屬於哪個邏輯分類」 + 「當前情境（租戶、時間等）」決定要傳哪個 `databaseId`：

| 要存取的資料 | 屬於分類 | 部署情境 | 使用的 DatabaseId |
|------------|---------|---------|------------------|
| common 表（`st_user`、`st_session`…） | common | 任何 | 固定為字串 `"common"`（框架契約：`DatabaseItem.Id == "common"`） |
| company 表（`st_employee`、業務表…） | company | 單一公司 | 該公司 `st_company` 列的 `company_database_id`，通常就是 `CategoryId=company` 的那一筆 |
| company 表 | company | 多租戶 | session 所在公司的 `company_database_id`（`SessionInfo.CompanyId` → `CompanyInfo.CompanyDatabaseId`） |
| 框架 log 表（`st_log_*`） | log | 任何 | 固定為字串 `"log"` |
| 應用程式日誌寫入 | log | 按年封存 | 由應用程式選擇，例如當前年份的 `log_YYYY`（`$"log_{DateTime.UtcNow.Year}"`） |
| 應用程式日誌跨年查詢 | log | 按年封存 | 依查詢年份範圍取多筆 DatabaseId，分別查詢後聚合 |

無論底層是哪種狀況，業務端程式碼都是同一個入口 `IDatabaseSettingsProvider.GetItem(databaseId)`，差異只在「依當前情境推導 databaseId 字串」這一步。

對於 bo repo（BO 層消費的 Repository），框架透過 `IRepositoryDatabaseRouter`（見 [ADR-010 §「後續延伸：執行時路由」](../adr/adr-010-logical-database-category.zh-TW.md)）統一推導，BO 程式碼不需手寫：

| 來源 | databaseId 推導方式 |
|------|---------------------|
| `DbScope.Common` | 固定字串 `"common"`（不需 session） |
| `DbScope.Log` | 固定字串 `"log"`（不需 session；Login / Logout 等 pre-EnterCompany 方法也能寫 audit log） |
| `DbScope.Company` | `SessionInfo.CompanyId`（由 `EnterCompany` 寫入）→ `CompanyInfo.CompanyDatabaseId`（由 `ICompanyInfoService` 取得） |

BO 方法透過 `BusinessObject.ResolveDatabaseId(DbScope)` 或（FormSchema-driven CRUD 用）`CreateDataFormRepository(progId)` helper 消費 router；後者依 schema 的 `CategoryId` 自動路由。

跨 DatabaseItem 聚合（如 log 跨年查詢）與封存資料的明確存取（如查 `log_2024`）不在預設路由範圍內——業務層直接指定目標 databaseId、透過 `IDbAccessFactory` 建自訂 bo repo。

`DatabaseItem.CategoryId` 與 `DbCategorySettings` 只在前述的設計階段（5.1–5.2）與部署階段（5.3）使用。執行時 BO 方法用 `DbScope`（執行時存取意圖）表達，不再操作 CategoryId 字串；執行時唯一讀取 CategoryId 字串的地方，是表單 Repository 將其 schema 的 `CategoryId` 轉成 `DbScope`（§3.4）。

---

## 6. 檔案位置與範例

### 6.1 檔案路徑

兩個設定檔皆位於 `PathOptions.DefinePath` 根目錄：

| 設定 | 檔案路徑 | 路徑解析 |
|------|---------|---------|
| DatabaseSettings | `<DefinePath>/DatabaseSettings.xml` | `PathOptions.GetDatabaseSettingsFilePath()` |
| DbCategorySettings | `<DefinePath>/DbCategorySettings.xml` | `PathOptions.GetDbCategorySettingsFilePath()` |

### 6.2 DatabaseSettings.xml 範例

```xml
<?xml version="1.0" encoding="utf-8"?>
<DatabaseSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                  xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Servers>
    <DatabaseServer Id="sql_main" DisplayName="主要 SQL Server"
                    DatabaseType="SQLServer"
                    ConnectionString="Server=sql.example;Database={@DbName};User ID={@UserId};Password={@Password};" />
  </Servers>
  <Items>
    <!-- common 分類：單一實體載體，Id 與 CategoryId 同名 -->
    <DatabaseItem Id="common" CategoryId="common" DisplayName="共用資料庫"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="erp_common" UserId="erp_user"
                  Password="enc:base64encodeddata..." />

    <!-- company 分類：多租戶切分，每家公司一筆 -->
    <DatabaseItem Id="company001" CategoryId="company" DisplayName="01 公司資料庫"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="company001" UserId="erp_user"
                  Password="enc:base64encodeddata..." />
    <DatabaseItem Id="company002" CategoryId="company" DisplayName="02 公司資料庫"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="company002" UserId="erp_user"
                  Password="enc:base64encodeddata..." />

    <!-- log 分類：框架的 st_log_* 表經由 Id="log" 寫入 -->
    <DatabaseItem Id="log" CategoryId="log" DisplayName="記錄資料庫"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="erp_log" UserId="erp_user"
                  Password="enc:base64encodeddata..." />

    <!-- log 分類：選用的按年項目，給應用程式自有的 log 表使用 -->
    <DatabaseItem Id="log2026" CategoryId="log" DisplayName="2026 應用程式記錄資料庫"
                  DatabaseType="SQLServer" ServerId="sql_main"
                  DbName="log2026" UserId="erp_user"
                  Password="enc:base64encodeddata..." />
  </Items>
</DatabaseSettings>
```

### 6.3 DbCategorySettings.xml

以框架隨附的 [`src/Polhem.Definition/Defaults/DbCategorySettings.xml`](../../src/Polhem.Definition/Defaults/DbCategorySettings.xml) 為起點，它已將每張框架表登錄在所屬分類下，再加上你自己的表。結構如下，框架的項目以 `...` 省略：

```xml
<?xml version="1.0" encoding="utf-8"?>
<DbCategorySettings xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Categories>
    <DbCategory Id="common" DisplayName="Common database">
      <Tables>
        <TableItem TableName="st_user" DisplayName="User" />
        ...
      </Tables>
    </DbCategory>
    <DbCategory Id="company" DisplayName="Company database">
      <Tables>
        <TableItem TableName="st_department" DisplayName="Department" />
        ...
        <!-- 你的業務表 -->
        <TableItem TableName="ft_project" DisplayName="Project" />
      </Tables>
    </DbCategory>
    <DbCategory Id="log" DisplayName="Log database">
      <Tables>
        <TableItem TableName="st_log_login" DisplayName="Login log" />
        ...
      </Tables>
    </DbCategory>
  </Categories>
</DbCategorySettings>
```

---

## 相關文件

- [架構總覽](architecture-overview.md) — Definition-Driven 架構全貌
- [開發指引](development-cookbook.md) — 框架初始化順序與開發流程
- [資料庫命名規範](database-naming-conventions.md) — 表名 / 欄位命名規則
- [ADR-005：FormSchema 定義驅動架構](../adr/adr-005-formschema-driven.zh-TW.md)
- [ADR-010：邏輯資料庫分類設計](../adr/adr-010-logical-database-category.zh-TW.md) — 為何引入 DbCategory 抽象層
