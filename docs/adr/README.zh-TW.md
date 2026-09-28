<!-- source: adr/README.md blob: ae0c07c21365e9902f4c9e64b278315c71218485 -->
# 架構決策紀錄（ADR）索引

[English](README.md)

← 回到 [文件索引](../zh-TW/README.md)

ADR 記錄**決策當下的脈絡與理由**，是理解「為何這樣設計」的主要來源。

> ADR 不隨實作演進改寫。決策被推翻時標記為「已取代」並指向新的 ADR；
> 實作細節偏離但決策仍成立時，於文末加〈實作演進〉段落說明，原文保留。
>
> 決策有一部分被推翻、其餘仍成立時，狀態為「已採納，部分取代」。該 ADR 的〈狀態〉段落會說明哪一部分不再成立、
> 由什麼取代：後來的 ADR，或其〈實作演進〉段落中的某一項。

> Polhem 以新名稱延續 Bee.NET 框架，多數決策做成時框架仍名為 Bee.NET。因此 ADR 中的 `4.x` 版號與「CHANGELOG 4.x」
> 指標，指的是 Bee.NET 的版本，不是 Polhem（Polhem 的變更紀錄從 1.0.0 開始）。這些版本的內容見
> [Bee.NET 變更紀錄](https://github.com/jeff377/bee-library/blob/7d6cc9d9/CHANGELOG.zh-TW.md)，以及同一 repo
> `docs/changelogs/` 下的各版明細。

| # | 決策 | 狀態 |
|---|------|------|
| [001](adr-001-dataset-as-dto.zh-TW.md) | 使用 DataSet 作為跨層 DTO | ✅ 已採納 |
| [002](adr-002-newtonsoft-json.zh-TW.md) | JSON 序列化函式庫的選擇與遷移 | 🔁 已取代 |
| [003](adr-003-static-service-locator.zh-TW.md) | 使用靜態 Service Locator 而非依賴注入 | 🔁 已取代 |
| [004](adr-004-messagepack-payload.zh-TW.md) | 使用 MessagePack 作為 API Payload 序列化格式 | ✅ 已採納，部分取代 |
| [005](adr-005-formschema-driven.zh-TW.md) | FormSchema 定義驅動架構 | ✅ 已採納 |
| [006](adr-006-dual-target-framework.zh-TW.md) | 雙目標框架策略（netstandard2.0 + net10.0） | 🔁 已取代 |
| [007](adr-007-convention-based-type-resolution.zh-TW.md) | 以命名慣例自動推導 API 型別 | ✅ 已採納 |
| [008](adr-008-polhem-db-namespace-layout.zh-TW.md) | Polhem.Db 命名空間佈局——語法層與模型層分離 | ✅ 已採納 |
| [009](adr-009-cache-implementation.zh-TW.md) | Polhem.ObjectCaching 採用 Microsoft.Extensions.Caching.Memory + IChangeToken | ✅ 已採納 |
| [010](adr-010-logical-database-category.zh-TW.md) | 邏輯資料庫分類（DbCategory）解耦資料庫部署彈性 | ✅ 已採納，部分取代 |
| [011](adr-011-di-replaces-service-locator.zh-TW.md) | 採用 DI 取代靜態 Service Locator | ✅ 已採納 |
| [012](adr-012-session-company-context.zh-TW.md) | Session 公司情境模型（兩階段 session lifecycle） | ✅ 已採納 |
| [013](adr-013-frontend-api-connection-strategy.zh-TW.md) | 前端 API 連線策略 — `Polhem.UI.*` 與 `Polhem.Web.*` 兩條 family 分流 | ✅ 已採納 |
| [014](adr-014-jsonrpc-plain-public-default.zh-TW.md) | JSON-RPC `Plain` 開放策略 — `Public` 為預設保護等級，HTTPS 為信任界線 | ✅ 已採納 |
| [015](adr-015-master-key-environment-default.zh-TW.md) | `MasterKeySource` 預設改為 `Environment` — 對齊 12-factor「config in env」 | ✅ 已採納 |
| [016](adr-016-multitenant-customization-overlay.zh-TW.md) | 多租戶客製化覆蓋層（雙層唯讀疊加） | ✅ 已採納 |
| [017](adr-017-db-cache-invalidation.zh-TW.md) | 資料庫快取相依/失效機制（通知表 + 輪詢 + 慣例分派） | ✅ 已採納 |
| [018](adr-018-db-define-storage.zh-TW.md) | 定義儲存於資料庫（`st_define` 單表 XML blob） | ✅ 已採納 |
| [019](adr-019-permission-authorization-model.zh-TW.md) | 權限授權模型（兩層 enforcement + record scope） | ✅ 已採納 |
| [020](adr-020-avalonia-datagrid-binding-strategy.zh-TW.md) | Avalonia DataGrid 對 DataTable 列的綁定策略 | ✅ 已採納 |
| [021](adr-021-avalonia-datagrid-editing-strategy.zh-TW.md) | Avalonia DataGrid 的 in-cell 編輯策略 | ✅ 已採納 |
| [022](adr-022-avalonia-datagrid-cell-recycling.zh-TW.md) | Avalonia DataGrid 清單儲存格不啟用模板回收 | ✅ 已採納 |
| [023](adr-023-lookup-relation-mechanism.zh-TW.md) | 定義驅動的 lookup 關連機制 | ✅ 已採納 |
| [024](adr-024-dataform-save-dataadapter.zh-TW.md) | DataForm 持久化改走 DataTable 級 DataAdapter | ✅ 已採納 |
| [025](adr-025-define-types-aot-xmlserializer-compat.zh-TW.md) | 定義型別相容 AOT reflection XmlSerializer（單一 Add + 無參數建構子） | ✅ 已採納 |
| [026](adr-026-numeric-semantics-rounding.zh-TW.md) | 數值語意、公司/貨幣/單位位數與 round-then-sum | ✅ 已採納 |
| [027](adr-027-audit-trail.zh-TW.md) | 資料軌跡 / 稽核日誌（六軸 `st_log_*` 設計） | ✅ 已採納 |
| [028](adr-028-expression-rule-engine.zh-TW.md) | 自訂運算式與規則引擎（減少 BO 手寫程式碼） | ✅ 已採納，部分取代 |
| [029](adr-029-lowercase-field-names.zh-TW.md) | 欄位名稱一律小寫（定義 / 資料 / UI 三層一致） | ✅ 已採納 |
| [030](adr-030-messagepack-name-based-keys.zh-TW.md) | MessagePack 合約改採 property-name key（keyAsPropertyName） | ✅ 已採納，部分取代 |
| [031](adr-031-calendar-day-column-semantics.zh-TW.md) | 日曆日欄位語意以顯式標記承載，不改 CLR 型別 | ✅ 已採納 |
| [032](adr-032-datetime-timezone.zh-TW.md) | DateTime 以 UTC 為單一時區來源，Connector 為唯一轉換點 | ✅ 已採納 |
| [033](adr-033-time-of-day-semantics.zh-TW.md) | 時刻語意（`FieldDbType.Time`）以定寬字串承載 | ✅ 已採納 |
| [034](adr-034-progid-type-registry.zh-TW.md) | ProgramSettings 作為全框架型別註冊表（選單分離、Repository 以 progId 綁定） | ✅ 已採納 |
| [035](adr-035-business-logic-plugin.zh-TW.md) | 業務邏輯 plugin（在既有流程上掛載、兩層相加、與規則引擎分界） | ✅ 已採納 |
| [036](adr-036-wire-serialization-externalized.zh-TW.md) | 傳輸序列化外置至 API 層，定義層不再承載 MessagePack | ✅ 已採納，部分取代 |
| [037](adr-037-wire-explicit-registration.zh-TW.md) | wire 型別一律顯式註冊 formatter，`object` 值改用判別式封套 | ✅ 已採納 |
| [038](adr-038-definition-dependency-boundary.zh-TW.md) | 定義層相依邊界：運算式抽象下沉至 `Polhem.Base`，判準以閘門固化 | ✅ 已採納 |
| [039](adr-039-formlayout-design-time-only.zh-TW.md) | `FormLayout` 收回設計階段，執行階段不再由 `FormSchema` 推導 | ✅ 已採納 |
| [040](adr-040-audit-trail-taxonomy.zh-TW.md) | 稽核軌跡的分類軸與寫入策略（四項實作、檢視預設關閉、DiffGram before/after） | ✅ 已採納 |
| [041](adr-041-per-form-audit-rule.zh-TW.md) | per-form 稽核規則 —— 異動與檢視改為逐表單設定 | ✅ 已採納 |
| [042](adr-042-api-replay-protection.zh-TW.md) | API 重放防護 —— 加密封套內的 wire frame | ✅ 已採納 |
| [043](adr-043-error-contract-single-registry.zh-TW.md) | 錯誤契約以單一登錄表達，兩端從同一份宣告消費 | ✅ 已採納 |
| [044](adr-044-payload-codec-negotiation.zh-TW.md) | body codec 由每個請求宣告，JSON 與 MessagePack 並存 | ✅ 已採納 |
| [045](adr-045-language-policy-and-local-plans.zh-TW.md) | 共同維護的內容一律英文；計畫不進 repo | ✅ 已採納 |
| [046](adr-046-api-evolution-policies-for-1-0.zh-TW.md) | 1.0 的 API 演進政策：同步的伺服器路徑、可擴充的主機介面、行程層級的設定 | ✅ 已採納 |
