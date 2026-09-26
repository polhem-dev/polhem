<!-- source: adr/adr-008-polhem-db-namespace-layout.md blob: fa91daf35179206a72f16609f231245c57ec9059 -->
# ADR-008：Polhem.Db 命名空間佈局——語法層與模型層分離

[English](adr-008-polhem-db-namespace-layout.md)

## 狀態

已採納（2026-04-27）

## 背景

`Polhem.Db` 在 SQLite provider 加入後，命名空間結構出現以下歸屬模糊問題：

1. `Polhem.Db.Sql` 命名籠統，實質僅放 DML 構件（query composition + IUD builders），但字面易讓人誤以為連 DDL 也包含。
2. `Polhem.Db.Providers` 根層平鋪混放四種職能介面：
   - DDL 字串產生契約（`ICreateTableCommandBuilder` / `ITableAlterCommandBuilder` / `ITableRebuildCommandBuilder`）
   - Schema 模型讀取（`ITableSchemaProvider`）
   - DML 字串產生契約（`IFormCommandBuilder`）
   - Provider 工廠（`IDialectFactory`）
3. `Polhem.Db.Schema` 雖有「Schema」命名，但實際內容（`TableSchemaBuilder` / `TableSchemaComparer` / `TableSchemaDiff` / `TableUpgradeOrchestrator` / `UpgradePlan` 等）並不產生 SQL，僅做模型比對與升級流程協調；DDL 字串產生反而集中在 `Providers/`。
4. 兩個 DML 相關檔案孤立在根層：
   - `TableSchemaCommandBuilder`：從 `TableSchema` 產 INSERT/UPDATE/DELETE，純 DML 字串產生
   - `JoinType`：SELECT 專用 enum，僅 SQL builder 使用

未來新增其他 DDL 契約（如 index 管理、TCL）若無明確規則，會持續加重歸屬混淆。

## 決策

採「**語法層 vs 模型層分離；契約依職能歸類，實作依 provider 歸類**」設計原則，命名空間佈局如下：

| 層次 | 命名空間 | 職能特徵 |
|------|----------|----------|
| 語法層 | `Polhem.Db.Ddl` / `Polhem.Db.Dml` | 產生 SQL 字串（含其抽象契約） |
| 模型層 | `Polhem.Db.Schema` | 操作 `TableSchema` 資料模型（比對、升級流程） |
| 工廠 | `Polhem.Db.Providers` | 將語法層契約綁定到 provider |
| 實作 | `Polhem.Db.Providers.{X}` | 各 provider 同時負責該家族的 DDL / DML / 讀取實作 |

### 三項核心要點

1. **語法層（`Polhem.Db.Ddl` / `Polhem.Db.Dml`）vs 模型層（`Polhem.Db.Schema`）vs 工廠（`Polhem.Db.Providers`）分離**

   - `Polhem.Db.Ddl`：DDL 字串產生契約（`ICreateTableCommandBuilder`、`ITableAlterCommandBuilder`、`ITableRebuildCommandBuilder`）
   - `Polhem.Db.Dml`：DML 字串產生 + 構件（SELECT/INSERT/UPDATE/DELETE builders、`IFormCommandBuilder`、`TableSchemaCommandBuilder`、`JoinType`、context / parameter collector 等）
   - `Polhem.Db.Schema`：操作 `TableSchema` 模型（builder façade、comparer、diff、upgrade orchestrator、plan、stage 等）+ 讀取契約（`ITableSchemaProvider`）
   - `Polhem.Db.Providers`：僅留 `IDialectFactory`

   依賴方向 `Schema → Ddl`（升級流程呼叫 DDL builder），單向。

2. **契約依職能歸類，實作依 provider 歸類**

   仿 .NET BCL `System.Data` / `System.Data.SqlClient` 慣例：
   - 介面 / 抽象契約 → 依「做什麼事」歸類（`Polhem.Db.Ddl` / `Polhem.Db.Dml` / `Polhem.Db.Schema`）
   - 具體 per-provider 實作 → 統一歸 `Polhem.Db.Providers.{SqlServer|PostgreSql|Sqlite}`

   此原則的好處：
   - 同一 provider 下所有實作（CREATE、ALTER、REBUILD、Form CRUD、SchemaProvider 等）共處一個命名空間，方便撰寫與審視
   - 對外註冊入口統一：`using Polhem.Db.Providers.Sqlite; new SqliteDialectFactory();`
   - 不在 provider 子目錄內二次切割 DDL / DML（每個 provider 子目錄檔案數約 8–9 份，切割成本大於價值）

3. **邊界判準：「會產 SQL 字串嗎？」**

   未來新增類別 / 介面時，依此判準歸屬：
   - **是** → 語法層（依 SQL 種類入 `Polhem.Db.Ddl` 或 `Polhem.Db.Dml`）
   - **否，且操作 `TableSchema` 等資料模型** → `Polhem.Db.Schema`
   - **是工廠 / 註冊機制** → `Polhem.Db.Providers`
   - **是具體 per-provider 實作** → `Polhem.Db.Providers.{X}`

## 結果

### 採納後的命名空間分配

```
Polhem.Db                       # 跨切面基礎設施：DbAccess、DbAccessFactory、DbCommandSpec、DbBatchSpec、
                             # DbConnectionScope、ILMapper、DbCommandKind、DbBatchResult 等執行核心
Polhem.Db.Manager               # IDbConnectionManager、DbProviderRegistry、DbDialectRegistry
Polhem.Db.CacheNotify           # 跨行程快取失效通知
Polhem.Db.Storage               # DbDefineStorage（定義落 DB 的儲存實作）
Polhem.Db.Ddl                   # DDL 字串產生契約（3 個 I*CommandBuilder）
Polhem.Db.Dml                   # DML 字串產生 + 構件（含 IFormCommandBuilder、TableSchemaCommandBuilder、JoinType）
Polhem.Db.Schema                # TableSchema 模型 / 比對 / 升級流程 + ITableSchemaProvider
Polhem.Db.Schema.Changes        # Add/Alter/Drop/Rename Field / Index 等變更模型
Polhem.Db.Providers             # 僅 IDialectFactory
Polhem.Db.Providers.SqlServer   # SQL Server 實作（DDL + DML + SchemaProvider + Helper + TypeMapping）
Polhem.Db.Providers.PostgreSql  # 同上（PostgreSQL）
Polhem.Db.Providers.Sqlite      # 同上（SQLite）
```

### 對外 API 變更

外部使用者主要受影響的 `using` 對應：

| 舊 | 新 |
|----|-----|
| `using Polhem.Db.Sql;` | `using Polhem.Db.Dml;` |
| 引用 `IFormCommandBuilder` 時 `using Polhem.Db.Providers;` | `using Polhem.Db.Dml;` |
| 引用 DDL 契約（`ICreateTableCommandBuilder` 等）時 `using Polhem.Db.Providers;` | `using Polhem.Db.Ddl;` |
| 引用 `ITableSchemaProvider` 時 `using Polhem.Db.Providers;` | `using Polhem.Db.Schema;` |

provider 註冊入口（`using Polhem.Db.Providers.Sqlite; DbDialectRegistry.Register(...)`）完全不變。

不另升版號（沿用 `4.0.x`），release notes 列出對應表即可。

## 替代方案（已評估後不採納）

1. **「最小改動」方案**：僅改名 `Sql → Dml`，不動 `Providers/` 內混置。
   - 拒絕原因：DDL 與 DML 契約混置、Schema 讀取契約被當成 DDL 同夥，未來新增契約仍會遇到一樣的歸屬模糊問題。
2. **`Polhem.Db.Schema` → `Polhem.Db.Ddl` 整批改名**：表面上與 `Polhem.Db.Dml` 形成對稱。
   - 拒絕原因：`Schema/*` 並不產 SQL（只做模型比對與流程），改名為 `Ddl` 會把「模型層」與「語法層」兩個關注點混在一起，本質上不正確。
3. **provider 子目錄內進一步分 DDL/DML 兩個子命名空間**（如 `Polhem.Db.Providers.Sqlite.Ddl`）。
   - 拒絕原因：對稱性差且每個 provider 子目錄檔案數不大（約 8–9 份），切割成本大於價值。違反「實作依 provider 歸類」的設計簡潔性。

## 相關文件

- 套件 README：[`src/Polhem.Db/README.md`](../../src/Polhem.Db/README.zh-TW.md)
