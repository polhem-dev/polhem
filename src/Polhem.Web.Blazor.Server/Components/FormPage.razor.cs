using System.Globalization;
using Polhem.Api.Client.Definitions;
using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Web.Blazor.Server.DataObjects;
using Polhem.Web.Blazor.Server.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Code-behind for <c>FormPage.razor</c>. Wires <see cref="Polhem.Web.Blazor.Server.Components.DynamicGrid"/>
    /// (list view) to <see cref="DynamicForm"/> (master detail) via a shared
    /// <see cref="FormDataObject"/>: selecting a list row drives
    /// <see cref="FormDataObject.LoadAsync"/>; the toolbar buttons fan out to
    /// <see cref="FormDataObject.NewAsync"/> / <c>SaveAsync</c> / <c>DeleteAsync</c>.
    /// </summary>
    /// <remarks>
    /// FormPage assumes the host has acquired an <c>AccessToken</c> elsewhere
    /// (e.g. via a sign-in page that calls <see cref="SystemApiConnector.LoginAsync"/>)
    /// and supplies it through a cascading parameter. Anonymous use is allowed
    /// (<see cref="AccessToken"/> defaults to <see cref="Guid.Empty"/>); the
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
    /// Save checks the fields marked <see cref="FormField.Required"/> first, through
    /// <see cref="RequiredFieldCheck"/>. When any is empty the page names them above the toolbar and
    /// sends nothing; the form stays open for the user to fill them in.
    /// </para>
    /// </remarks>
    public sealed partial class FormPage : ComponentBase
    {
        private FormSchema? _schema;
        private FormLayout? _formLayout;
        private LayoutGrid? _listLayout;
        private FormDataObject? _dataObject;
        private DataTable? _listRows;
        private string? _error;
        // A message for the user that leaves the form on screen, unlike `_error`, which replaces it.
        private string? _notice;
        private bool _isInitializing = true;
        private bool _isBusy;

        /// <summary>
        /// Gets or sets the program identifier (e.g. "Employee"). Drives the
        /// FormSchema lookup and the connector creation.
        /// </summary>
        [Parameter, EditorRequired]
        public string ProgId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cascading access token. Defaults to
        /// <see cref="Guid.Empty"/> (anonymous).
        /// </summary>
        [CascadingParameter]
        public Guid AccessToken { get; set; }

        [Inject]
        private PolhemApiConnectorFactory Factory { get; set; } = default!;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        /// <summary>
        /// Gets or sets the assembler that turns raw definitions into a localized schema and a
        /// runtime layout, for this page only. <c>null</c> — the default — uses the one
        /// <see cref="PolhemApiConnectorFactory.CreateDefinitionLoader"/> builds, unless the host
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
                var loader = DefinitionLoader
                    ?? (Factory.UseDefinitionLoader ? Factory.CreateDefinitionLoader(AccessToken) : null);
                // Without a loader both definitions are fetched exactly as stored. With one, both
                // layers of language and layout are fetched and assembled, so the captions are in the
                // circuit's language and tenant customization takes effect.
                if (loader is null)
                {
                    var system = Factory.CreateSystemConnector(AccessToken);
                    _schema = await system
                        .GetDefineAsync<FormSchema>(DefineType.FormSchema, [ProgId])
                        .ConfigureAwait(true);
                    // A layout is authored at design time, so a missing one is a configuration
                    // error rather than a cue to derive one from the schema.
                    _formLayout = await system
                        .GetDefineAsync<FormLayout>(DefineType.FormLayout, [ProgId])
                        .ConfigureAwait(true)
                        ?? throw new InvalidOperationException(
                            $"No FormLayout definition found for '{ProgId}'. Author one at design time "
                            + $"and save it as 'FormLayout/{ProgId}.FormLayout.xml' under the definition path.");
                }
                else
                {
                    _schema = await loader
                        .GetLocalizedSchemaAsync(ProgId, CultureInfo.CurrentUICulture.Name)
                        .ConfigureAwait(true);
                    _formLayout = await loader
                        .GetRuntimeLayoutAsync(ProgId, _schema).ConfigureAwait(true);
                }
                _listLayout = _schema.GetListLayout();

                _dataObject = new FormDataObject(_schema, Factory.CreateFormConnector(AccessToken, ProgId));
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

        private async Task ReloadListAsync()
        {
            var connector = Factory.CreateFormConnector(AccessToken, ProgId);
            var response = await connector.GetListAsync().ConfigureAwait(true);
            _listRows = response.Table;
        }

        private async Task OnRowSelectedAsync(Guid rowId)
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(() => _dataObject.LoadAsync(rowId)).ConfigureAwait(true);
        }

        private async Task OnNewAsync()
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(() => _dataObject.NewAsync()).ConfigureAwait(true);
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
                await ReloadListAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task OnDeleteAsync()
        {
            if (_dataObject is null) return;
            await RunGuardedAsync(async () =>
            {
                await _dataObject.DeleteAsync().ConfigureAwait(true);
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
