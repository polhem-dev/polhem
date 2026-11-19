# List Query Conditions

[繁體中文](../../zh-TW/guides/list-query-conditions.md) · [← Docs Index](../README.md)

> Letting users narrow a form's list with conditions they add one row at a time, and reusing the same panel in a view of your own

---

## Table of Contents

- [1. What the user sees](#1-what-the-user-sees)
- [2. Declaring the fields a list offers](#2-declaring-the-fields-a-list-offers)
- [3. What can be entered for each field](#3-what-can-be-entered-for-each-field)
- [4. How conditions combine](#4-how-conditions-combine)
- [5. Text matching](#5-text-matching)
- [6. Using the panel in your own view](#6-using-the-panel-in-your-own-view)
- [7. Saved queries](#7-saved-queries)
- [8. Calling GetList without the panel](#8-calling-getlist-without-the-panel)

---

## 1. What the user sees

The Avalonia `ListView` and the Blazor `FormPage` (MudBlazor and Fluent UI) show a **Query** button in its toolbar when the form offers query fields. It opens the query
pane, where the user adds conditions one row at a time: **Add condition** lists the fields, and each row shows the
field with a remove button, and below it an operator where the field offers more than one and the value. **Search** (or
Enter) reads the first page of the list with the conditions; **Clear** removes them and reads the whole list again.

The conditions of the last search stay visible above the list as chips, such as "Order date: This month" or "Shipper:
Federal Shipping and 2 more", and the Query button shows how many there are. A chip's ✕ removes that condition and
searches again (a required condition has none); clicking a chip opens the pane at its row; **Clear filters** at the end
removes them all.

Where the pane appears depends on the width of the list:

| Width | The pane |
|-------|----------|
| The list keeps at least 600 pixels beside a 360-pixel pane | Docked on the right; the list narrows, and its height does not change |
| Narrower | Over the right side of the list, closed by a click outside it or after a search |
| Under 600 pixels (a phone) | A card over the whole view, which dims the list; closed after a search |

A list starts with no conditions, or with the user's default saved query ([section 7](#7-saved-queries)).

On the Blazor `FormPage` the pane follows the same widths, with two differences: the pane over the list stays open
after a search, and closes with ✕ or Escape; and on a phone it covers the page instead of dimming it. A column header's
filter is a condition too: it shows as a chip and as a row of the pane, setting the column again replaces that row, and
clearing the conditions clears it.

## 2. Declaring the fields a list offers

`FormSchema.QueryFields` names the fields, comma-separated, like `ListFields` and `LookupFields`. When it is empty the
list's `ListFields` are offered.

```xml
<FormSchema ProgId="Order" ... ListFields="sys_id,order_date,ref_customer_name,ref_employee_name,status,total_amount"
            QueryFields="sys_id,order_date,customer_rowid,ref_customer_name,employee_rowid,shipper_rowid,status,freight,total_amount">
```

- Only master-table fields count. A relation display field such as `ref_customer_name` can be listed; the query joins
  the related table to filter on it.
- List the foreign key (`customer_rowid`) to let the user pick related records through the lookup dialog; list the
  display field (`ref_customer_name`) to let them type part of a name.
- `sys_rowid` and the columns the framework protects (`ProtectedFields`, such as the password hash) are never offered.
- A field whose `SensitiveCategory` the user cannot read is left out of the panel, as its list column is hidden. This
  is a convenience of the front end, not a data boundary; see
  [Permission & Authorization](../security/permission-authorization.md).
- A name that is not a field of the form is reported at build time as
  [POLHEM1004](../reference/analyzer-rules.md); at run time it is skipped.
- The server does not limit a filter to these fields: it accepts any field the table declares that is not protected.
  `QueryFields` decides what the panel offers.

DefineEditor edits the attribute with the same field picker as `ListFields`, and the Excel form workbook has a column
for it ([Modeling Forms in Excel](excel-form-modeling.md)).

## 3. What can be entered for each field

The kind of a field decides its editor and operators. It is derived from the field definition:

| Field | Kind | Editor | Operators |
|-------|------|--------|-----------|
| `RelationProgId` set | Relation | Records picked through the lookup dialog, shown as removable chips | is any of |
| `ListItems` or `LangEnumName` set | Choice | A check box per value | is any of |
| String named `sys_id` or ending in `_id` | Code | From and to | between, starts with, contains, equals |
| Other string | Text | A text box | contains, starts with, equals |
| Boolean | Yes / no | Any, Yes, No | equals |
| Number | Number | From and to, or one number | between, equals |
| Date | Date | From and to, or one day, with periods | between, equals |
| DateTime | Date-time | Entered by day, like a date | between, equals |

Long text (`Text`), binary data, a time of day and a `Guid` that names no related program offer no condition.

The **Period** menu of a date range offers today, yesterday, this week, last week, this month, last month, this quarter,
this year and the last 7 or 30 days. A period stays relative: it is resolved each time the query runs, against today in
the signed-in user's time zone, and the week starts on the day the user's culture starts it.

A relation to employees (`RelationProgId="Employee"`, the framework's `st_employee` form) and a field marked as the
owner column (`ScopeRole="Owner"`) also offer **Me**, beside **Pick…**: the signed-in user, alone or with picked
records. Me stays relative as well. Each time the query runs it becomes the user's row id and the row id of the employee
linked to the user in the entered company (`st_employee.user_rowid`), the same pair the `Own` record scope compares an
owner column with, so "my orders" means the orders of whoever applies the query. A user with neither id known matches
no record. The front end gets the two ids from the `EnterCompany` response (`IUserContext.Identity`).

## 4. How conditions combine

- Every row must match: rows are combined with AND.
- The same field may appear in more than one row, for example a name that contains "A" and contains "B".
- Several values in one row match any of them, so "status is any of Draft, Confirmed" needs no OR.
- Either end of a range may be left empty.
- A date-time range covers its last day whole: it reads up to, not including, the next day.
- The conditions in effect are those of the last search. Paging and sorting keep them, even while the user edits the
  rows; the edits apply at the next search.

## 5. Text matching

In the text operators (contains, starts with, ends with) only `%` is a wildcard: `abc%001` matches `abc-001` and
`abc_x_001`. Everything else matches itself, including `_` and, on SQL Server, `[`, so a code such as `abc_001` does not
also match `abcd001`.

Whether text matching ignores case depends on the database: SQL Server and MySQL follow the column's collation (their
usual collations ignore case), SQLite ignores case for ASCII letters only, and PostgreSQL and Oracle match case.

## 6. Using the panel in your own view

The panel and its model are public, so a view of your own, such as a report, hosts them the way `ListView` does:

| Type | Package | Role |
|------|---------|------|
| `CriteriaField` | `Polhem.UI.Core` | A field a condition can be entered for; `FromQueryFields` returns a form's |
| `QueryCriteria`, `CriteriaRow` | `Polhem.UI.Core` | The rows, with values already converted to the field's type |
| `GetListFilterConverter` | `Polhem.UI.Core` | Turns the rows into the `FilterNode` of a list request |
| `CriteriaPanel` | `Polhem.UI.Avalonia` | The Avalonia control that edits the rows |
| `CriteriaPanelBase` | `Polhem.Web.Blazor` | The rules of the Blazor pane; `CriteriaPanel` in `Polhem.Web.Blazor.Mud` and in `Polhem.Web.Blazor.Fluent` draws it with MudBlazor or Fluent UI |
| `ColumnFilterCriteria` | `Polhem.UI.Core` | Keeps a list's column filters as rows of its `QueryCriteria` |

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

`SearchRequested` is raised only when every required row has a value; otherwise the panel names the empty ones. A
required row is a convenience of the screen: a backend method that needs a value checks it itself, because a caller can
skip the screen.

A backend method other than `GetList`, such as a report, converts the same rows into its own parameters instead of
using `GetListFilterConverter`.

## 7. Saved queries

Users can save the conditions on screen under a name and apply them again, for one form at a time. The saved queries
sit at the top of the query pane: a picker that applies one and searches, a `*` beside it when the conditions were
changed since, and a **⋯** menu with **Save query**, **Save as new query…**, **Rename…**, **Share with everyone** /
**Stop sharing**, **Open the list with this query** / **Stop opening the list with this query**, and **Delete query**.

| Rule | Detail |
|------|--------|
| Personal or shared | A query is the user's own until they share it with every user of the company |
| Who may change it | Only the user who created a query may save over it, rename it, share it or delete it; anyone may save another user's shared query as a new one of their own |
| Default | Each user picks at most one query per form that the list opens with; it may be one another user shares |
| Permission | Every action needs the form's `Read` permission |
| Relative values | A period such as "this month", and Me, are stored as such and resolved each time the query runs; a version that predates Me leaves its row out |

A query stores what the user entered, not a converted filter, so the pane shows it again exactly. When the form's
definition changes, a row whose field is no longer offered, whose operator the field no longer allows, or whose value
no longer fits the field's type is left out, and the pane names what was left out; the other rows still apply. The text
of a listed value comes from the current definition, so a query shared by a user of another language reads in the
reader's.

The queries are stored in `st_user_query` and each user's default in `st_user_query_default`, both in the company
database ([Framework Reserved Names](../reference/framework-reserved-names.md)). A project whose definitions predate
them adds the two tables to its `DbCategorySettings.xml` from the framework defaults; until then the list opens without
saved queries.

In a view of your own, the same parts are public: `UserQueryCodec` (`Polhem.UI.Core`) writes the rows as the stored text
and reads it back against the fields offered now, `IUserQueryConnector` (`Polhem.Api.Contracts`) lists, saves, deletes
and sets the default through the form's business object (`GetUserQueries`, `SaveUserQuery`, `DeleteUserQuery`,
`SetDefaultUserQuery`, in the [API Method Reference](../api/api-method-reference.md)), and `CriteriaPanel.HeaderContent`
is the slot above the rows where `ListView` puts its picker. The Blazor pane has the same slot,
`CriteriaPanelBase.HeaderContent`, where `FormPage` puts `UserQueryBar`.

## 8. Calling GetList without the panel

A client that builds its own screen sends a `FilterNode` in the `filter` of `GetList` directly. The conditions above are
`FilterCondition` and `FilterGroup` nodes; the wire format is in
[JSON-RPC Frontend Integration](../api/jsonrpc-frontend-integration.md), and the text operators follow section 5.
