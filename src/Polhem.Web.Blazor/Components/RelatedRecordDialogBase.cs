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
using Polhem.UI.Core.DataObjects;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The state of a dialog showing the record a relation field points at, read-only, with a Close command. A form page
    /// opens it when the user double-clicks a relation field of a viewed record; a UI package draws it.
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
    /// The record renders the way <see cref="FormPageBase"/> renders one: localized through the same definition loader,
    /// with the sensitive fields the user may not read left out. A relation field in it opens the next record in a
    /// dialog of its own, stacked on top.
    /// </para>
    /// </remarks>
    public abstract class RelatedRecordDialogBase : ComponentBase
    {
        private FormSchema? _schema;
        private RelationTarget? _loadedTarget;
        private int _loadVersion;

        /// <summary>
        /// Gets or sets the record to show.
        /// </summary>
        [Parameter, EditorRequired]
        public RelationTarget Target { get; set; }

        /// <summary>
        /// Gets or sets the assembler the related form's definitions load through, as on <see cref="FormPageBase"/>.
        /// <c>null</c> uses one over the circuit's client, unless the host turned off
        /// <see cref="IBlazorDefinitionOptions.UseDefinitionLoader"/>.
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
        private IBlazorDefinitionOptions Options { get; set; } = default!;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private IUserContext? UserContext => Services?.GetService<IUserContext>();

        /// <summary>Gets the related form's layout once it has loaded.</summary>
        protected FormLayout? FormLayout { get; private set; }

        /// <summary>Gets the record once it has loaded.</summary>
        protected FormDataObject? DataObject { get; private set; }

        /// <summary>Gets the masters that decide numeric decimals once they have loaded.</summary>
        protected RoundingContext? RoundingContext { get; private set; }

        /// <summary>Gets why the dialog could not load.</summary>
        protected string? Error { get; private set; }

        /// <summary>Gets the message naming a form a field of this dialog may not open for the user.</summary>
        protected string? Notice { get; private set; }

        /// <summary>Gets whether the user may read the related form.</summary>
        protected bool IsPermitted { get; private set; }

        /// <summary>Gets the record a field of this dialog opened, drawn in a dialog stacked on top.</summary>
        protected RelationTarget? Relation { get; private set; }

        /// <summary>Gets the dialog's title.</summary>
        protected string? Title => _schema?.DisplayName;

        /// <summary>
        /// Gets the prefix of the record's input ids, unique per dialog so labels still point at the right input when
        /// dialogs stack.
        /// </summary>
        protected string IdPrefix => "polhem-related-" + Target.RowId.ToString("N", CultureInfo.InvariantCulture);

        /// <summary>Returns the localized text of <paramref name="key"/>.</summary>
        /// <param name="key">A <see cref="PolhemUIText"/> key.</param>
        /// <returns>The text.</returns>
        protected string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);

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
            FormLayout = null;
            DataObject = null;
            RoundingContext = null;
            Error = null;
            Notice = null;
            IsPermitted = false;
            Relation = null;

            var target = Target;
            try
            {
                var loader = BlazorDefinitions.CreateLoader(Client, Options, DefinitionLoader);
                var schema = await BlazorDefinitions.LoadSchemaAsync(Client, loader, target.ProgId).ConfigureAwait(true)
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
                IsPermitted = true;

                var formLayout = await BlazorDefinitions
                    .LoadFormLayoutAsync(Client, loader, target.ProgId, schema).ConfigureAwait(true);
                LayoutCapabilityApplier.Apply(formLayout, schema, capabilities);
                var roundingContext = await BlazorDefinitions
                    .LoadRoundingContextAsync(Client, loader, UserContext?.Company).ConfigureAwait(true);
                var dataObject = new FormDataObject(
                    schema, Client.Form(target.ProgId), () => Client.Session.Credentials.UserTimeZoneId);
                await dataObject.LoadAsync(target.RowId).ConfigureAwait(true);
                if (version != _loadVersion) return;

                FormLayout = formLayout;
                RoundingContext = roundingContext;
                DataObject = dataObject;
            }
            catch (Exception ex)
            {
                // The opener's record stays on screen behind the dialog, which shows why this one could not open.
                if (version == _loadVersion) Error = ex.Message;
            }
        }

        /// <summary>Opens <paramref name="target"/> in a dialog stacked on this one.</summary>
        /// <param name="target">The record a field of this dialog points at.</param>
        protected void OpenRelation(RelationTarget target)
        {
            Notice = null;
            Relation = target;
        }

        /// <summary>Closes the stacked dialog.</summary>
        protected void CloseRelation() => Relation = null;

        /// <summary>Closes the stacked dialog and shows why it could not open.</summary>
        /// <param name="message">The message to show.</param>
        protected void OnRelationRefused(string message)
        {
            Relation = null;
            Notice = message;
        }
    }
}
