using System.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Definition.Layouts;

namespace Polhem.Web.Blazor.Components
{
    /// <summary>
    /// One card of a form page's compact list: the visible columns of the list layout, each with its caption and the
    /// row's value. A UI package's form page places one per row.
    /// </summary>
    /// <remarks>
    /// The card renders again only when its row, its layout or its selection changes. A click on one card renders the
    /// page again, and without this every card of a long list went through a render and a diff and sent its click
    /// handler again. The values are formatted when the card renders, so a value that changes in place, without a new
    /// row, is not shown until one of those changes; the form page reads the list again after a save.
    /// </remarks>
    public partial class ListCard
    {
        private RenderedState _rendered;

        /// <summary>Gets or sets the row the card shows.</summary>
        [Parameter, EditorRequired]
        public DataRow Row { get; set; } = default!;

        /// <summary>Gets or sets the list layout whose visible columns the card shows.</summary>
        [Parameter, EditorRequired]
        public LayoutGrid Layout { get; set; } = default!;

        /// <summary>Gets or sets whether the card is the selected one.</summary>
        [Parameter]
        public bool IsSelected { get; set; }

        /// <summary>Gets or sets what formats a value of the row for a column.</summary>
        [Parameter, EditorRequired]
        public Func<DataRow, LayoutColumn, string> FormatValue { get; set; } = default!;

        /// <summary>Gets or sets the callback of a click, with the browser's click count in its arguments.</summary>
        [Parameter]
        public EventCallback<MouseEventArgs> OnClick { get; set; }

        /// <inheritdoc />
        protected override void OnInitialized() => _rendered = CurrentState();

        /// <inheritdoc />
        protected override bool ShouldRender()
        {
            var current = CurrentState();
            if (current == _rendered) return false;
            _rendered = current;
            return true;
        }

        private RenderedState CurrentState() => new(Row, Layout, IsSelected);

        private Task OnClickAsync(MouseEventArgs e) => OnClick.InvokeAsync(e);

        private readonly record struct RenderedState(DataRow Row, LayoutGrid Layout, bool IsSelected);
    }
}
