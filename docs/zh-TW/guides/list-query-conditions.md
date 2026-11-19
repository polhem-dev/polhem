<!-- source: en/guides/list-query-conditions.md blob: 33565f48069dfe51ffa19e1a89f045efb3646594 -->
# 清單查詢條件

[English](../../en/guides/list-query-conditions.md) · [← 文件索引](../README.md)

> 讓使用者逐條加入條件來縮小表單清單，以及在自己的畫面裡重用同一個查詢面板

---

## 目錄

- [1. 使用者看到的畫面](#1-使用者看到的畫面)
- [2. 宣告清單可查詢的欄位](#2-宣告清單可查詢的欄位)
- [3. 各欄位可以輸入什麼](#3-各欄位可以輸入什麼)
- [4. 條件如何組合](#4-條件如何組合)
- [5. 文字比對](#5-文字比對)
- [6. 在自己的畫面使用面板](#6-在自己的畫面使用面板)
- [7. 儲存的查詢](#7-儲存的查詢)
- [8. 不透過面板直接呼叫 GetList](#8-不透過面板直接呼叫-getlist)

---

## 1. 使用者看到的畫面

表單有可查詢欄位時，Avalonia 的 `ListView` 與 Blazor 的 `FormPage`（MudBlazor 與 Fluent UI）工具列會出現**查詢**按鈕。按下後開啟查詢窗格，使用者逐條加入條件：
**加入條件**列出欄位；每一列上方是欄位和移除鈕，下方是運算子（欄位有多種時才出現）和值。**搜尋**（或按 Enter）會帶著
條件讀取清單的第一頁；**清除**會移除條件並重新讀取整份清單。

最後一次搜尋的條件會以標籤顯示在清單上方，例如「訂單日期：本月」或「貨運商：Federal Shipping 等 3 筆」，查詢按鈕也會
顯示條件數。按標籤的 ✕ 會移除該條件並重新搜尋（必要條件沒有 ✕）；點標籤會開啟窗格並捲到那一列；尾端的**清除篩選**會
移除全部條件。

窗格出現的位置依清單寬度而定：

| 寬度 | 窗格 |
|------|------|
| 清單在 360 像素的窗格旁還有 600 像素以上 | 固定在右側；清單變窄，高度不變 |
| 比上面窄 | 浮在清單右側，點窗格外或搜尋後收起 |
| 小於 600 像素（手機） | 覆蓋整個畫面的卡片，清單變暗；搜尋後收起 |

清單一開始沒有任何條件，或套用使用者的預設查詢（[第 7 節](#7-儲存的查詢)）。

Blazor 的 `FormPage` 依同樣的寬度決定窗格位置，有兩點不同：蓋在清單上的窗格搜尋後不會自動關閉，要按 ✕ 或 Escape
關閉；手機寬度時窗格蓋住整頁，而不是讓清單變暗。欄頭篩選也是一個條件：會顯示成 chip、出現在窗格的條件列裡，同一欄再設定
時取代那一列，清除條件時也一併清除。

## 2. 宣告清單可查詢的欄位

`FormSchema.QueryFields` 列出這些欄位，以逗號分隔，寫法和 `ListFields`、`LookupFields` 相同。留空時改用清單的
`ListFields`。

```xml
<FormSchema ProgId="Order" ... ListFields="sys_id,order_date,ref_customer_name,ref_employee_name,status,total_amount"
            QueryFields="sys_id,order_date,customer_rowid,ref_customer_name,employee_rowid,shipper_rowid,status,freight,total_amount">
```

- 只計入主檔的欄位。關聯顯示欄位（例如 `ref_customer_name`）也可以列入，查詢時會 join 關聯的資料表來篩選。
- 列入外鍵（`customer_rowid`），使用者就能透過開窗挑選關聯資料；列入顯示欄位（`ref_customer_name`），使用者就能輸入
  名稱的一部分來查。
- `sys_rowid` 和框架保護的欄位（`ProtectedFields`，例如密碼雜湊）一律不會列出。
- 使用者沒有讀取權限的 `SensitiveCategory` 欄位不會出現在面板裡，和清單隱藏該欄的做法一致。這是前端的使用體驗，
  不是資料邊界；請參閱[權限與授權指南](../security/permission-authorization.md)。
- 不是表單欄位的名稱，建置時會回報 [POLHEM1004](../reference/analyzer-rules.md)；執行時則會略過。
- 伺服器不會把篩選限制在這些欄位：資料表宣告且未受保護的欄位都可以篩選。`QueryFields` 決定的是面板提供哪些欄位。

DefineEditor 用與 `ListFields` 相同的欄位挑選器編輯這個屬性，Excel 表單活頁簿也有對應的欄
（[用 Excel 建模表單](excel-form-modeling.md)）。

## 3. 各欄位可以輸入什麼

欄位的類型決定編輯器和運算子，由欄位定義推導而來：

| 欄位 | 類型 | 編輯器 | 運算子 |
|------|------|--------|--------|
| 有設定 `RelationProgId` | 關聯 | 透過開窗挑選資料，以可移除的標籤顯示 | 為下列任一 |
| 有設定 `ListItems` 或 `LangEnumName` | 選項 | 每個值一個核取方塊 | 為下列任一 |
| 名稱為 `sys_id` 或以 `_id` 結尾的字串 | 代碼 | 起～迄 | 介於、開頭為、包含、等於 |
| 其他字串 | 文字 | 文字框 | 包含、開頭為、等於 |
| Boolean | 是／否 | 全部、是、否 | 等於 |
| 數字 | 數字 | 起～迄，或單一數字 | 介於、等於 |
| Date | 日期 | 起～迄，或單一日期，可選期間 | 介於、等於 |
| DateTime | 日期時間 | 以日為單位輸入，和日期相同 | 介於、等於 |

長文字（`Text`）、二進位資料、時間（time of day），以及沒有指向關聯程式的 `Guid`，不提供條件。

日期區間的**期間**選單提供今天、昨天、本週、上週、本月、上月、本季、今年，以及最近 7 天或 30 天。期間會保持相對：
每次執行查詢時，才依登入使用者時區的今天換算；一週從哪天開始，依使用者的文化設定。

## 4. 條件如何組合

- 每一列都必須符合：條件列之間以 AND 組合。
- 同一個欄位可以出現在多列，例如名稱包含「A」且包含「B」。
- 同一列的多個值符合其中之一即可，所以「狀態為草稿或已確認」不需要 OR。
- 區間的兩端都可以留空。
- 日期時間區間包含最後一天的整天：查到隔天之前為止。
- 生效的條件是最後一次搜尋時的條件。換頁和排序都沿用它們，即使使用者正在修改條件列；修改要到下次搜尋才生效。

## 5. 文字比對

文字運算子（包含、開頭為、結尾為）只把 `%` 當萬用字元：`abc%001` 會比對到 `abc-001` 和 `abc_x_001`。其他字元都只比對
自己，包括 `_`，以及 SQL Server 上的 `[`，所以 `abc_001` 這樣的代碼不會連帶比對到 `abcd001`。

文字比對是否區分大小寫取決於資料庫：SQL Server 和 MySQL 依欄位的定序（常用的定序不區分大小寫），SQLite 只對 ASCII
英文字母不區分大小寫，PostgreSQL 和 Oracle 則區分大小寫。

## 6. 在自己的畫面使用面板

面板和它的模型都是公開的，所以自己的畫面（例如報表）可以照 `ListView` 的方式使用：

| 型別 | 套件 | 作用 |
|------|------|------|
| `CriteriaField` | `Polhem.UI.Core` | 可以輸入條件的欄位；`FromQueryFields` 回傳表單的可查詢欄位 |
| `QueryCriteria`、`CriteriaRow` | `Polhem.UI.Core` | 條件列，值已經轉成欄位的型別 |
| `GetListFilterConverter` | `Polhem.UI.Core` | 把條件列轉成清單請求的 `FilterNode` |
| `CriteriaPanel` | `Polhem.UI.Avalonia` | 編輯條件列的 Avalonia 控件 |
| `CriteriaPanelBase` | `Polhem.Web.Blazor` | Blazor 查詢窗格的規則；`Polhem.Web.Blazor.Mud` 與 `Polhem.Web.Blazor.Fluent` 的 `CriteriaPanel` 分別用 MudBlazor 與 Fluent UI 畫出來 |
| `ColumnFilterCriteria` | `Polhem.UI.Core` | 把清單的欄頭篩選存成 `QueryCriteria` 的條件列 |

```csharp
var criteria = new QueryCriteria();
// A report would add its required rows here; the user cannot remove them.
var orderDate = CriteriaField.FromField(schema.FindField("order_date")!)!;
criteria.Add(orderDate, kind: CriteriaRowKind.Required, lockOperator: true)
        .SetRelativeDate(new RelativeDate(RelativeDateKind.ThisMonth));

var panel = new CriteriaPanel { Today = () => FrameworkClock.Today(user.TimeZone) };
panel.Bind(CriteriaField.FromQueryFields(schema, capabilities), criteria);
panel.SearchRequested += async (_, _) =>
{
    var filter = GetListFilterConverter.ToFilter(criteria, FrameworkClock.Today(user.TimeZone),
        CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
    var response = await connector.GetListAsync(selectFields, filter);
    // Show response.Table.
};
```

只有在每一列必要條件都有值時，才會觸發 `SearchRequested`；否則面板會列出還沒填的條件。必要條件是畫面上的便利功能：
需要某個值的後端方法要自己檢查，因為呼叫端可以略過畫面。

`GetList` 以外的後端方法（例如報表）不用 `GetListFilterConverter`，而是把同樣的條件列轉成自己的參數。

## 7. 儲存的查詢

使用者可以把畫面上的條件以名稱儲存，之後再套用，每張表單各自一組。儲存的查詢在查詢窗格頂部：一個下拉選單，選一筆就套用並搜尋；
條件改過之後旁邊會出現 `*`；**⋯** 選單有**儲存查詢**、**另存新查詢…**、**重新命名…**、**與所有人共用**／**取消共用**、
**設為開啟清單時的預設**／**取消預設**，以及**刪除查詢**。

| 規則 | 說明 |
|------|------|
| 個人或共用 | 查詢屬於建立者個人，直到建立者把它分享給公司所有使用者 |
| 誰能修改 | 只有建立者能覆寫、重新命名、分享或刪除；任何人都能把別人共用的查詢另存成自己的 |
| 預設 | 每位使用者在每張表單最多選一筆開啟清單時套用的查詢，可以是別人共用的 |
| 權限 | 每個動作都需要表單的 `Read` 權限 |
| 相對日期 | 「本月」這類期間照原樣儲存，每次執行時才換算 |

查詢存的是使用者輸入的內容，不是轉換過的篩選條件，所以窗格能原樣顯示。表單定義改了之後，欄位已不提供、運算子已不允許，
或值已不符合欄位型別的條件列會被略過，窗格會寫出略過了哪些；其他條件照常套用。選項值的顯示文字取自目前的定義，
所以其他語系的使用者共用的查詢，會以讀者自己的語系顯示。

查詢存在 `st_user_query`，每位使用者的預設存在 `st_user_query_default`，兩張都在公司資料庫
（[框架保留名稱](../reference/framework-reserved-names.md)）。定義檔早於這兩張表的專案，要從框架預設把它們加進自己的
`DbCategorySettings.xml`；加入前，清單照常開啟，只是沒有儲存的查詢。

在自己的畫面裡，同樣的元件都是公開的：`UserQueryCodec`（`Polhem.UI.Core`）把條件列寫成儲存的文字，並依目前提供的欄位讀回；
`IUserQueryConnector`（`Polhem.Api.Contracts`）透過表單的 business object 列出、儲存、刪除與設定預設（`GetUserQueries`、
`SaveUserQuery`、`DeleteUserQuery`、`SetDefaultUserQuery`，見 [API 方法參考](../api/api-method-reference.md)）；
`CriteriaPanel.HeaderContent` 是條件列上方的插槽，`ListView` 把下拉選單放在這裡。Blazor 窗格有同樣的插槽
`CriteriaPanelBase.HeaderContent`，`FormPage` 把 `UserQueryBar` 放在這裡。

## 8. 不透過面板直接呼叫 GetList

自己做畫面的用戶端，直接在 `GetList` 的 `filter` 送出 `FilterNode`。上述條件對應到 `FilterCondition` 和 `FilterGroup`
節點；wire 格式請參閱 [JSON-RPC 前端整合指引](../api/jsonrpc-frontend-integration.md)，文字運算子的規則同第 5 節。
