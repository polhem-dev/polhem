namespace Polhem.DefineEditor.ViewModels;

/// <summary>
/// One item of the drafting conversation: a message (<see cref="DraftChatEntry"/>), the steps of a turn
/// (<see cref="DraftActivityCard"/>) or the questions the model asked (<see cref="DraftQuestionCard"/>). The chat pane
/// picks a template by the item's type.
/// </summary>
public abstract class DraftChatItem : ViewModelBase
{
}
