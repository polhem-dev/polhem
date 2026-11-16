using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Polhem.Definition.Drafting;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>One answer the model offers to a question, shown as a button.</summary>
public sealed partial class DraftQuestionChoice : ObservableObject
{
    private readonly DraftQuestionItem _question;

    internal DraftQuestionChoice(DraftQuestionItem question, DraftQuestionOption option, bool isRecommended)
    {
        _question = question;
        Label = option.Label;
        Description = option.Description;
        IsRecommended = isRecommended;
    }

    /// <summary>Gets the answer.</summary>
    public string Label { get; }

    /// <summary>Gets what choosing it means; may be empty.</summary>
    public string Description { get; }

    /// <summary>Gets whether there is a description to show.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>Gets whether the model suggests this answer.</summary>
    public bool IsRecommended { get; }

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Gets whether the question still takes answers.</summary>
    public bool IsOpen => _question.IsOpen;

    internal void NotifyOpenChanged() => OnPropertyChanged(nameof(IsOpen));

    [RelayCommand]
    private void Select() => _question.Choose(this);
}
