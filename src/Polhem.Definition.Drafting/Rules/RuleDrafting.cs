using System.Text.Json;
using Microsoft.Extensions.AI;
using Polhem.Definition.Drafting.Session;
using Polhem.Definition.Forms;

namespace Polhem.Definition.Drafting.Rules
{
    /// <summary>
    /// Writes a form validation rule from what a user says it should check, and explains an existing rule, with a model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model only proposes: it submits a rule through a tool, and the submission is accepted only when its
    /// expressions pass <see cref="FormExpressionCalculator.ValidateUntrustedExpression"/> (the size limits, the parse
    /// and the expression allowlist) and the expression engine computes, for every sample row, the outcome the model
    /// said it would. Otherwise the reasons go back to the model, which may try again. The samples a host shows are the
    /// engine's results, so a user who cannot read an expression can still check what the rule does.
    /// </para>
    /// <para>
    /// A rule is drafted for the form the instance was created with, and revisions continue the same conversation.
    /// One request runs at a time; a request that fails or is cancelled leaves the conversation as it was.
    /// </para>
    /// </remarks>
    public sealed class RuleDrafting
    {
        private readonly IChatClient _chatClient;
        private readonly FormSchema _schema;
        private readonly RuleDraftingOptions _options;
        private readonly RuleToolbox _toolbox;
        private readonly ChatOptions _draftOptions;
        private readonly ChatOptions _explainOptions;
        private readonly List<ChatMessage> _history = [];
        private int _requestInProgress;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleDrafting"/> class.
        /// </summary>
        /// <param name="chatClient">The model to talk to.</param>
        /// <param name="schema">The form the rules are for; it is read, never changed.</param>
        /// <param name="calculator">Checks the expressions and computes the samples, with an evaluator that implements the allowlist.</param>
        /// <param name="options">The settings; <see langword="null"/> uses the defaults.</param>
        public RuleDrafting(IChatClient chatClient, FormSchema schema, FormExpressionCalculator calculator,
            RuleDraftingOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(chatClient);
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(calculator);
            options ??= new RuleDraftingOptions();
            ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxToolRounds, 1, nameof(options));

            _chatClient = chatClient;
            _schema = schema;
            _options = options;
            _toolbox = new RuleToolbox(schema, calculator, options);
            _draftOptions = WithTool(options.ChatOptions, _toolbox.SubmitRule);
            _explainOptions = WithTool(options.ChatOptions, _toolbox.SubmitExplanation);
        }

        /// <summary>
        /// Starts a new conversation and asks the model for a rule that checks what <paramref name="request"/> says.
        /// </summary>
        /// <param name="request">What the rule should check, in the user's words, such as "an amount over 100,000 needs an approver".</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The rule when the model wrote one that passed every check, with what the model said.</returns>
        /// <exception cref="InvalidOperationException">A request is in progress.</exception>
        public Task<RuleDraftResult> DraftAsync(string request, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request);
            return RunDraftAsync(request, reset: true, cancellationToken);
        }

        /// <summary>
        /// Continues the conversation with the user's answer or correction, such as "only for orders from Taipei".
        /// </summary>
        /// <param name="feedback">What the user says about the last rule or question.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The revised rule when the model wrote one that passed every check, with what the model said.</returns>
        /// <exception cref="InvalidOperationException">No conversation has started, or a request is in progress.</exception>
        public Task<RuleDraftResult> ReviseAsync(string feedback, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(feedback);
            return RunDraftAsync(feedback, reset: false, cancellationToken);
        }

        /// <summary>
        /// Asks the model to explain <paramref name="rule"/> in plain words, with sample rows the engine computes.
        /// </summary>
        /// <param name="rule">A rule of the form; it is read, never changed.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        /// <returns>The explanation when the engine confirmed its samples, with what the model said.</returns>
        /// <remarks>The explanation is a conversation of its own; it does not touch the drafting conversation.</remarks>
        /// <exception cref="InvalidOperationException">A request is in progress.</exception>
        public async Task<RuleExplanationResult> ExplainAsync(FormRule rule, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rule);
            Enter();
            try
            {
                // The rule is evaluated for every sample, so it is checked as untrusted text first: a rule from
                // anywhere but a developer's definition file could otherwise reach the parser before its size is checked.
                if (_toolbox.CheckRule(rule) is { } problem)
                    return new RuleExplanationResult(null, $"The rule cannot be explained: {problem}");
                _toolbox.AcceptedExplanation = null;
                _toolbox.ExplainedRule = rule;
                var history = new List<ChatMessage>
                {
                    new(ChatRole.System, RuleDraftingPrompt.ExplainSystemPrompt(_schema, _options)),
                    new(ChatRole.User, RuleDraftingPrompt.ExplainRequest(rule, _options)),
                };
                var reply = await RunToolLoopAsync(history, _explainOptions, _toolbox.SubmitExplanation,
                    () => _toolbox.AcceptedExplanation is not null, cancellationToken).ConfigureAwait(false);
                return new RuleExplanationResult(_toolbox.AcceptedExplanation, reply);
            }
            finally
            {
                _toolbox.ExplainedRule = null;
                Volatile.Write(ref _requestInProgress, 0);
            }
        }

        private async Task<RuleDraftResult> RunDraftAsync(string text, bool reset, CancellationToken cancellationToken)
        {
            Enter();
            if (!reset && _history.Count == 0)
            {
                Volatile.Write(ref _requestInProgress, 0);
                throw new InvalidOperationException($"Call {nameof(DraftAsync)} first.");
            }
            var before = _history.ToList();
            var revisedBefore = _toolbox.RevisedRuleId;
            var completed = false;
            try
            {
                if (reset)
                {
                    _history.Clear();
                    _history.Add(new ChatMessage(ChatRole.System, RuleDraftingPrompt.DraftSystemPrompt(_schema, _options)));
                    _toolbox.RevisedRuleId = null;
                }
                _toolbox.AcceptedRule = null;
                _history.Add(new ChatMessage(ChatRole.User, reset ? RuleDraftingPrompt.FirstMessage(text, _options) : text));
                var reply = await RunToolLoopAsync(_history, _draftOptions, _toolbox.SubmitRule,
                    () => _toolbox.AcceptedRule is not null, cancellationToken).ConfigureAwait(false);
                if (_toolbox.AcceptedRule is { } accepted) { _toolbox.RevisedRuleId = accepted.RuleId; }
                completed = true;
                return new RuleDraftResult(_toolbox.AcceptedRule, reply);
            }
            finally
            {
                if (!completed)
                {
                    _history.Clear();
                    _history.AddRange(before);
                    _toolbox.RevisedRuleId = revisedBefore;
                }
                Volatile.Write(ref _requestInProgress, 0);
            }
        }

        // Asks, runs the one tool the model may call, and stops once a submission is accepted or the rounds run out. The
        // last request after an acceptance, or when the rounds run out, lets the model call no tool, so it replies.
        private async Task<string> RunToolLoopAsync(List<ChatMessage> history, ChatOptions toolOptions, AIFunction tool,
            Func<bool> accepted, CancellationToken cancellationToken)
        {
            // IMPORTANT: the reply request keeps the same tool list and only forbids calling it. A provider that binds the
            // model's reasoning to the tools it was produced with (Claude does) refuses a request whose tool list changed.
            var replyOptions = toolOptions.Clone();
            replyOptions.ToolMode = ChatToolMode.None;
            var reply = string.Empty;
            for (var round = 0; ; round++)
            {
                var lastRound = accepted() || round >= _options.MaxToolRounds;
                var response = await _chatClient.GetResponseAsync(history.ToList(), lastRound ? replyOptions : toolOptions,
                    cancellationToken).ConfigureAwait(false);
                var calls = new List<FunctionCallContent>();
                foreach (var message in response.Messages)
                {
                    history.Add(message);
                    calls.AddRange(message.Contents.OfType<FunctionCallContent>().Where(call => !call.InformationalOnly));
                    if (message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text)) { reply = message.Text.Trim(); }
                }
                if (calls.Count == 0) { return reply; }

                var results = new List<AIContent>();
                for (int i = 0; i < calls.Count; i++)
                {
                    var call = calls[i];
                    cancellationToken.ThrowIfCancellationRequested();
                    // One submission per response: each costs the samples' evaluation, so a response may not multiply it.
                    results.Add(lastRound ? new FunctionResultContent(call.CallId, "Not run: reply to the user instead.")
                        : i > 0 ? new FunctionResultContent(call.CallId, "Not run: submit one at a time.")
                        : await InvokeAsync(tool, call, cancellationToken).ConfigureAwait(false));
                }
                history.Add(new ChatMessage(ChatRole.Tool, results));
                if (lastRound) { return reply; }
            }
        }

        private static async Task<FunctionResultContent> InvokeAsync(AIFunction tool, FunctionCallContent call,
            CancellationToken cancellationToken)
        {
            if (call.Name != tool.Name)
                return new FunctionResultContent(call.CallId,
                    $"There is no tool named '{(call.Name.Length <= 60 ? call.Name : call.Name[..60] + "…")}'. The tool is {tool.Name}.");
            try
            {
                var result = await tool.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken).ConfigureAwait(false);
                return new FunctionResultContent(call.CallId, result);
            }
            catch (DraftToolException ex)
            {
                return new FunctionResultContent(call.CallId, ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException or FormatException or InvalidCastException)
            {
                // The arguments did not bind to the tool's parameters. The model gets the reason and can call again.
                return new FunctionResultContent(call.CallId, $"The arguments do not match the tool's parameters: {ex.Message}");
            }
        }

        private void Enter()
        {
            if (Interlocked.Exchange(ref _requestInProgress, 1) == 1)
                throw new InvalidOperationException("A request is already in progress.");
        }

        private static ChatOptions WithTool(ChatOptions? options, AIFunction tool)
        {
            var copy = options?.Clone() ?? new ChatOptions();
            copy.Tools = [tool.AsDeclarationOnly()];
            copy.ToolMode = ChatToolMode.Auto;
            return copy;
        }
    }
}
