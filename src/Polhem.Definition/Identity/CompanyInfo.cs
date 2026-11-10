using Polhem.Core;
using Polhem.Definition.Settings;

namespace Polhem.Definition.Identity
{
    /// <summary>
    /// Metadata describing a company that a user may enter for a session.
    /// </summary>
    /// <remarks>
    /// Returned by <c>EnterCompany</c> and resolved by company-aware repositories
    /// via the cached <see cref="ICompanyInfoService"/>. <c>CompanyDatabaseId</c> references
    /// a logical <see cref="DatabaseSettings"/> entry. Multiple companies may point at the
    /// same id, and then share that database's tables. The company tables carry
    /// <see cref="SysFields.CompanyId"/>, and the form repository keeps each company to its own rows;
    /// hand-written queries are not confined, so a query an application writes itself has to filter on the
    /// column. The log database is shared across all companies under a fixed
    /// <c>"log"</c> databaseId (see <see cref="DbScope.Log"/>), so there is no per-company
    /// log database id property.
    /// <para>
    /// WARNING: this is a cache-shared instance. It must not be mutated after it is loaded — every
    /// session in the company receives the same reference, and <see cref="CompanyDatabaseId"/>
    /// selects which database that company's repositories read and write, so a mutation redirects
    /// other sessions' data access. The properties are init-only; the override tables are still
    /// mutable collections, so treat an instance handed to you by the cache as frozen. See
    /// <c>docs/en/architecture/development-constraints.md</c> § <i>Cached Data Immutability After Init</i>.
    /// </para>
    /// </remarks>
    public sealed class CompanyInfo : IKeyObject
    {
        #region IKeyObject Interface

        /// <summary>
        /// Gets the item key value (the company id).
        /// </summary>
        public string GetKey()
        {
            return this.CompanyId;
        }

        #endregion

        /// <summary>
        /// The longest company id the framework accepts when it creates a company: the width of
        /// <see cref="SysFields.CompanyId"/>, which carries a copy of the id on every company row.
        /// </summary>
        public const int CompanyIdMaxLength = 10;

        /// <summary>
        /// Returns the canonical form of a company id: lowercase.
        /// </summary>
        /// <param name="companyId">The company id as a caller wrote it.</param>
        /// <returns>The id in the form <c>st_company.sys_id</c> stores it.</returns>
        /// <remarks>
        /// The framework compares company ids with <see cref="StringComparison.OrdinalIgnoreCase"/>,
        /// while a database compares them by its collation: case-sensitive on PostgreSQL and Oracle,
        /// case-insensitive on SQL Server and MySQL by default, and on SQLite through the
        /// <c>COLLATE NOCASE</c> the framework gives its text columns. One stored form keeps the two from
        /// disagreeing about whether <c>C001</c> and <c>c001</c> are the same company. Every id that reaches a query on <c>st_company</c>
        /// or is bound to a session passes through here first.
        /// </remarks>
        public static string NormalizeId(string companyId)
        {
            ArgumentNullException.ThrowIfNull(companyId);
            return companyId.ToLowerInvariant();
        }

        /// <summary>
        /// Gets whether <paramref name="companyId"/> is a valid canonical company id: one to
        /// <see cref="CompanyIdMaxLength"/> characters, each a lowercase ASCII letter or a digit.
        /// </summary>
        /// <param name="companyId">The id to check, normally the result of <see cref="NormalizeId"/>.</param>
        public static bool IsValidId(string? companyId)
            => !string.IsNullOrEmpty(companyId)
               && companyId.Length <= CompanyIdMaxLength
               && companyId.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c));

        /// <summary>
        /// Gets the company id (unique key).
        /// </summary>
        public string CompanyId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the company display name.
        /// </summary>
        public string CompanyName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the logical <see cref="DatabaseSettings"/> id used for the
        /// company-category database during this session.
        /// </summary>
        public string CompanyDatabaseId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the tenant customization code for this company.
        /// </summary>
        /// <remarks>
        /// Empty means the standard (non-customized) deployment. Companies map many-to-one onto a
        /// customization code (a group can share one customization set). Loaded from the
        /// <c>customize_id</c> column by <c>CompanyRepository</c>; <c>EnterCompany</c> copies it
        /// into <see cref="SessionInfo.CustomizeId"/> for the session's customization overlay.
        /// </remarks>
        public string CustomizeId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the company-level decimal-places override table. Empty means every kind uses
        /// the framework default. Loaded from the <c>number_formats_xml</c> column by
        /// <c>CompanyRepository</c>; carries Percent and UnitPrice/Cost display decimals. Quantity and
        /// Weight entries have no effect: <see cref="NumberFormatResolver"/> resolves those kinds from
        /// their unit, never from the company.
        /// </summary>
        public CompanyNumberFormats NumberFormats { get; init; } = [];

        /// <summary>
        /// Gets the decimal places for the specified kind: the company override when present,
        /// otherwise the framework default from <see cref="NumberKindProfile.GetDefaultDecimals"/>.
        /// </summary>
        /// <param name="kind">The number kind.</param>
        public int GetDecimals(NumberKind kind)
        {
            return NumberFormats.FindDecimals(kind) ?? NumberKindProfile.GetDefaultDecimals(kind);
        }

        /// <summary>
        /// Gets the company's default (local/home) currency code — an ISO 4217 alpha-3 code
        /// matching a <see cref="CurrencySettings"/> entry. Loaded from the <c>default_currency</c> column
        /// by <c>CompanyRepository</c>.
        /// </summary>
        /// <remarks>
        /// Required. An empty value is a configuration error, but it is not rejected when the company is
        /// loaded or entered: <see cref="NumberFormatResolver"/> throws <see cref="InvalidOperationException"/>
        /// when it resolves an amount with no reference currency for this company. The property still
        /// defaults to an empty string so the serializers can round-trip an unset value.
        /// </remarks>
        public string DefaultCurrency { get; init; } = string.Empty;

        /// <summary>
        /// Gets the company-level cash-rounding override table (SAP T001R-style). Empty means
        /// no extra cash rounding — final amounts stay at each currency's natural minor unit. Loaded
        /// from the <c>cash_rounding_xml</c> column by <c>CompanyRepository</c>.
        /// </summary>
        public CompanyCashRounding CashRounding { get; init; } = [];

        /// <summary>
        /// Gets the company's allowed-currency whitelist. Empty means every system currency is
        /// usable. Drives the currency drop-down options on documents. Loaded from the
        /// <c>allowed_currencies_xml</c> column by <c>CompanyRepository</c>.
        /// </summary>
        public CompanyAllowedCurrencies AllowedCurrencies { get; init; } = [];

        /// <summary>
        /// Gets the effective cash-rounding unit for the specified currency: the company override when
        /// present, otherwise the currency's natural minor unit from <paramref name="currencySettings"/>.
        /// </summary>
        /// <param name="currencyCode">The ISO 4217 alpha-3 currency code.</param>
        /// <param name="currencySettings">The system currency master used for the natural-unit fallback.</param>
        public decimal GetCashRounding(string currencyCode, CurrencySettings currencySettings)
        {
            return CashRounding.GetCashRounding(currencyCode, currencySettings);
        }

        /// <summary>
        /// Gets the effective list of usable currency codes: the company whitelist when non-empty,
        /// otherwise every code defined in <paramref name="currencySettings"/>.
        /// </summary>
        /// <param name="currencySettings">The system currency master used when the whitelist is empty.</param>
        public IReadOnlyList<string> GetAllowedCurrencies(CurrencySettings currencySettings)
        {
            return AllowedCurrencies.GetAllowedCurrencies(currencySettings);
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override string ToString()
        {
            return $"{CompanyId} : {CompanyName}";
        }
    }
}
