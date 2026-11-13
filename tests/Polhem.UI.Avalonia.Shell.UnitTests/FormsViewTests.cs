using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Polhem.UI.Avalonia.Shell.Controls;
using Polhem.UI.Avalonia.Shell.Navigation;
using Polhem.UI.Avalonia.Shell.ViewModels;
using Polhem.UI.Avalonia.Shell.Views;
using Polhem.UI.Avalonia.Views;

namespace Polhem.UI.Avalonia.Shell.UnitTests;

/// <summary>
/// The desktop behaviour of <see cref="FormsView"/>: a menu entry opens its form in a tab, one tab per form, and the
/// platform back request unwinds the open tabs.
/// </summary>
[Collection(AvaloniaControlsCollection.Name)]
public class FormsViewTests(HeadlessSessionFixture session)
{
    private static readonly NavItem s_header = NavItem.Header("Sales");
    private static readonly NavItem s_orders = NavItem.Form("Orders", "Order");
    private static readonly NavItem s_customers = NavItem.Form("Customers", "Customer");

    private static FormsViewModel CreateViewModel(params NavItem[] rows)
        => new(new PolhemShellOptions { AppTitle = "Demo" },
            () => Task.FromResult<IReadOnlyList<NavItem>>(rows), () => Task.CompletedTask, () => { });

    private static async Task<(FormsView View, FormsViewModel ViewModel)> CreateAsync()
    {
        var vm = CreateViewModel(s_header, s_orders, s_customers);
        await vm.MenuLoaded;
        return (new FormsView { DataContext = vm }, vm);
    }

    private static Window Show(Control content, double width)
    {
        var window = new Window { Width = width, Height = 600, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static TabItem[] OpenTabs(FormsView view) => [.. view.Tabs.Items.OfType<TabItem>()];

    private static Button CloseButtonOf(TabItem tab) => (Button)((StackPanel)tab.Header!).Children[1];

    private static void ClickClose(TabItem tab) => CloseButtonOf(tab).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [DisplayName("On the desktop the forms open in tabs and the single-form host stays hidden")]
    public Task Constructor_Desktop_ShowsTabs() => session.RunAsync(() =>
    {
        var view = new FormsView();

        Assert.True(view.Tabs.IsVisible);
        Assert.False(view.SingleHost.IsVisible);
        Assert.Equal(SplitViewDisplayMode.Inline, view.Split.DisplayMode);
    });

    [Fact]
    [DisplayName("The theme switch starts on light when the application runs the light theme")]
    public Task Constructor_LightTheme_ThemeToggleUnchecked() => session.RunAsync(() =>
    {
        var view = new FormsView();

        Assert.False(view.ThemeToggle.IsChecked);
    });

    [Fact]
    [DisplayName("Selecting a menu entry opens its form in a new selected tab")]
    public Task SelectEntry_Form_OpensTab() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();

        view.NavList.SelectedItem = s_orders;

        var tab = Assert.Single(OpenTabs(view));
        Assert.Equal("Order", tab.Tag);
        Assert.IsType<FormWorkspace>(tab.Content);
        Assert.Same(tab, view.Tabs.SelectedItem);
    });

    [Fact]
    [DisplayName("A tab header shows the menu caption and a close button")]
    public Task SelectEntry_Form_BuildsHeaderWithCloseButton() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();

        view.NavList.SelectedItem = s_orders;

        var header = Assert.IsType<StackPanel>(Assert.Single(OpenTabs(view)).Header);
        Assert.Equal("Orders", Assert.IsType<TextBlock>(header.Children[0]).Text);
        var close = Assert.IsType<Button>(header.Children[1]);
        Assert.Equal("Order", close.Tag);
        Assert.Contains("tabclose", close.Classes);
    });

    [Fact]
    [DisplayName("Selecting a form that is already open switches to its tab instead of opening a second one")]
    public Task SelectEntry_AlreadyOpen_ReusesTab() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();
        view.NavList.SelectedItem = s_orders;
        view.NavList.SelectedItem = s_customers;

        view.NavList.SelectedItem = s_orders;

        var tabs = OpenTabs(view);
        Assert.Equal(2, tabs.Length);
        Assert.Same(tabs[0], view.Tabs.SelectedItem);
    });

    [Fact]
    [DisplayName("Selecting a header row opens nothing")]
    public Task SelectEntry_Header_OpensNothing() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();

        view.NavList.SelectedItem = s_header;

        Assert.Empty(OpenTabs(view));
    });

    [Fact]
    [DisplayName("Tapping the menu opens the selected entry")]
    public Task NavTapped_EntrySelected_OpensTab() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();
        view.NavList.SelectedItem = s_orders;
        // The entry stays selected while its tab goes, as when the tab was closed some other way.
        view.Tabs.Items.Clear();
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var press = new PointerEventArgs(InputElement.PointerReleasedEvent, view.NavList, pointer, null, default, 0,
            new PointerPointProperties(), KeyModifiers.None);

        view.NavList.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, press));

        Assert.Equal("Order", Assert.Single(OpenTabs(view)).Tag);
    });

    [Fact]
    [DisplayName("A view that has not been laid out keeps the menu pane open after a selection")]
    public Task SelectEntry_NoWidthYet_KeepsPaneOpen() => session.RunAsync(async () =>
    {
        var (view, vm) = await CreateAsync();

        view.NavList.SelectedItem = s_orders;

        Assert.True(vm.IsPaneOpen);
    });

    [Fact]
    [DisplayName("On a narrow layout the menu pane closes once a form is picked")]
    public Task SelectEntry_NarrowLayout_ClosesPane() => session.RunAsync(async () =>
    {
        var vm = CreateViewModel(s_header, s_orders, s_customers);
        var view = new FormsView { DataContext = vm };
        var window = Show(view, FormView.DefaultCompactWidthThreshold - 100);

        view.NavList.SelectedItem = s_customers;

        Assert.False(vm.IsPaneOpen);
        window.Close();
    });

    [Fact]
    [DisplayName("On a wide layout the menu pane stays open once a form is picked")]
    public Task SelectEntry_WideLayout_KeepsPaneOpen() => session.RunAsync(async () =>
    {
        var vm = CreateViewModel(s_header, s_orders, s_customers);
        var view = new FormsView { DataContext = vm };
        var window = Show(view, FormView.DefaultCompactWidthThreshold + 400);

        view.NavList.SelectedItem = s_customers;

        Assert.True(vm.IsPaneOpen);
        window.Close();
    });

    [Fact]
    [DisplayName("Once loaded, the view opens the first form of the menu")]
    public Task OnLoaded_MenuArrived_OpensFirstForm() => session.RunAsync(async () =>
    {
        var vm = CreateViewModel(s_header, s_orders, s_customers);
        var view = new FormsView { DataContext = vm };

        var window = Show(view, 1200);
        await vm.MenuLoaded;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Order", Assert.Single(OpenTabs(view)).Tag);
        Assert.Same(s_orders, view.NavList.SelectedItem);
        window.Close();
    });

    [Fact]
    [DisplayName("Once loaded, a menu with only header rows opens nothing")]
    public Task OnLoaded_NoForms_OpensNothing() => session.RunAsync(async () =>
    {
        var vm = CreateViewModel(s_header);
        var view = new FormsView { DataContext = vm };

        var window = Show(view, 1200);
        await vm.MenuLoaded;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(OpenTabs(view));
        window.Close();
    });

    [Fact]
    [DisplayName("A form opened before the menu arrived is not replaced by the first form")]
    public Task OnLoaded_FormAlreadyOpen_KeepsIt() => session.RunAsync(async () =>
    {
        var menu = new TaskCompletionSource<IReadOnlyList<NavItem>>();
        var vm = new FormsViewModel(new PolhemShellOptions(), () => menu.Task, () => Task.CompletedTask, () => { });
        var view = new FormsView { DataContext = vm };
        var window = Show(view, 1200);
        var tab = new TabItem { Tag = "Customer" };
        view.Tabs.Items.Add(tab);

        menu.SetResult([s_orders]);
        await vm.MenuLoaded;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(tab, Assert.Single(OpenTabs(view)));
        window.Close();
    });

    [Fact]
    [DisplayName("Loading the view a second time does not open the first form again")]
    public Task OnLoaded_SecondTime_DoesNotReopen() => session.RunAsync(async () =>
    {
        var vm = CreateViewModel(s_orders);
        var view = new FormsView { DataContext = vm };
        var window = Show(view, 1200);
        await vm.MenuLoaded;
        Dispatcher.UIThread.RunJobs();
        ClickClose(OpenTabs(view)[0]);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Content = view;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(OpenTabs(view));
        window.Close();
    });

    [Fact]
    [DisplayName("The close button of a tab removes the tab and marks the click handled")]
    public Task CloseButton_Clicked_RemovesTab() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();
        view.NavList.SelectedItem = s_orders;
        var click = new RoutedEventArgs(Button.ClickEvent);

        CloseButtonOf(OpenTabs(view)[0]).RaiseEvent(click);

        Assert.Empty(OpenTabs(view));
        Assert.True(click.Handled);
    });

    [Fact]
    [DisplayName("Closing a tab selects its neighbour and highlights that form in the menu")]
    public Task CloseButton_OtherTabsRemain_SelectsNeighbourAndSyncsMenu() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();
        view.NavList.SelectedItem = s_orders;
        view.NavList.SelectedItem = s_customers;

        ClickClose(OpenTabs(view)[1]);

        var remaining = Assert.Single(OpenTabs(view));
        Assert.Same(remaining, view.Tabs.SelectedItem);
        Assert.Same(s_orders, view.NavList.SelectedItem);
    });

    [Fact]
    [DisplayName("Closing the last tab clears the menu selection")]
    public Task CloseButton_LastTab_ClearsMenuSelection() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();
        view.NavList.SelectedItem = s_orders;

        ClickClose(OpenTabs(view)[0]);

        Assert.Null(view.NavList.SelectedItem);
    });

    [Fact]
    [DisplayName("A back request with no open tab is left to the platform")]
    public Task TryHandleBack_NoTab_ReturnsFalse() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();

        Assert.False(view.TryHandleBack());
    });

    [Fact]
    [DisplayName("A back request on a tab showing its list closes that tab")]
    public Task TryHandleBack_TabOnList_ClosesTab() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();
        view.NavList.SelectedItem = s_orders;

        var handled = view.TryHandleBack();

        Assert.True(handled);
        Assert.Empty(OpenTabs(view));
    });

    [Fact]
    [DisplayName("A back request on a tab without a program id is left to the platform")]
    public Task TryHandleBack_TabWithoutTag_ReturnsFalse() => session.RunAsync(async () =>
    {
        var (view, _) = await CreateAsync();
        var tab = new TabItem { Content = new TextBlock() };
        view.Tabs.Items.Add(tab);
        view.Tabs.SelectedItem = tab;

        Assert.False(view.TryHandleBack());
    });

    [Fact]
    [DisplayName("Flipping the theme switch requests the dark theme, and flipping it back the light one")]
    public Task ThemeToggle_Flipped_ChangesApplicationTheme() => session.RunAsync(() =>
    {
        var app = Application.Current!;
        var original = app.RequestedThemeVariant;
        var view = new FormsView();
        try
        {
            view.ThemeToggle.IsChecked = true;
            var dark = app.RequestedThemeVariant;
            view.ThemeToggle.IsChecked = false;

            Assert.Equal(global::Avalonia.Styling.ThemeVariant.Dark, dark);
            Assert.Equal(global::Avalonia.Styling.ThemeVariant.Light, app.RequestedThemeVariant);
        }
        finally
        {
            app.RequestedThemeVariant = original;
        }
    });
}
