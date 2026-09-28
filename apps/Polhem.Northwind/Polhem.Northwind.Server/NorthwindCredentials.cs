namespace Polhem.Northwind.Server;

/// <summary>
/// The demo accounts the seeder writes into <c>st_user</c>, and the single company the session
/// enters at login. The login screen the heads share surfaces the credentials so a fresh visitor
/// knows what to type.
/// </summary>
/// <remarks>
/// These are seed values, not a credential check: sign-in runs the framework's own <c>st_user</c>
/// authentication, so the password below is only ever hashed on the way in and compared against
/// the stored hash on the way back.
/// </remarks>
public static class NorthwindCredentials
{
    /// <summary>The demo user id.</summary>
    public const string UserId = "demo";

    /// <summary>The demo password.</summary>
    public const string Password = "demo";

    /// <summary>The display name surfaced through <c>SessionInfo.UserName</c>.</summary>
    public const string DisplayName = "Demo User";

    /// <summary>
    /// The demo user's IANA time zone, seeded into <c>st_user.time_zone</c>. The session takes its
    /// zone from the user's row, which is what every user-facing date is then resolved against.
    /// </summary>
    public const string TimeZone = "Asia/Taipei";

    /// <summary>The demo user's culture, seeded into <c>st_user.culture</c>.</summary>
    public const string Culture = "en-US";

    /// <summary>
    /// The Traditional Chinese demo user id. The account sees the same company and data as
    /// <see cref="UserId"/>; only its culture differs, so signing in with it shows the zh-TW
    /// captions and number and date formats.
    /// </summary>
    public const string ZhTwUserId = "demo-tw";

    /// <summary>The Traditional Chinese demo password.</summary>
    public const string ZhTwPassword = "demo";

    /// <summary>The Traditional Chinese demo user's display name.</summary>
    public const string ZhTwDisplayName = "示範使用者";

    /// <summary>The Traditional Chinese demo user's culture, seeded into <c>st_user.culture</c>.</summary>
    public const string ZhTwCulture = "zh-TW";

    /// <summary>
    /// The single demo company the session auto-enters at login. Company-scoped forms
    /// (<c>CategoryId="company"</c>) resolve their database through this id.
    /// </summary>
    public const string CompanyId = "NORTHWIND";

    /// <summary>The demo company display name.</summary>
    public const string CompanyName = "Northwind Traders";

    /// <summary>
    /// The tenant customization code the demo company maps onto. Becomes the folder name under
    /// <c>PathOptions.CustomizePath</c>, so the customization layer reads
    /// <c>Customize/northwind-demo/Language/{lang}/{namespace}.Language.xml</c>.
    /// </summary>
    /// <remarks>
    /// Companies map many-to-one onto a customization code, so one code with one company is the
    /// smallest arrangement that still exercises the layer. Clearing it — here or on the
    /// <c>CompanyInfo</c> — short-circuits every customization lookup back to the packaged layer.
    /// </remarks>
    public const string CustomizeId = "northwind-demo";

    /// <summary>
    /// The demo company's default currency, seeded into <c>st_company.default_currency</c>.
    /// </summary>
    /// <remarks>
    /// A company must carry one. The order form's <c>amount</c> binds no currency field, so its
    /// decimals resolve from this code through <c>Define/CurrencySettings.xml</c>; with the value
    /// empty the framework throws on that resolution instead of guessing.
    /// </remarks>
    public const string DefaultCurrency = "USD";

    /// <summary>
    /// The logical <c>DatabaseSettings</c> id backing the demo company — the
    /// <c>CompanyInfo.CompanyDatabaseId</c> the router resolves company scope to.
    /// </summary>
    public const string CompanyDatabaseId = "company";

    /// <summary>
    /// Hard-coded Base64 AES-CBC-HMAC combined key (64 bytes) used by the demo when
    /// <c>POLHEM_MASTER_KEY</c> is not set in the environment.
    /// </summary>
    /// <remarks>
    /// Demo-only: a fixed value lets a fresh clone <c>dotnet run</c> with zero setup. It also keeps
    /// sessions alive across a server restart: no API encryption key is configured, so each
    /// session's payload key is derived from this master key and its access token, and a session
    /// restored from <c>st_session</c> after a restart only decrypts if the master key is the same.
    /// This value is public, so anyone can derive those keys. Production hosts inject their own
    /// <c>POLHEM_MASTER_KEY</c> through the deployment mechanism before
    /// <see cref="NorthwindBackend.AddNorthwindBackend"/> runs.
    /// </remarks>
    public const string DemoMasterKey =
        "epzayQV2UPmasMTfmO91cY25/7J35oNUvkNahhYZCl7qEXOdwluR2e41BJ5WIT7c5zVkSFFaDxrXzMiIUe2Dxw==";
}
