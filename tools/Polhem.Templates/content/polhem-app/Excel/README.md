# Excel

Keep the form workbooks of this application here. Each workbook describes one form: its fields, lookups, detail
tables and rules.

1. Run the **Polhem: Open DefineEditor** task in VS Code (or `dotnet tool restore`, then `dotnet polhem-define Define`).
2. In DefineEditor, choose **File → Import Form from Excel** and pick a workbook. DefineEditor lists the definition
   files it will write into `Define/`, writes them, and offers to upgrade the database tables of that form.
3. Import the workbooks in the order you want them in the menu: each import adds its form at the end.

An empty workbook to start from: `dotnet polhem forms template --out Excel/MyForm.xlsx` (the Polhem.Cli tool;
`--lang zh-TW` for Traditional Chinese headings). A form already defined can be exported back to a workbook from
DefineEditor (right-click the form → Export to Excel).
