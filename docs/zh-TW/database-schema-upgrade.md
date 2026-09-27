<!-- source: en/database-schema-upgrade.md blob: 5dcf6ce7af86f4f0bd9f5a5a0c530f04e5388e0d -->
# 資料庫 Schema 升級指引

[English](../en/database-schema-upgrade.md) · [← 文件索引](README.md)

> 本文件說明 Polhem 應用如何維護資料庫資料表結構：定義變更後如何同步到實際資料庫、底層採用何種升級策略、以及維運上的注意事項。
> 命名規範請參閱 [資料庫命名規範](database-naming-conventions.md)，定義驅動的整體理念請參閱 [ADR-005 FormSchema-Driven](../adr/adr-005-formschema-driven.zh-TW.md)。

## 1. 核心觀念

Polhem 採 **define-driven schema**：資料表結構由 FormSchema / TableSchema 的 XML 定義為唯一來源。host 呼叫升級 API 時（程式啟動或維運時；框架不會自行執行），框架比對 define 與實際 DB，產生並執行所需的升級指令。

開發者寫程式時只需要：

1. 修改定義檔（增欄位、改長度、加索引）
2. 呼叫升級 API
3. 框架負責決定要走 ALTER 還是 rebuild

### 成功契約

> **升級成功返回後，DB 至少包含 define 裡所有欄位，且型別／長度／nullable 與 define 一致**——方言無法表達定義的地方除外，例如 Oracle 的文字欄一律為 nullable（[資料庫方言差異 §3.1](database-dialect-differences.md#31-oracle-就是-null)）。

這是從應用程式視角唯一重要的事。內部走 `ALTER TABLE` 或整表 rebuild 是 library 的選擇，呼叫端不需要關心。

## 2. 入口 API

### 2.1 一般用法：`IDatabaseRepository.UpgradeTableSchema`

最簡單的呼叫方式，適用於大多數場景：

```csharp
// 透過 DI 解析：在使用端 class ctor 注入 IRepositoryFactory，
// 需要時透過 factory 建立 repository
public class MyService(IRepositoryFactory repoFactory)
{
    public bool UpgradeEmployeeTable()
    {
        var repo = repoFactory.Create<IDatabaseRepository>();
        // 參數順序：databaseId（實體連線識別）、categoryId（邏輯分類）、tableName
        return repo.UpgradeTableSchema("myDb", "company", "st_employee");
    }
}
```

回傳值代表「是否實際執行了升級」：`false` 表示 DB 與 define 已一致，無變更。

> 介面定義在 [IDatabaseRepository](../../src/Polhem.Repository.Abstractions/System/IDatabaseRepository.cs)，預設實作於 [DatabaseRepository](../../src/Polhem.Repository/System/DatabaseRepository.cs)。

### 2.2 進階用法：`TableSchemaBuilder`

需要更細的控制（dry-run、`UpgradeOptions`、結構化 diff）時，直接使用底層的 [TableSchemaBuilder](../../src/Polhem.Db/Schema/TableSchemaBuilder.cs)：

```csharp
// 透過 DI 取得 defineAccess 與 connectionManager（例如在 BO / Service ctor 注入）
var builder = new TableSchemaBuilder("myDb", defineAccess, connectionManager);

// 取得結構化 diff（不執行）；後續方法的第一參數為 categoryId
TableSchemaDiff diff = builder.CompareToDiff("company", "st_employee");

// 取得即將執行的 SQL（不執行）
string sql = builder.GetCommandText("company", "st_employee");

// 執行升級（可選傳 UpgradeOptions）
bool upgraded = builder.Execute("company", "st_employee", new UpgradeOptions
{
    AllowColumnNarrowing = true,
});
```

何時要降到這層：

- 部署前需要先看 SQL（dry-run）
- 想開啟「允許欄位縮小」等特殊選項
- 維運工具需要列出所有將變動的欄位 / 索引

## 3. 升級流程：Diff → Plan → Execute

底層拆成三個階段，每一階段都可以單獨呼叫：

```
┌─────────────────────────────┐
│ 1. CompareToDiff            │  比對 define vs 實際 DB
│    → TableSchemaDiff        │  純結構化、不含 SQL
└─────────────────────────────┘
              ↓
┌─────────────────────────────┐
│ 2. Orchestrator.Plan(diff)  │  決定走 ALTER 還是 rebuild
│    → UpgradePlan            │  含分階段 SQL 與警告
└─────────────────────────────┘
              ↓
┌─────────────────────────────┐
│ 3. Orchestrator.Execute     │  實際對 DB 執行
└─────────────────────────────┘
```

### TableSchemaDiff（結構化變更）

[TableSchemaDiff](../../src/Polhem.Db/Schema/TableSchemaDiff.cs) 是 provider 無關的中介結果，列出每一筆 `ITableChange`：

| Change 型別 | 對應變更 |
|-------------|----------|
| `AddFieldChange` | 新增欄位 |
| `AlterFieldChange` | 既有欄位定義變更（型別、長度、nullable、default） |
| `RenameFieldChange` | 欄位改名（需設定 `DbField.OriginalFieldName`） |
| `AddIndexChange` | 新增索引 |
| `DropIndexChange` | 刪除索引 |

另含 `DescriptionChanges`（表／欄位描述差異）。description 同步在 SQL Server、PostgreSQL、MySQL、Oracle 皆會執行；SQLite 無法儲存描述，因此比對器在該方言下根本不回報描述差異（見 §7 Stage 5）。

### UpgradePlan（執行計畫）

[UpgradePlan](../../src/Polhem.Db/Schema/UpgradePlan.cs) 含 `Mode`（`NoChange` / `Create` / `Alter` / `Rebuild`）、`Stages`（分階段 SQL）與 `Warnings`。可直接列印 SQL：

```csharp
var diff = builder.CompareToDiff("company", "st_employee");
var plan = new TableUpgradeOrchestrator("myDb", connectionManager).Plan(diff);

Console.WriteLine($"Mode: {plan.Mode}");
foreach (var sql in plan.AllStatements)
    Console.WriteLine(sql);
```

## 4. ALTER vs Rebuild：何時走哪條路

### 走 ALTER 的變更（秒級完成）

下列變更都能用 `ALTER TABLE` 在秒級完成，不搬資料：

- 新增欄位
- 同 family 內型別變化（如 `String(50) → String(100)`、`Integer → Long`）
- 改 nullable、改 default
- 新增 / 刪除索引
- 縮小長度（需要 `AllowColumnNarrowing=true`）

### 觸發 Rebuild 的情境

所有方言都從下表這套方言無關的規則出發；SQLite 與 Oracle 另有各自的限制。

| 類型 | 範例 |
|------|------|
| **欄位型別跨 family 變更** | `String → Integer`、數值 → `Date`、`Boolean → 任何其他`、`Binary → 非 Binary`、`Guid ↔ String` |
| **AutoIncrement 狀態切換** | 一般欄位 ↔ AutoIncrement 欄位（例如 SQL Server `ALTER COLUMN` 無法改 IDENTITY 屬性） |

> **SQLite** 是極端案例：其 `ALTER TABLE` 僅支援 `ADD` / `RENAME` / `DROP COLUMN`，因此**任何**欄位型別、nullability 或 default 的變更都需重建。
>
> **Oracle** 在文字欄跨越 LOB 邊界（`VARCHAR2` ↔ `CLOB`）時也會重建，因為它的 `ALTER ... MODIFY` 不接受這種變更。

**Rebuild 機制**：建臨時表 → `INSERT INTO tmp SELECT FROM original` → drop 舊表 → rename。大資料表（千萬筆）耗時可達數十分鐘到小時，期間表級鎖定。

### 自動判斷，不需開發者選

Orchestrator 會逐一檢查 `TableSchemaDiff` 內每一筆 change：

- 全部都能走 ALTER → ALTER 路徑
- 任一筆需要 rebuild → 整表走 rebuild
- 任一筆 provider 不支援 → 直接拋例外中止
- rebuild 與欄位改名（§6）同時出現 → 拋例外；請拆成兩次部署

> 設計上**刻意不暴露 Strategy option**：使用者不該需要決定「我這次要走 ALTER 還是 rebuild」，這由 schema 變更內容唯一決定。

## 5. UpgradeOptions

各選項如 [UpgradeOptions](../../src/Polhem.Db/Schema/UpgradeOptions.cs) 所宣告（註解經簡化）：

```csharp
public sealed class UpgradeOptions
{
    /// <summary>
    /// 允許縮小欄位長度／精度的 ALTER COLUMN（可能截斷資料）。
    /// 預設 false：拒絕縮小，避免靜默資料遺失。
    /// </summary>
    public bool AllowColumnNarrowing { get; init; } = false;

    /// <summary>所有選項皆為預設值的共用執行個體。</summary>
    public static UpgradeOptions Default { get; } = new UpgradeOptions();
}
```

屬性為 `init`-only，所以選項只能在建立執行個體時設定（`new UpgradeOptions { ... }`），共用的 `Default` 無法被修改。

### `AllowColumnNarrowing` 的意義

當 define 的欄位長度／精度小於 DB 現況時：

- 預設：**直接拒絕並拋例外**（`InvalidOperationException`），避免靜默資料截斷
- 開啟：明確同意截斷，並在 plan 的 `Warnings` 中記錄

這項檢查屬於 ALTER 路徑。會重建資料表的 plan（SQLite 上的任何欄位變更，或含有需要重建之變更的 plan）不會參考這個選項：搬資料步驟會把舊值寫進新欄位，放不下的值如何處理由資料庫決定。重建同時會縮小欄位時，務必先 dry-run（§9）。

```csharp
var options = new UpgradeOptions { AllowColumnNarrowing = true };
builder.Execute("company", "st_employee", options);
```

> **何時該開啟**：你已經確認 DB 現有資料在新長度內（可預先 `SELECT MAX(LEN(col))` 驗證），或欄位剛建立還沒有資料。**何時不該開啟**：對線上業務表的縮減，先做資料修整再升級 schema。

## 6. 欄位改名（`DbField.OriginalFieldName`）

預設情況下，Polhem 不會自動把「DB 有但 define 沒有的欄位」當成改名 — 那會被忽略（保留為 extension 欄位，不刪除）。如果要做改名，必須在 define 端**明確標示舊名**：

```xml
<DbField FieldName="employee_no" OriginalFieldName="emp_no" Caption="員工編號" />
```

升級時 comparer 會偵測到 `emp_no` → `employee_no` 的改名意圖，產出 `RenameFieldChange`，以該方言的欄位改名語法執行（SQL Server 為 `sp_rename`，其他方言為 `ALTER TABLE ... RENAME COLUMN`），資料會保留。

### 使用規則

| 情境 | 行為 |
|------|------|
| DB 有舊名 `emp_no`、無新名 | 將欄位改名 |
| DB 已有新名 `employee_no` | 不改名，照常比對該欄位（冪等，可重跑） |
| DB 無舊名也無新名 | 新增 `employee_no` 欄位，不發出警告 |
| 同一次升級也需要 rebuild | 拋例外拒絕（§4） |
| 跨多版本連續改名 | **不支援**：每版部署完成後請清掉 `OriginalFieldName` |

### 適用情境

- ✅ 新模組開發期、定義頻繁迭代
- ❌ 已部署到生產且跨多版本的累積改名 — 跳版部署不保證行為

> 改名後**下個版本就應移除** `OriginalFieldName`，避免 metadata 殘留。

## 7. 執行階段與 Transaction 行為

ALTER 路徑下，orchestrator 把 SQL 切成有序階段執行，每階段獨立 transaction：

```
Stage 1: DropIndexes        刪除即將被改定義的索引
Stage 2: AlterColumns       既有欄位 rename / 改型別 / 改長度 / 改 nullable
Stage 3: AddColumns         新增欄位
Stage 4: CreateIndexes      建新索引、重建先前刪除的
Stage 5: SyncDescriptions   同步表／欄位描述
```

> Stage 5（`SyncDescriptions`）的語句由該方言的 `IDescriptionSyncCommandBuilder` 產生
> （SQL Server 用 `sp_addextendedproperty`、PostgreSQL / Oracle 用 `COMMENT ON`、
> MySQL 用欄位定義內的 `COMMENT` 子句）。無法儲存描述的方言不提供 builder，此 stage 直接略過
> —— 目前只有 SQLite 屬此類。
>
> 此 stage 同時涵蓋 Stage 3 新加的欄位：比對器只對「兩側都存在」的欄位判斷描述差異，
> 資料庫裡還不存在的欄位不會產生任何差異記錄。少了這一段，in-place 升級加進來的欄位
> 永遠拿不到 caption，之後每次比對都會把該表判成尚未同步。

### 失敗行為

- 某 stage 失敗 → 該 stage 內 transaction rollback、後續 stage 不執行、例外拋出
- 已成功 commit 的前面 stage **不會回滾**
- comparer 是**冪等**的：修正失敗原因後重跑，已完成的 stage 會被視為「無變更」自動跳過

> 這個設計刻意不做整體 transaction：DDL 在多數 DB 不支援可靠的整體回滾，per-stage + 冪等重跑是更實際的策略。

## 8. 不支援的情境

下列情況**不在自動升級的範圍內**，需手動處理：

| 不支援 | 原因 / 替代方案 |
|--------|----------------|
| **Drop column** | 延續 extension field 保留政策；第三方整合可能依賴未列入 define 的欄位。確定要刪除請手動下 SQL |
| **Foreign key** | 框架原則：referential integrity 由 BO 處理，不依賴 DB 層 |
| **Trigger / View** | 同上，business rules 不放 DB |
| **跨版本多次 rename** | 單次 rename 才保證冪等；多版本累積請逐版部署 |
| **跨 DB schema** | 目前僅支援預設 schema |
| **Online schema change** | 如 SQL Server `ALTER INDEX ... WITH (ONLINE = ON)`，需自行下 SQL |

## 9. Dry-run 與部署實務

### 部署前先看 SQL

對大資料表（千萬筆以上）部署前，**強烈建議 dry-run** 先確認模式：

```csharp
var diff = builder.CompareToDiff("company", "ft_orders");
var plan = new TableUpgradeOrchestrator("myDb", connectionManager).Plan(diff);

if (plan.Mode == UpgradeExecutionMode.Rebuild)
{
    // 這次會走 rebuild — 排維護視窗
    Console.WriteLine("Rebuild will be triggered:");
    Console.WriteLine(builder.GetCommandText("company", "ft_orders"));
}
```

### 何時需要排維護視窗

| Plan.Mode | 影響 | 行動 |
|-----------|------|------|
| `NoChange` | 無 | 不需動作 |
| `Create` | 新表，無資料 | 直接執行 |
| `Alter` | 秒級，最多單欄位短暫鎖定 | 一般時段可執行 |
| `Rebuild` | 全表搬資料、表級鎖定 | **排維護視窗**，並先估算搬移時間 |

### 欄位縮小的標準流程

不要直接開 `AllowColumnNarrowing=true` 跑線上表。建議流程：

1. dry-run 確認哪些欄位被縮小
2. `SELECT COUNT(*) FROM table WHERE LEN(col) > <newLength>` 確認現有資料
3. 若有超出資料 → 先做資料修整（修剪、搬到新欄、業務溝通）
4. 確認資料都在範圍內 → 才開選項升級

### 大資料表的 rebuild 替代

如果 dry-run 顯示 rebuild、但業務不允許停機，請考慮：

- 拆解變更：把跨 family 的型別變更拆成「新增新欄位 → 業務雙寫 → backfill → 切換 → 刪舊欄」多步部署
- 手動 online schema change：依 DB provider 能力自行下 SQL，跳過 Polhem 自動升級
- 業務雙跑：暫不修改舊表，新功能用新表，舊表自然汰換

## 10. 框架表改名

> 手動 rename DDL，給以舊名稱保存框架表的資料庫使用。範例是 Polhem 1.0 之前完成的 `ft_department` / `ft_employee` → `st_department` / `st_employee` 改名：由使用舊名稱的早期版本建立的資料庫，需要它才能保留資料。對**公司資料庫**執行。

框架的自動升級管線不會自動 rename 表（見 §8 不支援情境）。當跨版本框架改了系統表名稱，已預先建好舊名表的部署需手動 rename。

### SQL Server / MySQL / Oracle / PostgreSQL

```sql
-- SQL Server
EXEC sp_rename 'ft_department', 'st_department';
EXEC sp_rename 'ft_employee',   'st_employee';

-- MySQL
RENAME TABLE ft_department TO st_department;
RENAME TABLE ft_employee   TO st_employee;

-- PostgreSQL
ALTER TABLE ft_department RENAME TO st_department;
ALTER TABLE ft_employee   RENAME TO st_employee;

-- Oracle
ALTER TABLE ft_department RENAME TO st_department;
ALTER TABLE ft_employee   RENAME TO st_employee;
```

以舊表名命名的 index（如 `pk_ft_employee`、`rx_ft_employee`）不受改名影響，在改名後的表上照常運作。框架的 index 名稱樣板以 `{0}` 代表表名，所以定義此時預期的是 `rx_st_employee` 等名稱。下一次 schema 升級不論主鍵名稱為何都找得到主鍵，但其他 index 是以套用表名後的名稱比對：它找不到 `rx_st_employee`，於是在舊的 `rx_ft_employee` 旁再建一個。若不想留下重複的 index，請手動刪除舊名稱的 index。

## 11. 參考

### 原始檔
- [TableSchemaBuilder](../../src/Polhem.Db/Schema/TableSchemaBuilder.cs) — 對外入口
- [TableUpgradeOrchestrator](../../src/Polhem.Db/Schema/TableUpgradeOrchestrator.cs) — Plan / Execute
- [TableSchemaDiff](../../src/Polhem.Db/Schema/TableSchemaDiff.cs) / [UpgradePlan](../../src/Polhem.Db/Schema/UpgradePlan.cs)
- [UpgradeOptions](../../src/Polhem.Db/Schema/UpgradeOptions.cs)
- [DbField.OriginalFieldName](../../src/Polhem.Definition/Database/DbField.cs)

### 相關文件
- [資料庫命名規範](database-naming-conventions.md)
- [架構總覽](architecture-overview.md)
- [開發指引](development-cookbook.md)
- [開發限制](development-constraints.md)
- [ADR-005：FormSchema-Driven](../adr/adr-005-formschema-driven.zh-TW.md)
