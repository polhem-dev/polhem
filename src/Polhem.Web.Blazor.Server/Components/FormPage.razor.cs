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
using Polhem.UI.Core;
using Polhem.UI.Core.Connectors;
using Polhem.UI.Core.Fields;
using Polhem.UI.Core.Permissions;
using Polhem.Definition.Settings;
using Polhem.Web.Blazor.Server.DataObjects;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Code-behind for <c>FormPage.razor</c>. Shows one program as a list, a
    /// <see cref="Polhem.Web.Blazor.Server.Components.DynamicGrid"/>, or as one record, a
    /// <see cref="Polhem.Web.Blazor.Server.Controls.FormLayoutPresenter"/> over a <see cref="FormDataObject"/> with its
    /// detail grids, one at a time, as the Avalonia <c>ListView</c> and <c>FormView</c> do.
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
    /// The list's toolbar has View, New, Edit and Delete; a click selects a row and a double-click opens it with
    /// View. The record opens in <see cref="SingleFormMode.View"/> after View, in <see cref="SingleFormMode.Edit"/>
    /// after Edit and in <see cref="SingleFormMode.Add"/> after New, so each field's
    /// <see cref="LayoutField.AllowEditModes"/> applies. A viewed record has Back; an edited one has Save and Cancel.
    /// A save that succeeds, Cancel and Back return to the list, which reloads after a save; Cancel discards the
    /// edits. Delete removes the selected row from the list.
    /// </para>
    /// <para>
    /// Save checks the fields marked <see cref="FormField.Required"/> first, through
    /// <see cref="RequiredFieldCheck"/>. When any is empty the page names them above the toolbar and
    /// sends nothing; the form stays open for the user to fill them in.
    /// </para>
    /// <para>
    /// On a viewed record, double-clicking a relation field opens the record it points at in a
    /// <see cref="RelatedRecordDialog"/>, read-only, when the user may read that form; otherwise the page names the
    /// form in a notice. Closing the dialog leaves the viewed record as it was.
    /// </para>
    /// <para>
    /// On a viewport narrower than <see cref="CompactWidthThreshold"/> the form switches to its compact layout
    /// (see <see cref="FormLayoutPresenter.IsCompact"/>), detected by a <see cref="CompactViewportObserver"/>.
    /// Setting <see cref="IsCompact"/> fixes the layout instead, and the page then measures nothing.
    /// </para>
    /// </remarks>
    public sealed partial class FormPage : ComponentBase, IDisposable
    {
        private FormSchema? _schema;
        private FormLayout? _formLayout;
        private LayoutGrid? _listLayout;
        private FormDataObject? _dataObject;
        private FormLiveComputation? _liveComputation;
        private RoundingContext? _roundingContext;
        private DataTable? _listRows;
        private string? _error;
        // A message for the user that leaves the form on screen, unlike `_error`, which replaces it and is kept for a
        // page that could not load.
        private string? _notice;
        private bool _isInitializing = true;
        private bool _isBusy;
        // Whether the record is on screen instead of the list, and the mode it was opened in.
        private bool _isShowingRecord;
        private SingleFormMode _recordMode = SingleFormMode.View;
        // The list row the toolbar acts on.
        private Guid? _selectedRowId;
        // The related record open in a dialog over the viewed one.
        private RelationTarget? _relation;
        private bool _isViewportCompact;
        private bool _hasLoaded;
        private string? _loadedProgId;
        private FormDefinitionLoader? _loadedDefinitionLoader;
        private int _loadVersion;

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
        /// <remarks>
        /// The page loads when it first renders and again whenever <see cref="ProgId"/> or
        /// <see cref="DefinitionLoader"/> changes, because a host that routes several programs to one page
        /// (<c>/form/{ProgId}</c>) keeps the same component instance and only sets new parameters.
        /// </remarks>
        protected override async Task OnParametersSetAsync()
        {
            if (_hasLoaded
                && string.Equals(_loadedProgId, ProgId, StringComparison.Ordinal)
                && ReferenceEquals(_loadedDefinitionLoader, DefinitionLoader))
                return;

            _hasLoaded = true;
            _loadedProgId = ProgId;
            _loadedDefinitionLoader = DefinitionLoader;
            await LoadFormAsync().ConfigureAwait(true);
        }

        /// <inheritdoc/>
        public void Dispose() => DetachForm();

        private async Task LoadFormAsync()
        {
            // A load that a newer one overtook leaves the page to the newer one.
            int version = ++_loadVersion;
            DetachForm();
            _schema = null;
            _formLayout = null;
            _listLayout = null;
            _dataObject = null;
            _liveComputation = null;
            _roundingContext = null;
            _listRows = null;
            _error = null;
            _notice = null;
            _isShowingRecord = false;
            _recordMode = SingleFormMode.View;
            _selectedRowId = null;
            _relation = null;
            _isInitializing = true;

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
                var schema = await CircuitDefinitions.LoadSchemaAsync(Client, loader, ProgId).ConfigureAwait(true)
                    ?? throw new InvalidOperationException($"No FormSchema definition found for '{ProgId}'.");
                var formLayout = await CircuitDefinitions
                    .LoadFormLayoutAsync(Client, loader, ProgId, schema).ConfigureAwait(true);
                // The layout is this page's own (the loader clones, the direct fetch deserializes), so the
                // sensitive fields are degraded in place.
                var capabilities = UserContext?.Capabilities;
                LayoutCapabilityApplier.Apply(formLayout, schema, capabilities);
                var listLayout = schema.GetListLayout();
                LayoutCapabilityApplier.ApplyGrid(listLayout, schema, capabilities);

                // Live preview recomputes computed fields as the user edits. The rounding context (company
                // decimals, currency and unit masters) aligns previews and the numeric editors to the server,
                // which still rounds authoritatively on save.
                var roundingContext = await CircuitDefinitions
                    .LoadRoundingContextAsync(Client, loader, UserContext?.Company).ConfigureAwait(true);
                var listRows = (await Client.Form(ProgId).GetListAsync(ListSelectFields.For(schema)).ConfigureAwait(true)).Table;
                if (version != _loadVersion) return;

                _schema = schema;
                _formLayout = formLayout;
                _listLayout = listLayout;
                _roundingContext = roundingContext;
                _listRows = listRows;
                _dataObject = new FormDataObject(schema, Client.Form(ProgId));
                _liveComputation = new FormLiveComputation(
                    schema, roundingContext, timeZoneProvider: () => Client.Session.Credentials.UserTimeZoneId);
                // Subscribed once per load because the data object keeps these events across DataSet
                // replacements on Load, New and Save.
                _liveComputation.Degraded += OnLiveComputationDegraded;
                _dataObject.FieldValueChanged += OnLiveFieldValueChanged;
                _dataObject.RowEditFieldChanged += OnLiveFieldValueChanged;
                _dataObject.RowAdded += OnLiveRowAdded;
                _dataObject.FieldValueChangeApplied += OnFieldValueChangeApplied;
                _dataObject.FieldValueChangeFailed += OnFieldValueChangeFailed;
            }
            catch (Exception ex)
            {
                if (version == _loadVersion) _error = ex.Message;
            }
            finally
            {
                if (version == _loadVersion) _isInitializing = false;
            }
        }

        // Unhooks the previous program's data object, so its late callbacks no longer re-render this page.
        private void DetachForm()
        {
            if (_liveComputation is not null)
                _liveComputation.Degraded -= OnLiveComputationDegraded;
            if (_dataObject is null) return;
            _dataObject.FieldValueChanged -= OnLiveFieldValueChanged;
            _dataObject.RowEditFieldChanged -= OnLiveFieldValueChanged;
            _dataObject.RowAdded -= OnLiveRowAdded;
            _dataObject.FieldValueChangeApplied -= OnFieldValueChangeApplied;
            _dataObject.FieldValueChangeFailed -= OnFieldValueChangeFailed;
        }

        // Nullable: a page rendered without AddPolhemBlazor has no context and treats the user as unrestricted.
        private IUserContext? UserContext => Services?.GetService<IUserContext>();

        // Whether the user may run a toolbar command. The backend still checks the call itself.
        private bool Can(PermissionActions action)
            => _schema is null || ElementCapabilityResolver.Default.Can(_schema, action, UserContext?.Capabilities);

        private SingleFormMode FormMode
            => _isShowingRecord && _dataObject?.MasterRow is not null ? _recordMode : SingleFormMode.View;

        private bool IsEditing => FormMode != SingleFormMode.View;

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

        // A server callback answers after the edit that started it has rendered, so the page re-renders itself to show
        // the values it wrote and what it reported, the way the required-field check reports. A failure leaves the
        // form on screen, so it is a notice too.
        private void OnFieldValueChangeApplied(object? sender, FieldValueChangeAppliedEventArgs e)
        {
            if (e.HasMessages)
                _notice = e.FormatMessages(_schema?.Tables?.GetOrDefault(e.TableName));
            _ = InvokeAsync(StateHasChanged);
        }

        private void OnFieldValueChangeFailed(object? sender, FieldValueChangeFailedEventArgs e)
        {
            _notice = e.Exception.Message;
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
            var response = await connector
                .GetListAsync(_schema is null ? string.Empty : ListSelectFields.For(_schema)).ConfigureAwait(true);
            _listRows = response.Table;
        }

        private void OnRowSelected(Guid rowId) => _selectedRowId = rowId;

        // Opening a row from the list is a read-only View; editing is the explicit Edit command, so clicking
        // through the list never changes a record by accident.
        private Task OnRowActivatedAsync(Guid rowId)
        {
            _selectedRowId = rowId;
            return OpenRecordAsync(rowId, SingleFormMode.View);
        }

        private Task OnViewAsync()
            => _selectedRowId is { } rowId ? OpenRecordAsync(rowId, SingleFormMode.View) : Task.CompletedTask;

        private Task OnEditAsync()
            => _selectedRowId is { } rowId ? OpenRecordAsync(rowId, SingleFormMode.Edit) : Task.CompletedTask;

        private async Task OpenRecordAsync(Guid rowId, SingleFormMode mode)
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(async () =>
            {
                await _dataObject.LoadAsync(rowId).ConfigureAwait(true);
                ShowRecord(mode);
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
                ShowRecord(SingleFormMode.Add);
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
                if (_dataObject.MasterRow is { } master && FormDataGuard.TryGetRowId(master, out var savedRowId))
                    _selectedRowId = savedRowId;
                ShowList();
                await ReloadListAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        // Cancel (Add / Edit) and Back (View) leave the record without saving; the next View, Edit or New reloads
        // the data object, so the edits are dropped with it.
        private void OnClose()
        {
            if (_isBusy) return;
            _notice = null;
            ShowList();
        }

        private async Task OnDeleteAsync()
        {
            if (_selectedRowId is not { } rowId) return;
            await RunGuardedAsync(async () =>
            {
                await Client.Form(ProgId).DeleteAsync(rowId).ConfigureAwait(true);
                _selectedRowId = null;
                await ReloadListAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private void ShowRecord(SingleFormMode mode)
        {
            _recordMode = mode;
            _isShowingRecord = true;
        }

        private void ShowList()
        {
            _isShowingRecord = false;
            _relation = null;
        }

        private void OpenRelation(RelationTarget target)
        {
            _notice = null;
            _relation = target;
        }

        private void CloseRelation() => _relation = null;

        private void OnRelationRefused(string message)
        {
            _relation = null;
            _notice = message;
        }

        private async Task RunGuardedAsync(Func<Task> action)
        {
            if (_isBusy) return;
            _isBusy = true;
            _notice = null;
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // The form stays on screen with what the user entered, so a rejected save can be corrected.
                _notice = ex.Message;
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
