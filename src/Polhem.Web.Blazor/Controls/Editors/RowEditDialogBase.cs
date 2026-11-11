using System.Data;
using Microsoft.AspNetCore.Components;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.UI.Core.DataObjects;

namespace Polhem.Web.Blazor.Controls.Editors
{
    /// <summary>
    /// The parameters of the dialog that edits one grid row, which every UI package renders with its own dialog.
    /// </summary>
    public abstract class RowEditDialogBase : ComponentBase
    {
        /// <summary>Gets or sets the data object that owns the row.</summary>
        [Parameter, EditorRequired]
        public FormDataObject? DataObject { get; set; }

        /// <summary>Gets or sets the grid layout whose columns drive the editors.</summary>
        [Parameter, EditorRequired]
        public LayoutGrid? Layout { get; set; }

        /// <summary>Gets or sets the row to edit.</summary>
        [Parameter, EditorRequired]
        public DataRow? Row { get; set; }

        /// <summary>Gets or sets whether the editors lay out in a single column.</summary>
        [Parameter]
        public bool Compact { get; set; }

        /// <summary>Gets or sets the currency master for the decimals of amount columns, or <c>null</c>.</summary>
        [Parameter]
        public CurrencySettings? CurrencySettings { get; set; }

        /// <summary>Gets or sets the currency code for amounts whose row carries no currency-key field.</summary>
        [Parameter]
        public string DefaultCurrencyCode { get; set; } = string.Empty;

        /// <summary>Gets or sets the unit-of-measure master for the decimals of quantity and weight columns, or <c>null</c>.</summary>
        [Parameter]
        public UnitSettings? UnitSettings { get; set; }

        /// <summary>Invoked after OK commits the edit.</summary>
        [Parameter]
        public EventCallback EditCommitted { get; set; }

        /// <summary>Invoked after Cancel rolls the edit back.</summary>
        [Parameter]
        public EventCallback EditCancelled { get; set; }
    }
}
