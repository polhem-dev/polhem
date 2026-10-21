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
/// IMPORTANT: the templates, one per <see cref="FormWorkbookLanguage"/>, are generated from this class. After
/// changing a column, a list or a text, regenerate them (see <c>FormWorkbookTemplateTests</c>); that test fails until
/// every template matches.
/// </remarks>
internal static class FormWorkbookCatalog
{
    public const string FormSheet = "Form";
    public const string TablesSheet = "Tables";
    public const string FieldsSheet = "Fields";
    public const string RulesSheet = "Rules";
    public const string ListsSheet = "Lists";

    /// <summary>
    /// The name of the instructions sheet. It is the only sheet whose name follows the language; the reader never
    /// looks at it.
    /// </summary>
    public static SheetText InstructionsSheet { get; } = new("Instructions", "說明");

    /// <summary>The row of a tabular sheet that holds the headers.</summary>
    public const int LabelRow = 1;

    /// <summary>The hidden row of a tabular sheet that holds the column keys.</summary>
    public const int KeyRow = 2;

    /// <summary>The first data row of a tabular sheet.</summary>
    public const int FirstDataRow = 3;

    /// <summary>The last row a drop-down covers on a tabular sheet.</summary>
    public const int LastValidatedRow = 1000;

    /// <summary>The opening paragraphs of the instructions sheet; the first line is its title.</summary>
    public static IReadOnlyList<SheetText> InstructionLines { get; } =
    [
        new("Form definition workbook", "表單定義活頁簿"),
        SheetText.Empty,
        new("1. On the Form sheet, fill in the program ID, the names and where the form goes in the menu.",
            "1. 在 Form 工作表填寫表單代號、名稱與選單位置。"),
        new("2. On the Tables sheet, list the tables: the first row is the master table, the others are detail tables. A form with only a master table can leave it empty.",
            "2. 在 Tables 工作表列出資料表：第一列是主檔，其餘是明細表。只有主檔時可以不填。"),
        new("3. On the Fields sheet, describe one field per row. System fields and the ref_ fields of relations are added by the import; do not list them.",
            "3. 在 Fields 工作表逐列描述欄位。系統欄位與關聯帶回的 ref_ 欄位不必填，匯入時自動補上。"),
        new("4. If the form needs checks before saving, fill in the Rules sheet.",
            "4. 需要存檔前檢查時，在 Rules 工作表填寫規則。"),
        new("5. Run dotnet polhem forms import <this file> --define <Define folder> to produce the definition files.",
            "5. 執行 dotnet polhem forms import <這個檔案> --define <Define 資料夾> 產生定義檔。"),
        SheetText.Empty,
        new("Row 2 holds the hidden column keys; do not delete it. You may reorder columns, reword the headers or insert columns of your own (the import ignores them).",
            "第 2 列是隱藏的欄位鍵，請勿刪除。可以調整欄順序、修改標題，或插入自己的備註欄（匯入時略過）。"),
        new("Columns with a grey header are advanced settings, collapsed by default; click the + above the columns to expand them.",
            "灰色標題的欄位是進階設定，預設折疊，點欄位上方的 + 展開。"),
    ];

    /// <summary>The headers of the column list on the instructions sheet: sheet, header, key, explanation.</summary>
    public static IReadOnlyList<SheetText> InstructionHeaders { get; } =
    [
        new("Sheet", "工作表"),
        new("Column", "欄位"),
        new("Key", "鍵"),
        new("Explanation", "說明"),
    ];

    /// <summary>The headers of the Form sheet's item, value and explanation columns.</summary>
    public static (SheetText Item, SheetText Value, SheetText Explanation) FormHeaders { get; } =
        (new("Item", "項目"), new("Value", "內容"), new("Explanation", "說明"));

    /// <summary>The Form sheet: one row per item, label in column A, hidden key in column B, value in column C.</summary>
    public static IReadOnlyList<SheetColumn<FormSheetRow>> FormItems { get; } =
    [
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.ProgId), new("Program ID", "程式代號"),
            r => r.Schema.ProgId, (r, v) => r.Schema.ProgId = v,
            new("The unique ID of the form: starts with a letter, no spaces, for example Order. It is also the TableName of the master table and the file name.",
                "表單的唯一代號，英文開頭、不含空白，例如 Order。也是主檔的 TableName 與檔名。")),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.DisplayName), new("Display name (English)", "顯示名稱（英文）"),
            r => r.Schema.DisplayName, (r, v) => r.Schema.DisplayName = v,
            new("The English name of the form.", "表單名稱的英文原文。")),
        SheetColumns.Text<FormSheetRow>("DisplayName_zh_TW", new("Display name (zh-TW)", "顯示名稱（中文）"),
            r => r.ZhDisplayName, (r, v) => r.ZhDisplayName = v,
            new("The Traditional Chinese name of the form.", "表單名稱的繁體中文。"), mapsProperty: false),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.CategoryId), new("Database category", "資料庫範圍"),
            r => r.Schema.CategoryId, (r, v) => r.Schema.CategoryId = v,
            new("Which database holds the data: business data always goes in company. Empty means company.",
                "資料放在哪個資料庫：業務資料一律 company。空白視為 company。"), listName: CategoryIdList),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.ListFields), new("List fields", "清單顯示欄位"),
            r => r.Schema.ListFields, (r, v) => r.Schema.ListFields = v,
            new("The fields the list view shows, separated by commas, for example sys_id,order_date,status.",
                "清單畫面要顯示的欄位名稱，以逗號分隔，例如 sys_id,order_date,status。")),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.LookupFields), new("Lookup fields", "查詢視窗欄位"),
            r => r.Schema.LookupFields, (r, v) => r.Schema.LookupFields = v,
            new("The fields the lookup dialog shows when another form relates to this one, separated by commas.",
                "被其他表單關聯時，查詢視窗顯示的欄位，以逗號分隔。")),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.CurrencyField), new("Currency field", "幣別欄位"),
            r => r.Schema.CurrencyField, (r, v) => r.Schema.CurrencyField = v,
            new("The currency field shared by the whole form.", "整張表單共用的幣別欄位名稱。"), advanced: true),
        SheetColumns.Text<FormSheetRow>(nameof(FormSchema.PermissionModelId), new("Permission model", "權限模型"),
            r => r.Schema.PermissionModelId, (r, v) => r.Schema.PermissionModelId = v,
            new("The ID of a model in PermissionModels.xml.", "對應 PermissionModels.xml 的模型代號。"), advanced: true),
        SheetColumns.Text<FormSheetRow>("MenuFolderId", new("Menu folder ID", "選單資料夾代號"),
            r => r.Menu.FolderId, (r, v) => r.Menu.FolderId = v,
            new("The menu folder the form goes in, for example transactions. A missing folder is created. Empty means the top level.",
                "放在哪個選單資料夾，例如 transactions。資料夾不存在時會新建。空白表示放在最上層。"), mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuFolderCaption", new("Menu folder name (English)", "選單資料夾名稱（英文）"),
            r => r.Menu.FolderCaption, (r, v) => r.Menu.FolderCaption = v,
            new("The English name of the folder, used when it is created.", "新建資料夾時使用的英文名稱。"), mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuFolderCaption_zh_TW", new("Menu folder name (zh-TW)", "選單資料夾名稱（中文）"),
            r => r.Menu.FolderCaptionZhTw, (r, v) => r.Menu.FolderCaptionZhTw = v,
            new("The Traditional Chinese name of the folder.", "資料夾的繁體中文名稱。"), mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuCaption", new("Menu name (English)", "選單名稱（英文）"),
            r => r.Menu.EntryCaption, (r, v) => r.Menu.EntryCaption = v,
            new("The English name of the menu entry. Empty means the display name.", "選單項目的英文名稱。空白時使用顯示名稱。"), mapsProperty: false),
        SheetColumns.Text<FormSheetRow>("MenuCaption_zh_TW", new("Menu name (zh-TW)", "選單名稱（中文）"),
            r => r.Menu.EntryCaptionZhTw, (r, v) => r.Menu.EntryCaptionZhTw = v,
            new("The Traditional Chinese name of the menu entry. Empty means the zh-TW display name.",
                "選單項目的繁體中文名稱。空白時使用中文顯示名稱。"), mapsProperty: false),
    ];

    public static IReadOnlyList<SheetColumn<TableSheetRow>> TableColumns { get; } =
    [
        SheetColumns.Text<TableSheetRow>(nameof(FormTable.TableName), new("Table name", "資料表代號"),
            r => r.Table.TableName, (r, v) => r.Table.TableName = v,
            new("The first row is the master table; its TableName must equal the program ID and is filled in when empty. The other rows are detail tables, for example OrderDetail.",
                "第一列是主檔，其 TableName 必須與程式代號相同，空白時自動帶入。其餘列是明細表，例如 OrderDetail。"), width: 18),
        SheetColumns.Text<TableSheetRow>(nameof(FormTable.DbTableName), new("Database table name", "資料庫表名"),
            r => r.Table.DbTableName, (r, v) => r.Table.DbTableName = v,
            new("Empty means ft_ plus the table name in lower case with underscores, for example ft_order_detail.",
                "空白時依慣例產生 ft_ 加上資料表代號的小寫底線形式，例如 ft_order_detail。"), width: 20),
        SheetColumns.Text<TableSheetRow>(nameof(FormTable.DisplayName), new("Display name (English)", "顯示名稱（英文）"),
            r => r.Table.DisplayName, (r, v) => r.Table.DisplayName = v, width: 20),
        SheetColumns.Text<TableSheetRow>("DisplayName_zh_TW", new("Display name (zh-TW)", "顯示名稱（中文）"),
            r => r.ZhDisplayName, (r, v) => r.ZhDisplayName = v, width: 20, mapsProperty: false),
    ];

    public static IReadOnlyList<SheetColumn<FieldSheetRow>> FieldColumns { get; } =
    [
        SheetColumns.Text<FieldSheetRow>(nameof(FormTable.TableName), new("Table", "所屬資料表"),
            r => r.TableName, (r, v) => r.TableName = v,
            new("A table name from the Tables sheet; empty means the master table.", "填 Tables 工作表的資料表代號；空白表示主檔。"),
            width: 16, mapsProperty: false),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.FieldName), new("Field name", "欄位名稱"),
            r => r.Field.FieldName, (r, v) => r.Field.FieldName = v,
            new("Lower case with underscores, for example order_date. System fields (sys_no, sys_rowid, sys_master_rowid) are added by the import; do not list them.",
                "英文小寫加底線，例如 order_date。系統欄位（sys_no、sys_rowid、sys_master_rowid）不必填，匯入時自動補上。"), width: 20),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.Caption), new("Caption (English)", "標題（英文）"),
            r => r.Field.Caption, (r, v) => r.Field.Caption = v, width: 18),
        SheetColumns.Text<FieldSheetRow>("Caption_zh_TW", new("Caption (zh-TW)", "標題（中文）"),
            r => r.ZhCaption, (r, v) => r.ZhCaption = v, width: 16, mapsProperty: false),
        SheetColumns.Enum<FieldSheetRow, FieldDbType>(nameof(FormField.DbType), new("Data type", "資料型別"),
            r => r.Field.DbType, (r, v) => r.Field.DbType = v, FieldDbType.String,
            new("String text, Integer whole number, Decimal number, Currency amount, Date, Boolean yes/no, Guid relation key, and more.",
                "String 文字、Integer 整數、Decimal 小數、Currency 金額、Date 日期、Boolean 是否、Guid 關聯鍵…"), alwaysWrite: true),
        SheetColumns.Int<FieldSheetRow>(nameof(FormField.MaxLength), new("Max length", "最大長度"),
            r => r.Field.MaxLength, (r, v) => r.Field.MaxLength = v,
            new("The maximum length of a String field. Required when the field is a table column; the import stops without it.",
                "String 欄位的長度上限。會建成資料表欄位時必填，沒填會無法匯入。")),
        SheetColumns.Bool<FieldSheetRow>(nameof(FormField.Required), new("Required", "必填"),
            r => r.Field.Required, (r, v) => r.Field.Required = v, false),
        SheetColumns.Bool<FieldSheetRow>(nameof(FormField.ReadOnly), new("Read-only", "唯讀"),
            r => r.Field.ReadOnly, (r, v) => r.Field.ReadOnly = v, false),
        SheetColumns.Bool<FieldSheetRow>(nameof(FormField.Visible), new("Visible", "顯示"),
            r => r.Field.Visible, (r, v) => r.Field.Visible = v, true,
            new("Empty means visible; N hides the field.", "空白表示顯示；填 N 隱藏。")),
        SheetColumns.Enum<FieldSheetRow, ControlType>(nameof(FormField.ControlType), new("Control", "輸入控制項"),
            r => r.Field.ControlType, (r, v) => r.Field.ControlType = v, ControlType.Auto,
            new("Empty means chosen from the data type. A field with list items uses DropDownEdit.",
                "空白依資料型別自動決定。有下拉選項時填 DropDownEdit。")),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DefaultValue), new("Default value", "預設值"),
            r => r.Field.DefaultValue, (r, v) => r.Field.DefaultValue = v),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.ListItems), new("List items", "下拉選項"),
            r => CellLists.FormatListItems(r.Field.ListItems), (r, v) => CellLists.ParseListItems(v, r.Field.ListItems!),
            new("Separated by semicolons, value=text, for example Draft=Draft; Confirmed=Confirmed. Write only the value when it equals the text.",
                "以分號分隔，值=顯示文字，例如 Draft=Draft; Confirmed=Confirmed。值與文字相同時只寫值。"), width: 28),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.RelationProgId), new("Related form", "關聯表單"),
            r => r.Field.RelationProgId, (r, v) => r.Field.RelationProgId = v,
            new("When the field picks a record of another form, the program ID of that form, for example Customer. Name the field customer_rowid; the import adds ref_customer_id and ref_customer_name.",
                "這個欄位挑選另一張表單的資料時，填那張表單的程式代號，例如 Customer。欄位名稱慣例為 customer_rowid，匯入時自動補上 ref_customer_id、ref_customer_name。"), width: 16),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.RelationFieldMappings), new("Relation mappings", "關聯帶回欄位"),
            r => CellLists.FormatMappings(r.Field.RelationFieldMappings), (r, v) => CellLists.ParseMappings(v, r.Field.RelationFieldMappings!),
            new("Empty means the convention. To customize, write source=target separated by semicolons, for example sys_id=ref_customer_id; sys_name=ref_customer_name.",
                "空白時依慣例產生。自訂時寫 來源=目的，以分號分隔，例如 sys_id=ref_customer_id; sys_name=ref_customer_name。"), width: 28),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.ValueExpression), new("Formula", "計算公式"),
            r => r.Field.ValueExpression, (r, v) => r.Field.ValueExpression = v,
            new("A value computed from other fields, for example quantity * unit_price. A field with a formula is usually read-only too.",
                "由其他欄位算出的值，例如 quantity * unit_price。有公式的欄位通常也設為唯讀。"), width: 28),
        SheetColumns.Enum<FieldSheetRow, NumberKind>(nameof(FormField.NumberKind), new("Number kind", "數值種類"),
            r => r.Field.NumberKind, (r, v) => r.Field.NumberKind = v, NumberKind.None,
            new("Decides the decimal places and rounding: Quantity, Amount, UnitPrice, and more.",
                "決定小數位數與進位方式：Quantity 數量、Amount 金額、UnitPrice 單價…")),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.UnitField), new("Unit field", "單位欄位"),
            r => r.Field.UnitField, (r, v) => r.Field.UnitField = v,
            new("Required when the number kind is Quantity or Weight: the field on the same table that holds the unit.",
                "數值種類為 Quantity 或 Weight 時必填，填同一張表上存放單位的欄位名稱。")),
        SheetColumns.Int<FieldSheetRow>(nameof(FormField.Width), new("Display width", "顯示寬度"),
            r => r.Field.Width, (r, v) => r.Field.Width = v),

        SheetColumns.Enum<FieldSheetRow, FieldType>(nameof(FormField.Type), new("Field type", "欄位種類"),
            r => r.Field.Type, (r, v) => r.Field.Type = v, FieldType.DbField,
            new("Empty means a database field. RelationField is a field brought back by a relation; VirtualField is a computed field that is not stored.",
                "空白表示實體欄位。RelationField 是關聯帶回的欄位，VirtualField 是不存資料庫的計算欄位。"), advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DefaultValueExpression), new("Default value formula", "預設值公式"),
            r => r.Field.DefaultValueExpression, (r, v) => r.Field.DefaultValueExpression = v, advanced: true, width: 24),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DisplayFormat), new("Display format", "顯示格式"),
            r => r.Field.DisplayFormat, (r, v) => r.Field.DisplayFormat = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.NumberFormat), new("Number format", "數字格式"),
            r => r.Field.NumberFormat, (r, v) => r.Field.NumberFormat = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.CurrencyField), new("Currency field", "幣別欄位"),
            r => r.Field.CurrencyField, (r, v) => r.Field.CurrencyField = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.LookupProgId), new("Lookup form", "查詢表單"),
            r => r.Field.LookupProgId, (r, v) => r.Field.LookupProgId = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.LookupFieldMappings), new("Lookup mappings", "查詢帶回欄位"),
            r => CellLists.FormatMappings(r.Field.LookupFieldMappings), (r, v) => CellLists.ParseMappings(v, r.Field.LookupFieldMappings!),
            new("Write source=target, separated by semicolons.", "寫 來源=目的，以分號分隔。"), advanced: true, width: 28),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.DisplayFields), new("Display fields", "顯示欄位"),
            r => r.Field.DisplayFields, (r, v) => r.Field.DisplayFields = v, advanced: true),
        SheetColumns.Text<FieldSheetRow>(nameof(FormField.LangEnumName), new("Language enum", "語系列舉"),
            r => r.Field.LangEnumName, (r, v) => r.Field.LangEnumName = v, advanced: true),
        SheetColumns.Enum<FieldSheetRow, ScopeRole>(nameof(FormField.ScopeRole), new("Scope role", "資料範圍角色"),
            r => r.Field.ScopeRole, (r, v) => r.Field.ScopeRole = v, ScopeRole.None, advanced: true),
        SheetColumns.Enum<FieldSheetRow, SensitiveCategory>(nameof(FormField.SensitiveCategory), new("Sensitive category", "敏感資料類別"),
            r => r.Field.SensitiveCategory, (r, v) => r.Field.SensitiveCategory = v, SensitiveCategory.None, advanced: true),
    ];

    public static IReadOnlyList<SheetColumn<RuleSheetRow>> RuleColumns { get; } =
    [
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.RuleId), new("Rule ID", "規則代號"),
            r => r.Rule.RuleId, (r, v) => r.Rule.RuleId = v, width: 20),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.TargetTable), new("Table checked", "檢查的資料表"),
            r => r.Rule.TargetTable, (r, v) => r.Rule.TargetTable = v,
            new("Empty means the master table; a detail table name checks every detail row.",
                "空白表示主檔；填明細表代號時，每一筆明細都檢查。"), width: 16),
        SheetColumns.Enum<RuleSheetRow, FormRuleTrigger>(nameof(FormRule.Trigger), new("Trigger", "觸發時機"),
            r => r.Rule.Trigger, (r, v) => r.Rule.Trigger = v, FormRuleTrigger.BeforeSave,
            new("Empty means before saving.", "空白表示存檔前。")),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.When), new("Applies when", "適用條件"),
            r => r.Rule.When, (r, v) => r.Rule.When = v,
            new("The rule is checked only while this condition holds; empty means always.",
                "只在這個條件成立時檢查；空白表示一律檢查。"), width: 24),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.Condition), new("Condition that must hold", "必須成立的條件"),
            r => r.Rule.Condition, (r, v) => r.Rule.Condition = v,
            new("Saving is refused when it does not hold, for example quantity > 0.", "不成立時擋下存檔，例如 quantity > 0。"), width: 28),
        SheetColumns.Text<RuleSheetRow>(nameof(FormRule.Message), new("Error message (English)", "錯誤訊息（英文）"),
            r => r.Rule.Message, (r, v) => r.Rule.Message = v, width: 32),
        SheetColumns.Text<RuleSheetRow>("Message_zh_TW", new("Error message (zh-TW)", "錯誤訊息（中文）"),
            r => r.ZhMessage, (r, v) => r.ZhMessage = v, width: 32, mapsProperty: false),
        SheetColumns.Bool<RuleSheetRow>(nameof(FormRule.Enabled), new("Enabled", "啟用"),
            r => r.Rule.Enabled, (r, v) => r.Rule.Enabled = v, true,
            new("Empty means enabled; N turns the rule off.", "空白表示啟用；填 N 停用。")),
        SheetColumns.Int<RuleSheetRow>(nameof(FormRule.Order), new("Order", "順序"),
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
