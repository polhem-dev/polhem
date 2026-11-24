using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using Polhem.Chat;
using Polhem.DefineEditor.Services;
using Polhem.Definition.Drafting;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// The questions the model asked, with the answers to choose from. A single question is answered by clicking an option;
/// several are answered together. The answers go back as an ordinary message, so typing a reply works as well.
/// </summary>
/// <remarks>An entry of the drafting conversation of the application's own, with its template in the drafting window.</remarks>
public sealed partial class DraftQuestionCard : ChatEntry
{
    private readonly Action<string> _submit;

    /// <summary>Initializes a new instance of the <see cref="DraftQuestionCard"/> class.</summary>
    /// <param name="questions">The questions the model asked.</param>
    /// <param name="submit">Sends the composed answers as the user's message.</param>
    public DraftQuestionCard(IReadOnlyList<DraftQuestion> questions, Action<string> submit)
    {
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(submit);
        _submit = submit;
        foreach (var question in questions)
            Questions.Add(new DraftQuestionItem(this, question));
    }

    /// <summary>Gets the questions.</summary>
    public ObservableCollection<DraftQuestionItem> Questions { get; } = [];

    /// <summary>Gets whether there are several questions, answered together with one button.</summary>
    public bool IsMultiple => Questions.Count > 1;

    /// <summary>Gets whether any question has an option the model suggests.</summary>
    public bool HasRecommendations => Questions.Any(q => q.Recommended is not null);

    /// <summary>Gets whether the card still takes answers: until they are sent, or the user replies some other way.</summary>
    public bool IsOpen
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            SubmitCommand.NotifyCanExecuteChanged();
            UseRecommendationsCommand.NotifyCanExecuteChanged();
            foreach (var question in Questions)
                question.NotifyOpenChanged();
        }
    } = true;

    /// <summary>Stops taking answers, because the user replied in the conversation instead.</summary>
    public void Close() => IsOpen = false;

    /// <summary>The message the answers are sent as: each question with its answer, or a note that it has none.</summary>
    public string ComposeAnswers()
    {
        if (Questions.Count == 1)
            return Questions[0].Answer;
        var builder = new StringBuilder();
        builder.AppendLine(L("Drafting_AnswersHeader"));
        for (var i = 0; i < Questions.Count; i++)
        {
            var answer = Questions[i].Answer;
            builder.Append(i + 1).Append(". ").Append(Questions[i].Text).Append(" → ")
                .AppendLine(answer.Length > 0 ? answer : L("Drafting_NotAnswered"));
        }
        return builder.ToString().TrimEnd();
    }

    internal void OnAnswerChanged(bool chosen)
    {
        SubmitCommand.NotifyCanExecuteChanged();
        if (chosen && !IsMultiple)
            Submit();
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private void Submit()
    {
        if (!CanSubmit()) return;
        var text = ComposeAnswers();
        IsOpen = false;
        _submit(text);
    }

    private bool CanSubmit() => IsOpen && Questions.Any(q => q.Answer.Length > 0);

    [RelayCommand(CanExecute = nameof(IsOpen))]
    private void UseRecommendations()
    {
        foreach (var question in Questions.Where(q => q.Answer.Length == 0))
        {
            if (question.Recommended is { } recommended)
            {
                foreach (var choice in question.Choices)
                    choice.IsSelected = ReferenceEquals(choice, recommended);
            }
        }
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private static string L(string key) => LocalizationService.Current[key];
}
