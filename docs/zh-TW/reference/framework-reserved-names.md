<!-- source: en/reference/framework-reserved-names.md blob: 22cf9a001dd19b2bf883e47f45d38fd5dba987aa -->
# 框架保留命名

[English](../../en/reference/framework-reserved-names.md) · [← 文件索引](../README.md)

> 列出 **polhem** 框架所擁有的命名：哪些 `st_*` 系統表存在、框架保留了哪些 `progId`。
>
> - 命名**規則**（`st_` vs `ft_` 前綴、欄位／索引慣例）見 [資料庫命名規範](../database/database-naming-conventions.md)。
> - 完整 **API 方法參考**（action 簽章、`[ApiAccessControl]`、用途）見 [API 方法參考](../api/api-method-reference.md)。
> - 本文件列出「**哪些具體名稱**被框架保留」。權威來源是 [`src/Polhem.Definition/Defaults/TableSchema/`](../../../src/Polhem.Definition/Defaults/TableSchema/) 下的資料表定義與 `Polhem.Business.ReservedProgIds`。擴充或整合框架時，請勿與此處列出的任何名稱衝突。

---

## 1. 系統表（`st_*`）

`st_` 前綴語意為「框架所有的表」，**與該表所在的資料庫位置正交**：`st_*` 表可以位於 common 資料庫、per-company 資料庫，也可以位於 log 資料庫——重點是「誰擁有」而不是「住哪裡」。

**這些表是必備的執行階段設施，不是可選的附加物。** 框架在服務一般請求的過程中就會讀寫它們，因此部署端一律要建，不論自己用不用得到。**覆寫框架行為不等於豁免其背後的資料表**：自訂認證（`AuthenticateUser`）取代的只有「帳號密碼的驗證」這一段——登入時仍要從 `st_user` 取該使用者的時區與語系，並把 session 種子寫進 `st_session`。缺表時的徵狀是「第一個碰到它的操作」丟出資料庫錯誤，而不是一則明確的設定診斷訊息。

### 1.1 Common 資料庫（全域共用）

| 表名 | 用途 |
|------|------|
| `st_user` | 全域使用者主檔（登入帳號、密碼雜湊、個資）。 |
| `st_company` | 公司清單（per-tenant 根目錄）。 |
| `st_user_company` | 哪些使用者可進入哪些公司。 |
| `st_session` | Session 種子，以 access token 的雜湊為鍵（token 本身不存放）。 |
| `st_api_key` | 已發放的 API 金鑰（`X-Api-Key`）：應用識別，以雜湊存放。 |
| `st_define` | DB-backed 定義儲存（FormSchema / TableSchema 等，非 XML 檔案版本）。 |
| `st_cache_notify` | 跨節點 cache 失效通道（[ADR-017](../../../maintainers/adr/adr-017-db-cache-invalidation.md)）。 |

### 1.2 公司資料庫（per-tenant）

| 表名 | 用途 |
|------|------|
| `st_role` | 角色定義（[ADR-019](../../../maintainers/adr/adr-019-permission-authorization-model.md)）。 |
| `st_role_grant` | 角色↔資源授權（per model / action）。 |
| `st_user_role` | 使用者↔角色綁定。 |
| `st_department` | 組織部門。 |
| `st_employee` | 員工（連結 common DB 的 `st_user` 至 per-company 的組織位置）。 |
| `st_audit_rule` | per-form 稽核規則（哪些表單要記異動／檢視，見 [ADR-027](../../../maintainers/adr/adr-027-audit-trail.md)）。 |

> `st_department` / `st_employee` 雖位於公司資料庫，但仍是 **框架所有**（record-scope 與組織樹功能所需），不是業務資料。Per-company 業務表請使用 `ft_` 前綴。

### 1.3 Log 資料庫（資料軌跡與異常）

| 表名 | 用途 |
|------|------|
| `st_log_login` | 登入事件（種類即 `LoginEvent` 的成員）。 |
| `st_log_change` | 異動記錄——一次 Save / Delete 一列。Save 的 `changes_xml` 承載異動列的 DataSet DiffGram 新舊值；Delete 則存刪除前的完整原單。 |
| `st_log_access` | 檢視記錄（誰看了哪筆記錄）。 |
| `st_log_anomaly_api` | API 層異常——哪個動作偏離正常（種類為 `AnomalyKind` 中標示 `(API)` 與共用的成員）。 |
| `st_log_anomaly_db` | DB 層異常——哪個資料庫 + 指令偏離正常（`AnomalyKind` 中標示 `(DB)` 與共用的成員）。 |

> 這些表是兩件不同的事共用一個資料庫。`st_log_login`、`st_log_change` 與 `st_log_access` 是**稽核軌跡** —— 誰對哪一筆做了什麼，經 `IAuditLogWriter` 寫入。兩張 `st_log_anomaly_*` 表是**執行異常** —— 哪一次執行偏離了正常，經 `IAnomalyLogWriter` 寫入；它們是維運訊號而非業務紀錄，所以 `st_log_anomaly_db` 連觸發者都沒有。見 [ADR-040](../../../maintainers/adr/adr-040-audit-trail-taxonomy.md)。
>
> Log 表**預設關閉（opt-in，`AuditLogOptions.Enabled` 預設為關）**且自足：去正規化觸發者的 user / company，查詢不需跨資料庫 join（log 資料庫可能實體分離）。與 session 相關的列存放 token 指紋（`token_fingerprint`），絕不存 access token 本身。目前一律寫入固定的 `log` 資料庫 id；依年份拆分 log 資料庫是 [ADR-027](../../../maintainers/adr/adr-027-audit-trail.md) 記錄的方向，並非現有功能。

---

## 2. 保留 `progId`

### 2.1 系統軸

- **`System`**（formalize 為 `Polhem.Definition.SysProgIds.System`）—— 系統層級 business object（`SystemBusinessObject`）的單例 entry point。所有與特定 form 無關的框架層級 action（登入、ping、取定義等）皆由此 dispatch。
- **`AuditLog`**（formalize 為 `Polhem.Definition.SysProgIds.AuditLog`）—— 稽核日誌 business object（`AuditLogBusinessObject`）的單例 entry point：對 `st_log_*` 表的唯讀查詢。同時作為 gate 稽核讀取的權限模型 id。

這兩者連同下方的 `AuditRule` 表單（`SysProgIds.AuditRule`），就是 `Polhem.Business.ReservedProgIds` 的保留 progId：host 啟動時若 `ProgramSettings.xml` 缺少它們會自行補上，且若某一個被綁到框架預期基底以外的型別，host 會拒絕啟動。見[定義檔全景 §4](../definitions/definition-files-overview.md#4-programsettings-是型別註冊表)。

完整 action 清單見 [API 方法參考 §軸：System](../api/api-method-reference.md)。

### 2.2 框架預設 form

框架隨組織／record-scope 功能 ship 出下列預設 form：

| progId | 對應表 | 用途 |
|--------|-------|------|
| `Department` | `st_department` | 部門維護表單。 |
| `Employee` | `st_employee` | 員工維護表單。 |
| `AuditRule` | `st_audit_rule` | 稽核規則維護表單。**唯一宣告 `PermissionModelId` 的框架預設 form** —— 稽核政策是特權操作，enforcement 為 fail-closed，需先授權才能使用。 |

每個 form progId 共通繼承的 FormBO action 清單見 [API 方法參考 §軸：Form](../api/api-method-reference.md)。

---

## 3. 消費者命名守則

擴充 polhem 或在其上建立應用時：

- **自家業務表用 `ft_` 前綴**，不要用 `st_`（保留給框架）。詳見 [資料庫命名規範](../database/database-naming-conventions.md)。
- **避開保留的 `progId`**——`System`、`AuditLog`、`AuditRule`、`Department`、`Employee` 已被框架使用。自家 progId 請取不同名稱；慣例為 `PascalCase`，常以模組縮寫前綴。
- **要擴充框架表**（例如為 `st_employee` 加自訂欄位）：在應用程式的 `DefinePath` 中放一份同名 `.TableSchema.xml`。runtime 框架只讀 `DefinePath`，這份檔就是框架實際看到的唯一來源。框架內 embedded 預設 runtime 不會參與——只供下方 API 一次性匯出使用。
- **取得 base XML 起手**——三種途徑，按一般偏好排序：
    - **程式碼層 API**（canonical）：`Polhem.Definition.Defaults.MaterializeTo("./Define")` 把所有 embedded 框架預設 XML 寫入指定目錄。預設 skip-existing，重複跑安全、不會覆蓋你的客製。詳見 [`src/Polhem.Definition/Defaults.cs`](../../../src/Polhem.Definition/Defaults.cs) 中的 `Polhem.Definition.Defaults`。
    - **CLI**（CI / setup 腳本首選）：一次性安裝 `dotnet tool install -g Polhem.Cli`（之後升版用 `dotnet tool update -g Polhem.Cli`），後續 `dotnet polhem defines materialize --path ./Define`——同一份 API 的 thin shell。`dotnet polhem defines list` 列出所有 embedded 檔、`dotnet polhem defines materialize --filter TableSchema/` 只 materialize 子集。
    - **GitHub 瀏覽**：所有 embedded 預設都活在 repo 的 [`src/Polhem.Definition/Defaults/`](../../../src/Polhem.Definition/Defaults/)——打開你要的檔，內容複製到自家 `DefinePath`。
- **框架升版若異動 `st_*` 表結構**，會在 [CHANGELOG](../../../CHANGELOG.zh-TW.md) 中標示為 breaking change。改名類異動需手動執行 `RENAME TABLE`——範例見 [資料表結構升級指南 §框架表改名](../database/database-schema-upgrade.md)。

---

## 延伸閱讀

- [資料庫命名規範](../database/database-naming-conventions.md)——`st_` / `ft_` 區分背後的命名規則。
- [API 方法參考](../api/api-method-reference.md)——完整 BO 方法目錄。
- [架構總覽](../architecture/architecture-overview.md)——`st_*` 表在整體 N-tier + clean architecture 中的位置。
- [ADR-019：權限授權模型](../../../maintainers/adr/adr-019-permission-authorization-model.md)——為何 `st_role` / `st_user_role` / `st_employee` 是框架所有。
