namespace Polhem.DefineEditor.Services;

/// <summary>The credential an AI provider will use, as shown to the user. It never holds a key or a token.</summary>
/// <param name="IsAvailable">Whether a credential was found at all.</param>
/// <param name="Description">Where the credential comes from, in English, such as the profile name.</param>
/// <param name="Warnings">English notes about the setup that may surprise the user.</param>
public sealed record DraftingCredential(bool IsAvailable, string Description, IReadOnlyList<string> Warnings);
