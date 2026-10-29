using System.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.UI.Core.Fields;
using Polhem.Web.Blazor.Server.Components;

namespace Polhem.Web.Blazor.Server.Controls.Editors
{
    /// <summary>
    /// Field editor for <see cref="ControlType.ButtonEdit"/>: a <see cref="TextEdit"/> with a trailing lookup
    /// button. When the bound field carries relation metadata
    /// (<see cref="Polhem.Definition.Forms.FormField.RelationProgId"/>), the editor runs the built-in lookup
    /// flow: the text shows the resolved display fields, the button opens <see cref="LookupDialog"/>, a
    /// selection writes the row id and the mapped fields back through the data object, and Delete or
    /// Backspace clears the selection. Without relation metadata the button only raises
    /// <see cref="ButtonClick"/> and the editor behaves like a plain <see cref="TextEdit"/>. This is the
    /// behavior of the Avalonia head's <c>Polhem.UI.Avalonia.Controls.Editors.ButtonEdit</c>.
    /// </summary>
    /// <remarks>
    /// In lookup mode the input is always read-only: the display value is not hand-editable, and the lookup
    /// flow is the only write path. The button is shown only while the editor accepts input.
    /// <para>
    /// On a read-only form a relation field with a value opens the record it points at instead: the input shows a
    /// hint and a hand cursor, and a double-click passes the record to the enclosing <see cref="FormScope"/>'s
    /// <see cref="FormScope.OpenRelation"/>, which decides whether the user may see it. Without that handler the
    /// field offers nothing. The rules are <see cref="RelationOpenRules"/>, which every UI head shares.
    /// </para>
    /// </remarks>
    public sealed partial class ButtonEdit : TextEdit
    {
        private bool _lookupOpen;
        private string? _failure;

        /// <summary>
        /// Invoked when the button is clicked on an editor without relation metadata; the lookup flow of
        /// relation-bound editors replaces it.
        /// </summary>
        [Parameter]
        public EventCallback ButtonClick { get; set; }

        /// <summary>
        /// Invoked when the built-in lookup flow fails (the schema or connector cannot be obtained, or the
        /// selection cannot be written). Without a handler the failure is shown as the input's tooltip.
        /// </summary>
        [Parameter]
        public EventCallback<Exception> LookupFailed { get; set; }

        /// <summary>
        /// Gets a value indicating whether the bound field carries relation metadata and therefore uses the
        /// built-in lookup flow.
        /// </summary>
        public bool HasLookup => LookupFieldRules.HasLookup(FormField);

        /// <inheritdoc />
        protected override string ModifierClass => "polhem-field-editor--button";

        /// <inheritdoc />
        protected override bool ShouldWriteBackText => !HasLookup;

        // Nullable: a component created outside a renderer has no services, and still renders.
        [Inject]
        private IServiceProvider? Services { get; set; }

        private string SearchText => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), PolhemUIText.Search);

        // The record a double-click opens, when the field offers one: a relation field with a value, on a read-only
        // form whose scope has someone to open it.
        private RelationTarget? OpenTarget
            => OpenRelation.HasDelegate
               && RelationOpenRules.IsAvailable(FormMode, FormField)
               && RelationOpenRules.TryGetTarget(FormField, FieldValue, out var target)
                ? target
                : null;

        private string? OpenHint
            => OpenTarget is null ? null : PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), PolhemUIText.OpenRelatedRecord);

        private string InputCssClass
        {
            get
            {
                var css = BuildCssClass(ModifierClass);
                return OpenTarget is null ? css : css + " polhem-field-editor--opens-record";
            }
        }

        /// <inheritdoc />
        protected override void RefreshFromSource()
        {
            if (!HasLookup)
            {
                base.RefreshFromSource();
                return;
            }
            // A lookup editor composes its display fields (typically "<id> - <name>", the id only for
            // transactional targets); a relation field without any resolvable display field shows an
            // empty string rather than the raw Guid.
            var displayFields = LookupFieldRules.ResolveDisplayFieldNames(BoundLayoutField, FormField);
            var dataObject = BoundDataObject;
            if (displayFields.Count == 0 || dataObject is null)
            {
                Text = string.Empty;
                return;
            }
            var row = TargetRow;
            Text = LookupDisplay.Compose(displayFields
                .Select(f => row is not null ? dataObject.GetField(row, f) : dataObject.GetField(f)));
        }

        /// <inheritdoc />
        protected override void ApplyMetadata()
        {
            base.ApplyMetadata();
            // A change to any display field (a lookup write-back, for example) refreshes this editor, even
            // though it binds the row-id field.
            var displayFields = HasLookup
                ? LookupFieldRules.ResolveDisplayFieldNames(BoundLayoutField, FormField)
                : [];
            WatchFieldNames = displayFields.Count == 0 ? null : displayFields;
        }

        private async Task OnButtonClickAsync()
        {
            if (!IsEditable) return;
            if (!HasLookup)
            {
                await ButtonClick.InvokeAsync().ConfigureAwait(true);
                return;
            }
            _failure = null;
            _lookupOpen = true;
        }

        private async Task OnDoubleClickAsync()
        {
            if (OpenTarget is { } target)
                await OpenRelation.InvokeAsync(target).ConfigureAwait(true);
        }

        private void OnKeyDown(KeyboardEventArgs e)
        {
            // Delete or Backspace clears the lookup selection (the row id and the mapped fields).
            if (!HasLookup || !IsEditable || e.Key is not ("Delete" or "Backspace")) return;
            if (BoundDataObject is { } dataObject && FormField is { } field)
                dataObject.ClearLookupSelection(field, TargetRow);
        }

        private async Task ApplySelectionAsync(DataRow selected)
        {
            _lookupOpen = false;
            if (BoundDataObject is not { } dataObject || FormField is not { } field) return;
            try
            {
                dataObject.ApplyLookupSelection(field, selected, TargetRow);
            }
            catch (InvalidOperationException ex)
            {
                // A mapping outside the lookup result is a definition error; report it rather than break
                // the page.
                await ReportFailureAsync(ex).ConfigureAwait(true);
            }
        }

        private void CloseLookup() => _lookupOpen = false;

        private async Task ReportFailureAsync(Exception ex)
        {
            if (LookupFailed.HasDelegate)
            {
                _lookupOpen = false;
                await LookupFailed.InvokeAsync(ex).ConfigureAwait(true);
            }
            else
            {
                _failure = ex.Message;
            }
        }
    }
}
