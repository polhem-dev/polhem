using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;

namespace Polhem.Definition.Spreadsheet;

/// <summary>
/// The single description of the form workbook: its sheets, their columns in order, and the drop-down lists.
/// The reader and the writer use it to move values, the template generator lays the template out from it,
/// and the coverage gate checks it against the definition types.
/// </summary>
/// <remarks>
/// IMPORTANT: the template is generated from this class. After changing a column or a list, regenerate it
/// (see <c>FormWorkbookTemplateTests</c>); that test fails until the template matches.
/// </remarks>
internal static class FormWorkbookCatalog
{
    public const string InstructionsSheet = "說明";
    public const string FormSheet = "Form";
    public const string TablesSheet = "Tables";
    public const string FieldsSheet = "Fields";
    public const string RulesSheet = "Rules";
    public const string ListsSheet = "Lists";

    /// <summary>The row of a tabular sheet that holds the zh-TW headers.</summary>
    public const int LabelRow = 1;

    /// <summary>The hidden row of a tabular sheet that holds the column keys.</summary>
    public const int KeyRow = 2;

    /// <summary>The first data row of a tabular sheet.</summary>
    public const int FirstDataRow = 3;

    /// <summary>The last row a drop-down covers on a tabular sheet.</summary>
    public const int LastValidatedRow = 1000;

    /// <summary>The Form sheet: one row per item, label in column A, hidden key in column B, value in column C.</summary>
    public static IReadOnlyList<SheetColumn<FormSheetRow>> FormItems { get; } =
    [
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.ProgId), "程式代號",
            r => r.Schema.ProgId, (r, v) => r.Schema.ProgId = v,
            "表單的唯一代號，英文開頭、不含空白，例如 Order。也是主檔的 TableName 與檔名。"),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.DisplayName), "顯示名稱（英文）",
            r => r.Schema.DisplayName, (r, v) => r.Schema.DisplayName = v,
            "表單名稱的英文原文。"),
        SheetColumns.Text<FormSheetRow>("DisplayName_zh_TW", "顯示名稱（中文）",
            r => r.ZhDisplayName, (r, v) => r.ZhDisplayName = v,
            "表單名稱的繁體中文。", mapsProperty: false),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.CategoryId), "資料庫範圍",
            r => r.Schema.CategoryId, (r, v) => r.Schema.CategoryId = v,
            "資料放在哪個資料庫：業務資料一律 company。空白視為 company。", listName: CategoryIdList),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.ListFields), "清單顯示欄位",
            r => r.Schema.ListFields, (r, v) => r.Schema.ListFields = v,
            "清單畫面要顯示的欄位名稱，以逗號分隔，例如 sys_id,order_date,status。"),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.LookupFields), "查詢視窗欄位",
            r => r.Schema.LookupFields, (r, v) => r.Schema.LookupFields = v,
            "被其他表單關聯時，查詢視窗顯示的欄位，以逗號分隔。"),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.CurrencyField), "幣別欄位",
            r => r.Schema.CurrencyField, (r, v) => r.Schema.CurrencyField = v,
            "整張表單共用的幣別欄位名稱。", advanced: true),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.PermissionModelId), "權限模型",
            r => r.Schema.PermissionModelId, (r, v) => r.Schema.PermissionModelId = v,
            "對應 PermissionModels.xml 的模型代號。", advanced: true),
        SheetColumns.Text<FormSheetRow>("MenuFolderId", "選單資料夾代號",
            r => r.Menu.FolderId, (r, v) => r.Menu.FolderId = v,
            "放在哪個選單資料夾，例如 transactions。資料夾不存在時會新建。空白表示放在最上層。", mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuFolderCaption", "選單資料夾名稱（英文）",
            r => r.Menu.FolderCaption, (r, v) => r.Menu.FolderCaption = v,
            "新建資料夾時使用的英文名稱。", mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuFolderCaption_zh_TW", "選單資料夾名稱（中文）",
            r => r.Menu.FolderCaptionZhTw, (r, v) => r.Menu.FolderCaptionZhTw = v,
            "資料夾的繁體中文名稱。", mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuCaption", "選單名稱（英文）",
            r => r.Menu.EntryCaption, (r, v) => r.Menu.EntryCaption = v,
            "選單項目的英文名稱。空白時使用顯示名稱。", mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuCaption_zh_TW", "選單名稱（中文）",
            r => r.Menu.EntryCaptionZhTw, (r, v) => r.Menu.EntryCaptionZhTw = v,
            "選單項目的繁體中文名稱。空白時使用中文顯示名稱。", mapsProperty: false),
    ];

    public static IReadOnlyList<SheetColumn<TableSheetRow>> TableColumns { get; } =
    [
        SheetColumns.Text<TableSheetRow>(nameof(FormTable.TableName), "資料表代號",
            r => r.Table.TableName, (r, v) => r.Table.TableName = v,
            "第一列是主檔，其 TableName 必須與程式代號相同，空白時自動帶入。其餘列是明細表，例如 OrderDetail。", width: 18),
        SheetColumns.Text<TableSheetRow>(nameof(FormTable.DbTableName), "資料庫表名",
            r => r.Table.DbTableName, (r, v) => r.Table.DbTableName = v,
            "空白時依慣例產生 ft_ 加上資料表代號的小寫底線形式，例如 ft_order_detail。", width: 20),
        SheetColumns.Text<TableSheetRow>(nameof(FormTable.DisplayName), "顯示名稱（英文）",
            r => r.Table.DisplayName, (r, v) => r.Table.DisplayName = v, width: 20),
        SheetColumns.Text<TableSheetRow>("DisplayName_zh_TW", "顯示名稱（中文）",
            r => r.ZhDisplayName, (r, v) => r.ZhDisplayName = v, width: 20, mapsProperty: false),
    ];

    public static IReadOnlyList<SheetColumn<FieldSheetRow>> FieldColumns { get; } =
    [
        SheetColumns.Text<FieldSheetRow>(nameof(FormTable.TableName), "所屬資料表",
            r => r.TableName, (r, v) => r.TableName = v,
            "填 Tables 工作表的資料表代號；空白表示主檔。", width: 16, mapsProperty: false),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.FieldName), "欄位名稱",
            r => r.Field.FieldName, (r, v) => r.Field.FieldName = v,
            "英文小寫加底線，例如 order_date。系統欄位（sys_no、sys_rowid、sys_master_rowid）不必填，匯入時自動補上。", width: 20),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.Caption), "標題（英文）",
            r => r.Field.Caption, (r, v) => r.Field.Caption = v, width: 18),
        SheetColumns.Text<FieldSheetRow>("Caption_zh_TW", "標題（中文）",
            r => r.ZhCaption, (r, v) => r.ZhCaption = v, width: 16, mapsProperty: false),
        SheetColumns.Enum<FieldSheetRow, FieldDbType>(nameof(FormField.DbType), "資料型別",
            r => r.Field.DbType, (r, v) => r.Field.DbType = v, FieldDbType.String,
            "String 文字、Integer 整數、Decimal 小數、Currency 金額、Date 日期、Boolean 是否、Guid 關聯鍵…", alwaysWrite: true),
        SheetColumns.Int<FieldSheetRow>(nameof(FormField.MaxLength), "最大長度",
            r => r.Field.MaxLength, (r, v) => r.Field.MaxLength = v,
            "文字欄位的長度上限；空白使用預設。"),
        SheetColumns.Bool<FieldSheetRow>(nameof(FormField.Required), "必填",
            r => r.Field.Required, (r, v) => r.Field.Required = v, false),
        SheetColumns.Bool<FieldSheetRow>(nameof(FormField.ReadOnly), "唯讀",
            r => r.Field.ReadOnly, (r, v) => r.Field.ReadOnly = v, false),
        SheetColumns.Bool<FieldSheetRow>(nameof(FormField.Visible), "顯示",
            r => r.Field.Visible, (r, v) => r.Field.Visible = v, true,
            "空白表示顯示；填 N 隱藏。"),
        SheetColumns.Enum<FieldSheetRow, ControlType>(nameof(FormField.ControlType), "輸入控制項",
            r => r.Field.ControlType, (r, v) => r.Field.ControlType = v, ControlType.Auto,
            "空白依資料型別自動決定。有下拉選項時填 DropDownEdit。"),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DefaultValue), "預設值",
            r => r.Field.DefaultValue, (r, v) => r.Field.DefaultValue = v),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.ListItems), "下拉選項",
            r => CellLists.FormatListItems(r.Field.ListItems), (r, v) => CellLists.ParseListItems(v, r.Field.ListItems!),
            "以分號分隔，值=顯示文字，例如 Draft=Draft; Confirmed=Confirmed。值與文字相同時只寫值。", width: 28),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.RelationProgId), "關聯表單",
            r => r.Field.RelationProgId, (r, v) => r.Field.RelationProgId = v,
            "這個欄位挑選另一張表單的資料時，填那張表單的程式代號，例如 Customer。欄位名稱慣例為 customer_rowid，匯入時自動補上 ref_customer_id、ref_customer_name。", width: 16),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.RelationFieldMappings), "關聯帶回欄位",
            r => CellLists.FormatMappings(r.Field.RelationFieldMappings), (r, v) => CellLists.ParseMappings(v, r.Field.RelationFieldMappings!),
            "空白時依慣例產生。自訂時寫 來源=目的，以分號分隔，例如 sys_id=ref_customer_id; sys_name=ref_customer_name。", width: 28),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.ValueExpression), "計算公式",
            r => r.Field.ValueExpression, (r, v) => r.Field.ValueExpression = v,
            "由其他欄位算出的值，例如 quantity * unit_price。有公式的欄位通常也設為唯讀。", width: 28),
        SheetColumns.Enum<FieldSheetRow, NumberKind>(nameof(FormField.NumberKind), "數值種類",
            r => r.Field.NumberKind, (r, v) => r.Field.NumberKind = v, NumberKind.None,
            "決定小數位數與進位方式：Quantity 數量、Amount 金額、UnitPrice 單價…"),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.UnitField), "單位欄位",
            r => r.Field.UnitField, (r, v) => r.Field.UnitField = v,
            "數值種類為 Quantity 或 Weight 時必填，填同一張表上存放單位的欄位名稱。"),
        SheetColumns.Int<FieldSheetRow>(nameof(FormField.Width), "顯示寬度",
            r => r.Field.Width, (r, v) => r.Field.Width = v),

        SheetColumns.Enum<FieldSheetRow, FieldType>(nameof(FormField.Type), "欄位種類",
            r => r.Field.Type, (r, v) => r.Field.Type = v, FieldType.DbField,
            "空白表示實體欄位。RelationField 是關聯帶回的欄位，VirtualField 是不存資料庫的計算欄位。", advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DefaultValueExpression), "預設值公式",
            r => r.Field.DefaultValueExpression, (r, v) => r.Field.DefaultValueExpression = v, advanced: true, width: 24),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DisplayFormat), "顯示格式",
            r => r.Field.DisplayFormat, (r, v) => r.Field.DisplayFormat = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.NumberFormat), "數字格式",
            r => r.Field.NumberFormat, (r, v) => r.Field.NumberFormat = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.CurrencyField), "幣別欄位",
            r => r.Field.CurrencyField, (r, v) => r.Field.CurrencyField = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.LookupProgId), "查詢表單",
            r => r.Field.LookupProgId, (r, v) => r.Field.LookupProgId = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.LookupFieldMappings), "查詢帶回欄位",
            r => CellLists.FormatMappings(r.Field.LookupFieldMappings), (r, v) => CellLists.ParseMappings(v, r.Field.LookupFieldMappings!),
            "寫 來源=目的，以分號分隔。", advanced: true, width: 28),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DisplayFields), "顯示欄位",
            r => r.Field.DisplayFields, (r, v) => r.Field.DisplayFields = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.LangEnumName), "語系列舉",
            r => r.Field.LangEnumName, (r, v) => r.Field.LangEnumName = v, advanced: true),
        SheetColumns.Enum<FieldSheetRow, ScopeRole>(nameof(FormField.ScopeRole), "資料範圍角色",
            r => r.Field.ScopeRole, (r, v) => r.Field.ScopeRole = v, ScopeRole.None, advanced: true),
        SheetColumns.Enum<FieldSheetRow, SensitiveCategory>(nameof(FormField.SensitiveCategory), "敏感資料類別",
            r => r.Field.SensitiveCategory, (r, v) => r.Field.SensitiveCategory = v, SensitiveCategory.None, advanced: true),
    ];

    public static IReadOnlyList<SheetColumn<RuleSheetRow>> RuleColumns { get; } =
    [
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.RuleId), "規則代號",
            r => r.Rule.RuleId, (r, v) => r.Rule.RuleId = v, width: 20),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.TargetTable), "檢查的資料表",
            r => r.Rule.TargetTable, (r, v) => r.Rule.TargetTable = v,
            "空白表示主檔；填明細表代號時，每一筆明細都檢查。", width: 16),
        SheetColumns.Enum<RuleSheetRow, FormRuleTrigger>(nameof(FormRule.Trigger), "觸發時機",
            r => r.Rule.Trigger, (r, v) => r.Rule.Trigger = v, FormRuleTrigger.BeforeSave,
            "空白表示存檔前。"),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.When), "適用條件",
            r => r.Rule.When, (r, v) => r.Rule.When = v,
            "只在這個條件成立時檢查；空白表示一律檢查。", width: 24),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.Condition), "必須成立的條件",
            r => r.Rule.Condition, (r, v) => r.Rule.Condition = v,
            "不成立時擋下存檔，例如 quantity > 0。", width: 28),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.Message), "錯誤訊息（英文）",
            r => r.Rule.Message, (r, v) => r.Rule.Message = v, width: 32),
        SheetColumns.Text<RuleSheetRow>("Message_zh_TW", "錯誤訊息（中文）",
            r => r.ZhMessage, (r, v) => r.ZhMessage = v, width: 32, mapsProperty: false),
        SheetColumns.Bool<RuleSheetRow>(nameof(FormRule.Enabled), "啟用",
            r => r.Rule.Enabled, (r, v) => r.Rule.Enabled = v, true,
            "空白表示啟用；填 N 停用。"),
        SheetColumns.Int<RuleSheetRow>(nameof(FormRule.Order), "順序",
            r => r.Rule.Order, (r, v) => r.Rule.Order = v),
    ];

    /// <summary>The name of the database-category list on the Lists sheet.</summary>
    public const string CategoryIdList = "CategoryId";

    /// <summary>
    /// The drop-down lists, in the column order of the Lists sheet. Enum lists come from the enum itself.
    /// </summary>
    public static IReadOnlyList<(string Name, IReadOnlyList<string> Values)> Lists { get; } =
    [
        (SheetColumns.YesNoList, ["Y", "N"]),
        (CategoryIdList, [DbCategoryIds.Company, DbCategoryIds.Common, DbCategoryIds.Log]),
        (nameof(FieldDbType), Enum.GetNames<FieldDbType>()),
        (nameof(ControlType), Enum.GetNames<ControlType>()),
        (nameof(NumberKind), Enum.GetNames<NumberKind>()),
        (nameof(FieldType), Enum.GetNames<FieldType>()),
        (nameof(ScopeRole), Enum.GetNames<ScopeRole>()),
        (nameof(SensitiveCategory), Enum.GetNames<SensitiveCategory>()),
        (nameof(FormRuleTrigger), Enum.GetNames<FormRuleTrigger>()),
    ];

    /// <summary>The tabular sheets and their columns, in workbook order.</summary>
    public static IReadOnlyList<(string Sheet, IReadOnlyList<ISheetColumn> Columns)> TabularSheets { get; } =
    [
        (TablesSheet, TableColumns),
        (FieldsSheet, FieldColumns),
        (RulesSheet, RuleColumns),
    ];
}
