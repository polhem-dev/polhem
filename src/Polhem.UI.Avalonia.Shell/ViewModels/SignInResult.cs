namespace Polhem.UI.Avalonia.Shell.ViewModels
{
    /// <summary>The outcome of a successful sign-in.</summary>
    /// <param name="UserName">The signed-in user's display name.</param>
    /// <param name="Culture">The account's culture name; empty when the server sent none.</param>
    internal sealed record SignInResult(string UserName, string Culture);
}
