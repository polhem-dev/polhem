using System.Globalization;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Language;
using Polhem.Web.Blazor.Components;

namespace Polhem.Web.Blazor.Controls
{
    /// <summary>
    /// The state of the chips above a list that show the query conditions in effect, which every UI package renders with
    /// its own markup. A chip opens the query pane, its remove button drops its condition, and a last button clears them
    /// all; a required condition has no remove button. Nothing renders while no condition is in effect.
    /// </summary>
    public abstract class CriteriaChipsBase : ComponentBase
    {
        /// <summary>Gets or sets the conditions in effect.</summary>
        [Parameter, EditorRequired]
        public IReadOnlyList<CriteriaChip> Chips { get; set; } = [];

        /// <summary>Gets or sets a value indicating whether the buttons wait, because the list is busy.</summary>
        [Parameter]
        public bool Disabled { get; set; }

        /// <summary>Invoked when the user clicks a chip, to open the query pane.</summary>
        [Parameter]
        public EventCallback<CriteriaChip> Opened { get; set; }

        /// <summary>Invoked when the user removes a chip's condition.</summary>
        [Parameter]
        public EventCallback<CriteriaChip> Removed { get; set; }

        /// <summary>Invoked when the user clears every condition.</summary>
        [Parameter]
        public EventCallback Cleared { get; set; }

        [Inject]
        private IServiceProvider? Services { get; set; }

        /// <summary>Gets a value indicating whether any chip is shown.</summary>
        protected bool HasChips => Chips.Count > 0;

        /// <summary>Gets the localized caption of the button that clears every condition.</summary>
        protected string ClearText => Text(PolhemUIText.ClearFilters);

        /// <summary>Returns the accessible name of a chip's remove button.</summary>
        /// <param name="chip">The chip.</param>
        /// <returns>The name.</returns>
        protected string RemoveLabel(CriteriaChip chip)
        {
            ArgumentNullException.ThrowIfNull(chip);
            return string.Format(CultureInfo.CurrentCulture, Text(PolhemUIText.RemoveItem), chip.Text);
        }

        /// <summary>Returns a framework text in the user's language.</summary>
        /// <param name="key">The text's key in <see cref="PolhemUIText"/>.</param>
        /// <returns>The text.</returns>
        protected string Text(string key) => PolhemUIText.Get(PolhemBlazorText.GetLocalizer(Services), key);
    }
}
