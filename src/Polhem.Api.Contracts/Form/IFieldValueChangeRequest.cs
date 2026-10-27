using System.Data;

namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the FieldValueChange request.
    /// </summary>
    /// <remarks>
    /// Sent when the user changes a field marked <see cref="Polhem.Definition.Forms.FormField.ValueChangeCallback"/>.
    /// The fields the server fills are resolved from the form's own definition and its business object, never from
    /// the request: the request only says which field changed, to what, and in which row.
    /// </remarks>
    public interface IFieldValueChangeRequest
    {
        /// <summary>
        /// Gets the name of the form table that holds the field: the master table, named after the program, or a
        /// detail table.
        /// </summary>
        string TableName { get; }

        /// <summary>
        /// Gets the name of the field whose value changed.
        /// </summary>
        string FieldName { get; }

        /// <summary>
        /// Gets the new value in the invariant text form a field binding uses; empty when the field was cleared.
        /// </summary>
        string Value { get; }

        /// <summary>
        /// Gets a one-row table holding the edited row as it is after the change, or <c>null</c> when the client sends
        /// none. A business object reads the row's other fields from it, such as the warehouse a stock lookup needs.
        /// </summary>
        /// <remarks>
        /// Its <c>DateTime</c> values are in the user's time zone, as the client holds them: like the data set of a
        /// save, a request's table is not moved to UTC. A table of more than one row is refused.
        /// </remarks>
        DataTable? Row { get; }
    }
}
