using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using Polhem.Chat;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Drafting;
using Polhem.Definition.Drafting.Rules;
using Polhem.Definition.Forms;
using Polhem.Expressions;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// Writes a rule from what the user says it should check, and explains the rule being edited, with a model, beside
/// the expression editor. What it shows comes from <see cref="RuleDrafting"/>: the samples are the expression engine's
/// results, so a user who cannot read the expression can still check what the rule does.
/// </summary>
/// <remarks>
/// The conversation shows in a chat view: the first prompt asks for a rule, the next ones revise it, until the user starts
/// a new conversation. Each drafted or explained rule is a <see cref="RuleProposalEntry"/> in the conversation, after the
/// reply of its turn; the newest card of a draft is the one the user applies. Nothing is sent before the user asks: the chat client is created on the first request. The draft replaces the whole
/// rule (its table, When, Condition and message) when the user applies it, and keeps the rule's id, the key its
/// translations are stored under.
/// </remarks>
public sealed partial class RuleAssistantViewModel : ViewModelBase, IDisposable
{
    private readonly Func<IChatClient> _createClient;
    private readonly FormSchema _schema;
    private readonly Func<FormRule> _currentRule;
    private readonly FormExpressionCalculator _calculator;
    private readonly RuleDraftingOptions _options;
    private IChatClient? _client;
    private RuleDrafting? _drafting;
    private readonly SynchronizationContext? _context;
    private readonly DraftingChatFeed _feed;
    private CancellationTokenSource? _cancellation;
    private bool _hasConversation;
    private bool _replyStreamed;
    private RuleDraftingUsage _reportedUsage = RuleDraftingUsage.None;
    private RuleProposalEntry? _proposal;

    /// <param name="schema">The form; it is read, never changed.</param>
    /// <param name="ruleId">The rule being edited. The model drafts against the form without it, so its id is free.</param>
    /// <param name="currentRule">The rule as the editor holds it now, for an explanation.</param>
    /// <param name="calculator">Checks the drafted expressions and computes the samples.</param>
    /// <param name="files">The Language files the message is translated in.</param>
    /// <param name="credential">The credential the provider will use.</param>
    /// <param name="createClient">Creates the chat client on the first request; the assistant disposes it.</param>
    /// <param name="timeZoneId">The IANA time zone the samples' <c>Today()</c> sees.</param>
    public RuleAssistantViewModel(FormSchema schema, string ruleId, Func<FormRule> currentRule,
        FormExpressionCalculator calculator, IReadOnlyList<RuleMessageFile> files, DraftingCredential credential,
        Func<IChatClient> createClient, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(currentRule);
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(createClient);
        _schema = schema.Clone();
        if (_schema.Rules?.GetOrDefault(ruleId) is { } edited) _schema.Rules.Remove(edited);
        RuleId = ruleId;
        _currentRule = currentRule;
        _calculator = calculator;
        _createClient = createClient;
        _context = SynchronizationContext.Current;
        _feed = new DraftingChatFeed((name, _) => name, Post, DraftingChatFeed.DefaultRedrawInterval);
        IsAvailable = credential.IsAvailable;
        CredentialText = credential.IsAvailable
            ? L("RuleAssistant_Sends", credential.Description)
            : L("RuleAssistant_NoCredential");
        Translations = new ObservableCollection<RuleMessageTranslation>(files.Select(f => new RuleMessageTranslation(f)));
        _options = new RuleDraftingOptions
        {
            ReplyCulture = LocalizationService.Current.Culture,
            MarkdownReplies = true,
            MessageLanguages = files.Select(f => f.Language).ToList(),
            Functions = DynamicExpressoEvaluator.HelperFunctions
                .Where(DynamicExpressoEvaluator.IsAllowedFunction)
                .Select(f => new RuleDraftFunction(f.Name, ExpressionMembers.Signature(f), f.Summary, f.Example))
                .ToList(),
            TimeZoneId = timeZoneId,
        };
    }

    /// <summary>The id of the rule being edited, which an applied draft keeps.</summary>
    public string RuleId { get; }

    /// <summary>Whether the provider has a credential to use.</summary>
    public bool IsAvailable { get; }

    /// <summary>What a request sends and with which credential, or how to get one.</summary>
    public string CredentialText { get; }

    /// <summary>Gets the conversation, as the chat view shows it.</summary>
    public ChatConversation Conversation => _feed.Conversation;

    /// <summary>Whether a request is in progress; the chat view shows its stop button meanwhile.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExplainCommand), nameof(ApplyCommand), nameof(NewConversationCommand))]
    private bool _isBusy;

    /// <summary>What the drafted or explained rule does, in plain words.</summary>
    [ObservableProperty]
    private string _explanation = string.Empty;

    /// <summary>The drafted rule's expressions, as text to read; empty when there is no draft.</summary>
    [ObservableProperty]
    private string _ruleText = string.Empty;

    /// <summary>The drafted rule's English message, which the user can change before applying it.</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>The rule the model drafted; <see langword="null"/> before one passed every check.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDraft))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private RuleDraft? _draft;

    /// <summary>Whether there is a drafted rule to apply.</summary>
    public bool HasDraft => Draft is not null;

    /// <summary>The samples of the drafted or explained rule, with what the engine computed for each.</summary>
    public ObservableCollection<RuleAssistantSample> Samples { get; } = [];

    /// <summary>The message in each language of the form; filled from a draft, and changeable before it is applied.</summary>
    public ObservableCollection<RuleMessageTranslation> Translations { get; }

    /// <summary>Whether the form has a language besides English to translate the message into.</summary>
    public bool HasTranslations => Translations.Count > 0;

    /// <summary>The rule an applied draft becomes, with the rule's own id; <see langword="null"/> before one is applied.</summary>
    public FormRule? AppliedRule { get; private set; }

    /// <summary>The translations of an applied draft, as the user left them.</summary>
    public IReadOnlyList<RuleMessageTranslation> AppliedTranslations { get; private set; } = [];

    /// <summary>Raised when the user applies the draft.</summary>
    public event EventHandler? Applied;

    private bool CanExplain() => IsAvailable && !IsBusy && !string.IsNullOrWhiteSpace(_currentRule().Condition);

    /// <summary>
    /// Sends what the user wrote: the first message of a conversation asks for a rule that checks it, the next ones say
    /// what to change in the last rule or answer the model's question. Nothing is sent while a request is in progress.
    /// </summary>
    /// <param name="text">The message.</param>
    public Task SendAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        if (!IsAvailable || IsBusy || text.Length == 0) return Task.CompletedTask;
        if (!_hasConversation)
        {
            return RunAsync(text, async (drafting, token) =>
            {
                var result = await drafting.DraftAsync(text, token);
                _hasConversation = true;
                ShowDraft(result);
            });
        }
        return RunAsync(text, async (drafting, token) => ShowDraft(await drafting.ReviseAsync(text, token)));
    }

    /// <summary>Starts over: the next message asks for a new rule.</summary>
    [RelayCommand(CanExecute = nameof(CanStartOver))]
    private void NewConversation()
    {
        _hasConversation = false;
        _proposal = null;
        Conversation.Clear();
        Draft = null;
        Explanation = string.Empty;
        RuleText = string.Empty;
        Message = string.Empty;
        Samples.Clear();
    }

    private bool CanStartOver() => !IsBusy;

    /// <summary>Explains the rule as the editor holds it, with samples.</summary>
    [RelayCommand(CanExecute = nameof(CanExplain))]
    private Task ExplainAsync() => RunAsync(L("RuleAssistant_ExplainPrompt"), async (drafting, token) =>
    {
        var result = await drafting.ExplainAsync(_currentRule(), token);
        RuleProposalEntry? card = null;
        if (result.Explanation is { } explanation)
        {
            Draft = null;
            RuleText = string.Empty;
            card = ShowResult(explanation.Explanation, explanation.Samples);
        }
        ShowReply(card, result.Reply);
    });

    /// <summary>Stops the request in progress.</summary>
    public void Stop() => _cancellation?.Cancel();

    /// <summary>Applies the draft: the editor writes it over the rule and closes.</summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (Draft is not { } draft) return;
        var rule = draft.ToFormRule();
        AppliedRule = new FormRule(RuleId, rule.Condition, Message.Trim())
        {
            TargetTable = rule.TargetTable,
            When = rule.When,
        };
        AppliedTranslations = Translations.ToList();
        Applied?.Invoke(this, EventArgs.Empty);
    }

    private bool CanApply() => HasDraft && !IsBusy;

    private void ShowDraft(RuleDraftResult result)
    {
        RuleProposalEntry? card = null;
        if (result.Draft is { } draft)
        {
            Draft = draft;
            Message = draft.Message;
            RuleText = DescribeRule(draft);
            foreach (var translation in Translations)
                translation.Text = draft.MessageTranslations.GetValueOrDefault(translation.Language, string.Empty);
            card = ShowResult(draft.Explanation, draft.Samples);
        }
        ShowReply(card, result.Reply);
    }

    // A reply the model wrote shows as it streamed in. A provider that streams nothing has it only in the result. The
    // accepted result follows the reply as a card; without one and without a reply, the conversation says the assistant
    // did not finish.
    private void ShowReply(RuleProposalEntry? card, string reply)
    {
        if (!_replyStreamed && reply.Length > 0)
            _feed.AppendReply(reply);
        if (card is not null)
            _feed.Complete(card);
        else
            _feed.Complete(reply.Length > 0 ? [] : [new ErrorEntry(L("RuleAssistant_NoResult"))]);
    }

    // The newest result replaces the one before it: the older card stays in the conversation to read, and only the new
    // one applies.
    private RuleProposalEntry ShowResult(string explanation, IReadOnlyList<RuleDraftSample> samples)
    {
        Explanation = explanation;
        Samples.Clear();
        foreach (var sample in samples)
            Samples.Add(new RuleAssistantSample(sample.Description, DescribeValues(sample), sample.Outcome, OutcomeText(sample.Outcome)));
        _proposal?.Supersede();
        _proposal = new RuleProposalEntry(this, explanation, RuleText, Samples.ToList());
        return _proposal;
    }

    private async Task RunAsync(string prompt, Func<RuleDrafting, CancellationToken, Task> request)
    {
        _cancellation = new CancellationTokenSource();
        _replyStreamed = false;
        _reportedUsage = RuleDraftingUsage.None;
        _feed.Begin(prompt);
        IsBusy = true;
        try
        {
            _client ??= _createClient();
            if (_drafting is null)
            {
                _drafting = new RuleDrafting(_client, _schema, _calculator, _options);
                _drafting.Progress += OnProgress;
                _drafting.ReplyDelta += OnReplyDelta;
                _drafting.ReasoningDelta += OnReasoningDelta;
            }
            await request(_drafting, _cancellation.Token);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            _feed.Stop(string.Empty);
        }
        catch (Exception ex)
        {
            // Boundary: a request runs from the chat view or a button of the dialog; an exception that escaped it would
            // end the editor. The drafting session leaves its conversation as it was before a failed request, so the user
            // can retry.
            _feed.Fail(DraftingChatClients.DescribeFailure(ex) ?? L("RuleAssistant_Failed", ex.Message), ex);
        }
        finally
        {
            // A result that did not end the turn (an exception thrown after it) still ends it here.
            _feed.Complete();
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
        }
    }

    private void OnProgress(object? sender, RuleDraftingProgress e)
    {
        Post(() =>
        {
            // Each call's tokens are reported when it returns; the conversation counts them for the request.
            var usage = e.Usage;
            _feed.AddUsage(usage.InputTokens - _reportedUsage.InputTokens, usage.OutputTokens - _reportedUsage.OutputTokens, 0);
            _reportedUsage = usage;
            if (!e.WaitingForModel) return;
            _replyStreamed = false;
            _feed.BeginRequest();
        });
    }

    private void OnReplyDelta(object? sender, AssistantMessageDeltaEventArgs e)
        => Post(() =>
        {
            _replyStreamed = true;
            _feed.AppendReply(e.Text);
        });

    private void OnReasoningDelta(object? sender, ReasoningDeltaEventArgs e) => Post(() => _feed.AppendReasoning(e.Text));

    private void Post(Action action)
    {
        if (_context is null || SynchronizationContext.Current == _context)
            action();
        else
            _context.Post(_ => action(), null);
    }

    /// <summary>The drafted rule's table, When and Condition, one per line.</summary>
    private static string DescribeRule(RuleDraft draft)
    {
        var lines = new List<string>();
        if (draft.TargetTable.Length > 0) lines.Add($"{nameof(FormRule.TargetTable)}: {draft.TargetTable}");
        if (draft.When.Length > 0) lines.Add($"{nameof(FormRule.When)}: {draft.When}");
        lines.Add($"{nameof(FormRule.Condition)}: {draft.Condition}");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>A sample's values, then the number of its detail rows per table, in one line.</summary>
    public static string DescribeValues(RuleDraftSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var parts = sample.Values.Select(pair => $"{pair.Key} = {FormatValue(pair.Value)}").ToList();
        parts.AddRange(sample.DetailRows.Select(pair => L("RuleAssistant_DetailRows", pair.Key, pair.Value.Count)));
        return parts.Count == 0 ? L("RuleAssistant_AllBlank") : string.Join(", ", parts);
    }

    private static string FormatValue(object? value) => value switch
    {
        null => L("RuleAssistant_Blank"),
        string { Length: 0 } => L("RuleAssistant_Blank"),
        Guid guid when guid == Guid.Empty => L("RuleAssistant_Blank"),
        string text => $"\"{text}\"",
        DateTime date => date.ToString(date.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string OutcomeText(RuleSampleOutcome outcome) => L(outcome switch
    {
        RuleSampleOutcome.Passes => "RuleAssistant_Passes",
        RuleSampleOutcome.Blocked => "RuleAssistant_Blocked",
        _ => "RuleAssistant_Skipped",
    });

    /// <summary>Stops a request in progress and releases the chat client.</summary>
    public void Dispose()
    {
        _cancellation?.Cancel();
        _client?.Dispose();
        _client = null;
    }
}
