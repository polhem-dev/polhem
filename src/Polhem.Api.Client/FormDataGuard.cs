using System.Data;
using Polhem.Api.Client.Connectors;
using Polhem.Definition;

namespace Polhem.Api.Client
{
    /// <summary>
    /// Preconditions a form's CRUD calls must satisfy before they reach the API.
    /// </summary>
    /// <remarks>
    /// Shared by every head so the failure a developer sees is the same wherever the form is
    /// hosted. The message text is the point: it is what a developer reads when a form is wired
    /// up wrongly, and two copies of it drift into two different explanations of one mistake.
    /// </remarks>
    public static class FormDataGuard
    {
        /// <summary>
        /// Returns the connector, or explains that the form was constructed without one.
        /// </summary>
        /// <param name="connector">The connector held by the form, possibly <c>null</c>.</param>
        /// <param name="operation">The operation being attempted, used in the message.</param>
        /// <returns>The non-null connector.</returns>
        /// <exception cref="InvalidOperationException">The form holds no connector.</exception>
        public static FormApiConnector RequireConnector(FormApiConnector? connector, string operation)
        {
            return connector
                ?? throw new InvalidOperationException(
                    $"{operation} requires a FormApiConnector; pass one to the FormDataObject constructor.");
        }

        /// <summary>
        /// Reads the master row's identifier, or explains which part of the precondition failed.
        /// </summary>
        /// <param name="masterRow">The loaded master row, possibly <c>null</c>.</param>
        /// <returns>The master row's identifier.</returns>
        /// <exception cref="InvalidOperationException">
        /// No master row is loaded, the master table has no identifier column, or the identifier is null.
        /// </exception>
        /// <remarks>
        /// Three separate messages rather than one: each names a different mistake, and collapsing
        /// them would leave the caller to guess which of the three they made.
        /// </remarks>
        public static Guid RequireMasterRowId(DataRow? masterRow)
        {
            var row = masterRow
                ?? throw new InvalidOperationException("No master row is loaded; cannot delete.");
            if (!row.Table.Columns.Contains(SysFields.RowId))
                throw new InvalidOperationException(
                    $"Master table is missing the '{SysFields.RowId}' column; cannot delete.");

            var raw = row[SysFields.RowId];
            if (raw is null || raw == DBNull.Value)
                throw new InvalidOperationException(
                    $"Master row has a null '{SysFields.RowId}'; cannot delete.");

            return raw is Guid g ? g : Guid.Parse(raw.ToString()!);
        }

        /// <summary>
        /// Reads a row's identifier when it has one.
        /// </summary>
        /// <param name="row">The row, typically the one a user selected in a grid or list.</param>
        /// <param name="rowId">The row's identifier, or <see cref="Guid.Empty"/> when the method returns <c>false</c>.</param>
        /// <returns>
        /// <c>true</c> when the row's table has the identifier column and the row holds a GUID, or a string
        /// that parses as one; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// The non-throwing counterpart of <see cref="RequireMasterRowId"/>, for selection handlers that
        /// ignore a row without an identifier instead of reporting it.
        /// </remarks>
        public static bool TryGetRowId(DataRow row, out Guid rowId)
        {
            ArgumentNullException.ThrowIfNull(row);

            rowId = Guid.Empty;
            if (!row.Table.Columns.Contains(SysFields.RowId)) return false;
            var raw = row[SysFields.RowId];
            if (raw is null || raw == DBNull.Value) return false;
            if (raw is Guid g) { rowId = g; return true; }
            return Guid.TryParse(raw.ToString(), out rowId);
        }
    }
}
