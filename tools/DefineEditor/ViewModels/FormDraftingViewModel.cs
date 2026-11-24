using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Drafting;
using Polhem.Definition.Layouts;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The window that drafts a form from documents: the conversation with the model on the left, and on the right the
/// form as it stands (the FormLayout the import would write), its fields and the open questions.
/// </summary>
/// <remarks>
/// The session raises its events on the thread that runs the turn. They are passed to the thread that created this
/// view-model, so the collections the window binds to change only there.
/// </remarks>
public sealed partial class FormDraftingViewModel : ViewModelBase, IDisposable
{
    private readonly FormDraftSession _session;
    private readonly IFormDraftingHost _host;
    private readonly string _definePath;
    private readonly Func<FormDraftResult, Task<bool>> _import;
    private readonly SynchronizationContext? _context;
    private CancellationTokenSource? _cancellation;
    private readonly List<DraftActivityCard> _turnCards = [];
    private DraftActivityCard? _activity;
    private DraftQuestionCard? _pendingQuestions;
    private readonly StringBuilder _streamText = new();
    private DraftChatEntry? _streaming;
    private bool _redrawPending;
    private readonly StringBuilder _reasoningText = new();
    private FormDraftUsage? _turnUsage;

    /// <summary>
    /// How often a streaming reply is redrawn. Each redraw parses the Markdown again, so the pieces that arrive in
    /// between are gathered rather than drawn one by one.
    /// </summary>
    internal static TimeSpan StreamRedrawInterval { get; set; } = TimeSpan.FromMilliseconds(80);

    /// <summary>Initializes a new instance of the <see cref="FormDraftingViewModel"/> class.</summary>
    /// <param name="session">The conversation; this view-model runs every turn of it.</param>
    /// <param name="host">The checks the session's host runs, listed in the Questions tab with the conversion's own.</param>
    /// <param name="definePath">The definition folder the form will be imported into.</param>
    /// <param name="documentsLabel">The documents being drafted from, as the window title shows them.</param>
    /// <param name="credentialText">Which credential the requests use, shown so a forgotten API key is noticed.</param>
    /// <param name="import">Imports the converted draft and returns whether files were written; the caller closes the window.</param>
    public FormDraftingViewModel(FormDraftSession session, IFormDraftingHost host, string definePath, string documentsLabel, string credentialText,
        Func<FormDraftResult, Task<bool>> import)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(import);
        _session = session;
        _host = host;
        _definePath = definePath;
        _import = import;
        _context = SynchronizationContext.Current;
        DocumentsLabel = documentsLabel;
        CredentialText = credentialText;
        _session.AssistantMessage += OnAssistantMessage;
        _session.AssistantMessageDelta += OnAssistantMessageDelta;
        _session.ReasoningDelta += OnReasoningDelta;
        _session.UsageChanged += OnUsageChanged;
        _session.DraftChanged += OnSessionDraftChanged;
        _session.Activity += OnSessionActivity;
        _session.QuestionsAsked += OnSessionQuestionsAsked;
    }

    /// <summary>Occurs after the draft changed; the window redraws the preview once the changes pause.</summary>
    public event EventHandler? DraftChanged;

    /// <summary>Gets the documents being drafted from.</summary>
    public string DocumentsLabel { get; }

    /// <summary>Gets which credential the requests use.</summary>
    public string CredentialText { get; }

    /// <summary>Gets the messages of the conversation.</summary>
    public ObservableCollection<DraftChatItem> Messages { get; } = [];

    /// <summary>Gets the fields of the draft, table by table.</summary>
    public ObservableCollection<DraftFieldRow> Fields { get; } = [];

    /// <summary>Gets the problems the draft still has.</summary>
    public ObservableCollection<DraftIssueRow> Issues { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditFields), nameof(ShowReadyHint))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(StopCommand), nameof(CreateCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReadyHint))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isReady;

    /// <summary>Gets whether to tell the user the definitions can now be created.</summary>
    public bool ShowReadyHint => IsReady && !IsBusy;

    [ObservableProperty]
    private DraftFieldRow? _selectedField;

    /// <summary>Gets or sets the selected tab: 0 the form, 1 the fields, 2 the questions.</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private FormLayout? _previewLayout;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewError))]
    private string _previewError = string.Empty;

    [ObservableProperty]
    private bool _isPreviewCompact;

    [ObservableProperty]
    private bool _isPreviewViewMode;

    /// <summary>Gets the time and tokens of the turn in progress, or of the last turn once it ended.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsage))]
    private string _usageText = string.Empty;

    /// <summary>Gets whether there is a turn to report the cost of.</summary>
    public bool HasUsage => UsageText.Length > 0;

    /// <summary>Gets whether the preview could not be drawn.</summary>
    public bool HasPreviewError => PreviewError.Length > 0;

    /// <summary>Gets whether the user may edit the fields: not while the model is working on the draft.</summary>
    public bool CanEditFields => !IsBusy;

    /// <summary>Gets whether the draft has no open problems.</summary>
    public bool HasNoIssues => Issues.Count == 0;

    /// <summary>Gets the title of the Questions tab, with how many problems are open.</summary>
    public string IssuesTabTitle => Issues.Count == 0 ? L("Drafting_TabIssues") : L("Drafting_TabIssuesCount", Issues.Count);

    /// <summary>Sends the documents and waits for the model's first draft.</summary>
    /// <param name="documents">The documents, as <see cref="Polhem.Definition.Drafting.Documents.DocumentSource"/> read them.</param>
    public Task StartAsync(IReadOnlyList<AIContent> documents)
    {
        Messages.Add(new DraftChatEntry(isUser: true, L("Drafting_SentDocuments", DocumentsLabel)));
        RefreshDraft();
        return RunTurnAsync(token => _session.StartAsync(documents, token));
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync()
    {
        var text = InputText.Trim();
        InputText = string.Empty;
        return SendTextAsync(text);
    }

    /// <summary>Sends the answers of a question card as the user's message.</summary>
    private void SubmitAnswers(string text)
    {
        if (IsBusy || text.Trim().Length == 0) return;
        _ = SendTextAsync(text);
    }

    private Task SendTextAsync(string text)
    {
        // A reply of any kind answers the open questions; their cards stop taking answers.
        foreach (var card in Messages.OfType<DraftQuestionCard>())
            card.Close();
        Messages.Add(new DraftChatEntry(isUser: true, text));
        return RunTurnAsync(token => _session.SendAsync(text, token));
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Stop() => _cancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        await _import(FormDraftConverter.Convert(_session.Draft));
    }

    private bool CanCreate() => IsReady && !IsBusy;

    /// <summary>
    /// Hands a committed edit of a row of the Fields tab to the session. The model hears of it with the user's next
    /// message, and the draft has to be marked ready again.
    /// </summary>
    /// <param name="row">The edited row.</param>
    public void CommitFieldEdit(DraftFieldRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var description = row.DescribeChanges();
        if (description.Length == 0 || IsBusy) return;
        _session.ApplyUserEdit(draft =>
        {
            if (row.TableIndex < draft.Tables.Count && row.FieldIndex < draft.Tables[row.TableIndex].Fields.Count)
                row.ApplyTo(draft.Tables[row.TableIndex].Fields[row.FieldIndex]);
        }, description);
    }

    /// <summary>Shows a field picked in the preview in the Fields tab.</summary>
    /// <param name="fieldName">The field name the preview shows, as the conversion wrote it.</param>
    public void RevealField(string fieldName)
    {
        var row = Fields.FirstOrDefault(r => string.Equals(ConvertedFieldName(r.FieldName), fieldName, StringComparison.OrdinalIgnoreCase));
        if (row is null) return;
        SelectedField = row;
        SelectedTabIndex = 1;
    }

    /// <summary>
    /// Redraws the preview from the draft as it stands: the draft is converted, the form conventions applied, and the
    /// FormLayout drawn the way the import will write it.
    /// </summary>
    public void RefreshPreview()
    {
        try
        {
            var result = FormDraftConverter.Convert(_session.Draft);
            var zhTw = LocalizationService.Current.Culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            PreviewLayout = FormDraftImport.BuildPreviewLayout(result, _definePath, zhTw);
            PreviewError = string.Empty;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        {
            // An incomplete draft must not stop the conversation; the preview says what is wrong and waits for the next change.
            PreviewLayout = null;
            PreviewError = L("Preview_RenderFailed", ex.Message);
        }
    }

    /// <summary>Stops the turn in progress and detaches from the session.</summary>
    public void Dispose()
    {
        _cancellation?.Cancel();
        _session.AssistantMessage -= OnAssistantMessage;
        _session.AssistantMessageDelta -= OnAssistantMessageDelta;
        _session.ReasoningDelta -= OnReasoningDelta;
        _session.UsageChanged -= OnUsageChanged;
        _session.DraftChanged -= OnSessionDraftChanged;
        _session.Activity -= OnSessionActivity;
        _session.QuestionsAsked -= OnSessionQuestionsAsked;
    }

    private async Task RunTurnAsync(Func<CancellationToken, Task> turn)
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true;
        var completed = false;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _turnUsage = null;
        UsageText = DescribeProgress(null, clock.Elapsed);
        using var ticking = new CancellationTokenSource();
        _ = TickAsync(clock, ticking.Token);
        try
        {
            await turn(cancellation.Token);
            completed = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Messages.Add(new DraftChatEntry(isUser: false, L("Drafting_Stopped"), isError: true));
        }
        catch (Exception ex)
        {
            // Boundary: a turn runs from a button of the window; an exception that escaped it would end the editor. The
            // session has already put the draft back as it was before the turn, so the user can simply try again.
            Messages.Add(new DraftChatEntry(isUser: false,
                DraftingChatClients.DescribeFailure(ex) ?? L("Drafting_TurnFailed", ex.Message), isError: true));
        }
        finally
        {
            _cancellation = null;
            // NOTE: the ticker is cancelled, not awaited. Awaiting it adds a real asynchronous hop at the end of every turn,
            // so the caller resumes after the turn off the thread that created this view-model, and a session event it
            // causes next (DraftChanged after a field edit) is then posted instead of handled at once.
            ticking.Cancel();
            UsageText = DescribeUsage(_session.LastTurnUsage, clock.Elapsed);
            // The session's events were posted before the turn's task completed, so they have all been handled here.
            EndTurn(completed);
            IsBusy = false;
            RefreshDraft();
        }
    }

    /// <summary>
    /// Closes the turn's activity cards, drops the ones that never showed a step, and shows the questions the model
    /// asked, last, where the user answers them. A turn that failed loses its questions: the session no longer holds them.
    /// </summary>
    private void EndTurn(bool completed)
    {
        foreach (var card in _turnCards)
        {
            card.Finish(completed);
            if (!card.HasSteps)
                Messages.Remove(card);
        }
        _turnCards.Clear();
        _activity = null;
        // A reply still arriving when the turn ended belongs to no answer: the session has removed the turn.
        if (_streaming is not null)
            Messages.Remove(_streaming);
        _streaming = null;
        _streamText.Clear();
        _reasoningText.Clear();
        if (completed && _pendingQuestions is { } questions)
            Messages.Add(questions);
        _pendingQuestions = null;
    }

    private void OnAssistantMessage(object? sender, AssistantMessageEventArgs e)
        => Post(() =>
        {
            var text = e.Text.Trim();
            if (_streaming is { } entry)
                entry.Complete(text);
            else
                Messages.Add(new DraftChatEntry(isUser: false, text));
            _streaming = null;
            _streamText.Clear();
        });

    private void OnAssistantMessageDelta(object? sender, AssistantMessageDeltaEventArgs e)
        => Post(() =>
        {
            _streamText.Append(e.Text);
            if (_streaming is null)
            {
                // Leading whitespace opens no bubble: a reply that turns out to be only whitespace is never shown.
                if (_streamText.ToString().Trim().Length == 0) return;
                _streaming = DraftChatEntry.Streaming();
                Messages.Add(_streaming);
                ShowStreamText();
                return;
            }
            RedrawSoon();
        });

    private void OnReasoningDelta(object? sender, ReasoningDeltaEventArgs e)
        => Post(() =>
        {
            // The pieces belong to the card the request started in, even after a reply has streamed in below it.
            if (_activity is null) return;
            var first = _reasoningText.Length == 0;
            _reasoningText.Append(e.Text);
            if (first)
                _activity.ShowReasoning(_reasoningText.ToString());
            else
                RedrawSoon();
        });

    // Gathers the pieces that arrive within one redraw interval: each redraw lays the text out again, and a reply's
    // Markdown is parsed again.
    private void RedrawSoon()
    {
        if (_redrawPending) return;
        _redrawPending = true;
        _ = RedrawLaterAsync();
    }

    private async Task RedrawLaterAsync()
    {
        await Task.Delay(StreamRedrawInterval);
        Post(() =>
        {
            _redrawPending = false;
            ShowStreamText();
            if (_reasoningText.Length > 0)
                _activity?.ShowReasoning(_reasoningText.ToString());
        });
    }

    private void ShowStreamText() => _streaming?.ShowPartial(_streamText.ToString().Trim());

    private void OnUsageChanged(object? sender, FormDraftUsageEventArgs e)
        => Post(() => _turnUsage = e.Usage);

    // Runs on the thread that started the turn, as the turn does, so the usage line changes there.
    private async Task TickAsync(System.Diagnostics.Stopwatch clock, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500, cancellationToken);
                // The turn may have ended while the delay completed; its final line must stay.
                if (cancellationToken.IsCancellationRequested) return;
                UsageText = DescribeProgress(_turnUsage, clock.Elapsed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The turn ended; the caller writes the final usage.
        }
    }

    /// <summary>The call in progress and the seconds so far, while a turn runs.</summary>
    /// <param name="usage">What the turn has cost so far, or <see langword="null"/> before its first call.</param>
    /// <param name="elapsed">The time since the turn began.</param>
    /// <returns>The line.</returns>
    public static string DescribeProgress(FormDraftUsage? usage, TimeSpan elapsed)
        => L("Drafting_UsageRunning", Math.Max(usage?.Calls ?? 1, 1), (int)elapsed.TotalSeconds);

    /// <summary>The calls, the tokens and the time a finished turn took, in one line.</summary>
    /// <param name="usage">What the turn cost.</param>
    /// <param name="elapsed">The time the turn took, as the window measured it.</param>
    /// <returns>The line.</returns>
    public static string DescribeUsage(FormDraftUsage usage, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var seconds = (int)Math.Round(elapsed.TotalSeconds);
        if (usage.InputTokens == 0 && usage.OutputTokens == 0)
            return L("Drafting_UsageNoTokens", usage.Calls, seconds);
        var input = usage.InputTokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        var output = usage.OutputTokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        return usage.CachedInputTokens == 0
            ? L("Drafting_Usage", usage.Calls, input, output, seconds)
            : L("Drafting_UsageCached", usage.Calls, input,
                usage.CachedInputTokens.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), output, seconds);
    }

    private void OnSessionActivity(object? sender, DraftActivityEventArgs e)
        => Post(() =>
        {
            // A response's reasoning is reported once the response has ended, after its reply streamed in below the
            // card; it still belongs to the card the response started in.
            if (e.Kind is DraftActivityKind.Thinking or DraftActivityKind.Reasoning)
                _reasoningText.Clear();
            if (e.Kind == DraftActivityKind.Reasoning && _activity is not null)
            {
                _activity.Apply(e);
                return;
            }
            // A reply the model wrote between tool calls ends the card above it; the next steps start a new one below.
            if (_activity is null || !ReferenceEquals(Messages.LastOrDefault(), _activity))
            {
                _activity = new DraftActivityCard();
                _turnCards.Add(_activity);
                Messages.Add(_activity);
            }
            _activity.Apply(e);
        });

    private void OnSessionQuestionsAsked(object? sender, DraftQuestionsEventArgs e)
        => Post(() => _pendingQuestions = new DraftQuestionCard(e.Questions, SubmitAnswers));

    private void OnSessionDraftChanged(object? sender, EventArgs e)
        => Post(() =>
        {
            RefreshDraft();
            DraftChanged?.Invoke(this, EventArgs.Empty);
        });

    /// <summary>Rebuilds the Fields and Questions tabs and the ready state from the draft.</summary>
    private void RefreshDraft()
    {
        IsReady = _session.IsReady;
        var selected = SelectedField is { } current ? (current.TableIndex, current.FieldIndex) : ((int, int)?)null;

        Fields.Clear();
        var draft = _session.Draft;
        for (var tableIndex = 0; tableIndex < draft.Tables.Count; tableIndex++)
        {
            var table = draft.Tables[tableIndex];
            var label = tableIndex == 0
                ? L("Drafting_MasterTable")
                : (table.TableName.Length > 0 ? table.TableName : table.DisplayName);
            for (var fieldIndex = 0; fieldIndex < table.Fields.Count; fieldIndex++)
                Fields.Add(new DraftFieldRow(tableIndex, fieldIndex, label, table.Fields[fieldIndex]));
        }
        SelectedField = selected is { } key
            ? Fields.FirstOrDefault(r => r.TableIndex == key.Item1 && r.FieldIndex == key.Item2)
            : null;

        Issues.Clear();
        var result = FormDraftConverter.Convert(draft);
        foreach (var issue in result.Issues.Concat(SessionChecks(draft)).Concat(_host.Validate(result)))
            Issues.Add(DraftIssueRow.From(issue));
        OnPropertyChanged(nameof(IssuesTabTitle));
        OnPropertyChanged(nameof(HasNoIssues));
    }

    /// <summary>
    /// What the session checks before it lets the model mark the draft ready, beyond the conversion and the host: the
    /// ProgId, a master table with fields, and relations the user has not confirmed.
    /// </summary>
    private static IEnumerable<DraftIssue> SessionChecks(FormDraft draft)
    {
        if (draft.ProgId.Trim().Length == 0)
            yield return new DraftIssue(DraftIssueSeverity.Error, "Schema", "The ProgId is not decided yet.");
        if (draft.Tables.Count == 0 || draft.Tables[0].Fields.Count == 0)
            yield return new DraftIssue(DraftIssueSeverity.Error, "master", "The master table has no fields.");
        var threshold = new FormDraftingOptions().RelationConfirmationThreshold;
        foreach (var field in draft.Tables.SelectMany(t => t.Fields))
        {
            if (field.Relation is { ProgId.Length: > 0 } relation && relation.Confidence < threshold)
            {
                yield return new DraftIssue(DraftIssueSeverity.Error, field.FieldName,
                    $"The relation to '{relation.ProgId}' has low confidence and is not confirmed by the user.");
            }
        }
    }

    /// <summary>The name the conversion gives a field the draft names <paramref name="draftName"/>.</summary>
    private static string ConvertedFieldName(string draftName)
    {
        var draft = new FormDraft { ProgId = "Probe" };
        draft.Tables.Add(new DraftTable { Fields = { new DraftField { FieldName = draftName } } });
        return FormDraftConverter.Convert(draft).Schema.Tables!.First().Fields!.FirstOrDefault()?.FieldName ?? string.Empty;
    }

    private void Post(Action action)
    {
        if (_context is null || SynchronizationContext.Current == _context)
            action();
        else
            _context.Post(_ => action(), null);
    }
}
