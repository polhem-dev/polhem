using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Api.Client;
using Polhem.Api.Client.Definitions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core;
using Polhem.UI.Core.Fields;
using Polhem.UI.Core.Permissions;
using Polhem.Web.Blazor.Server.DataObjects;
using Polhem.Web.Blazor.Server.DependencyInjection;

namespace Polhem.Web.Blazor.Server.Components
{
    /// <summary>
    /// Shows the record a relation field points at, read-only, in a modal dialog with a Close command. The
    /// <see cref="FormPage"/> opens it when the user double-clicks a relation field of a viewed record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog loads the related form's schema first and asks <see cref="RelationOpenRules.CanOpen"/> whether the
    /// user's capability snapshot grants <see cref="Polhem.Definition.Settings.PermissionActions.Read"/> on it. When it
    /// does not, the dialog never shows: it calls nothing more and raises <see cref="OnRefused"/> with the message to
    /// show, and the opener closes it. When it does, the dialog loads the layout and the record through the related
    /// form's connector, whose <c>GetData</c> checks the permission and the record scope again on the server.
    /// </para>
    /// <para>
    /// The record renders the way <see cref="FormPage"/> renders one: localized through the same definition loader,
    /// with the sensitive fields the user may not read left out. A relation field in it opens the next record in a
    /// dialog of its own, stacked on top.
    /// </para>
    /// </remarks>
    public sealed partial class RelatedRecordDialog : ComponentBase
    {
        private FormSchema? _schema;
        private FormLayout? _formLayout;
        private FormDataObject? _dataObject;
        private RoundingContext? _roundingContext;
        private string? _error;
        // Names the form a field of this dialog may not open for the user.
        private string? _notice;
        private bool _isPermitted;
        private RelationTarget? _relation;
        private RelationTarget? _loadedTarget;
        private int _loadVersion;

        /// <summary>
        /// Gets or sets the record to show.
        /// </summary>
        [Parameter, EditorRequired]
        public RelationTarget Target { get; set; }

        /// <summary>
        /// Gets or sets the assembler the related form's definitions load through, as on <see cref="FormPage"/>.
        /// <c>null</c> uses one over the circuit's client, unless the host turned off
        /// <see cref="PolhemBlazorOptions.UseDefinitionLoader"/>.
        /// </summary>
        [Parameter]
        public FormDefinitionLoader? DefinitionLoader { get; set; }

        /// <summary>
        /// Gets or sets whether the record renders in its compact layout.
        /// </summary>
        [Parameter]
        public bool IsCompact { get; set; }

        /// <summary>
        /// Invoked when the user closes the dialog. The opener stops rendering it.
        /// </summary>
        [Parameter]
        public EventCallback OnClosed { get; set; }

        /// <summary>
        /// Invoked, instead of showing the dialog, when the user may not read the related form. The argument is the
        /// message to show the user. The opener stops rendering the dialog.
        /// </summary>
        [Parameter]
        public EventCallback<string> OnRefused { get; set; }

        [Inject]
        private PolhemApiClient Client { get; set; } = default!;

        [Inject]
        private PolhemBlazorOptions Options { get; set; } = default!;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private IUserContext? UserContext => Services?.GetService<IUserContext>();

        private string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

        private string? Title => _schema?.DisplayName;

        // Each dialog's inputs get their own ids, so labels still point at the right input when dialogs stack.
        private string IdPrefix => "polhem-related-" + Target.RowId.ToString("N", CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        protected override async Task OnParametersSetAsync()
        {
            if (_loadedTarget == Target) return;
            _loadedTarget = Target;
            await LoadAsync().ConfigureAwait(true);
        }

        private async Task LoadAsync()
        {
            // A load that a newer one overtook leaves the dialog to the newer one.
            int version = ++_loadVersion;
            _schema = null;
            _formLayout = null;
            _dataObject = null;
            _roundingContext = null;
            _error = null;
            _notice = null;
            _isPermitted = false;
            _relation = null;

            var target = Target;
            try
            {
                var loader = CircuitDefinitions.CreateLoader(Client, Options, DefinitionLoader);
                var schema = await CircuitDefinitions.LoadSchemaAsync(Client, loader, target.ProgId).ConfigureAwait(true)
                    ?? throw new InvalidOperationException($"No FormSchema definition found for '{target.ProgId}'.");
                if (version != _loadVersion) return;

                var capabilities = UserContext?.Capabilities;
                if (!RelationOpenRules.CanOpen(schema, capabilities))
                {
                    await OnRefused.InvokeAsync(string.Format(CultureInfo.CurrentCulture,
                        Text(PolhemUIText.RelatedRecordDenied), schema.DisplayName)).ConfigureAwait(true);
                    return;
                }
                _schema = schema;
                _isPermitted = true;

                var formLayout = await CircuitDefinitions
                    .LoadFormLayoutAsync(Client, loader, target.ProgId, schema).ConfigureAwait(true);
                LayoutCapabilityApplier.Apply(formLayout, schema, capabilities);
                var roundingContext = await CircuitDefinitions
                    .LoadRoundingContextAsync(Client, loader, UserContext?.Company).ConfigureAwait(true);
                var dataObject = new FormDataObject(schema, Client.Form(target.ProgId));
                await dataObject.LoadAsync(target.RowId).ConfigureAwait(true);
                if (version != _loadVersion) return;

                _formLayout = formLayout;
                _roundingContext = roundingContext;
                _dataObject = dataObject;
            }
            catch (Exception ex)
            {
                // The opener's record stays on screen behind the dialog, which shows why this one could not open.
                if (version == _loadVersion) _error = ex.Message;
            }
        }

        private Task CloseAsync() => OnClosed.InvokeAsync();

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
    }
}
