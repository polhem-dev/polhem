using Microsoft.Extensions.AI;
using Polhem.AI.Claude;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// The AI providers that can draft a form from documents. The drafting feature knows only <see cref="IChatClient"/>;
/// this is the one place that knows the provider packages, so adding a provider changes nothing else.
/// </summary>
/// <remarks>
/// The editor keeps no credential of its own. Each provider resolves its credential through its own tooling (for
/// Claude, the Anthropic SDK reads the <c>ant auth login</c> profile or the <c>ANTHROPIC_API_KEY</c> environment
/// variable), and the editor only shows which one will be used.
/// </remarks>
public static class DraftingChatClients
{
    /// <summary>The name of the Claude provider, the default and currently the only one.</summary>
    public const string Claude = "Claude";

    /// <summary>Describes the credential <paramref name="provider"/> will use, without reading any secret.</summary>
    /// <param name="provider">The provider name from <see cref="UserSettings.DraftingProvider"/>.</param>
    /// <returns>The description.</returns>
    public static DraftingCredential DescribeCredential(string provider)
    {
        // Every provider name maps to Claude until a second provider package exists.
        _ = provider;
        var source = ClaudeCredentials.Describe();
        return new DraftingCredential(source.Kind != ClaudeCredentialKind.None, source.Description, source.Warnings);
    }

    /// <summary>Creates a chat client for <paramref name="provider"/>.</summary>
    /// <param name="provider">The provider name from <see cref="UserSettings.DraftingProvider"/>.</param>
    /// <returns>A chat client; the caller disposes it.</returns>
    public static IChatClient Create(string provider)
    {
        _ = provider;
        return ClaudeChatClientFactory.Create(new ClaudeChatOptions());
    }

    /// <summary>
    /// The message to show for a failed turn, in the interface language, when the provider reports the failure in a way
    /// the user can act on; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="exception">What the turn threw.</param>
    /// <returns>The message, or <see langword="null"/>.</returns>
    public static string? DescribeFailure(Exception exception)
    {
        if (exception is not ClaudeChatException claude) return null;
        // The raw text of an authentication failure is of no use: the sign-in expires on a fixed schedule however often
        // it is used, so the answer is always to sign in again.
        return LocalizationService.Current[claude.Kind switch
        {
            ClaudeFailureKind.NotAuthenticated => "Drafting_NotAuthenticated",
            ClaudeFailureKind.Refused => "Drafting_Refused",
            _ => "Drafting_OutputLimitReached",
        }];
    }
}
