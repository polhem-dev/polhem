using System.Text;
using Microsoft.Extensions.AI;
using Polhem.Chat;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Feeds what a drafting session reports while it runs its own tool loop into a <see cref="ChatConversation"/>, which a
/// <c>ChatView</c> shows: the user's prompt, then for each turn the streamed reasoning and reply, the tools it runs and
/// the tokens it reports.
/// </summary>
/// <remarks>
/// <para>
/// The sessions report events, not <see cref="ChatResponseUpdate"/>s, so the feed builds the updates the conversation
/// turns into entries. A tool call gets an identifier of its own, which pairs its start with its end; the sessions run
/// their tools one at a time.
/// </para>
/// <para>
/// The streamed pieces are gathered and applied at most once per redraw interval: each change of a reply
/// parses its Markdown again. Any other step applies the gathered pieces first, so the entries keep the order the session
/// reported. The feed may be called from any thread; the delayed redraw goes through <c>post</c>, which the caller points
/// at the thread the conversation is bound on.
/// </para>
/// </remarks>
public sealed class DraftingChatFeed
{
    /// <summary>The key of a tool call's <see cref="AIContent.AdditionalProperties"/> that carries what it works on.</summary>
    public const string SubjectProperty = "polhem.subject";

    private readonly object _gate = new();
    private readonly Action<Action> _post;
    private readonly TimeSpan _redrawInterval;
    private readonly StringBuilder _reply = new();
    private readonly StringBuilder _reasoning = new();
    private ChatTurn? _turn;
    private string _callId = string.Empty;
    private bool _redrawPending;
    private bool _replyOpen;

    /// <summary>Initializes a new instance of the <see cref="DraftingChatFeed"/> class.</summary>
    /// <param name="describeTool">The text a tool call shows, from the tool's name and what it works on.</param>
    /// <param name="post">Runs an action on the thread the conversation is bound on.</param>
    /// <param name="redrawInterval">How often the streamed pieces are applied; zero applies each piece as it arrives.</param>
    public DraftingChatFeed(Func<string, string, string> describeTool, Action<Action> post, TimeSpan redrawInterval)
    {
        ArgumentNullException.ThrowIfNull(describeTool);
        ArgumentNullException.ThrowIfNull(post);
        _post = post;
        _redrawInterval = redrawInterval;
        Conversation = new ChatConversation
        {
            DescribeToolCall = call => describeTool(call.Name,
                call.AdditionalProperties?.TryGetValue(SubjectProperty, out var subject) == true ? subject as string ?? string.Empty : string.Empty),
        };
    }

    /// <summary>
    /// How often the assistants apply the streamed pieces: each change of a reply parses its Markdown again, so the pieces
    /// that arrive in between are gathered rather than drawn one by one.
    /// </summary>
    public static TimeSpan DefaultRedrawInterval { get; } = TimeSpan.FromMilliseconds(80);

    /// <summary>Gets the conversation the view shows.</summary>
    public ChatConversation Conversation { get; }

    /// <summary>Gets whether a turn is in progress.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate) return _turn is not null;
        }
    }

    /// <summary>Adds the user's prompt and starts the turn that answers it.</summary>
    /// <param name="prompt">What the user sent, as the conversation shows it.</param>
    public void Begin(string prompt)
    {
        lock (_gate)
        {
            Conversation.AddUserPrompt(prompt);
            _turn = Conversation.BeginTurn();
            _reply.Clear();
            _reasoning.Clear();
            _replyOpen = false;
        }
    }

    /// <summary>
    /// Marks the start of a request to the model within the turn: the turn counts it, and the request's reasoning and
    /// reply start entries of their own.
    /// </summary>
    public void BeginRequest()
    {
        lock (_gate)
        {
            if (_turn is not { } turn) return;
            Flush();
            turn.BeginCall();
            _replyOpen = false;
        }
    }

    /// <summary>Adds a piece of the reply the model is writing.</summary>
    public void AppendReply(string text)
    {
        lock (_gate)
        {
            if (_turn is null || text.Length == 0) return;
            if (_reasoning.Length > 0) Flush();
            _reply.Append(text);
            RedrawSoon();
        }
    }

    /// <summary>Adds a piece of the reasoning the model is writing.</summary>
    public void AppendReasoning(string text)
    {
        lock (_gate)
        {
            if (_turn is null || text.Length == 0) return;
            if (_reply.Length > 0) Flush();
            _reasoning.Append(text);
            RedrawSoon();
        }
    }

    /// <summary>Shows a tool the model called, as running.</summary>
    /// <param name="name">The tool.</param>
    /// <param name="subject">What it works on; may be empty.</param>
    public void StartTool(string name, string subject)
    {
        lock (_gate)
        {
            if (_turn is not { } turn) return;
            Flush();
            _callId = Guid.NewGuid().ToString("N");
            var call = new FunctionCallContent(_callId, name)
            {
                AdditionalProperties = new AdditionalPropertiesDictionary { [SubjectProperty] = subject },
            };
            turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [call]));
            _replyOpen = false;
        }
    }

    /// <summary>Ends the tool that is running.</summary>
    /// <param name="refusal">
    /// Why the tool refused its input, or <see langword="null"/> when it ran. A refused tool shows as rejected, with the
    /// reason: the model reads it and usually calls the tool again.
    /// </param>
    public void EndTool(string? refusal)
    {
        lock (_gate)
        {
            if (_turn is not { } turn || _callId.Length == 0) return;
            Flush();
            // The session refused the call itself, so it did not fail: it shows as rejected. The entry keeps that state
            // when the result arrives.
            if (refusal is not null && FindToolCall(_callId) is { } entry)
            {
                entry.RejectionReason = refusal;
                entry.Status = ToolCallStatus.Rejected;
            }
            turn.Apply(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent(_callId, result: null)]));
            _callId = string.Empty;
            _replyOpen = false;
        }
    }

    /// <summary>
    /// Adds an entry of the application's own while the turn is in progress, such as the questions the model asked. The
    /// reply before it ends, so text that arrives later starts a reply of its own.
    /// </summary>
    public void AddEntry(ChatEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            if (_turn is not { } turn) return;
            Flush();
            turn.AddEntry(entry);
            _replyOpen = false;
        }
    }

    /// <summary>Adds the tokens of a call that returned to the turn's count.</summary>
    public void AddUsage(long inputTokens, long outputTokens, long cachedInputTokens)
    {
        lock (_gate)
        {
            if (_turn is not { } turn || (inputTokens == 0 && outputTokens == 0 && cachedInputTokens == 0)) return;
            turn.Apply(new ChatResponseUpdate(ChatRole.Assistant,
            [
                new UsageContent(new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = outputTokens,
                    CachedInputTokenCount = cachedInputTokens,
                }),
            ]));
        }
    }

    /// <summary>Ends the turn the model finished.</summary>
    /// <param name="entries">Entries of the application's own to show after the reply, such as questions to answer.</param>
    public void Complete(params ChatEntry[] entries)
    {
        lock (_gate)
        {
            if (_turn is not { } turn) return;
            Flush();
            foreach (var entry in entries)
                turn.AddEntry(entry);
            turn.Complete();
            _turn = null;
        }
    }

    /// <summary>Ends a turn the user stopped; what arrived stays, marked as stopped.</summary>
    /// <param name="note">A note shown after the turn, such as what happened to the draft; empty for none.</param>
    public void Stop(string note)
    {
        lock (_gate)
        {
            if (_turn is not { } turn) return;
            Flush();
            turn.Stop();
            _turn = null;
            if (note.Length > 0)
                Conversation.Entries.Add(new ErrorEntry(note));
        }
    }

    /// <summary>Ends a turn that failed; what arrived stays, and the failure shows with <paramref name="message"/>.</summary>
    public void Fail(string message, Exception exception)
    {
        lock (_gate)
        {
            if (_turn is not { } turn) return;
            Flush();
            turn.Fail(string.Equals(message, exception.Message, StringComparison.Ordinal)
                ? exception
                : new InvalidOperationException(message, exception));
            _turn = null;
        }
    }

    private ToolCallEntry? FindToolCall(string callId)
    {
        for (var i = Conversation.Entries.Count - 1; i >= 0; i--)
        {
            if (Conversation.Entries[i] is ToolCallEntry call && string.Equals(call.CallId, callId, StringComparison.Ordinal))
                return call;
        }
        return null;
    }

    // Must be called under the gate.
    private void RedrawSoon()
    {
        if (_redrawPending) return;
        _redrawPending = true;
        _ = RedrawLaterAsync();
    }

    private async Task RedrawLaterAsync()
    {
        if (_redrawInterval > TimeSpan.Zero)
            await Task.Delay(_redrawInterval).ConfigureAwait(false);
        _post(() =>
        {
            lock (_gate)
            {
                _redrawPending = false;
                // Applies whatever is gathered now; a turn that ended in between applied its own pieces when it did.
                Flush();
            }
        });
    }

    // Applies the gathered pieces. Must be called under the gate.
    private void Flush()
    {
        if (_turn is not { } turn) return;
        if (_reasoning.Length > 0)
        {
            var text = _reasoning.ToString();
            _reasoning.Clear();
            turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(text)]));
            _replyOpen = false;
        }
        if (_reply.Length > 0)
        {
            var text = _reply.ToString();
            // Leading whitespace opens no reply: a reply that turns out to be only whitespace is never shown.
            if (!_replyOpen)
                text = text.TrimStart();
            if (text.Length == 0) return;
            _reply.Clear();
            turn.Apply(new ChatResponseUpdate(ChatRole.Assistant, text));
            _replyOpen = true;
        }
    }
}
