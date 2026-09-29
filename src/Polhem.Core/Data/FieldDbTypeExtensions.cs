namespace Polhem.Core.Data
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
        /// <returns>
        /// The type's empty value, boxed as the CLR type the column holds, or <see cref="DBNull.Value"/>
        /// for <see cref="FieldDbType.AutoIncrement"/> and <see cref="FieldDbType.Unknown"/>, which have
        /// no empty value.
        /// </returns>
        /// <remarks>
        /// This is the single table of per-type defaults. <c>FormRowDefaults.DefaultForDbType</c> in
        /// <c>Polhem.Definition</c> uses it for every type except <see cref="FieldDbType.Date"/> and
        /// <see cref="FieldDbType.DateTime"/>, where it reads the user's clock instead.
        /// </remarks>
        public static object GetDefaultValue(this FieldDbType dbType) => dbType switch
        {
            // A time of day has no spare value to stand for "unset" — `00:00` is a legal midnight — so
            // an unfilled time is the empty string, never `"00:00"` (ADR-033).
            FieldDbType.String or FieldDbType.Text or FieldDbType.Time => string.Empty,
            FieldDbType.Boolean => false,
            FieldDbType.Short => (short)0,
            FieldDbType.Integer => 0,
            FieldDbType.Long => 0L,
            FieldDbType.Decimal or FieldDbType.Currency => 0m,
            // No user context reaches here, so this uses UTC rather than a user zone (ADR-032 D12).
            // `DbParameterSpecCollection` calls it to give a NOT NULL parameter a value, which is a
            // data integrity backstop and not a value the user reads. `AddColumn` deliberately does
            // not use it for date types, because a column default is fixed when the column is built.
            // User-facing new-row defaults come from `FormRowDefaults`, which is given the session's zone.
            FieldDbType.Date => DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified),
            FieldDbType.DateTime => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            FieldDbType.Guid => Guid.Empty,
            FieldDbType.Binary => Array.Empty<byte>(),
            _ => DBNull.Value,
        };
    }
}
