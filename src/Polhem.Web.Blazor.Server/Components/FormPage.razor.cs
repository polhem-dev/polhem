using System.Globalization;
using Polhem.Api.Client;
using Polhem.Api.Client.Definitions;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.Controls;
using Polhem.Web.Blazor.Server.DataObjects;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Code-behind for <c>FormPage.razor</c>. Wires <see cref="Polhem.Web.Blazor.Server.Components.DynamicGrid"/>
    /// (list view) to <see cref="Polhem.Web.Blazor.Server.Controls.FormLayoutPresenter"/> (master record and detail grids) via a shared
    /// <see cref="FormDataObject"/>: selecting a list row drives
    /// <see cref="FormDataObject.LoadAsync"/>; the toolbar buttons fan out to
    /// <see cref="FormDataObject.NewAsync"/> / <c>SaveAsync</c> / <c>DeleteAsync</c>.
    /// </summary>
    /// <remarks>
    /// FormPage calls with the circuit's <see cref="PolhemApiClient"/>, so it acts as whoever signed in
    /// on that client (for example through a <see cref="PolhemLoginPanel"/>, which calls
    /// <see cref="SystemApiConnector.LoginAsync"/>). Before a sign-in it calls anonymously, and the
    /// backend BO methods being called must then declare
    /// <see cref="Polhem.Definition.Security.ApiAccessRequirement.Anonymous"/> themselves.
    /// <para>
    /// Captions, the toolbar text and the display of numbers and dates follow the circuit's
    /// <see cref="CultureInfo.CurrentUICulture"/> and <see cref="CultureInfo.CurrentCulture"/>; the
    /// captions do because the page assembles its definitions through a
    /// <see cref="FormDefinitionLoader"/> (see <see cref="DefinitionLoader"/>). A
    /// host applies the signed-in user's culture — <see cref="Polhem.Api.Core.Messages.System.LoginResponse.Culture"/>
    /// — through ASP.NET Core request localization (for example a culture cookie set after sign-in),
    /// because a circuit's culture is fixed when it starts. The page localizes when it initializes;
    /// a language switch takes effect on pages opened afterwards.
    /// </para>
    /// <para>
    /// The record is edited in <see cref="SingleFormMode.Add"/> after New, in <see cref="SingleFormMode.Edit"/>
    /// after a row is loaded or saved, and shown in <see cref="SingleFormMode.View"/> while there is no record,
    /// so each field's <see cref="LayoutField.AllowEditModes"/> applies.
    /// </para>
    /// <para>
    /// Save checks the fields marked <see cref="FormField.Required"/> first, through
    /// <see cref="RequiredFieldCheck"/>. When any is empty the page names them above the toolbar and
    /// sends nothing; the form stays open for the user to fill them in.
    /// </para>
    /// <para>
    /// On a viewport narrower than <see cref="CompactWidthThreshold"/> the form switches to its compact layout
    /// (see <see cref="FormLayoutPresenter.IsCompact"/>), detected by a <see cref="CompactViewportObserver"/>.
    /// Setting <see cref="IsCompact"/> fixes the layout instead, and the page then measures nothing.
    /// </para>
    /// </remarks>
    public sealed partial class FormPage : ComponentBase
    {
        private FormSchema? _schema;
        private FormLayout? _formLayout;
        private LayoutGrid? _listLayout;
        private FormDataObject? _dataObject;
        private FormLiveComputation? _liveComputation;
        private RoundingContext? _roundingContext;
        private DataTable? _listRows;
        private string? _error;
        // A message for the user that leaves the form on screen, unlike `_error`, which replaces it.
        private string? _notice;
        private bool _isInitializing = true;
        private bool _isBusy;
        // Whether the record on screen came from New and has not been saved yet.
        private bool _isNewRecord;
        private bool _isViewportCompact;

        /// <summary>
        /// Gets or sets the program identifier (e.g. "Employee"). Drives the
        /// FormSchema lookup and the connector creation.
        /// </summary>
        [Parameter, EditorRequired]
        public string ProgId { get; set; } = string.Empty;

        [Inject]
        private PolhemApiClient Client { get; set; } = default!;

        [Inject]
        private PolhemBlazorOptions Options { get; set; } = default!;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <summary>
        /// Gets or sets the assembler that turns raw definitions into a localized schema and a
        /// runtime layout, for this page only. <c>null</c> — the default — uses one over the circuit's
        /// client, unless the host
        /// turned off <see cref="PolhemBlazorOptions.UseDefinitionLoader"/>; with neither, the page
        /// renders the schema and the layout exactly as stored.
        /// </summary>
        /// <remarks>
        /// A loader localizes the captions in the circuit's UI culture and applies the tenant's
        /// customized layout and the framework's number formats. Pass one here to change how a
        /// single page assembles, for example to give it a
        /// <see cref="FormDefinitionLoader.CompanyAccessor"/>.
        /// </remarks>
        [Parameter]
        public FormDefinitionLoader? DefinitionLoader { get; set; }

        /// <summary>
        /// Gets or sets whether the form renders in its compact layout, or <c>null</c> — the default — to decide
        /// from the viewport width against <see cref="CompactWidthThreshold"/>. A value set here wins over the
        /// viewport.
        /// </summary>
        [Parameter]
        public bool? IsCompact { get; set; }

        /// <summary>
        /// Gets or sets the viewport width, in CSS pixels, below which the form renders in its compact layout
        /// while <see cref="IsCompact"/> is <c>null</c>. Defaults to
        /// <see cref="CompactViewportObserver.DefaultCompactWidthThreshold"/>.
        /// </summary>
        [Parameter]
        public double CompactWidthThreshold { get; set; } = CompactViewportObserver.DefaultCompactWidthThreshold;

        /// <summary>
        /// Gets or sets the detail grids' editing model outside the compact layout, which always edits in
        /// <see cref="GridEditMode.EditForm"/>. Defaults to <see cref="GridEditMode.InCell"/>.
        /// </summary>
        [Parameter]
        public GridEditMode DetailEditMode { get; set; } = GridEditMode.InCell;

        private bool EffectiveIsCompact => IsCompact ?? _isViewportCompact;

        private void OnCompactChanged(bool compact) => _isViewportCompact = compact;

        /// <inheritdoc/>
        protected override async Task OnInitializedAsync()
        {
            if (string.IsNullOrWhiteSpace(ProgId))
            {
                _error = "FormPage.ProgId must be set.";
                _isInitializing = false;
                return;
            }

            try
            {
                var loader = CircuitDefinitions.CreateLoader(Client, Options, DefinitionLoader);
                // Without a loader both definitions are fetched exactly as stored. With one, both
                // layers of language and layout are fetched and assembled, so the captions are in the
                // circuit's language and tenant customization takes effect.
                _schema = await CircuitDefinitions.LoadSchemaAsync(Client, loader, ProgId).ConfigureAwait(true)
                    ?? throw new InvalidOperationException($"No FormSchema definition found for '{ProgId}'.");
                if (loader is null)
                {
                    // A layout is authored at design time, so a missing one is a configuration
                    // error rather than a cue to derive one from the schema.
                    _formLayout = await Client.System
                        .GetDefineAsync<FormLayout>(DefineType.FormLayout, [ProgId])
                        .ConfigureAwait(true)
                        ?? throw new InvalidOperationException(
                            $"No FormLayout definition found for '{ProgId}'. Author one at design time "
                            + $"and save it as 'FormLayout/{ProgId}.FormLayout.xml' under the definition path.");
                }
                else
                {
                    _formLayout = await loader
                        .GetRuntimeLayoutAsync(ProgId, _schema).ConfigureAwait(true);
                }
                _listLayout = _schema.GetListLayout();

                _dataObject = new FormDataObject(_schema, Client.Form(ProgId));
                // Live preview recomputes computed fields as the user edits. It is subscribed once because the
                // data object keeps these events across DataSet replacements on Load, New and Save. The
                // rounding context (company decimals, currency and unit masters) aligns previews and the
                // numeric editors to the server, which still rounds authoritatively on save.
                _roundingContext = await CircuitDefinitions.LoadRoundingContextAsync(Client, loader).ConfigureAwait(true);
                _liveComputation = new FormLiveComputation(
                    _schema, _roundingContext, timeZoneProvider: () => Client.Session.Credentials.UserTimeZoneId);
                _liveComputation.Degraded += OnLiveComputationDegraded;
                _dataObject.FieldValueChanged += OnLiveFieldValueChanged;
                _dataObject.RowEditFieldChanged += OnLiveFieldValueChanged;
                _dataObject.RowAdded += OnLiveRowAdded;
                await ReloadListAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private SingleFormMode FormMode
        {
            get
            {
                if (_dataObject?.MasterRow is null) return SingleFormMode.View;
                return _isNewRecord ? SingleFormMode.Add : SingleFormMode.Edit;
            }
        }

        // Recomputes the edited row's computed fields. The write-backs re-raise the change event; the
        // engine's guard makes this handler a no-op for those echoes, so one edit yields one pass. A row
        // under an edit session takes the results into its proposed version, so Cancel rolls them back.
        private void OnLiveFieldValueChanged(object? sender, FieldValueChangedEventArgs e)
        {
            if (_liveComputation is null || _liveComputation.IsRecomputing) return;
            _liveComputation.Recompute(e.TableName, e.FieldName, e.Row);
        }

        // The message stays on screen for the rest of the page's life, because computation stays off until
        // then (see the markup). The edit that failed came from a child editor, so this page re-renders itself.
        // The expression and the exception go to the log only.
        private void OnLiveComputationDegraded(object? sender, EventArgs e)
        {
            if (_liveComputation?.Degradation is not { } degradation) return;
            Services?.GetService<ILogger<FormPage>>()?.LogWarning(degradation.Exception,
                "Live computation of form {ProgId} stopped at table {TableName}, field {FieldName}, expression {Expression}.",
                ProgId, degradation.Table.TableName, degradation.Field?.FieldName, degradation.Expression);
            _ = InvokeAsync(StateHasChanged);
        }

        private string? LiveComputationNotice
            => _liveComputation?.Degradation?.FormatMessage(PolhemBlazorText.GetLocalizer(Services));

        // Fills a newly added detail row's default-value expressions and computes its computed fields.
        private void OnLiveRowAdded(object? sender, RowChangedEventArgs e)
            => _liveComputation?.InitializeNewRow(e.TableName, e.Row);

        private async Task ReloadListAsync()
        {
            var connector = Client.Form(ProgId);
            var response = await connector.GetListAsync().ConfigureAwait(true);
            _listRows = response.Table;
        }

        private async Task OnRowSelectedAsync(Guid rowId)
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(async () =>
            {
                await _dataObject.LoadAsync(rowId).ConfigureAwait(true);
                _isNewRecord = false;
            }).ConfigureAwait(true);
        }

        private async Task OnNewAsync()
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(async () =>
            {
                await _dataObject.NewAsync().ConfigureAwait(true);
                // The server's GetNewData has evaluated the default-value expressions, but computed fields
                // are left to the client. The master row is populated before the event bridge attaches and
                // raises no RowAdded, so it is initialized here.
                if (_dataObject.MasterRow is { } master)
                    _liveComputation?.InitializeNewRow(_dataObject.MasterTable.TableName, master);
                _isNewRecord = true;
            }).ConfigureAwait(true);
        }

        private async Task OnSaveAsync()
        {
            if (_dataObject is null || _isBusy) return;

            // Required fields are checked before the save leaves the circuit, so the user sees every
            // empty one named at once instead of the server's rejection of the first.
            var missing = _schema is { } schema
                ? RequiredFieldCheck.FindMissing(schema, _dataObject.DataSet)
                : [];
            if (missing.Count > 0)
            {
                _notice = RequiredFieldCheck.FormatPrompt(PolhemBlazorText.GetLocalizer(Services), missing);
                return;
            }

            await RunGuardedAsync(async () =>
            {
                await _dataObject.SaveAsync().ConfigureAwait(true);
                _isNewRecord = false;
                await ReloadListAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task OnDeleteAsync()
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(async () =>
            {
                await _dataObject.DeleteAsync().ConfigureAwait(true);
                _isNewRecord = false;
                await ReloadListAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (_isBusy) return;
            _isBusy = true;
            _error = null;
            _notice = null;
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
