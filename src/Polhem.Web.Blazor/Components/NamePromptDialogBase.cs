using Microsoft.AspNetCore.Components;
using Polhem.Web.Blazor.Controls;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The state of a dialog that asks for a name, such as when a query is saved under a new name or renamed, with an
    /// optional check box under it. Every UI package renders it with its own dialog.
    /// </summary>
    /// <remarks>
    /// The host renders the dialog while the question is open and removes it when either callback runs. The name is
    /// trimmed, and the dialog accepts only a name that is not empty; Enter in the box accepts it, and Escape or a click
    /// outside cancels.
    /// </remarks>
    public abstract class NamePromptDialogBase : ComponentBase
    {
        private bool _initialized;

        /// <summary>Gets or sets the title.</summary>
        [Parameter]
        public string? Title { get; set; }

        /// <summary>Gets or sets the caption of the name box.</summary>
        [Parameter]
        public string? Label { get; set; }

        /// <summary>Gets or sets the name the box starts with.</summary>
        [Parameter]
        public string? Name { get; set; }

        /// <summary>Gets or sets the longest name the box takes. Defaults to 100.</summary>
        [Parameter]
        public int MaxLength { get; set; } = 100;

        /// <summary>Gets or sets the caption of the check box under the name; <c>null</c>, the default, shows none.</summary>
        [Parameter]
        public string? OptionText { get; set; }

        /// <summary>Gets or sets whether the check box starts checked.</summary>
        [Parameter]
        public bool OptionChecked { get; set; }

        /// <summary>Gets or sets the caption of the button that accepts the name.</summary>
        [Parameter]
        public string? AcceptText { get; set; }

        /// <summary>Gets or sets the caption of the button that cancels.</summary>
        [Parameter]
        public string? CancelText { get; set; }

        /// <summary>Invoked with the name when the user accepts it.</summary>
        [Parameter]
        public EventCallback<NamePromptAnswer> OnAccepted { get; set; }

        /// <summary>Invoked when the user cancels, including through Escape or a click outside.</summary>
        [Parameter]
        public EventCallback OnCancelled { get; set; }

        /// <summary>Gets or sets the name in the box.</summary>
        internal string EnteredName { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the check box is checked.</summary>
        internal bool IsOptionChecked { get; set; }

        /// <summary>Gets a value indicating whether the name can be accepted: it is not empty once trimmed.</summary>
        internal bool CanAccept => EnteredName.Trim().Length > 0;

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            // The box keeps what the user typed when the host renders again.
            if (_initialized) return;
            _initialized = true;
            EnteredName = Name ?? string.Empty;
            IsOptionChecked = OptionChecked;
        }

        /// <summary>Accepts the name when it is not empty.</summary>
        /// <param name="cancellationToken">A token that cancels the operation before the answer is sent.</param>
        /// <returns>The operation.</returns>
        internal Task AcceptAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanAccept) return Task.CompletedTask;
            return OnAccepted.InvokeAsync(new NamePromptAnswer(EnteredName.Trim(), OptionText is not null && IsOptionChecked));
        }

        /// <summary>Cancels the question.</summary>
        /// <param name="cancellationToken">A token that cancels the operation before the answer is sent.</param>
        /// <returns>The operation.</returns>
        internal Task CancelAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return OnCancelled.InvokeAsync();
        }
    }
}
