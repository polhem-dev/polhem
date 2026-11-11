namespace Polhem.Api.Core.Wire
{
    /// <summary>
    /// The one rule both body codecs apply to a <see cref="DateTime"/> outside a <see cref="System.Data.DataTable"/>:
    /// it travels as a UTC instant and arrives with <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MessagePack's timestamp extension stores an absolute instant, so it already behaves this way: a
    /// <see cref="DateTimeKind.Local"/> value is converted to UTC with the writer's time zone, an
    /// <see cref="DateTimeKind.Unspecified"/> value is taken to be UTC, and every value is read back as
    /// <see cref="DateTimeKind.Utc"/>. The JSON codec used to keep the <see cref="DateTime.Kind"/> instead, so the same
    /// request delivered an Unspecified value over one codec and a UTC value over the other, and a Local value written
    /// with an offset was read back in the server's own time zone. The JSON codec applies this rule to match.
    /// <c>WireDateTimeKindTests</c> checks it for a parameter value, filter values and message properties;
    /// <c>WireCodecParityTests</c> samples every <see cref="DateTime"/> as UTC, so it does not see a kind difference.
    /// </para>
    /// <para>
    /// <see cref="System.Data.DataTable"/> cells are not covered. Their columns use
    /// <see cref="System.Data.DataSetDateTime.Unspecified"/> and carry the naive values ADR-032 converts in the client's
    /// connector.
    /// </para>
    /// </remarks>
    internal static class WireDateTime
    {
        /// <summary>
        /// Returns the UTC instant the value stands for: a Local value is converted with this process's time zone, and an
        /// Unspecified value is taken to be UTC already.
        /// </summary>
        /// <param name="value">The value to normalize.</param>
        public static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };


        /// <summary>
        /// The parse styles that read a string as the UTC instant it names: text with an offset is moved to UTC by that
        /// offset, and text without one is taken to be UTC.
        /// </summary>
        /// <remarks>
        /// Parsing with <see cref="System.Globalization.DateTimeStyles.RoundtripKind"/> instead turns text with an offset
        /// into a Local value in this process's zone first. Near either end of the range that value does not exist, so the
        /// text was refused or clamped depending on the server's zone.
        /// </remarks>
        public const System.Globalization.DateTimeStyles ParseStyles =
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal;
    }
}
