using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Writes one line for each model call: how long it took, whether reasoning came back and how much, and the tokens it
/// used. It records counts only, never the text of the conversation.
/// </summary>
/// <remarks>
/// The drafting window and the rule assistant show the model's reasoning while it arrives, and some long calls show
/// none. A call that returned no reasoning block means the model did not think; a call whose reasoning blocks carry no
/// text, or whose output tokens far exceed its text, means it thought and the summary was not shown. The line tells
/// the two apart.
/// </remarks>
public sealed class CallLogChatClient : DelegatingChatClient
{
    private readonly string _purpose;
    private readonly Action<string> _write;
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Wraps <paramref name="innerClient"/> and writes a line through <paramref name="write"/> for each call.</summary>
    /// <param name="innerClient">The client that makes the calls.</param>
    /// <param name="purpose">The feature that makes the calls, written on each line.</param>
    /// <param name="write">Receives each line.</param>
    /// <param name="now">The clock for the line's time stamp; the system clock when omitted.</param>
    public CallLogChatClient(IChatClient innerClient, string purpose, Action<string> write, Func<DateTimeOffset>? now = null)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentNullException.ThrowIfNull(write);
        _purpose = purpose;
        _write = write;
        _now = now ?? (() => DateTimeOffset.Now);
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var started = _now();
        var clock = Stopwatch.StartNew();
        ChatResponse? response = null;
        try
        {
            response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            return response;
        }
        finally
        {
            _write(Describe(started, clock.Elapsed, firstReasoning: null, response));
        }
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var started = _now();
        var clock = Stopwatch.StartNew();
        TimeSpan? firstReasoning = null;
        var updates = new List<ChatResponseUpdate>();
        var completed = false;
        try
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
            {
                updates.Add(update);
                if (firstReasoning is null && update.Contents.OfType<TextReasoningContent>().Any(r => r.Text.Length > 0))
                    firstReasoning = clock.Elapsed;
                yield return update;
            }
            completed = true;
        }
        finally
        {
            _write(Describe(started, clock.Elapsed, firstReasoning, completed ? updates.ToChatResponse() : null));
        }
    }

    // A response of null means the call failed or was cancelled before it ended.
    private string Describe(DateTimeOffset started, TimeSpan elapsed, TimeSpan? firstReasoning, ChatResponse? response)
    {
        var line = new StringBuilder();
        line.Append(started.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture))
            .Append(' ').Append(_purpose)
            .Append(" elapsed=").Append(Seconds(elapsed));
        if (response is null)
            return line.Append(" outcome=failed").ToString();

        var contents = response.Messages.Where(m => m.Role == ChatRole.Assistant).SelectMany(m => m.Contents).ToList();
        var reasoning = contents.OfType<TextReasoningContent>().ToList();
        line.Append(" model=").Append(response.ModelId ?? "-")
            .Append(" finish=").Append(response.FinishReason?.Value ?? "-")
            .Append(" reasoning=").Append(reasoning.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" reasoningChars=").Append(reasoning.Sum(r => r.Text.Length).ToString(CultureInfo.InvariantCulture))
            .Append(" emptyReasoning=").Append(reasoning.Count(r => string.IsNullOrWhiteSpace(r.Text)).ToString(CultureInfo.InvariantCulture))
            .Append(" firstReasoning=").Append(firstReasoning is { } first ? Seconds(first) : "-")
            .Append(" textChars=").Append(contents.OfType<TextContent>().Sum(t => t.Text.Length).ToString(CultureInfo.InvariantCulture))
            .Append(" toolCalls=").Append(contents.OfType<FunctionCallContent>().Count().ToString(CultureInfo.InvariantCulture))
            .Append(" in=").Append(Count(response.Usage?.InputTokenCount))
            .Append(" cached=").Append(Count(response.Usage?.CachedInputTokenCount))
            .Append(" out=").Append(Count(response.Usage?.OutputTokenCount));
        return line.ToString();
    }

    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";

    private static string Count(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
}
