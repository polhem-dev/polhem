using System.Globalization;
using Polhem.Core.Data;
using Polhem.Definition.Database;

namespace Polhem.Db.Providers
{
    /// <summary>
    /// Validates the <see cref="DbField.DefaultValue"/> of a non-string column before a dialect emits it
    /// unquoted into a <c>DEFAULT</c> clause.
    /// </summary>
    /// <remarks>
    /// String-like defaults are quoted and escaped by each dialect. Every other default is written
    /// into the DDL as it stands, so anything but a plain literal of the column's type would run as
    /// SQL. The check is dialect-neutral: a signed integer, a decimal with a <c>.</c> separator and
    /// <c>0</c> / <c>1</c> for a boolean read the same in every supported database.
    /// </remarks>
    internal static class DefaultValueLiteral
    {
        private const NumberStyles IntegerStyle = NumberStyles.AllowLeadingSign;
        private const NumberStyles DecimalStyle = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

        /// <summary>
        /// Returns the field's default value when it is a literal of the field's type.
        /// </summary>
        /// <param name="field">A field whose default value is not empty and is emitted unquoted.</param>
        /// <returns>The default value, unchanged.</returns>
        /// <exception cref="InvalidOperationException">
        /// The value is not a literal of the field's type, or the type has no unquoted literal form
        /// (dates, times of day, GUIDs, binary), in which case the default must be left empty so the
        /// framework's own default applies.
        /// </exception>
        public static string Require(DbField field)
        {
            ArgumentNullException.ThrowIfNull(field);

            string value = field.DefaultValue;
            if (IsLiteral(field.DbType, value)) { return value; }

            throw new InvalidOperationException(
                $"The default value of field '{field.FieldName}' is not a valid {field.DbType} literal. " +
                "Use a plain number (or 0 / 1 for Boolean), or leave it empty for the built-in default.");
        }

        private static bool IsLiteral(FieldDbType dbType, string value) => dbType switch
        {
            FieldDbType.Boolean => value is "0" or "1",
            FieldDbType.Short => short.TryParse(value, IntegerStyle, CultureInfo.InvariantCulture, out _),
            FieldDbType.Integer => int.TryParse(value, IntegerStyle, CultureInfo.InvariantCulture, out _),
            FieldDbType.Long => long.TryParse(value, IntegerStyle, CultureInfo.InvariantCulture, out _),
            FieldDbType.Decimal or FieldDbType.Currency
                => decimal.TryParse(value, DecimalStyle, CultureInfo.InvariantCulture, out _),
            _ => false,
        };
    }
}
