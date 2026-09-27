namespace Polhem.Base.Data
{
    /// <summary>
    /// Extension methods for <see cref="FieldDbType"/>.
    /// </summary>
    public static class FieldDbTypeExtensions
    {
        /// <summary>
        /// Returns the default value for the specified field database type.
        /// </summary>
        /// <param name="dbType">The field database type.</param>
        public static object GetDefaultValue(this FieldDbType dbType)
        {
            switch (dbType)
            {
                case FieldDbType.String:
                case FieldDbType.Text:
                // A time of day has no spare value to stand for "unset" — `00:00` is a legal
                // midnight — so an unfilled time is the empty string, never `"00:00"` (ADR-033).
                case FieldDbType.Time:
                    return string.Empty;
                case FieldDbType.Boolean:
                    return false;
                case FieldDbType.Integer:
                case FieldDbType.Decimal:
                case FieldDbType.Currency:
                    return 0;
                // No user context reaches here, so this uses UTC rather than a user zone (ADR-032 D12).
                // `DbParameterSpecCollection` calls it to give a NOT NULL parameter a value, which is a
                // data integrity backstop and not a value the user reads. `AddColumn` deliberately does
                // not use it for date types, because a column default is fixed when the column is built.
                // User-facing new-row defaults come from `FormRowDefaults`, which is given the session's zone.
                case FieldDbType.Date:
                    return DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
                case FieldDbType.DateTime:
                    return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                case FieldDbType.Guid:
                    return Guid.Empty;
                default:
                    return DBNull.Value;
            }
        }
    }
}
