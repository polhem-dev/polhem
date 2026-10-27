using System.Data;

namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Contract interface for the FieldValueChange response.
    /// </summary>
    /// <remarks>
    /// Every member applies to the row the request named. The client writes <see cref="Values"/> into it without
    /// calling back again for the fields it writes, and shows <see cref="Errors"/> and <see cref="Warnings"/> on the
    /// fields they name. An error does not undo the user's value; saving still validates on the server.
    /// The keys of the message maps are the field names as the schema declares them; compare them without regard to
    /// case, since a deserialized map does not keep the server's comparer.
    /// </remarks>
    public interface IFieldValueChangeResponse
    {
        /// <summary>
        /// Gets a one-row table whose columns are the fields to write and whose values are what to write into them, or
        /// <c>null</c> when nothing is to be written. A cleared field carries its type's empty value, or
        /// <see cref="DBNull"/> for a date.
        /// </summary>
        DataTable? Values { get; }

        /// <summary>
        /// Gets the validation errors, keyed by field name: messages to show on fields whose value is not acceptable.
        /// </summary>
        IReadOnlyDictionary<string, string> Errors { get; }

        /// <summary>
        /// Gets the warnings, keyed by field name: messages to show on fields without refusing their value.
        /// </summary>
        IReadOnlyDictionary<string, string> Warnings { get; }
    }
}
