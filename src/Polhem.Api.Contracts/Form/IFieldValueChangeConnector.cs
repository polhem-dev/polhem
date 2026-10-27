using System.Data;

namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract for calling a form's business object back when a field marked
    /// <see cref="Polhem.Definition.Forms.FormField.ValueChangeCallback"/> changes.
    /// </summary>
    /// <remarks>
    /// It is separate from <see cref="IFormApiConnector"/> so that adding it breaks no existing implementation: a
    /// front end checks whether its connector also implements this interface, and without it a change fills only what
    /// the client can fill on its own. The API client's form connector implements both.
    /// </remarks>
    public interface IFieldValueChangeConnector
    {
        /// <summary>
        /// Reports a field's new value to the form's business object and returns what it asks to change in the row.
        /// </summary>
        /// <param name="tableName">The form table that holds the field.</param>
        /// <param name="fieldName">The field whose value changed.</param>
        /// <param name="value">The new value in the invariant text form a field binding uses; empty when cleared.</param>
        /// <param name="row">A one-row table holding the edited row after the change, or <c>null</c>.</param>
        /// <param name="cancellationToken">A token that cancels the call.</param>
        Task<IFieldValueChangeResponse> FieldValueChangeAsync(string tableName, string fieldName, string value,
            DataTable? row, CancellationToken cancellationToken = default);
    }
}
