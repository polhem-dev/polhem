# Polhem.Definition.Spreadsheet

[English](README.md)

讀寫描述一張表單的 Excel 活頁簿，並把活頁簿轉成可執行表單的定義檔。這是開發期使用的類別庫，由
[Polhem.Cli](../Polhem.Cli/README.zh-TW.md)（`dotnet polhem forms`）與 [DefineEditor](../DefineEditor/README.zh-TW.md)
共用；它不發佈成套件，所以應用程式不會帶著 Excel 相依。

活頁簿怎麼填、匯入會做什麼，給使用者看的說明在 [用 Excel 建模表單](../../docs/zh-TW/guides/excel-form-modeling.md)。
本檔說明的是程式碼。

## 組成

| 型別 | 角色 |
|------|------|
| `FormWorkbookCatalog` | 活頁簿的唯一描述：工作表、各欄順序、下拉清單。其他部分都讀它 |
| `FormWorkbookWriter` / `FormWorkbookReader` | 把表單填進內嵌的範本，以及把填好的活頁簿讀回來 |
| `FormSchemaConventions` | 補上活頁簿交給慣例的部分：系統欄位、關聯的 ref 欄位、資料表名稱、資料庫範圍 |
| `FormSchemaValidator` | DefineEditor 顯示在表單旁的檢查；匯入器遇到有錯誤的表單就拒絕 |
| `FormImporter` | 規劃匯入要寫的每個檔案（這份規劃就是 dry-run 報告），再寫入 |
| `FormExporter` | 從定義資料夾收集表單、它的中文文字與選單位置 |
| `FormSync` | 規劃讓已儲存的 FormSchema 的 TableSchema、FormLayout 與資料表登記跟上它：等於以資料夾中現有的表單做一次匯入，只新增不修改；主檔新增的可見欄位也會加到非空白的 `ListFields` |

## 範本

MiniExcel 會填範本，但無法建立下拉選單，所以版面放在 `Templates/` 下的範本，每個 `FormWorkbookLanguage` 一份
（`FormSchema.en.xlsx`、`FormSchema.zh-TW.xlsx`）。它們由測試專案以 ClosedXML 依 `FormWorkbookCatalog` 產生後 commit；
目錄中的每個標題、說明與說明工作表文字都有兩種語言，兩份範本也只差這些文字。請勿用 Excel 修改。改了欄位目錄後重新產生：

```bash
POLHEM_REGENERATE_FORM_TEMPLATE=1 dotnet test tests/Polhem.Definition.Spreadsheet.UnitTests \
    --filter FullyQualifiedName~FormWorkbookTemplateTests
```

範本與欄位目錄不一致、或兩份範本除了這些文字之外還有差異時，`FormWorkbookTemplateTests` 會失敗；文字只有一種語言時，
`FormWorkbookLanguageTests` 會失敗；`FormSchema`、`FormTable`、`FormField` 或 `FormRule`
有序列化屬性沒有對應欄位時，`FormWorkbookCoverageTests` 會失敗。

## 維持 trim 與 AOT 安全

DefineEditor 以單一執行檔發佈，本類別庫也以 `IsAotCompatible` 建置。MiniExcel 以反射讀取物件屬性，且沒有 trim 標註，
它的強型別路徑在 trim 與 NativeAOT 下會失效。以兩條規則避開：

- 範本資料一律傳 `Dictionary<string, object>` 及其清單，不傳匿名型別或其他物件。
- 讀取一律用非泛型的 `Query`，並把每列轉型為 `IDictionary<string, object>`。不用 `Query<T>`，也不用帶型別的
  `foreach` 走訪結果，那會經過 `dynamic` 轉型。

分析器抓得到本專案中的 `dynamic` 轉型，但看不到對 MiniExcel 反射路徑的呼叫。

MiniExcel 固定在 1.x 線：2.0 會拆成三個套件並改變 API，升級本身就是一次遷移。
