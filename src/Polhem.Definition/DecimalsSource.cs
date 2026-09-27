namespace Polhem.Definition
{
    /// <summary>
    /// The source that determines the decimal places for a <see cref="NumberKind"/>.
    /// </summary>
    /// <remarks>
    /// How each source turns into a decimal count, including the fallbacks when no reference
    /// currency or unit is resolved, is described on <see cref="NumberFormatResolver"/>.
    /// </remarks>
    public enum DecimalsSource
    {
        /// <summary>Company override table (<see cref="CompanyNumberFormats"/>), falling back to the framework default.</summary>
        Company = 0,

        /// <summary>Bound currency key field (SAP CUKY); falls back to the company's default currency when the field is empty.</summary>
        Currency,

        /// <summary>Bound unit-of-measure field (SAP UNIT); falls back to the framework default, never the company, when no unit is resolved.</summary>
        Unit,

        /// <summary>System-fixed framework default, independent of company or reference field.</summary>
        SystemFixed,
    }
}
