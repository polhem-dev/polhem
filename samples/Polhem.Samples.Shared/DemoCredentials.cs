namespace Polhem.Samples.Shared;

/// <summary>
/// The demo account <see cref="DemoAuthenticatingSystemBusinessObject"/> accepts, and the single
/// company the demo session enters after sign-in. The Blazor demos surface the credentials on their
/// landing page so a fresh visitor knows what to type in the <c>PolhemLoginPanel</c>.
/// </summary>
public static class DemoCredentials
{
    /// <summary>The demo user id (shown in the Blazor login panel hint).</summary>
    public const string UserId = "demo";

    /// <summary>The demo password (shown in the Blazor login panel hint).</summary>
    public const string Password = "demo";

    /// <summary>The display name surfaced through <c>SessionInfo.UserName</c>.</summary>
    public const string DisplayName = "Demo User";

    /// <summary>
    /// The single demo company, seeded into <c>st_company</c>. Every client enters it after sign-in:
    /// the demo forms are <c>CategoryId="company"</c>, and a session that has not entered a company
    /// cannot open them.
    /// </summary>
    public const string CompanyId = "DEMO";

    /// <summary>The demo company display name.</summary>
    public const string CompanyName = "Demo Company";

    /// <summary>
    /// The logical <c>DatabaseSettings</c> id backing the demo company, which the router resolves
    /// the company scope to.
    /// </summary>
    public const string CompanyDatabaseId = "company";

    /// <summary>
    /// The demo company's default currency, seeded into <c>st_company.default_currency</c>.
    /// </summary>
    public const string DefaultCurrency = "USD";

    /// <summary>
    /// Hard-coded Base64 AES-CBC-HMAC combined key (64 bytes) used by the bundled
    /// demos when <c>POLHEM_MASTER_KEY</c> is not set in the environment.
    /// </summary>
    /// <remarks>
    /// Demo-only: a fixed value lets a fresh clone <c>dotnet run</c> with zero setup. It also keeps
    /// sessions alive across a restart: no API encryption key is configured, so each session's
    /// payload key is derived from this master key and its access token, and a session restored
    /// from <c>st_session</c> after a restart only decrypts if the master key is the same.
    /// This value is public, so anyone can derive those keys. Production hosts inject their own
    /// <c>POLHEM_MASTER_KEY</c> through the deployment mechanism (K8s Secret, env file, Vault, etc.)
    /// before <see cref="DemoBackend.AddPolhemBackend"/> runs; see <c>samples/README.md</c>.
    /// </remarks>
    public const string DemoMasterKey =
        "epzayQV2UPmasMTfmO91cY25/7J35oNUvkNahhYZCl7qEXOdwluR2e41BJ5WIT7c5zVkSFFaDxrXzMiIUe2Dxw==";
}
