using Polhem.Definition.Database;
using Polhem.Core.Data;
using Polhem.Definition;
using System.Data;
using System.Globalization;
using System.Text;

namespace Polhem.Db.Dml
{
    /// <summary>
    /// Generates Insert, Update, and Delete database commands based on a <see cref="TableSchema"/>;
    /// can also package them directly into a <see cref="DataTableUpdateSpec"/>.
    /// </summary>
    public sealed class TableSchemaCommandBuilder
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TableSchemaCommandBuilder"/>.
        /// </summary>
        /// <param name="databaseType">The database type.</param>
        /// <param name="tableSchema">The table schema definition.</param>
        public TableSchemaCommandBuilder(DatabaseType databaseType, TableSchema tableSchema)
        {
            DatabaseType = databaseType;
            TableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema), "TableSchema cannot be null.");
        }

        /// <summary>
        /// Gets the database type.
        /// </summary>
        public DatabaseType DatabaseType { get; }

        /// <summary>
        /// Gets the table schema definition.
        /// </summary>
        public TableSchema TableSchema { get; }

        /// <summary>
        /// Gets the field the generated statements pin to <see cref="PinnedValue"/>, or an empty string
        /// when none is pinned.
        /// </summary>
        public string PinnedField { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the value <see cref="PinnedField"/> is pinned to.
        /// </summary>
        public object? PinnedValue { get; private set; }

        /// <summary>
        /// Pins one field of the table to a fixed value in every statement this builder generates:
        /// the INSERT writes <paramref name="value"/> into it whatever the row holds, the UPDATE leaves
        /// it out of the SET list, and the UPDATE and DELETE only touch a row that holds
        /// <paramref name="value"/>.
        /// </summary>
        /// <param name="fieldName">A field of <see cref="TableSchema"/>.</param>
        /// <param name="value">The value the field is pinned to.</param>
        /// <returns>This builder.</returns>
        /// <remarks>
        /// The value is a parameter of its own rather than one read from the
        /// <see cref="System.Data.DataTable"/>, so a row cannot supply a different one, and a row that
        /// holds another value is reported by the update as affecting no rows.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the table schema has no such field.</exception>
        public TableSchemaCommandBuilder PinField(string fieldName, object value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
            ArgumentNullException.ThrowIfNull(value);
            if (!TableSchema.Fields!.Contains(fieldName))
            {
                throw new InvalidOperationException(
                    $"Table '{TableSchema.TableName}' has no field '{fieldName}' to pin.");
            }
            PinnedField = fieldName;
            PinnedValue = value;
            return this;
        }

        private bool IsPinned(DbField field)
            => PinnedField.Length > 0 && string.Equals(field.FieldName, PinnedField, StringComparison.OrdinalIgnoreCase);

        private void AddPinnedParameter(DbCommandSpec command)
        {
            var field = TableSchema.Fields![PinnedField];
            command.Parameters.Add(new DbParameterSpec
            {
                Name = field.FieldName,
                DbType = DbTypeConverter.ToDbType(field.DbType),
                Value = PinnedValue,
                Size = (field.DbType == FieldDbType.String) ? field.Length : 0,
            });
        }

        /// <summary>
        /// Adds the parameter an INSERT reads one field from: the fixed value for the pinned field, the
        /// row's value for any other.
        /// </summary>
        private void AddInsertParameter(DbCommandSpec command, DbField field)
        {
            if (IsPinned(field))
                AddPinnedParameter(command);
            else
                command.Parameters.Add(field);
        }

        private void AppendPinnedCondition(StringBuilder buffer, DbCommandSpec command)
        {
            if (PinnedField.Length == 0) { return; }
            buffer.AppendLine(CultureInfo.InvariantCulture,
                $" And {QuoteIdentifier(PinnedField)}={GetParameterName(PinnedField)}");
            AddPinnedParameter(command);
        }

        /// <summary>
        /// Returns the properly quoted identifier string for the current database type.
        /// </summary>
        /// <param name="identifier">The identifier name to quote.</param>
        /// <returns>The quoted identifier.</returns>
        private string QuoteIdentifier(string identifier)
        {
            return DatabaseType.QuoteIdentifier(identifier);
        }

        /// <summary>
        /// Gets the full parameter name including its prefix character.
        /// </summary>
        /// <param name="name">The parameter name without the prefix character.</param>
        private string GetParameterName(string name)
        {
            return DatabaseType.GetParameterName(name);
        }

        /// <summary>
        /// Builds the INSERT command specification.
        /// </summary>
        public DbCommandSpec BuildInsertCommand()
        {
            var command = new DbCommandSpec();
            var buffer = new StringBuilder();
            string tableName = QuoteIdentifier(this.TableSchema.TableName);
            buffer.AppendLine(CultureInfo.InvariantCulture, $"Insert Into {tableName} ");

            // Build the INSERT column list
            buffer.Append('(');
            int count = 0;
            foreach (DbField field in this.TableSchema.Fields!)
            {
                if (field.DbType != FieldDbType.AutoIncrement)
                {
                    if (count > 0)
                        buffer.Append(", ");
                    buffer.Append(QuoteIdentifier(field.FieldName));
                    count++;
                }
            }
            buffer.AppendLine(")");

            // Build the INSERT values list
            buffer.AppendLine(" Values ");
            buffer.Append('(');
            count = 0;
            foreach (DbField field in this.TableSchema.Fields)
            {
                if (field.DbType != FieldDbType.AutoIncrement)
                {
                    if (count > 0)
                        buffer.Append(", ");
                    buffer.Append(GetParameterName(field.FieldName));
                    AddInsertParameter(command, field);
                    count++;
                }
            }
            buffer.AppendLine(")");

            command.CommandText = buffer.ToString();
            return command;
        }

        /// <summary>
        /// Builds the UPDATE command specification.
        /// </summary>
        public DbCommandSpec BuildUpdateCommand()
        {
            var command = new DbCommandSpec();
            var buffer = new StringBuilder();
            string tableName = QuoteIdentifier(this.TableSchema.TableName);
            buffer.AppendLine(CultureInfo.InvariantCulture, $"Update {tableName} Set ");

            string fieldName;
            // Get the primary key field
            var keyField = this.TableSchema.Fields![SysFields.RowId];
            // Build the SET clause with field names and parameter values
            int setCount = 0;
            foreach (DbField field in this.TableSchema.Fields)
            {
                if (field != keyField && field.DbType != FieldDbType.AutoIncrement && !IsPinned(field))
                {
                    fieldName = QuoteIdentifier(field.FieldName);
                    // Add command parameter
                    command.Parameters.Add(field);
                    if (setCount > 0)
                        buffer.Append(", ");
                    buffer.Append(CultureInfo.InvariantCulture, $"{fieldName}={GetParameterName(field.FieldName)}");
                    setCount++;
                }
            }
            // Add primary key condition to WHERE clause
            fieldName = QuoteIdentifier(keyField.FieldName);
            command.Parameters.Add(keyField, System.Data.DataRowVersion.Original);
            buffer.AppendLine();
            buffer.AppendLine(CultureInfo.InvariantCulture, $"Where {fieldName}={GetParameterName(keyField.FieldName)}");
            AppendPinnedCondition(buffer, command);

            command.CommandText = buffer.ToString();
            return command;
        }

        /// <summary>
        /// Builds the DELETE command specification.
        /// </summary>
        public DbCommandSpec BuildDeleteCommand()
        {
            var command = new DbCommandSpec();
            var buffer = new StringBuilder();
            string tableName = QuoteIdentifier(this.TableSchema.TableName);
            buffer.AppendLine(CultureInfo.InvariantCulture, $"Delete From {tableName} ");

            // Add primary key condition to WHERE clause
            var keyField = this.TableSchema.Fields![SysFields.RowId];
            string fieldName = QuoteIdentifier(keyField.FieldName);
            command.Parameters.Add(keyField, System.Data.DataRowVersion.Original);
            buffer.AppendLine(CultureInfo.InvariantCulture, $"Where {fieldName}={GetParameterName(keyField.FieldName)}");
            AppendPinnedCondition(buffer, command);

            command.CommandText = buffer.ToString();
            return command;
        }

        /// <summary>
        /// Builds a <see cref="DataTableUpdateSpec"/> by packaging the Insert, Update, and Delete commands together with the specified DataTable.
        /// </summary>
        /// <param name="dataTable">The DataTable to write back to the database.</param>
        /// <returns>A <see cref="DataTableUpdateSpec"/> containing the three command specifications.</returns>
        public DataTableUpdateSpec BuildUpdateSpec(DataTable dataTable)
        {
            if (dataTable == null)
                throw new ArgumentNullException(nameof(dataTable), "DataTable cannot be null.");

            var insertCmd = BuildInsertCommand();
            var updateCmd = BuildUpdateCommand();
            var deleteCmd = BuildDeleteCommand();

            return new DataTableUpdateSpec()
            {
                DataTable = dataTable,
                InsertCommand = insertCmd,
                UpdateCommand = updateCmd,
                DeleteCommand = deleteCmd
            };
        }
    }
}
