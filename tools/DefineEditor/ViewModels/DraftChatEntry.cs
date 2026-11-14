namespace Polhem.DefineEditor.ViewModels;

/// <summary>One message of the drafting conversation, as the chat pane shows it.</summary>
/// <param name="IsUser">Whether the user wrote it; otherwise the model or the editor did.</param>
/// <param name="Text">The text of the message.</param>
/// <param name="IsError">Whether it reports a failed turn.</param>
public sealed record DraftChatEntry(bool IsUser, string Text, bool IsError = false)
{
    /// <summary>Gets whether the model or the editor wrote it.</summary>
    public bool IsAssistant => !IsUser;
}
