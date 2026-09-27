using System.Data;
using Polhem.Base;
using Polhem.Base.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Sorting;

namespace Polhem.Db.Providers
{
    /// <summary>
    /// Turns the index rows a provider's catalog query returns into <see cref="DbTableIndex"/> entries.
    /// </summary>
    /// <remarks>
    /// The rows carry <c>Name</c>, <c>IsUnique</c>, <c>FieldName</c>, <c>IsDesc</c> and
    /// <c>KeyOrdinal</c>. The PostgreSQL, MySQL and SQL Server schema providers shape their catalog
    /// queries to that layout and share this parser; Oracle keeps its own because it also translates
    /// the uppercase storage names.
    /// </remarks>
    internal static class IndexRowsParser
    {
        /// <summary>
        /// Parses and populates all remaining indexes from the index data, consuming its rows.
        /// </summary>
        /// <param name="dbTable">The table schema to populate.</param>
        /// <param name="table">The index data table; the primary key rows are expected to be removed already.</param>
        public static void ParseIndexes(TableSchema dbTable, DataTable table)
        {
            while (!table.IsEmpty())
            {
                var firstRow = table.Rows[0];
                string name = ValueUtilities.CStr(firstRow["Name"]);
                bool isUnique = ValueUtilities.CBool(firstRow["IsUnique"]);

                var tableIndex = new DbTableIndex
                {
                    Name = name,
                    Unique = isUnique
                };
                dbTable.Indexes!.Add(tableIndex);

                table.DefaultView.RowFilter = $"Name='{name.Replace("'", "''")}'";
                table.DefaultView.Sort = "Name,KeyOrdinal";
                foreach (DataRowView rowView in table.DefaultView)
                {
                    var indexField = new IndexField
                    {
                        FieldName = ValueUtilities.CStr(rowView["FieldName"]),
                        SortDirection = ValueUtilities.CBool(rowView["IsDesc"]) ? SortDirection.Desc : SortDirection.Asc
                    };
                    tableIndex.IndexFields!.Add(indexField);
                }
                table.DefaultView.DeleteRows(true);
            }
        }
    }
}
