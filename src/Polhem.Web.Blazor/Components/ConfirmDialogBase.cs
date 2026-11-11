using Microsoft.AspNetCore.Components;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// The parameters of the yes-or-no question a form page asks, such as before a delete, which every UI package renders
    /// with its own dialog.
    /// </summary>
    /// <remarks>
    /// The host renders the dialog while the question is open and removes it when either callback runs. The focus starts
    /// on the safe answer, so a stray Enter or Space does not go ahead.
    /// </remarks>
    public abstract class ConfirmDialogBase : ComponentBase
    {
        /// <summary>Gets or sets the title.</summary>
        [Parameter]
        public string? Title { get; set; }

        /// <summary>Gets or sets the question.</summary>
        [Parameter]
        public string? Message { get; set; }

        /// <summary>Gets or sets the caption of the safe answer.</summary>
        [Parameter]
        public string? CancelText { get; set; }

        /// <summary>Gets or sets the caption of the answer that goes ahead.</summary>
        [Parameter]
        public string? ConfirmText { get; set; }

        /// <summary>Invoked when the user goes ahead.</summary>
        [Parameter]
        public EventCallback OnConfirmed { get; set; }

        /// <summary>Invoked when the user backs out, including through Escape or a click outside.</summary>
        [Parameter]
        public EventCallback OnCancelled { get; set; }
    }
}
