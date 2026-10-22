using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;
using Polhem.Definition.Forms;
using Polhem.Definition.Spreadsheet;

namespace Polhem.DefineEditor.UnitTests;

/// <summary>
/// The batch import wizard: its steps, the order it imports in, and where it stops. Workbooks are written to and
/// imported into an isolated temporary folder; the database upgrade is a stand-in that records its calls.
/// </summary>
public sealed class ImportBatchViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "polhem-batch-" + Guid.NewGuid().ToString("N"));
    private readonly string _define;
    private readonly FakeHost _host = new();

    public ImportBatchViewModelTests()
    {
        _define = Path.Combine(_root, "Define");
        Directory.CreateDirectory(_define);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private ImportBatchViewModel NewWizard(params string[] workbooks)
    {
        var wizard = new ImportBatchViewModel(_define, _host);
        wizard.AddFiles(workbooks);
        return wizard;
    }

    /// <summary>Writes a workbook for a master-data form with a code and a name, and the fields given.</summary>
    private string Workbook(string fileName, string progId, params FormField[] fields)
    {
        var schema = new FormSchema(progId, progId);
        var table = schema.Tables!.Add(progId, progId);
        table.Fields!.Add(new FormField("sys_id", "Code", FieldDbType.String) { MaxLength = 20 });
        table.Fields!.Add(new FormField("sys_name", "Name", FieldDbType.String) { MaxLength = 60 });
        foreach (var field in fields) table.Fields!.Add(field);

        var path = Path.Combine(_root, fileName);
        using var stream = File.Create(path);
        FormWorkbookWriter.Write(new FormWorkbookContent(schema), stream);
        return path;
    }

    private string SchemaPath(string progId) => Path.Combine(_define, "FormSchema", progId + ".FormSchema.xml");

    private static async Task RunToEnd(ImportBatchViewModel wizard)
    {
        wizard.Step = ImportBatchStep.Confirm;
        await wizard.RunAsync();
    }

    [Fact]
    [DisplayName("The first step cannot go on until a workbook is listed, and cannot go back")]
    public void FilesStep_Empty_CannotGoNext()
    {
        var wizard = NewWizard();

        Assert.False(wizard.CanGoNext());
        Assert.False(wizard.CanGoBack());
        wizard.AddFiles([Workbook("01-Category.xlsx", "Category")]);
        Assert.True(wizard.CanGoNext());
    }

    [Fact]
    [DisplayName("Added workbooks are listed in file name order, each once")]
    public void AddFiles_ListsInFileNameOrderWithoutDuplicates()
    {
        var second = Workbook("02-Supplier.xlsx", "Supplier");
        var first = Workbook("01-Category.xlsx", "Category");
        var wizard = NewWizard(second);

        wizard.AddFiles([first, second]);

        Assert.Equal(["01-Category.xlsx", "02-Supplier.xlsx"], wizard.Files.Select(f => f.FileName));
    }

    [Fact]
    [DisplayName("Next and Back move between the steps before the run starts")]
    public void NextAndBack_MoveBetweenSteps()
    {
        var wizard = NewWizard(Workbook("01-Category.xlsx", "Category"));

        wizard.NextCommand.Execute(null);
        Assert.Equal(ImportBatchStep.Options, wizard.Step);
        wizard.NextCommand.Execute(null);
        Assert.Equal(ImportBatchStep.Confirm, wizard.Step);
        Assert.Contains("01-Category.xlsx", wizard.ConfirmText, StringComparison.Ordinal);
        wizard.BackCommand.Execute(null);
        wizard.BackCommand.Execute(null);
        Assert.Equal(ImportBatchStep.Files, wizard.Step);
    }

    [Fact]
    [DisplayName("The run imports every workbook in order and upgrades only the tables of each one")]
    public async Task Run_AllValid_ImportsAndUpgradesEachInOrder()
    {
        var wizard = NewWizard(Workbook("01-Category.xlsx", "Category"), Workbook("02-Supplier.xlsx", "Supplier"));

        await RunToEnd(wizard);

        Assert.All(wizard.Files, f => Assert.Equal(ImportBatchItemStatus.Done, f.Status));
        Assert.True(File.Exists(SchemaPath("Category")));
        Assert.True(File.Exists(SchemaPath("Supplier")));
        Assert.Equal([["ft_category"], ["ft_supplier"]], _host.Upgrades);
        Assert.False(wizard.HasFailed);
        Assert.False(wizard.CanGoBack());
        Assert.Equal(SchemaPath("Supplier"), _host.LastSchemaPath);
    }

    [Fact]
    [DisplayName("A workbook with errors stops the run there: nothing of it is written and the ones after it do not run")]
    public async Task Run_WorkbookWithErrors_StopsTheBatch()
    {
        var invalid = new FormField("note", "Note", FieldDbType.String);
        var wizard = NewWizard(
            Workbook("01-Category.xlsx", "Category"),
            Workbook("02-Supplier.xlsx", "Supplier", invalid),
            Workbook("03-Shipper.xlsx", "Shipper"));

        await RunToEnd(wizard);

        Assert.Equal(
            [ImportBatchItemStatus.Done, ImportBatchItemStatus.Failed, ImportBatchItemStatus.NotRun],
            wizard.Files.Select(f => f.Status));
        Assert.False(File.Exists(SchemaPath("Supplier")));
        Assert.False(File.Exists(SchemaPath("Shipper")));
        Assert.Equal([["ft_category"]], _host.Upgrades);
        Assert.True(wizard.HasFailed);
    }

    [Fact]
    [DisplayName("A failed database upgrade stops the run after that workbook was written")]
    public async Task Run_UpgradeFails_StopsTheBatch()
    {
        _host.FailingTable = "ft_category";
        var wizard = NewWizard(Workbook("01-Category.xlsx", "Category"), Workbook("02-Supplier.xlsx", "Supplier"));

        await RunToEnd(wizard);

        Assert.Equal([ImportBatchItemStatus.Failed, ImportBatchItemStatus.NotRun], wizard.Files.Select(f => f.Status));
        Assert.True(File.Exists(SchemaPath("Category")));
        Assert.False(File.Exists(SchemaPath("Supplier")));
    }

    [Fact]
    [DisplayName("Going back after a stopped run lists only the workbooks still to import, ready to start again")]
    public async Task Back_AfterFailure_KeepsTheRemainingWorkbooks()
    {
        var invalid = new FormField("note", "Note", FieldDbType.String);
        var wizard = NewWizard(
            Workbook("01-Category.xlsx", "Category"),
            Workbook("02-Supplier.xlsx", "Supplier", invalid),
            Workbook("03-Shipper.xlsx", "Shipper"));
        await RunToEnd(wizard);

        Assert.True(wizard.CanGoBack());
        wizard.BackCommand.Execute(null);

        Assert.Equal(ImportBatchStep.Files, wizard.Step);
        Assert.Equal(["02-Supplier.xlsx", "03-Shipper.xlsx"], wizard.Files.Select(f => f.FileName));
        Assert.All(wizard.Files, f => Assert.Equal(ImportBatchItemStatus.Pending, f.Status));
    }

    [Fact]
    [DisplayName("Stopping during a run finishes the current workbook, upgrade included, and skips the rest")]
    public async Task RequestStop_FinishesTheCurrentWorkbook()
    {
        var wizard = NewWizard(Workbook("01-Category.xlsx", "Category"), Workbook("02-Supplier.xlsx", "Supplier"));
        _host.DuringUpgrade = wizard.RequestStop;

        await RunToEnd(wizard);

        Assert.Equal([ImportBatchItemStatus.Done, ImportBatchItemStatus.NotRun], wizard.Files.Select(f => f.Status));
        Assert.Single(_host.Upgrades);
        Assert.True(wizard.HasFailed);
    }

    [Fact]
    [DisplayName("A workbook is planned after the ones before it are written, so a relation to an earlier form resolves")]
    public async Task Run_RelationToEarlierForm_IsFound()
    {
        var relation = new FormField("category_rowid", "Category", FieldDbType.String) { RelationProgId = "Category" };
        var wizard = NewWizard(Workbook("01-Category.xlsx", "Category"), Workbook("02-Product.xlsx", "Product", relation));

        await RunToEnd(wizard);

        Assert.All(wizard.Files, f => Assert.Equal(ImportBatchItemStatus.Done, f.Status));
        Assert.DoesNotContain("was not found", wizard.OutputText, StringComparison.Ordinal);
    }

    [Fact]
    [DisplayName("Without the database upgrade the run imports the workbooks and runs no upgrade")]
    public async Task Run_WithoutUpgrade_ImportsOnly()
    {
        var wizard = NewWizard(Workbook("01-Category.xlsx", "Category"));
        wizard.UpgradeDatabase = false;

        await RunToEnd(wizard);

        Assert.Equal(ImportBatchItemStatus.Done, Assert.Single(wizard.Files).Status);
        Assert.Empty(_host.Upgrades);
    }

    [Fact]
    [DisplayName("Regenerating layouts replaces a FormLayout that is already there")]
    public async Task Run_RegenerateLayouts_ReplacesTheLayout()
    {
        var workbook = Workbook("01-Category.xlsx", "Category");
        await RunToEnd(NewWizard(workbook));
        var layoutPath = Path.Combine(_define, "FormLayout", "Category.FormLayout.xml");
        var generated = File.ReadAllText(layoutPath);
        var adjusted = generated.Replace("Caption=\"Code\"", "Caption=\"Category Code\"", StringComparison.Ordinal);
        Assert.NotEqual(generated, adjusted);
        File.WriteAllText(layoutPath, adjusted);

        var wizard = NewWizard(workbook);
        wizard.RegenerateLayouts = true;
        await RunToEnd(wizard);

        Assert.Equal(generated, File.ReadAllText(layoutPath));
    }

    [Fact]
    [DisplayName("When the checks before the run are declined, nothing is imported and the wizard stays on the confirmation")]
    public async Task Run_PrepareDeclined_ImportsNothing()
    {
        _host.Accept = false;
        var wizard = NewWizard(Workbook("01-Category.xlsx", "Category"));

        await RunToEnd(wizard);

        Assert.Equal(ImportBatchStep.Confirm, wizard.Step);
        Assert.False(File.Exists(SchemaPath("Category")));
    }

    private sealed class FakeHost : IImportBatchHost
    {
        public bool Accept { get; set; } = true;
        public string? FailingTable { get; set; }
        public Action? DuringUpgrade { get; set; }
        public List<IReadOnlyList<string>> Upgrades { get; } = [];
        public string? LastSchemaPath { get; private set; }

        public Task<bool> PrepareAsync(bool upgradeDatabase) => Task.FromResult(Accept);

        public Task<ProcessRunResult> UpgradeTablesAsync(
            IReadOnlyList<string> tableNames, Action<string> onLine, CancellationToken cancellationToken)
        {
            Upgrades.Add(tableNames);
            DuringUpgrade?.Invoke();
            onLine("upgraded " + string.Join(", ", tableNames));
            var failed = FailingTable is not null && tableNames.Contains(FailingTable);
            return Task.FromResult(new ProcessRunResult(failed ? 1 : 0, null, false));
        }

        public void Imported(IReadOnlyList<string> writtenFiles, string? lastSchemaPath) => LastSchemaPath = lastSchemaPath;
    }
}
