using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
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
/// Nothing is sent before the user asks: the chat client is created on the first request. The draft replaces the whole
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
    private CancellationTokenSource? _cancellation;
    private bool _hasConversation;
    // Written by the drafting session's progress event, which may run on any thread, and read by the timer that updates
    // the progress line on the UI thread.
    private RuleDraftingProgress? _progress;
    // The streamed text of the request in progress. The drafting session raises its pieces on any thread; they gather
    // here under the lock, and the timer that updates the progress line shows them on the UI thread.
    private readonly object _streamLock = new();
    private readonly StringBuilder _streamReply = new();
    private readonly StringBuilder _streamReasoning = new();
    private int _streamCall;
    private int _streamReplyCall;
    private bool _streamChanged;
    private bool _streaming;

    /// <summary>The longest tail of the reasoning summary <see cref="ThinkingText"/> shows.</summary>
    public const int ThinkingTailLength = 300;

    /// <summary>
    /// How often the progress line, the reasoning and the reply in progress are redrawn. The reply is Markdown, which each
    /// redraw parses again, so the pieces that arrive in between are gathered rather than drawn one by one.
    /// </summary>
    private static readonly TimeSpan s_redrawInterval = TimeSpan.FromMilliseconds(80);

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

    /// <summary>What the user says the rule should check, or what to change.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DraftCommand), nameof(ReviseCommand))]
    private string _request = string.Empty;

    /// <summary>Whether a request is in progress.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DraftCommand), nameof(ReviseCommand), nameof(ExplainCommand), nameof(ApplyCommand),
        nameof(StopCommand))]
    private bool _isBusy;

    /// <summary>What the model said last, or why the request failed.</summary>
    [ObservableProperty]
    private string _reply = string.Empty;

    /// <summary>
    /// While a request runs, which call to the model it is waiting for and for how long; after it, the calls, the tokens
    /// and the time it took. Empty before the first request.
    /// </summary>
    [ObservableProperty]
    private string _usageText = string.Empty;

    /// <summary>
    /// The end of the reasoning summary the model is writing in the call in progress; empty when no request runs.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThinking))]
    private string _thinkingText = string.Empty;

    /// <summary>Whether there is reasoning to show.</summary>
    public bool HasThinking => ThinkingText.Length > 0;

    /// <summary>Whether <see cref="Reply"/> reports a failure.</summary>
    [ObservableProperty]
    private bool _isReplyError;

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

    /// <summary>Whether there is an explanation or a draft to show.</summary>
    public bool HasResult => Explanation.Length > 0;

    partial void OnExplanationChanged(string value) => OnPropertyChanged(nameof(HasResult));

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

    private bool CanAsk() => IsAvailable && !IsBusy && !string.IsNullOrWhiteSpace(Request);

    private bool CanRevise() => CanAsk() && _hasConversation;

    private bool CanExplain() => IsAvailable && !IsBusy && !string.IsNullOrWhiteSpace(_currentRule().Condition);

    /// <summary>Starts a new conversation and asks for a rule that checks what <see cref="Request"/> says.</summary>
    [RelayCommand(CanExecute = nameof(CanAsk))]
    private Task DraftAsync() => RunAsync(async (drafting, token) =>
    {
        var result = await drafting.DraftAsync(Request.Trim(), token);
        _hasConversation = true;
        ShowDraft(result);
    });

    /// <summary>Continues the conversation with what <see cref="Request"/> says about the last rule or question.</summary>
    [RelayCommand(CanExecute = nameof(CanRevise))]
    private Task ReviseAsync() => RunAsync(async (drafting, token) =>
        ShowDraft(await drafting.ReviseAsync(Request.Trim(), token)));

    /// <summary>Explains the rule as the editor holds it, with samples.</summary>
    [RelayCommand(CanExecute = nameof(CanExplain))]
    private Task ExplainAsync() => RunAsync(async (drafting, token) =>
    {
        var result = await drafting.ExplainAsync(_currentRule(), token);
        ShowReply(result.Explanation is not null, result.Reply);
        if (result.Explanation is { } explanation)
        {
            Draft = null;
            RuleText = string.Empty;
            ShowResult(explanation.Explanation, explanation.Samples);
        }
    });

    /// <summary>Stops the request in progress.</summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Stop() => _cancellation?.Cancel();

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
        ShowReply(result.Draft is not null, result.Reply);
        if (result.Draft is not { } draft) return;
        Draft = draft;
        Message = draft.Message;
        RuleText = DescribeRule(draft);
        foreach (var translation in Translations)
            translation.Text = draft.MessageTranslations.GetValueOrDefault(translation.Language, string.Empty);
        ShowResult(draft.Explanation, draft.Samples);
    }

    // After an accepted result the model only closes with a sentence that repeats the explanation, so it is not shown.
    // Without one, its reply is a question to answer with Revise, or why it could not write the rule.
    private void ShowReply(bool accepted, string reply)
    {
        StopStreaming();
        Reply = accepted ? string.Empty : reply.Length > 0 ? reply : L("RuleAssistant_NoResult");
        IsReplyError = !accepted && reply.Length == 0;
    }

    private void ShowResult(string explanation, IReadOnlyList<RuleDraftSample> samples)
    {
        Explanation = explanation;
        Samples.Clear();
        foreach (var sample in samples)
            Samples.Add(new RuleAssistantSample(sample.Description, DescribeValues(sample), sample.Outcome, OutcomeText(sample.Outcome)));
    }

    private async Task RunAsync(Func<RuleDrafting, CancellationToken, Task> request)
    {
        _cancellation = new CancellationTokenSource();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Volatile.Write(ref _progress, null);
        IsBusy = true;
        IsReplyError = false;
        Reply = string.Empty;
        UsageText = DescribeProgress(clock.Elapsed);
        StartStreaming();
        using var ticking = new CancellationTokenSource();
        _ = TickAsync(clock, ticking.Token);
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
            StopStreaming();
            Reply = L("RuleAssistant_Stopped");
            IsReplyError = true;
        }
        catch (Exception ex)
        {
            // Boundary: a request runs from a button of the dialog; an exception that escaped it would end the editor.
            // The drafting session leaves its conversation as it was before a failed request, so the user can retry.
            StopStreaming();
            Reply = DraftingChatClients.DescribeFailure(ex) ?? L("RuleAssistant_Failed", ex.Message);
            IsReplyError = true;
        }
        finally
        {
            // NOTE: the ticker is cancelled, not awaited: awaiting it would add a real asynchronous hop at the end of
            // every request (see FormDraftingViewModel.RunTurnAsync).
            ticking.Cancel();
            StopStreaming();
            UsageText = DescribeUsage(Volatile.Read(ref _progress)?.Usage, clock.Elapsed);
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
            DraftCommand.NotifyCanExecuteChanged();
            ReviseCommand.NotifyCanExecuteChanged();
        }
    }

    // Runs on the UI thread, as the request does, so the progress line and the streamed text change there.
    private async Task TickAsync(System.Diagnostics.Stopwatch clock, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(s_redrawInterval, cancellationToken);
                // The request may have ended while the delay completed; its final line must stay.
                if (cancellationToken.IsCancellationRequested) return;
                UsageText = DescribeProgress(clock.Elapsed);
                ShowStreamed();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The request ended; the caller writes the final usage.
        }
    }

    private void OnProgress(object? sender, RuleDraftingProgress e)
    {
        Volatile.Write(ref _progress, e);
        if (!e.WaitingForModel) return;
        lock (_streamLock)
        {
            // A new call writes its own reasoning; its reply replaces the previous call's only once it writes one.
            _streamCall = e.Usage.Calls;
            _streamReasoning.Clear();
            _streamChanged = true;
        }
    }

    private void OnReplyDelta(object? sender, AssistantMessageDeltaEventArgs e)
    {
        lock (_streamLock)
        {
            if (_streamReplyCall != _streamCall)
            {
                _streamReply.Clear();
                _streamReplyCall = _streamCall;
            }
            _streamReply.Append(e.Text);
            _streamChanged = true;
        }
    }

    private void OnReasoningDelta(object? sender, ReasoningDeltaEventArgs e)
    {
        lock (_streamLock)
        {
            _streamReasoning.Append(e.Text);
            _streamChanged = true;
        }
    }

    private void StartStreaming()
    {
        lock (_streamLock)
        {
            _streamReply.Clear();
            _streamReasoning.Clear();
            _streamCall = 0;
            _streamReplyCall = 0;
            _streamChanged = false;
            _streaming = true;
        }
        ThinkingText = string.Empty;
    }

    // Ends what the request streamed: the result, an error or the stop message takes the reply's place.
    private void StopStreaming()
    {
        lock (_streamLock)
        {
            _streaming = false;
            _streamReply.Clear();
            _streamReasoning.Clear();
        }
        ThinkingText = string.Empty;
    }

    /// <summary>Shows what has streamed since the last redraw: the reasoning's tail and the reply so far.</summary>
    public void ShowStreamed()
    {
        string reply;
        string reasoning;
        lock (_streamLock)
        {
            if (!_streaming || !_streamChanged) return;
            _streamChanged = false;
            reply = _streamReply.ToString().Trim();
            reasoning = _streamReasoning.ToString().Trim();
        }
        ThinkingText = StreamedText.Tail(reasoning, ThinkingTailLength);
        if (reply.Length > 0) Reply = reply;
    }

    private string DescribeProgress(TimeSpan elapsed)
    {
        var progress = Volatile.Read(ref _progress);
        var seconds = (int)elapsed.TotalSeconds;
        return progress is null || progress.WaitingForModel
            ? L("RuleAssistant_ProgressCalling", Math.Max(progress?.Usage.Calls ?? 1, 1), seconds)
            : L("RuleAssistant_ProgressChecking", progress.Usage.Calls, seconds);
    }

    /// <summary>The calls, the tokens and the time a finished request took, in one line.</summary>
    public static string DescribeUsage(RuleDraftingUsage? usage, TimeSpan elapsed)
    {
        var seconds = (int)Math.Round(elapsed.TotalSeconds);
        if (usage is null || usage.Calls == 0) return L("RuleAssistant_UsageNoCalls", seconds);
        return usage.InputTokens == 0 && usage.OutputTokens == 0
            ? L("RuleAssistant_UsageNoTokens", usage.Calls, seconds)
            : L("RuleAssistant_Usage", usage.Calls, usage.InputTokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                usage.OutputTokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), seconds);
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
