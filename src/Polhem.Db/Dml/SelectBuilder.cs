using Polhem.Definition.Forms;
using Polhem.Base;
using Polhem.Definition.Database;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// Builds the SQL SELECT clause.
    /// </summary>
    public sealed class SelectBuilder
    {
        private readonly DatabaseType _databaseType;

        /// <summary>
        /// Initializes a new instance of <see cref="SelectBuilder"/>.
        /// </summary>
        /// <param name="databaseType">The database type.</param>
        public SelectBuilder(DatabaseType databaseType)
        {
            _databaseType = databaseType;
        }

        /// <summary>
        /// Builds the SELECT clause.
        /// </summary>
        /// <param name="formTable">The form table.</param>
        /// <param name="selectFields">A comma-separated string of field names to retrieve; an empty string retrieves all fields.</param>
        /// <param name="selectContext">The field source mappings and table JOIN relationships for the query.</param>
        /// <exception cref="InvalidOperationException">
        /// A named field does not exist in the table, or is a <see cref="Polhem.Definition.ProtectedFields"/> column.
        /// An empty <paramref name="selectFields"/> leaves protected columns out instead.
        /// </exception>
        public string Build(FormTable formTable, string selectFields, SelectContext selectContext)
        {
            bool allFields = string.IsNullOrWhiteSpace(selectFields);
            var selectFieldNames = GetSelectFields(formTable, selectFields);
            var selectParts = new List<string>();
            foreach (var fieldName in selectFieldNames)
            {
                var field = formTable.Fields!.GetOrDefault(fieldName);
                if (field == null)
                    throw new InvalidOperationException($"Field '{fieldName}' does not exist in table '{formTable.TableName}'.");

                // "Every field" means every field the caller may read: a protected column is left out
                // rather than refused, so a form that declares one still loads. Naming it is refused.
                if (allFields)
                {
                    if (SelectFieldGuard.IsProtected(formTable, field, selectContext)) { continue; }
                }
                else
                {
                    SelectFieldGuard.RequireSelectable(formTable, field, selectContext);
                }

                if (field.Type == FieldType.DbField)
                {
                    selectParts.Add($"    A.{QuoteIdentifier(fieldName)}");
                }
                else
                {
                    var mapping = selectContext.FieldMappings.GetOrDefault(fieldName);
                    if (mapping == null)
                        throw new InvalidOperationException($"Field mapping for '{fieldName}' is null.");
                    selectParts.Add($"    {mapping.SourceAlias}.{QuoteIdentifier(mapping.SourceField)} AS {QuoteIdentifier(fieldName)}");
                }
            }
            return "SELECT\n" + string.Join(",\n", selectParts);
        }

        /// <summary>
        /// Returns the set of field names to include in the SELECT clause.
        /// </summary>
        /// <param name="formTable">The form table.</param>
        /// <param name="selectFields">A comma-separated string of field names to retrieve; an empty string retrieves all fields.</param>
        private static HashSet<string> GetSelectFields(FormTable formTable, string selectFields)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(selectFields))
            {
                // Retrieve all fields
                foreach (var field in formTable.Fields!)
                {
                    set.Add(field.FieldName);
                }
            }
            else
            {
                // Retrieve only the specified fields
                set.UnionWith(StringUtilities.Split(selectFields, ","));
            }
            return set;
        }

        private string QuoteIdentifier(string identifier)
        {
            return _databaseType.QuoteIdentifier(identifier);
        }
    }
}
