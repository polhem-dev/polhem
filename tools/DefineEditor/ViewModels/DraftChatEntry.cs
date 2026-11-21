namespace Polhem.DefineEditor.ViewModels;

/// <summary>One message of the drafting conversation, as the chat pane shows it.</summary>
public sealed class DraftChatEntry : DraftChatItem
{
    /// <summary>Initializes a new instance of the <see cref="DraftChatEntry"/> class.</summary>
    /// <param name="isUser">Whether the user wrote it; otherwise the model or the editor did.</param>
    /// <param name="text">The text of the message.</param>
    /// <param name="isError">Whether it reports a failed turn.</param>
    public DraftChatEntry(bool isUser, string text, bool isError = false)
    {
        IsUser = isUser;
        Text = text;
        IsError = isError;
    }

    /// <summary>Gets whether the user wrote it.</summary>
    public bool IsUser { get; }

    /// <summary>Gets the text of the message.</summary>
    public string Text { get; }

    /// <summary>Gets whether it reports a failed turn.</summary>
    public bool IsError { get; }

    /// <summary>Gets whether the model or the editor wrote it.</summary>
    public bool IsAssistant => !IsUser;

    /// <summary>
    /// Gets whether the text is rendered as Markdown: a reply of the model. What the user typed and the editor's own
    /// error messages show as written.
    /// </summary>
    public bool RendersMarkdown => !IsUser && !IsError;
}
