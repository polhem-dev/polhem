namespace Polhem.Analyzers.Definitions
{
    /// <summary>
    /// Decides whether a relation field's declared data type holds the values of the source field it is mapped from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Types are compared by how the framework holds their values, not by name: <c>Integer</c> and
    /// <c>AutoIncrement</c> are both a 32-bit integer, <c>Decimal</c> and <c>Currency</c> both a decimal, and
    /// <c>String</c> and <c>Text</c> both text. An integer destination wider than its source (<c>Short</c> into
    /// <c>Integer</c> or <c>Long</c>, <c>Integer</c> into <c>Long</c>) holds every source value, and the database
    /// compares the two widths, so it is accepted; a narrower one is not.
    /// </para>
    /// <para>
    /// IMPORTANT: DefineEditor compiles this file too (a linked <c>Compile</c> item in its project), so the editor's
    /// relation dialog and POLHEM2008 give one verdict. Keep it free of anything outside the base class library that
    /// both a netstandard2.0 analyzer and a net10.0 application can compile.
    /// </para>
    /// </remarks>
    internal static class RelationFieldTypes
    {
        /// <summary>
        /// Determines whether a destination declared as <paramref name="destinationType"/> holds the values of a source
        /// declared as <paramref name="sourceType"/>.
        /// </summary>
        /// <param name="destinationType">The relation field's data type name, such as <c>String</c>.</param>
        /// <param name="sourceType">The source field's data type name.</param>
        /// <returns><c>true</c> when every source value is held the same way by the destination.</returns>
        public static bool Holds(string destinationType, string sourceType)
        {
            var destination = ValueKind(destinationType);
            var source = ValueKind(sourceType);
            if (string.Equals(destination, source, StringComparison.Ordinal))
                return true;

            var destinationWidth = IntegerWidth(destination);
            var sourceWidth = IntegerWidth(source);
            return destinationWidth > 0 && sourceWidth > 0 && destinationWidth >= sourceWidth;
        }

        private static int IntegerWidth(string valueKind) => valueKind switch
        {
            "Short" => 16,
            "Integer" => 32,
            "Long" => 64,
            _ => 0,
        };

        private static string ValueKind(string dbType) => dbType switch
        {
            "AutoIncrement" => "Integer",
            "Currency" => "Decimal",
            "Text" => "String",
            _ => dbType,
        };
    }
}
