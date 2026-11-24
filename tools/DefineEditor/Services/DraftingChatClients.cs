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
        var description = DescribeCredential(source.Kind, source.ProfileName, key => LocalizationService.Current[key]);
        return new DraftingCredential(source.Kind != ClaudeCredentialKind.None, description, source.Warnings);
    }

    /// <summary>
    /// The one-line description of a Claude credential in the interface language. The provider package describes it in
    /// English only, so the editor builds its own text from the kind and the profile name.
    /// </summary>
    /// <param name="kind">Where the credential comes from.</param>
    /// <param name="profileName">The profile name, for a profile.</param>
    /// <param name="lookup">Returns the string for a resource key, such as <see cref="LocalizationService"/>'s indexer.</param>
    /// <returns>The description.</returns>
    public static string DescribeCredential(ClaudeCredentialKind kind, string? profileName, Func<string, string> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        var key = kind switch
        {
            ClaudeCredentialKind.ApiKey => "Drafting_CredentialApiKey",
            ClaudeCredentialKind.AuthToken => "Drafting_CredentialAuthToken",
            ClaudeCredentialKind.Profile => "Drafting_CredentialProfile",
            ClaudeCredentialKind.FederatedProfile => "Drafting_CredentialFederatedProfile",
            ClaudeCredentialKind.WorkloadIdentity => "Drafting_CredentialWorkloadIdentity",
            _ => "Drafting_CredentialNone",
        };
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, lookup(key), profileName ?? string.Empty);
    }

    /// <summary>The purpose that marks the form drafting window's calls in <see cref="CallLogFile"/>.</summary>
    public const string FormDraftingPurpose = "form-drafting";

    /// <summary>The purpose that marks the rule assistant's calls in <see cref="CallLogFile"/>.</summary>
    public const string RuleAssistantPurpose = "rule-assistant";

    /// <summary>
    /// Creates a chat client for <paramref name="provider"/> that writes a line for each call to <see cref="CallLogFile"/>.
    /// </summary>
    /// <param name="provider">The provider name from <see cref="UserSettings.DraftingProvider"/>.</param>
    /// <param name="purpose">The feature that uses the client, such as <see cref="FormDraftingPurpose"/>.</param>
    /// <returns>A chat client; the caller disposes it.</returns>
    public static IChatClient Create(string provider, string purpose)
    {
        _ = provider;
        return new CallLogChatClient(ClaudeChatClientFactory.Create(new ClaudeChatOptions()), purpose, CallLogFile.Append);
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
