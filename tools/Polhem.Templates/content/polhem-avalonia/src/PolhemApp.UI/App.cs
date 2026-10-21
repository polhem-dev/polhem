using Polhem.UI.Avalonia.Shell;

namespace PolhemApp.UI;

/// <summary>
/// The application's front end: the Polhem shell (connect, sign in, and the forms of the server's menu) with this
/// application's title and the values the connection and sign-in screens start with. Every platform head starts it.
/// </summary>
public class App : ShellApplication
{
    /// <inheritdoc/>
    protected override ShellOptions CreateOptions() => new()
    {
        AppTitle = "PolhemApp",
        DefaultEndpoint = "http://localhost:5180/api",
        // The server checks only that a key is sent until an API key is issued (it logs a warning at startup). Issue
        // one before deploying and enter it on the connection screen.
        DefaultApiKey = "development",
        DefaultCompanyId = "POLHEM_COMPANY_ID",
        DefaultUserId = "admin",
    };
}
