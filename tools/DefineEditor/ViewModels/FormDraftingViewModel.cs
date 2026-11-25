using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using Polhem.Chat;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Drafting;
using Polhem.Definition.Layouts;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The window that drafts a form from documents: the conversation with the model on the left, and on the right the
/// form as it stands (the FormLayout the import would write), its fields and the open questions.
/// </summary>
/// <remarks>
/// The session runs its own tool loop; what it reports is fed into <see cref="Conversation"/>, which the window's chat
/// view shows. The session raises its events on the thread that runs the turn. They are passed to the thread that
/// created this view-model, so the collections the window binds to change only there.
/// </remarks>
public sealed partial class FormDraftingViewModel : ViewModelBase, IDisposable
{
    private readonly FormDraftSession _session;
    private readonly IFormDraftingHost _host;
    private readonly string _definePath;
    private readonly Func<FormDraftResult, Task<bool>> _import;
    private readonly SynchronizationContext? _context;
    private readonly DraftingChatFeed _feed;
    private CancellationTokenSource? _cancellation;
    private DraftQuestionCard? _pendingQuestions;
    private FormDraftUsage _turnUsage = FormDraftUsage.None;

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
        _feed = new DraftingChatFeed(DescribeTool, Post, DraftingChatFeed.DefaultRedrawInterval);
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

    /// <summary>Gets the conversation, as the chat view shows it.</summary>
    public ChatConversation Conversation => _feed.Conversation;

    /// <summary>Gets the fields of the draft, table by table.</summary>
    public ObservableCollection<DraftFieldRow> Fields { get; } = [];

    /// <summary>Gets the problems the draft still has.</summary>
    public ObservableCollection<DraftIssueRow> Issues { get; } = [];

    /// <summary>Gets whether a turn is in progress; the chat view shows its stop button meanwhile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditFields), nameof(ShowReadyHint))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
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
        RefreshDraft();
        return RunTurnAsync(L("Drafting_SentDocuments", DocumentsLabel), token => _session.StartAsync(documents, token));
    }

    /// <summary>Sends what the user wrote, or the answers of a question card; nothing while a turn is in progress.</summary>
    /// <param name="text">The message.</param>
    public Task SendAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        if (IsBusy || text.Length == 0) return Task.CompletedTask;
        // A reply of any kind answers the open questions; their cards stop taking answers.
        foreach (var card in Conversation.Entries.OfType<DraftQuestionCard>())
            card.Close();
        return RunTurnAsync(text, token => _session.SendAsync(text, token));
    }

    private void SubmitAnswers(string text) => _ = SendAsync(text);

    /// <summary>Stops the turn in progress.</summary>
    public void Stop() => _cancellation?.Cancel();

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

    private async Task RunTurnAsync(string prompt, Func<CancellationToken, Task> turn)
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _pendingQuestions = null;
        _turnUsage = FormDraftUsage.None;
        _feed.Begin(prompt);
        IsBusy = true;
        try
        {
            await turn(cancellation.Token);
            _feed.Complete();
            // The questions show where the model asked them, and take answers once the turn has ended. A turn that failed
            // or was stopped leaves them closed: the session no longer holds them.
            _pendingQuestions?.Open();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _feed.Stop(L("Drafting_Stopped"));
        }
        catch (Exception ex)
        {
            // Boundary: a turn runs from the window's prompt box; an exception that escaped it would end the editor. The
            // session has already put the draft back as it was before the turn, so the user can simply try again.
            _feed.Fail(DraftingChatClients.DescribeFailure(ex) ?? L("Drafting_TurnFailed", ex.Message), ex);
        }
        finally
        {
            _cancellation = null;
            _pendingQuestions = null;
            IsBusy = false;
            RefreshDraft();
        }
    }

    private void OnAssistantMessage(object? sender, AssistantMessageEventArgs e)
        => Post(() =>
        {
            // A provider that streams nothing reports the whole reply only here.
            if (!_replyStreamed) _feed.AppendReply(e.Text.Trim());
        });

    private void OnAssistantMessageDelta(object? sender, AssistantMessageDeltaEventArgs e)
        => Post(() =>
        {
            _replyStreamed = true;
            _feed.AppendReply(e.Text);
        });

    private void OnReasoningDelta(object? sender, ReasoningDeltaEventArgs e)
        => Post(() =>
        {
            _reasoningStreamed = true;
            _feed.AppendReasoning(e.Text);
        });

    private void OnUsageChanged(object? sender, FormDraftUsageEventArgs e)
        => Post(() =>
        {
            var usage = e.Usage;
            _feed.AddUsage(usage.InputTokens - _turnUsage.InputTokens, usage.OutputTokens - _turnUsage.OutputTokens,
                usage.CachedInputTokens - _turnUsage.CachedInputTokens);
            _turnUsage = usage;
        });

    private bool _replyStreamed;
    private bool _reasoningStreamed;

    private void OnSessionActivity(object? sender, DraftActivityEventArgs e)
        => Post(() =>
        {
            switch (e.Kind)
            {
                case DraftActivityKind.Thinking:
                    _replyStreamed = false;
                    _reasoningStreamed = false;
                    _feed.BeginRequest();
                    break;
                case DraftActivityKind.Reasoning:
                    // The whole summary, once the response has ended; shown only when its pieces did not stream in.
                    if (!_reasoningStreamed) _feed.AppendReasoning(e.Detail);
                    break;
                case DraftActivityKind.ToolStarted:
                    _feed.StartTool(e.ToolName, e.Subject);
                    break;
                case DraftActivityKind.ToolCompleted:
                    _feed.EndTool(refusal: null);
                    break;
                case DraftActivityKind.ToolRefused:
                    _feed.EndTool(e.Detail);
                    break;
            }
        });

    /// <summary>
    /// What a tool call does, such as "Setting up the field leave_type", from the tool's name and what it works on.
    /// </summary>
    /// <param name="toolName">The tool, such as <c>upsert_field</c>.</param>
    /// <param name="subject">What it works on, such as a field name.</param>
    public static string DescribeTool(string toolName, string subject)
    {
        var key = "DraftingStep_" + toolName;
        var format = LocalizationService.Current[key];
        return string.Equals(format, key, StringComparison.Ordinal)
            ? L("DraftingStep_Other", toolName)
            : string.Format(CultureInfo.InvariantCulture, format, subject);
    }

    private void OnSessionQuestionsAsked(object? sender, DraftQuestionsEventArgs e)
        => Post(() =>
        {
            var card = new DraftQuestionCard(e.Questions, SubmitAnswers, isOpen: false);
            _pendingQuestions = card;
            _feed.AddEntry(card);
        });

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
