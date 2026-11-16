using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Polhem.Definition.Drafting;

namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// What the model is doing, shown in the conversation while a turn runs: a title that follows the current step, and the
/// steps so far, which the user can expand. When the turn ends the card collapses into how many steps it took.
/// </summary>
public sealed partial class DraftActivityCard : DraftChatItem
{
    private int _toolSteps;

    /// <summary>Initializes a new instance of the <see cref="DraftActivityCard"/> class.</summary>
    public DraftActivityCard()
    {
        _title = L("DraftingStep_Thinking") + "…";
    }

    /// <summary>Gets the steps so far.</summary>
    public ObservableCollection<DraftActivityStep> Steps { get; } = [];

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isRunning = true;

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Gets whether the card shows anything beyond its title; an empty card is removed when the turn ends.</summary>
    public bool HasSteps => Steps.Count > 0;

    /// <summary>Records a step the session reported.</summary>
    /// <param name="activity">The step.</param>
    public void Apply(DraftActivityEventArgs activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        switch (activity.Kind)
        {
            case DraftActivityKind.Thinking:
                Title = L("DraftingStep_Thinking") + "…";
                break;
            case DraftActivityKind.Reasoning:
                Steps.Add(new DraftActivityStep(L("DraftingStep_Reasoning"), activity.Detail, IsError: false, IsReasoning: true));
                break;
            case DraftActivityKind.ToolStarted:
                Title = Describe(activity.ToolName, activity.Subject) + "…";
                break;
            case DraftActivityKind.ToolCompleted:
                _toolSteps++;
                Steps.Add(new DraftActivityStep(Describe(activity.ToolName, activity.Subject), string.Empty, IsError: false, IsReasoning: false));
                break;
            case DraftActivityKind.ToolRefused:
                _toolSteps++;
                Steps.Add(new DraftActivityStep(Describe(activity.ToolName, activity.Subject), activity.Detail, IsError: true, IsReasoning: false));
                break;
        }
        OnPropertyChanged(nameof(HasSteps));
    }

    /// <summary>Ends the card when the turn ends: it stops spinning and its title says how the turn went.</summary>
    /// <param name="completed">Whether the turn completed; otherwise it failed or was stopped.</param>
    public void Finish(bool completed)
    {
        IsRunning = false;
        IsExpanded = false;
        Title = (completed, _toolSteps) switch
        {
            (true, 1) => L("DraftingStep_DoneOne"),
            (true, _) => L("DraftingStep_Done", _toolSteps),
            (false, 1) => L("DraftingStep_StoppedOne"),
            _ => L("DraftingStep_Stopped", _toolSteps),
        };
    }

    /// <summary>
    /// What a tool call does, such as "Setting up the field leave_type", from the tool's name and what it works on.
    /// </summary>
    /// <param name="toolName">The tool, such as <c>upsert_field</c>.</param>
    /// <param name="subject">What it works on, such as a field name.</param>
    public static string Describe(string toolName, string subject)
    {
        var key = "DraftingStep_" + toolName;
        var format = Services.LocalizationService.Current[key];
        return string.Equals(format, key, StringComparison.Ordinal)
            ? L("DraftingStep_Other", toolName)
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, format, subject);
    }
}
