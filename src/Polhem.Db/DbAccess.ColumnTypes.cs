using System.Data;
using System.Data.Common;

namespace Polhem.Db
{
    /// <summary>
    /// Reading a result set into a table whose columns take the types declared in
    /// <see cref="DbCommandSpec.ColumnTypes"/>.
    /// </summary>
    public partial class DbAccess
    {
        /// <summary>
        /// Builds the columns of a table from the reader's result set, in result order, taking the declared
        /// type where <see cref="DbCommandSpec.ColumnTypes"/> names the column and the provider's type
        /// otherwise.
        /// </summary>
        /// <param name="reader">The open reader, before its first row is read.</param>
        /// <param name="command">The database command specification.</param>
        /// <remarks>
        /// The columns exist before any row arrives, so each value is converted into the declared type as
        /// it is loaded. <c>DbDataAdapter.Fill</c> would type the columns itself, which is what loses the
        /// fractional part of a SQLite decimal column; see <see cref="DbCommandSpec.ColumnTypes"/>.
        /// </remarks>
        private static DataTable CreateTypedTable(DbDataReader reader, DbCommandSpec command)
        {
            var table = new DataTable("DataTable");
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                var type = command.ColumnTypes.TryGetValue(name, out var declared) ? declared : reader.GetFieldType(i);
                table.Columns.Add(name, type);
            }
            return table;
        }

        /// <summary>
        /// Reads every row of the result set into a table built by <see cref="CreateTypedTable"/>.
        /// </summary>
        /// <param name="reader">The open reader, before its first row is read.</param>
        /// <param name="command">The database command specification.</param>
        /// <remarks>
        /// The rows load as Unchanged, as they do from <c>Fill</c>. A value that does not convert to its
        /// declared type throws rather than reading as NULL.
        /// </remarks>
        private static DataTable LoadTypedTable(DbDataReader reader, DbCommandSpec command)
        {
            var table = CreateTypedTable(reader, command);
            var values = new object[reader.FieldCount];
            table.BeginLoadData();
            while (reader.Read())
            {
                reader.GetValues(values);
                table.LoadDataRow(values, true);
            }
            table.EndLoadData();
            return table;
        }

        /// <summary>
        /// Asynchronously reads every row of the result set into a table built by
        /// <see cref="CreateTypedTable"/>.
        /// </summary>
        /// <param name="reader">The open reader, before its first row is read.</param>
        /// <param name="command">The database command specification.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling long-running commands.</param>
        private static async Task<DataTable> LoadTypedTableAsync(
            DbDataReader reader, DbCommandSpec command, CancellationToken cancellationToken)
        {
            var table = CreateTypedTable(reader, command);
            var values = new object[reader.FieldCount];
            table.BeginLoadData();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                reader.GetValues(values);
                table.LoadDataRow(values, true);
            }
            table.EndLoadData();
            return table;
        }
    }
}
