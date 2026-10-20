<!-- source: en/guides/excel-form-modeling.md blob: 7b6ea745f2b09ad21558d6aec3e976ae38795a15 -->
# 用 Excel 建模表單

[English](../../en/guides/excel-form-modeling.md) · [← 文件索引](../README.md)

> 在 Excel 活頁簿描述一張表單，匯入後就得到一張能用的表單：清單、新增、修改、刪除、關聯挑選、計算欄位與驗證規則，
> 不必寫程式

---

## 目錄

1. [適用對象](#1-適用對象)
2. [來回流程](#2-來回流程)
3. [活頁簿](#3-活頁簿)
4. [匯入時自動補上的內容](#4-匯入時自動補上的內容)
5. [匯入會寫入的檔案](#5-匯入會寫入的檔案)
6. [再次匯入](#6-再次匯入)
7. [限制](#7-限制)

---

## 1. 適用對象

專案初期，熟悉業務的人（系統分析師、關鍵使用者）負責描述表單，再由開發人員把描述轉成定義檔。表單活頁簿讓前者直接用
框架讀得懂的格式寫下描述。一份活頁簿描述一張表單：它的資料表、欄位、驗證規則，以及在選單中的位置。匯入後會產生這張
表單需要的所有定義檔，表單可以立刻開啟使用，之後再到 DefineEditor 細調。

活頁簿不是和定義檔並存的第二份權威來源。表單匯入之後，FormSchema 就是定義本身；需要再交給分析師修改時，就重新匯出
一份活頁簿。

## 2. 來回流程

指令在 `dotnet polhem` 中（[Polhem.Cli](../../../tools/Polhem.Cli/README.zh-TW.md)）；DefineEditor 的選單也提供
相同的匯入與匯出。

```bash
# 新表單用的空白活頁簿
dotnet polhem forms template --out Quote.xlsx

# 既有表單的活頁簿，用來修改
dotnet polhem forms export Quote --define ./Define --out Quote.xlsx

# 先看匯入會變更哪些檔案，再正式匯入
dotnet polhem forms import Quote.xlsx --define ./Define --dry-run
dotnet polhem forms import Quote.xlsx --define ./Define
```

`--define` 是定義資料夾，也就是放 `FormSchema/` 的那一層。匯入只寫定義檔；建立資料表是應用程式的工作。以 Northwind
示範為例，它的伺服器啟動時會建立 `DbCategorySettings.xml` 中註冊的所有資料表（`NorthwindSchemaSeeder`），所以在那裡
只要重新啟動，表單就會出現在選單中並可以使用。

## 3. 活頁簿

| 工作表 | 內容 |
|--------|------|
| `說明` | 填寫方式，以及每一欄的意義 |
| `Form` | 表單本身：程式代號、顯示名稱、資料庫範圍、清單欄位、選單位置 |
| `Tables` | 每列一張資料表：第一列是主檔，其餘是明細表 |
| `Fields` | 每列一個欄位，並註明所屬的資料表 |
| `Rules` | 每列一條驗證規則，寫法見 [運算式與規則](../definitions/expression-rules.md) |

各欄的說明以活頁簿的「說明」工作表為準，本文不重複。只能填固定值的欄位（資料型別、輸入控制項、數值種類、是／否）
提供下拉選單。多數表單用不到的欄位集中在 Fields 工作表右側，預設折疊。

每個顯示文字都有英文欄與中文欄。英文寫進定義檔，中文寫進語系資源，機制見 [租戶客製化](../definitions/customization.md)。

一格可以用 `a=b; c=d` 寫一小串清單：欄位的下拉選項（`Draft=Draft; Sent=Sent`，值與文字相同時可只寫 `Draft; Sent`），
以及關聯或查詢的欄位對應（`sys_id=ref_customer_id`）。

每張表格式工作表的第 2 列是隱藏列，存放各欄的鍵。匯入時依鍵找欄，不依位置，所以可以調整欄順序、修改標題，或插入
自己的欄位；沒有鍵的欄會被略過。請勿刪除這一列。

## 4. 匯入時自動補上的內容

活頁簿只需要列出業務欄位。框架慣例決定的部分由匯入補上，而且只在活頁簿沒有寫的時候才補：

- **資料庫範圍** 預設為 `company`，業務資料一律放在這裡。
- **主檔** 以程式代號命名。Tables 工作表一列都沒有時，會自動建立主檔。
- **資料庫表名** 預設為 `ft_` 加上資料表代號的小寫底線形式：`QuoteLine` 會變成 `ft_quote_line`。
- **系統欄位**：每張表補上 `sys_no` 與 `sys_rowid`，每張明細表再補上 `sys_master_rowid`，都設為隱藏。
- **關聯欄位**：指定了關聯表單的欄位（`customer_rowid` 關聯 `Customer`）會設為 `Guid`，並補上顯示關聯資料的兩個欄位
  `ref_customer_id` 與 `ref_customer_name`，分別對應關聯表單的 `sys_id` 與 `sys_name`。它們的標題沿用關聯欄位的
  標題：`Customer Code`、`客戶代號`。

## 5. 匯入會寫入的檔案

| 檔案 | 已存在時 |
|------|----------|
| `FormSchema/<ProgId>.FormSchema.xml` | 覆寫，除非內容已經描述同一張表單 |
| `TableSchema/<範圍>/<資料表>.TableSchema.xml`，每張表一份 | 依 TableSchema 模式而定，見 [再次匯入](#6-再次匯入)。既有資料庫透過 [結構升級](../database/database-schema-upgrade.md) 同步 |
| `FormLayout/<ProgId>.FormLayout.xml` | **保留**，見 [再次匯入](#6-再次匯入) |
| `DbCategorySettings.xml` | 資料表尚未登記時，加入對應的範圍 |
| `ProgramSettings.xml` | 程式尚未登記時加入 |
| `MenuSettings.xml` | 尚未有選單項目開啟這張表單時，加入到 Form 工作表指定的資料夾；資料夾不存在就新建 |
| `Language/zh-TW/<ProgId>.Language.xml` 與 `Language/zh-TW/Menu.Language.xml` | 寫入活頁簿的中文文字，其他鍵保留 |

設定檔與語系檔是原地編輯：只加入項目，檔案其餘內容（包括註解）保持原狀。

寫入任何檔案之前，會先用 DefineEditor 相同的驗證檢查表單。錯誤會標出它來自活頁簿的哪一列
（`Fields!12 (Quote.qty.UnitField)`），只要有錯誤就停止匯入，不寫入任何檔案。警告（例如關聯到資料夾中還沒有的表單）
只會顯示，不會中止匯入。

## 6. 再次匯入

修改活頁簿後再匯入一次即可；內容已經與活頁簿一致的檔案不會被動到。

TableSchema 常常包含表單推導不出來的內容：欄位長度、表單上沒有的欄位、手動加的索引。匯入時如何處理既有的
TableSchema，以 `--tables` 選擇（DefineEditor 會詢問）：

| 模式 | 效果 |
|------|------|
| `merge`（預設） | 加入表單有、檔案沒有的欄位與索引。檔案中既有的內容不會被修改或刪除，所以移除欄位或變更資料型別要在 DefineEditor 處理 |
| `keep` | 檔案維持原狀 |
| `overwrite` | 以表單產生的 TableSchema 取代 |

FormLayout 是例外。只有表單還沒有版面時才會產生，因為版面通常會在之後手動調整。要換成重新產生的版面，加上
`--regenerate-layout`，或在 DefineEditor 對 FormSchema 執行 **產生 FormLayout**。之後才加進活頁簿的欄位，要放進
既有版面才會出現在畫面上。

## 7. 限制

- 一份活頁簿描述一張表單。
- 活頁簿承載的是定義，不是版面：區段、欄數與欄位在畫面上的順序，在 DefineEditor 中安排。
- 定義無法表達的商業邏輯（自訂的 business object 或 repository）仍以程式碼撰寫，並登記在 `ProgramSettings.xml`，
  與其他表單相同。
- 匯入不會動到資料庫。新資料表由應用程式建立結構的機制建立，既有資料表則透過
  [結構升級](../database/database-schema-upgrade.md) 變更。
