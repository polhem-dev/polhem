using System.Text.RegularExpressions;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Hides the values of password keys in text DefineEditor shows from a child process: the command line it runs, the
/// package sources it names and every line the process writes.
/// </summary>
/// <remarks>
/// The <c>db upgrade</c> command does not print connection strings, but its failure lines carry the message of a
/// database driver's exception, which DefineEditor does not control.
/// </remarks>
public static class SecretMasker
{
    /// <summary>What a hidden value is replaced with.</summary>
    public const string Mask = "********";

    private static readonly Regex s_passwordValue = new(
        @"(?<key>\b(?:user\s+)?(?:password|pwd)\s*[=:]\s*)(?<value>""[^""]*""|'[^']*'|[^;\s]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex s_urlUserInfo = new(
        @"(?<scheme>\b[a-z][a-z0-9+.-]*://)(?<user>[^/@:\s]+):(?<secret>[^/@\s]+)@",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Returns <paramref name="text"/> with the value of every password key, and the password in a URL's user info
    /// (<c>https://user:token@host</c>), replaced by <see cref="Mask"/>.
    /// </summary>
    public static string Hide(string text)
    {
        var masked = s_passwordValue.Replace(text, m => m.Groups["value"].Length == 0 ? m.Value : m.Groups["key"].Value + Mask);
        return s_urlUserInfo.Replace(masked, m => m.Groups["scheme"].Value + m.Groups["user"].Value + ":" + Mask + "@");
    }
}
