using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Definition.Drafting;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>One question of a <see cref="DraftQuestionCard"/>: the answers to choose from, or one in the user's words.</summary>
public sealed partial class DraftQuestionItem : ObservableObject
{
    private readonly DraftQuestionCard _card;

    internal DraftQuestionItem(DraftQuestionCard card, DraftQuestion question)
    {
        _card = card;
        Text = question.Text;
        for (var i = 0; i < question.Options.Count; i++)
            Choices.Add(new DraftQuestionChoice(this, question.Options[i], i == question.RecommendedIndex));
    }

    /// <summary>Gets the question.</summary>
    public string Text { get; }

    /// <summary>Gets the answers to choose from.</summary>
    public ObservableCollection<DraftQuestionChoice> Choices { get; } = [];

    /// <summary>Gets or sets an answer in the user's own words; typing one clears the chosen option.</summary>
    [ObservableProperty]
    private string _otherText = string.Empty;

    /// <summary>Gets whether the card still takes answers.</summary>
    public bool IsOpen => _card.IsOpen;

    /// <summary>Gets the answer: the chosen option, or the user's own words; empty when there is none.</summary>
    public string Answer => Choices.FirstOrDefault(c => c.IsSelected)?.Label ?? OtherText.Trim();

    /// <summary>Gets the option the model suggests, or <see langword="null"/>.</summary>
    public DraftQuestionChoice? Recommended => Choices.FirstOrDefault(c => c.IsRecommended);

    internal void Choose(DraftQuestionChoice choice)
    {
        if (!IsOpen) return;
        foreach (var other in Choices)
            other.IsSelected = ReferenceEquals(other, choice);
        if (OtherText.Length > 0)
            OtherText = string.Empty;
        _card.OnAnswerChanged(chosen: true);
    }

    partial void OnOtherTextChanged(string value)
    {
        if (value.Trim().Length > 0)
        {
            foreach (var choice in Choices)
                choice.IsSelected = false;
        }
        _card.OnAnswerChanged(chosen: false);
    }

    internal void NotifyOpenChanged()
    {
        OnPropertyChanged(nameof(IsOpen));
        foreach (var choice in Choices)
            choice.NotifyOpenChanged();
    }
}
