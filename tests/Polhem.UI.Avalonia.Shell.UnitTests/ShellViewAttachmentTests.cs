using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Polhem.UI.Avalonia.Shell.Navigation;
using Polhem.UI.Avalonia.Shell.ViewModels;
using Polhem.UI.Avalonia.Shell.Views;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// <see cref="ShellView"/> attached to a desktop top level, which reports no safe area and no on-screen keyboard.
/// </summary>
[Collection(AvaloniaControlsCollection.Name)]
public class ShellViewAttachmentTests(HeadlessSessionFixture session)
{
    [Fact]
    [DisplayName("On a desktop window the shell keeps no padding")]
    public Task Attach_DesktopWindow_HasNoPadding() => session.RunAsync(() =>
    {
        var view = new ShellView();
        var window = new Window { Width = 800, Height = 600, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(default(Thickness), view.Padding);
        window.Close();
    });

    [Fact]
    [DisplayName("The shell can be detached and attached again")]
    public Task Detach_ThenAttach_DoesNotThrow() => session.RunAsync(() =>
    {
        var view = new ShellView();
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var error = Record.Exception(() =>
        {
            window.Content = null;
            Dispatcher.UIThread.RunJobs();
            window.Content = view;
            Dispatcher.UIThread.RunJobs();
        });

        Assert.Null(error);
        Assert.Same(window, TopLevel.GetTopLevel(view));
        window.Close();
    });

    [Fact]
    [DisplayName("A back request with no forms screen on display is left to the platform")]
    public Task BackRequested_NoFormsScreen_NotHandled() => session.RunAsync(() =>
    {
        var view = new ShellView();
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var request = new RoutedEventArgs(TopLevel.BackRequestedEvent);

        window.RaiseEvent(request);

        Assert.False(request.Handled);
        window.Close();
    });

    [Fact]
    [DisplayName("A back request closes the open tab of the forms screen on display")]
    public Task BackRequested_FormsScreenWithTab_ClosesTab() => session.RunAsync(async () =>
    {
        var vm = new FormsViewModel(new PolhemShellOptions(),
            () => Task.FromResult<IReadOnlyList<NavItem>>([NavItem.Form("Orders", "Order")]),
            () => Task.CompletedTask, () => { });
        var forms = new FormsView { DataContext = vm };
        var view = new ShellView();
        var window = new Window { Width = 1200, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        view.GetVisualDescendants().OfType<ContentControl>().First().Content = forms;
        await vm.MenuLoaded;
        Dispatcher.UIThread.RunJobs();
        var request = new RoutedEventArgs(TopLevel.BackRequestedEvent);

        window.RaiseEvent(request);

        Assert.True(request.Handled);
        Assert.Empty(forms.Tabs.Items);
        window.Close();
    });
}

