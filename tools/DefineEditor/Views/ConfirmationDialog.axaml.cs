using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Polhem.DefineEditor.Views;

/// <summary>
/// Outcome of the three-button "unsaved changes" prompt. <c>Cancel</c> is the
/// first member so it is also the defensive default should the result ever be
/// materialised without an explicit choice.
/// </summary>
public enum ConfirmCloseResult
{
    Cancel,
    Save,
    Discard,
}

public partial class ConfirmationDialog : Window
{
    /// <summary>
    /// True when the dialog runs in the three-button unsaved-changes mode —
    /// the shared Confirm / Cancel click handlers then close with a
    /// <see cref="ConfirmCloseResult"/> instead of a bool.
    /// </summary>
    private bool _isUnsavedMode;

    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the dialog modally over <paramref name="owner"/> and returns
    /// <c>true</c> if the user clicked the confirm button, <c>false</c> if
    /// they cancelled or closed the window. Pass <paramref name="confirmLabel"/>
    /// to override the default "OK" button text (e.g. "Save anyway" or
    /// "Delete") — caller resolves the label through LocalizationService.
    /// </summary>
    public static async Task<bool> ShowAsync(
        Window owner, string title, string message,
        string? confirmLabel = null, string? cancelLabel = null)
    {
        var dialog = new ConfirmationDialog();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.Title = title;
        if (!string.IsNullOrEmpty(confirmLabel)) dialog.ConfirmButton.Content = confirmLabel;
        if (!string.IsNullOrEmpty(cancelLabel)) dialog.CancelButton.Content = cancelLabel;
        return await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>
    /// Shows a message with a single button, for an outcome the user can only acknowledge.
    /// </summary>
    public static async Task ShowMessageAsync(Window owner, string title, string message)
    {
        var dialog = new ConfirmationDialog();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.Title = title;
        dialog.CancelButton.IsVisible = false;
        await dialog.ShowDialog<bool>(owner);
    }

    /// <summary>
    /// Shows a message with one radio button per choice and returns the index of the chosen one, or
    /// <see langword="null"/> when the user cancelled or closed the window.
    /// </summary>
    public static async Task<int?> ShowChoiceAsync(
        Window owner, string title, string message, IReadOnlyList<string> choices, int selectedIndex,
        string? confirmLabel = null)
    {
        var dialog = new ConfirmationDialog();
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.Title = title;
        if (!string.IsNullOrEmpty(confirmLabel)) dialog.ConfirmButton.Content = confirmLabel;

        var buttons = choices
            .Select((choice, index) => new RadioButton
            {
                Content = new TextBlock { Text = choice, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                GroupName = "choices",
                IsChecked = index == selectedIndex,
            })
            .ToList();
        foreach (var button in buttons) dialog.ChoicesPanel.Children.Add(button);
        dialog.ChoicesPanel.IsVisible = true;

        return await dialog.ShowDialog<bool>(owner)
            ? buttons.FindIndex(b => b.IsChecked == true)
            : null;
    }

    /// <summary>
    /// Shows the three-button unsaved-changes prompt (Save / Don't Save /
    /// Cancel) modally over <paramref name="owner"/>. Closing the window
    /// without choosing counts as <see cref="ConfirmCloseResult.Cancel"/>.
    /// </summary>
    public static async Task<ConfirmCloseResult> ShowUnsavedAsync(
        Window owner, string title, string message,
        string saveLabel, string discardLabel, string cancelLabel)
    {
        var dialog = new ConfirmationDialog();
        dialog._isUnsavedMode = true;
        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.Title = title;
        dialog.ConfirmButton.Content = saveLabel;
        dialog.CancelButton.Content = cancelLabel;
        dialog.DiscardButton.Content = discardLabel;
        dialog.DiscardButton.IsVisible = true;
        return await dialog.ShowDialog<ConfirmCloseResult?>(owner) ?? ConfirmCloseResult.Cancel;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (_isUnsavedMode) Close(ConfirmCloseResult.Save);
        else Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (_isUnsavedMode) Close(ConfirmCloseResult.Cancel);
        else Close(false);
    }

    private void OnDiscard(object? sender, RoutedEventArgs e) => Close(ConfirmCloseResult.Discard);
}
