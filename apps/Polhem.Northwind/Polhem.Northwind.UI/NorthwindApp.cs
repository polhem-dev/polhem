using Polhem.UI.Avalonia.Shell;

namespace Polhem.Northwind.UI;

/// <summary>
/// The Northwind front end: the framework's shell with Northwind's title and the values the demo signs in with.
/// Every platform head starts this class.
/// </summary>
/// <remarks>
/// The values match what <c>Polhem.Northwind.Server</c> listens on and seeds. They are spelled out here rather than
/// shared with the server's <c>NorthwindCredentials</c>: this project is a remote JSON-RPC client and references no
/// server assembly.
/// </remarks>
public class NorthwindApp : ShellApplication
{
    /// <inheritdoc/>
    protected override ShellOptions CreateOptions() => new()
    {
        AppTitle = "Polhem.Northwind",
        DefaultEndpoint = "http://localhost:5100/api",
        DefaultApiKey = "northwind-demo",
        DefaultCompanyId = "NORTHWIND",
        DefaultUserId = "demo",
        DefaultPassword = "demo",
        // The seeder writes `demo-tw` as well: the same account with a zh-TW culture.
        SignInHint = "Use demo / demo, or demo-tw / demo for Traditional Chinese.",
    };
}
