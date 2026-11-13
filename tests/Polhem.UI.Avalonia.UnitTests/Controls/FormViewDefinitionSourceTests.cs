using System.ComponentModel;
using System.Data;
using Avalonia.LogicalTree;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages.Form;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Avalonia.Controls;
using Polhem.UI.Avalonia.UnitTests.Controls.Dialogs;
using Polhem.UI.Avalonia.Views;
using Polhem.UI.Core.Fields;
using Polhem.UI.Core.Definitions;
using Polhem.UI.Core.Editing;

namespace Polhem.UI.Avalonia.UnitTests.Controls
{
    /// <summary>
    /// A <see cref="FormView"/> that fetches its definitions through its own <see cref="FormView.DefinitionSource"/>:
    /// the schema, the layout and the optional rounding masters, and how a failure on each of those paths reaches the
    /// user. Also the default related-record view and its overlay dialog.
    /// </summary>
    /// <remarks>
    /// The view's own <see cref="FormView.DefinitionSource"/> and <see cref="FormView.FormConnector"/> are set, so the
    /// services registered in <c>UIServices</c> are not needed; none are registered in this test assembly.
    /// </remarks>
    public class FormViewDefinitionSourceTests
    {
        private const string ProgId = "Category";

        private static FormSchema BuildSchema()
        {
            var schema = new FormSchema(ProgId, "Categories");
            var master = schema.Tables!.Add(ProgId, "Categories");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            master.Fields.Add("sys_name", "Name", FieldDbType.String);
            var detail = schema.Tables.Add("CategoryNote", "Notes");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields.Add(SysFields.MasterRowId, "Category", FieldDbType.Guid);
            detail.Fields.Add("note", "Note", FieldDbType.String);
            return schema;
        }

        private static DataSet BuildDataSet(Guid rowId)
        {
            var dataSet = new DataSet(ProgId);
            var master = new DataTable(ProgId);
            master.Columns.Add(SysFields.RowId, typeof(Guid));
            master.Columns.Add("sys_name", typeof(string));
            master.Rows.Add(rowId, "Beverages");
            dataSet.Tables.Add(master);
            var detail = new DataTable("CategoryNote");
            detail.Columns.Add(SysFields.RowId, typeof(Guid));
            detail.Columns.Add(SysFields.MasterRowId, typeof(Guid));
            detail.Columns.Add("note", typeof(string));
            dataSet.Tables.Add(detail);
            dataSet.AcceptChanges();
            return dataSet;
        }

        private static MessageBar MessageBarOf(FormView view)
            => view.GetLogicalDescendants().OfType<MessageBar>().First();

        [Fact]
        [DisplayName("A view with only a program id fetches its schema and layout from its definition source and loads the record")]
        public async Task ViewAsync_DefinitionSource_ResolvesSchemaAndLayout()
        {
            var source = new FakeDefinitionSource(BuildSchema());
            var view = new FormView { ProgId = ProgId, DefinitionSource = source, FormConnector = new FakeConnector() };

            await view.ViewAsync(Guid.NewGuid());

            Assert.Same(source.Schema, view.Schema);
            Assert.Equal(1, source.LayoutRequests);
            Assert.Equal(SingleFormMode.View, view.FormMode);
            Assert.NotNull(view.DataObject?.MasterRow);
        }

        [Fact]
        [DisplayName("Rounding masters the source refuses or cannot provide are left out instead of failing the form")]
        public async Task ViewAsync_RoundingMastersFail_StillLoads()
        {
            var source = new FakeDefinitionSource(BuildSchema())
            {
                CurrencyFailure = new InvalidOperationException("no currency master"),
                UnitFailure = new ForbiddenException("no unit master"),
            };
            var view = new FormView { ProgId = ProgId, DefinitionSource = source, FormConnector = new FakeConnector() };

            await view.ViewAsync(Guid.NewGuid());

            Assert.Equal(SingleFormMode.View, view.FormMode);
            Assert.Equal(1, source.CurrencyRequests);
            Assert.Equal(1, source.UnitRequests);
        }

        [Fact]
        [DisplayName("A schema fetch that fails is reported on the view and nothing loads")]
        public async Task ViewAsync_SchemaFetchFails_ReportsError()
        {
            var source = new FakeDefinitionSource(BuildSchema()) { SchemaFailure = new InvalidOperationException("schema is gone") };
            var view = new FormView { ProgId = ProgId, DefinitionSource = source, FormConnector = new FakeConnector() };

            await view.ViewAsync(Guid.NewGuid());

            Assert.Null(view.DataObject);
            Assert.Contains("schema is gone", MessageBarOf(view).Text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A schema fetch cancelled by the caller's token is passed on to the caller")]
        public async Task ViewAsync_SchemaFetchCancelled_Throws()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var source = new FakeDefinitionSource(BuildSchema()) { SchemaFailure = new OperationCanceledException(cts.Token) };
            var view = new FormView { ProgId = ProgId, DefinitionSource = source, FormConnector = new FakeConnector() };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => view.ViewAsync(Guid.NewGuid(), cts.Token));
        }

        [Fact]
        [DisplayName("Without a definition source the schema stays unresolved and nothing loads")]
        public async Task ViewAsync_NoDefinitionSource_LoadsNothing()
        {
            var view = new FormView { ProgId = ProgId, FormConnector = new FakeConnector() };

            await view.ViewAsync(Guid.NewGuid());

            Assert.Null(view.Schema);
            Assert.Null(view.DataObject);
        }

        [Fact]
        [DisplayName("Without a connector and with no connector factory registered, nothing loads")]
        public async Task ViewAsync_NoConnector_LoadsNothing()
        {
            var view = new FormView { ProgId = ProgId, Schema = BuildSchema() };

            await view.ViewAsync(Guid.NewGuid());

            Assert.Null(view.FormConnector);
            Assert.Null(view.DataObject);
        }

        [Fact]
        [DisplayName("Without a layout and without a definition source, the load is refused with the reason")]
        public async Task ViewAsync_NoLayoutNoSource_Throws()
        {
            var view = new FormView { ProgId = ProgId, Schema = BuildSchema(), FormConnector = new FakeConnector() };

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => view.ViewAsync(Guid.NewGuid()));

            Assert.Contains(nameof(IFormDefinitionSource), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("A row added to a detail table of a loaded form is initialized by the live computation")]
        public async Task RowAdded_LoadedForm_IsInitialized()
        {
            var view = new FormView { ProgId = ProgId, DefinitionSource = new FakeDefinitionSource(BuildSchema()), FormConnector = new FakeConnector() };
            await view.EditAsync(Guid.NewGuid());
            var notes = view.DataObject!.DataSet.Tables["CategoryNote"]!;

            var row = notes.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            row["note"] = "first";
            notes.Rows.Add(row);

            Assert.Single(notes.Rows.Cast<DataRow>());
            Assert.Equal("first", notes.Rows[0]["note"]);
        }

        [Fact]
        [DisplayName("A related record whose schema fetch fails is reported on the view")]
        public async Task OpenRelatedRecord_SchemaFetchFails_ReportsError()
        {
            var source = new FakeDefinitionSource(BuildSchema()) { SchemaFailure = new InvalidOperationException("related is gone") };
            var view = new FormView { DefinitionSource = source };

            await view.OpenRelatedRecordAsync(new RelationTarget(ProgId, Guid.NewGuid()));

            Assert.Contains("related is gone", MessageBarOf(view).Text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The default related view shows the related form and carries over the opening view's settings")]
        public void CreateRelatedView_CopiesSettings()
        {
            var source = new FakeDefinitionSource(BuildSchema());
            var view = new ExposedFormView { DefinitionSource = source, DetailEditMode = GridEditMode.InCell, CompactWidthThreshold = 480 };

            var related = view.CreateRelated(source.Schema);

            Assert.Equal(ProgId, related.ProgId);
            Assert.Same(source.Schema, related.Schema);
            Assert.Same(source, related.DefinitionSource);
            Assert.Equal(GridEditMode.InCell, related.DetailEditMode);
            Assert.Equal(480, related.CompactWidthThreshold);
        }

        [Fact]
        [DisplayName("On a host without windows the related view opens as an overlay card until the token closes it")]
        public void ShowRelatedViewAsync_NoWindow_ShowsOverlayUntilCancelled()
        {
            using var scope = new InlineContinuationScope();
            var view = new ExposedFormView();
            var host = new OverlayTestHost(view);
            var related = new FormView();
            using var cts = new CancellationTokenSource();

            var task = view.ShowRelated(related, "Category", cts.Token);

            Assert.Equal("Category", host.Title);
            Assert.Same(related, host.Panel<FormView>());
            cts.Cancel();
            InlineContinuationScope.Completed(task);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("The default related view and dialog reject null arguments")]
        public void RelatedView_NullArguments_Throw()
        {
            var view = new ExposedFormView();

            Assert.Throws<ArgumentNullException>(() => view.CreateRelated(null!));
            Assert.IsType<ArgumentNullException>(view.ShowRelated(null!, "T", default).Exception?.InnerException);
        }

        // Exposes the protected related-view hooks so their default implementations can be called directly.
        private sealed class ExposedFormView : FormView
        {
            public FormView CreateRelated(FormSchema schema) => CreateRelatedView(schema);

            public Task ShowRelated(FormView view, string title, CancellationToken cancellationToken)
                => ShowRelatedViewAsync(view, title, cancellationToken);
        }

        private sealed class FakeDefinitionSource(FormSchema schema) : IFormDefinitionSource
        {
            public FormSchema Schema { get; } = schema;
            public Exception? SchemaFailure { get; init; }
            public Exception? CurrencyFailure { get; init; }
            public Exception? UnitFailure { get; init; }
            public int LayoutRequests { get; private set; }
            public int CurrencyRequests { get; private set; }
            public int UnitRequests { get; private set; }

            public Task<FormSchema> GetFormSchemaAsync(string progId, CancellationToken cancellationToken = default)
                => SchemaFailure is { } failure ? Task.FromException<FormSchema>(failure) : Task.FromResult(Schema);

            public Task<FormLayout> GetFormLayoutAsync(string progId, FormSchema schema, CancellationToken cancellationToken = default)
            {
                LayoutRequests++;
                return Task.FromResult(FormLayoutGenerator.Generate(schema, progId));
            }

            public Task<CurrencySettings?> GetCurrencySettingsAsync(CancellationToken cancellationToken = default)
            {
                CurrencyRequests++;
                return CurrencyFailure is { } failure
                    ? Task.FromException<CurrencySettings?>(failure)
                    : Task.FromResult<CurrencySettings?>(null);
            }

            public Task<UnitSettings?> GetUnitSettingsAsync(CancellationToken cancellationToken = default)
            {
                UnitRequests++;
                return UnitFailure is { } failure
                    ? Task.FromException<UnitSettings?>(failure)
                    : Task.FromResult<UnitSettings?>(null);
            }
        }

        private sealed class FakeConnector()
            : FormApiConnector(Polhem.Api.Client.PolhemApiClient.CreateLocal(Polhem.Tests.Shared.EmptyServiceProvider.Instance), FormViewDefinitionSourceTests.ProgId)
        {
            public override Task<GetDataResponse> GetDataAsync(Guid rowId, CancellationToken cancellationToken = default)
                => Task.FromResult(new GetDataResponse { DataSet = BuildDataSet(rowId) });
        }
    }
}
