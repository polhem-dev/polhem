using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Polhem.Definition.Drafting.Session;

namespace Polhem.Definition.Drafting
{
    /// <summary>
    /// A conversation with a model that drafts a form from documents. The model changes the <see cref="Draft"/> only
    /// through tools the session runs itself, and the draft can be marked ready only when it converts without errors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session runs the tool loop itself rather than through an automatic function-invoking client, so it can check
    /// every call, raise <see cref="DraftChanged"/> after each change and keep <c>mark_ready</c> behind validation. The
    /// model sees only declarations of the tools: a function-invoking client wrapped around <c>chatClient</c> finds
    /// nothing it can run, and leaves the calls to the session.
    /// </para>
    /// <para>
    /// The history only grows. What the model returns, reasoning included, is sent back exactly as it came, as providers
    /// that keep reasoning across tool calls require. A turn that fails or is cancelled is removed as a whole, and the
    /// draft is put back as it was before the turn, so the next request continues from the last complete turn.
    /// </para>
    /// <para>
    /// One turn runs at a time: <see cref="StartAsync"/>, <see cref="SendAsync"/> and <see cref="ApplyUserEdit"/> throw
    /// <see cref="InvalidOperationException"/> while a turn is in progress. Events are raised on the thread that runs
    /// the turn.
    /// </para>
    /// </remarks>
    public sealed class FormDraftSession
    {
        private readonly IChatClient _chatClient;
        private readonly IFormDraftingHost _host;
        private readonly DraftToolbox _toolbox;
        private readonly Dictionary<string, AIFunction> _functions;
        private readonly ChatOptions _toolOptions;
        private readonly ChatOptions _finalOptions;
        private readonly int _maxToolRounds;
        private readonly List<ChatMessage> _history;
        private readonly List<string> _pendingEdits = [];
        private int _turnInProgress;
        private readonly System.Globalization.CultureInfo? _replyCulture;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormDraftSession"/> class.
        /// </summary>
        /// <param name="chatClient">The model to talk to.</param>
        /// <param name="host">The existing forms and the validation of the application that drafts the form.</param>
        /// <param name="options">The settings; <see langword="null"/> uses the defaults.</param>
        public FormDraftSession(IChatClient chatClient, IFormDraftingHost host, FormDraftingOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(chatClient);
            ArgumentNullException.ThrowIfNull(host);
            options ??= new FormDraftingOptions();
            ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxToolRoundsPerTurn, 1, nameof(options));

            _chatClient = chatClient;
            _host = host;
            _maxToolRounds = options.MaxToolRoundsPerTurn;
            _replyCulture = options.ReplyCulture;
            _toolbox = new DraftToolbox(Draft, host, options.RelationConfirmationThreshold);
            _functions = _toolbox.Functions.ToDictionary(function => function.Name, StringComparer.Ordinal);

            _toolOptions = options.ChatOptions?.Clone() ?? new ChatOptions();
            _toolOptions.Tools = [.. _toolbox.Functions.Select(function => function.AsDeclarationOnly())];
            _toolOptions.ToolMode = ChatToolMode.Auto;
            _finalOptions = _toolOptions.Clone();
            _finalOptions.ToolMode = ChatToolMode.None;

            _history = [new ChatMessage(ChatRole.System, FormDraftingPrompt.SystemPrompt(options.MarkdownReplies))];
            History = new ReadOnlyCollection<ChatMessage>(_history);
        }

        /// <summary>
        /// Occurs after the draft changed: after each tool call that changed it, after <see cref="ApplyUserEdit"/>, and
        /// after a failed or cancelled turn put it back.
        /// </summary>
        public event EventHandler? DraftChanged;

        /// <summary>
        /// Occurs when the model replies to the user with text.
        /// </summary>
        public event EventHandler<AssistantMessageEventArgs>? AssistantMessage;

        /// <summary>
        /// Occurs at each step of a turn: before each request to the model, when its response carries a reasoning summary,
        /// and before and after each tool it calls. A host shows these while the turn runs.
        /// </summary>
        /// <remarks>
        /// The steps of a turn that fails or is cancelled have been raised already; the session puts the draft back, but
        /// the host decides what to do with the steps it showed.
        /// </remarks>
        public event EventHandler<DraftActivityEventArgs>? Activity;

        /// <summary>
        /// Occurs when the model asks the user to decide something, with answers to choose from. The model then ends its
        /// turn; the host sends the answers with <see cref="SendAsync"/>.
        /// </summary>
        /// <remarks>
        /// When the turn fails or is cancelled afterwards, the history no longer holds the questions, and the host should
        /// not offer them any more.
        /// </remarks>
        public event EventHandler<DraftQuestionsEventArgs>? QuestionsAsked;

        /// <summary>
        /// Gets the draft. The session changes this instance in place; read it after <see cref="DraftChanged"/>.
        /// </summary>
        public FormDraft Draft { get; } = new();

        /// <summary>
        /// Gets whether the model marked the draft ready and nothing changed it since. Only then should the host import
        /// the result of <see cref="FormDraftConverter.Convert(FormDraft)"/>.
        /// </summary>
        public bool IsReady => _toolbox.IsReady;

        /// <summary>
        /// Gets the messages of the conversation so far, starting with the instructions. It only grows.
        /// </summary>
        public IReadOnlyList<ChatMessage> History { get; }

        /// <summary>
        /// Sends the documents and asks the model for a first draft.
        /// </summary>
        /// <param name="documents">The documents, such as those <see cref="Documents.DocumentSource.FromFiles"/> reads.</param>
        /// <param name="cancellationToken">Cancels the turn; the session then stays as it was before the call.</param>
        /// <returns>A task that completes when the model has finished its turn.</returns>
        /// <exception cref="InvalidOperationException">The session has already started, or a turn is in progress.</exception>
        public Task StartAsync(IReadOnlyList<AIContent> documents, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(documents);
            if (documents.Count == 0)
                throw new ArgumentException("Pass at least one document.", nameof(documents));
            if (_history.Count > 1)
                throw new InvalidOperationException("The session has already started.");

            var contents = new List<AIContent> { new TextContent(FormDraftingPrompt.StartMessage(_host.GetCatalog(), _replyCulture)) };
            contents.AddRange(documents);
            return RunTurnAsync(contents, cancellationToken);
        }

        /// <summary>
        /// Sends the user's message and lets the model answer it.
        /// </summary>
        /// <param name="userText">What the user wrote.</param>
        /// <param name="cancellationToken">Cancels the turn; the session then stays as it was before the call.</param>
        /// <returns>A task that completes when the model has finished its turn.</returns>
        /// <exception cref="InvalidOperationException">The session has not started, or a turn is in progress.</exception>
        public Task SendAsync(string userText, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userText);
            if (_history.Count <= 1)
                throw new InvalidOperationException($"Call {nameof(StartAsync)} first.");
            return RunTurnAsync([new TextContent(userText)], cancellationToken);
        }

        /// <summary>
        /// Applies a change the user made to the draft directly, such as in a table beside the conversation.
        /// </summary>
        /// <param name="edit">Changes <see cref="Draft"/>.</param>
        /// <param name="description">
        /// What the user changed, in English, such as <c>Renamed the field leave_type to leave_kind.</c>. It reaches the
        /// model at the start of the user's next message; the history is not rewritten.
        /// </param>
        /// <remarks>The draft is no longer ready after an edit; the model has to mark it ready again.</remarks>
        /// <exception cref="InvalidOperationException">A turn is in progress.</exception>
        public void ApplyUserEdit(Action<FormDraft> edit, string description)
        {
            ArgumentNullException.ThrowIfNull(edit);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            if (Interlocked.Exchange(ref _turnInProgress, 1) == 1)
                throw new InvalidOperationException("The draft cannot be edited while a turn is in progress.");
            try
            {
                edit(Draft);
                _toolbox.IsReady = false;
                _pendingEdits.Add(description.Trim());
            }
            finally
            {
                Volatile.Write(ref _turnInProgress, 0);
            }
            DraftChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task RunTurnAsync(List<AIContent> contents, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _turnInProgress, 1) == 1)
                throw new InvalidOperationException("A turn is already in progress.");

            var historyCount = _history.Count;
            var draftBefore = DraftCopy.Clone(Draft);
            var readyBefore = _toolbox.IsReady;
            var editsBefore = _pendingEdits.ToList();
            var draftTouched = false;
            var completed = false;
            try
            {
                if (_pendingEdits.Count > 0)
                {
                    contents.Insert(0, new TextContent(FormDraftingPrompt.UserEdits(_pendingEdits)));
                    _pendingEdits.Clear();
                }
                _history.Add(new ChatMessage(ChatRole.User, contents));
                await RunToolLoopAsync(() => draftTouched = true, cancellationToken).ConfigureAwait(false);
                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    _history.RemoveRange(historyCount, _history.Count - historyCount);
                    _pendingEdits.Clear();
                    _pendingEdits.AddRange(editsBefore);
                    _toolbox.IsReady = readyBefore;
                    if (draftTouched)
                        DraftCopy.Restore(Draft, draftBefore);
                }
                Volatile.Write(ref _turnInProgress, 0);
                if (!completed && draftTouched)
                    DraftChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task RunToolLoopAsync(Action onDraftChanged, CancellationToken cancellationToken)
        {
            var questionsAsked = false;
            for (var round = 0; ; round++)
            {
                // Once the model has asked the user something, it may only reply: the answers come with the next message.
                var lastRound = round >= _maxToolRounds || questionsAsked;
                Activity?.Invoke(this, new DraftActivityEventArgs(DraftActivityKind.Thinking));
                var response = await _chatClient.GetResponseAsync(_history.ToList(), lastRound ? _finalOptions : _toolOptions,
                    cancellationToken).ConfigureAwait(false);

                var calls = RecordResponse(response);
                if (calls.Count == 0) return;

                var (results, asked) = await AnswerCallsAsync(calls, lastRound, questionsAsked, onDraftChanged, cancellationToken)
                    .ConfigureAwait(false);
                questionsAsked |= asked;
                _history.Add(new ChatMessage(ChatRole.Tool, results));
                if (lastRound) return;
            }
        }

        // Adds the response to the history, reports its reasoning and its reply, and returns the tool calls it makes.
        private List<FunctionCallContent> RecordResponse(ChatResponse response)
        {
            var calls = new List<FunctionCallContent>();
            foreach (var message in response.Messages)
            {
                _history.Add(message);
                foreach (var reasoning in message.Contents.OfType<TextReasoningContent>().Where(r => !string.IsNullOrWhiteSpace(r.Text)))
                    Activity?.Invoke(this, new DraftActivityEventArgs(DraftActivityKind.Reasoning, detail: reasoning.Text.Trim()));
                calls.AddRange(message.Contents.OfType<FunctionCallContent>().Where(call => !call.InformationalOnly));
                if (message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
                    AssistantMessage?.Invoke(this, new AssistantMessageEventArgs(message.Text));
            }
            return calls;
        }

        // Runs the round's tool calls, or refuses each of them in the last round, and reports whether one of them asked
        // the user questions.
        private async Task<(List<AIContent> Results, bool QuestionsAsked)> AnswerCallsAsync(List<FunctionCallContent> calls,
            bool lastRound, bool questionsAsked, Action onDraftChanged, CancellationToken cancellationToken)
        {
            var results = new List<AIContent>();
            var asked = false;
            foreach (var call in calls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (lastRound)
                {
                    results.Add(Refused(call, questionsAsked
                        ? "Not run: you asked the user questions, so end the turn with a reply and wait for the answers."
                        : "Not run: this turn has used all its tool calls. Reply to the user instead."));
                    continue;
                }
                results.Add(await InvokeToolAsync(call, onDraftChanged, cancellationToken).ConfigureAwait(false));
                if (_toolbox.AskedQuestions is { } questions)
                {
                    _toolbox.AskedQuestions = null;
                    asked = true;
                    QuestionsAsked?.Invoke(this, new DraftQuestionsEventArgs(questions));
                }
            }
            return (results, asked);
        }

        private async Task<FunctionResultContent> InvokeToolAsync(FunctionCallContent call, Action onDraftChanged,
            CancellationToken cancellationToken)
        {
            var subject = DescribeSubject(call);
            Activity?.Invoke(this, new DraftActivityEventArgs(DraftActivityKind.ToolStarted, call.Name, subject));
            if (!_functions.TryGetValue(call.Name, out var function))
                return RefusedStep(call, subject, $"There is no tool named '{call.Name}'. Tools: {string.Join(", ", _functions.Keys)}.");

            _toolbox.DraftModified = false;
            _toolbox.AskedQuestions = null;
            object? result;
            try
            {
                result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken).ConfigureAwait(false);
            }
            catch (DraftToolException ex)
            {
                return RefusedStep(call, subject, ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException or FormatException or InvalidCastException)
            {
                // The arguments did not bind to the tool's parameters. The model gets the reason and can call again.
                return RefusedStep(call, subject, $"The arguments do not match the tool's parameters: {ex.Message}");
            }

            if (_toolbox.DraftModified)
            {
                _toolbox.IsReady = false;
                onDraftChanged();
                DraftChanged?.Invoke(this, EventArgs.Empty);
            }
            Activity?.Invoke(this, new DraftActivityEventArgs(DraftActivityKind.ToolCompleted, call.Name, subject));
            return new FunctionResultContent(call.CallId, result);
        }

        private FunctionResultContent RefusedStep(FunctionCallContent call, string subject, string message)
        {
            Activity?.Invoke(this, new DraftActivityEventArgs(DraftActivityKind.ToolRefused, call.Name, subject, message));
            return Refused(call, message);
        }

        /// <summary>
        /// What a tool call works on, from its arguments: a field (with its detail table), a table, a ProgId or a search.
        /// </summary>
        private static string DescribeSubject(FunctionCallContent call)
        {
            var arguments = call.Arguments;
            if (arguments is null) return string.Empty;
            var table = ArgumentText(arguments, "table");
            var field = ArgumentText(arguments, "fieldName");
            if (field.Length > 0)
                return table.Length == 0 || string.Equals(table, DraftToolbox.MasterTableKey, StringComparison.OrdinalIgnoreCase)
                    ? field
                    : $"{table}.{field}";
            if (table.Length > 0) return table;
            var progId = ArgumentText(arguments, "progId");
            return progId.Length > 0 ? progId : ArgumentText(arguments, "query");
        }

        private static string ArgumentText(IDictionary<string, object?> arguments, string name)
        {
            if (!arguments.TryGetValue(name, out var value) || value is null) return string.Empty;
            var text = value is JsonElement { ValueKind: JsonValueKind.String } element ? element.GetString() : value.ToString();
            return text?.Trim() ?? string.Empty;
        }

        private static FunctionResultContent Refused(FunctionCallContent call, string message)
            => new(call.CallId, "Error: " + message);
    }
}
