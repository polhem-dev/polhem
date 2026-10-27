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
    /// </remarks>
    public interface IFieldValueChangeResponse
    {
        /// <summary>
        /// Gets a one-row table whose columns are the fields to write and whose values are what to write into them, or
        /// <c>null</c> when nothing is to be written. A <see cref="DBNull"/> value clears its field.
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
