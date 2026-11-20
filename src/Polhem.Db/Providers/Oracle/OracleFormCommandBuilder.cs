using Polhem.Db.Dml;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Storage;

namespace Polhem.Db.Providers.Oracle
{
    /// <summary>
    /// Oracle 19c+ form-related SQL command builder, generating SELECT, SELECT COUNT,
    /// and DELETE statements. Counterpart to <see cref="MySql.MySqlFormCommandBuilder"/>
    /// and <see cref="Sqlite.SqliteFormCommandBuilder"/>; each method delegates to
    /// the dialect-agnostic cores in <see cref="Polhem.Db.Dml"/> with
    /// <see cref="DatabaseType.Oracle"/>, so double-quote identifier quoting and
    /// <c>:</c> bind-variable prefix flow from the <see cref="DatabaseTypeExtensions"/> dictionaries.
    /// </summary>
    public sealed class OracleFormCommandBuilder : IFormCommandBuilder
    {
        private readonly IDefineAccess _defineAccess;

        /// <summary>
        /// Initializes a new instance of <see cref="OracleFormCommandBuilder"/> using the specified form schema.
        /// </summary>
        /// <param name="formDefine">The form schema definition.</param>
        /// <param name="defineAccess">The define access service used to resolve relation-form schemas during SELECT construction.</param>
        public OracleFormCommandBuilder(FormSchema formDefine, IDefineAccess defineAccess)
        {
            FormSchema = formDefine ?? throw new ArgumentNullException(nameof(formDefine));
            _defineAccess = defineAccess ?? throw new ArgumentNullException(nameof(defineAccess));
        }

        /// <summary>
        /// Gets the form schema definition.
        /// </summary>
        private FormSchema FormSchema { get; }

        /// <summary>
        /// Builds the SELECT command specification.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="query">The fields, the filter, the order and the page to select.</param>
        public DbCommandSpec BuildSelect(string tableName, FormSelectQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            var builder = new SelectCommandBuilder(FormSchema, DatabaseType.Oracle, _defineAccess);
            return builder.Build(tableName, query.SelectFields, query.Filter, query.SortFields, query.Skip, query.Take);
        }

        /// <summary>
        /// Builds the SELECT COUNT(*) command specification.
        /// </summary>
        /// <param name="tableName">The form table name.</param>
        /// <param name="filter">The filter condition.</param>
        public DbCommandSpec BuildCount(string tableName, FilterNode? filter)
        {
            var builder = new SelectCommandBuilder(FormSchema, DatabaseType.Oracle, _defineAccess);
            return builder.BuildCount(tableName, filter);
        }

        /// <summary>
        /// Builds the DELETE command specification.
        /// </summary>
        /// <param name="tableName">The form table name.</param>
        /// <param name="filter">The filter that becomes the WHERE clause; must not be null.</param>
        public DbCommandSpec BuildDelete(string tableName, FilterNode filter)
        {
            var builder = new DeleteCommandBuilder(FormSchema, DatabaseType.Oracle);
            return builder.Build(tableName, filter);
        }
    }
}
