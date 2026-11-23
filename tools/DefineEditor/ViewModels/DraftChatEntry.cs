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
        _text = text;
        IsError = isError;
    }

    /// <summary>Creates a reply of the model that is still arriving; <see cref="Complete"/> ends it.</summary>
    /// <returns>The entry, with no text yet.</returns>
    public static DraftChatEntry Streaming() => new(isUser: false, string.Empty) { IsStreaming = true };

    private string _text;

    /// <summary>Gets whether the user wrote it.</summary>
    public bool IsUser { get; }

    /// <summary>Gets the text of the message; it grows while <see cref="IsStreaming"/>.</summary>
    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    /// <summary>Gets whether the reply is still arriving.</summary>
    public bool IsStreaming
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
                OnPropertyChanged(nameof(CanCopy));
        }
    }

    /// <summary>Gets whether the copy button shows: for a finished reply of the model.</summary>
    public bool CanCopy => RendersMarkdown && !IsStreaming;

    /// <summary>Shows the part of a streaming reply that has arrived so far.</summary>
    /// <param name="text">The reply so far.</param>
    public void ShowPartial(string text)
    {
        if (IsStreaming) Text = text;
    }

    /// <summary>Ends a streaming reply with its whole text.</summary>
    /// <param name="text">The whole reply.</param>
    public void Complete(string text)
    {
        Text = text;
        IsStreaming = false;
    }

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
