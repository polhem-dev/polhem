using System.Data;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Polhem.Web.Blazor.Mud.Controls
{
    /// <summary>
    /// A grid column over a field. <see cref="TemplateColumn{T}"/> reports a random id as its sort name, so a server sort
    /// could not tell which field the user chose; here the sort name is the field name.
    /// </summary>
    public sealed class FieldColumn : TemplateColumn<DataRow>
    {
        /// <summary>
        /// Gets or sets the field the column shows.
        /// </summary>
        [Parameter, EditorRequired]
        public string FieldName { get; set; } = string.Empty;

        /// <inheritdoc />
        public override string PropertyName => FieldName;
    }
}
