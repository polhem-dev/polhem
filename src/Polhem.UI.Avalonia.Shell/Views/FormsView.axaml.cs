using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Polhem.UI.Avalonia.Shell.Controls;
using Polhem.UI.Avalonia.Shell.Navigation;
using Polhem.UI.Avalonia.Shell.ViewModels;
using Polhem.UI.Avalonia.Views;

namespace Polhem.UI.Avalonia.Shell.Views
{
    /// <summary>
    /// Application shell. The left menu opens each form as a document tab hosting a
    /// <see cref="FormWorkspace"/> (the ERP list ↔ record flow lives inside the workspace);
    /// one tab per form, de-duplicated by program id.
    /// </summary>
    /// <remarks>
    /// Opening is driven from both the menu's <c>Tapped</c> event, so tapping an already-selected entry
    /// still re-opens its tab, and <c>SelectionChanged</c>, so a selection made through the accessibility
    /// interface opens the entry too. The code-behind relies on the
    /// source-generated <c>InitializeComponent</c> so the <c>x:Name</c> controls are wired into
    /// fields.
    /// <para>
    /// The iOS and Android heads work like a phone app instead: one form fills the screen, picking
    /// another menu entry replaces it, and the menu slides over the form and closes once an entry is
    /// picked.
    /// </para>
    /// </remarks>
    internal sealed partial class FormsView : UserControl
    {
        private readonly bool _singleForm = OperatingSystem.IsIOS() || OperatingSystem.IsAndroid();
        private bool _initialSelectionDone;
        private bool _selectingInCode;

        /// <summary>
        /// Initializes a new instance of <see cref="FormsView"/>.
        /// </summary>
        public FormsView()
        {
            InitializeComponent();

            // The switch starts from the theme the application was given (ShellOptions.DefaultThemeVariant).
            ThemeToggle.IsChecked = global::Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

            if (_singleForm)
            {
                Tabs.IsVisible = false;
                SingleHost.IsVisible = true;
                Split.DisplayMode = SplitViewDisplayMode.Overlay;
            }
        }

        private bool HasOpenForm => _singleForm ? SingleHost.Content is not null : Tabs.Items.Count > 0;

        /// <inheritdoc/>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            // Open the first form once the bound DataContext has populated the menu. Doing this
            // in the constructor is too early: the ViewLocator assigns DataContext afterwards.
            if (_initialSelectionDone) return;
            _initialSelectionDone = true;

            if (DataContext is FormsViewModel vm)
            {
                if (_singleForm) vm.IsPaneOpen = false;
                _ = OpenFirstFormAsync(vm);
            }
        }

        /// <summary>
        /// Opens the first form once the menu has arrived from the server.
        /// </summary>
        /// <remarks>
        /// The menu is fetched after sign-in and can still be empty when the view loads. On the browser and
        /// iOS heads it was, so reading the menu as the view loaded opened nothing.
        /// </remarks>
        private async Task OpenFirstFormAsync(FormsViewModel vm)
        {
            await vm.MenuLoaded.ConfigureAwait(true);

            // The user may have opened a form in the meantime; do not take the tab away from them.
            if (HasOpenForm) return;

            var first = vm.NavItems.FirstOrDefault(n => !n.IsHeader);
            if (first is not null)
            {
                SelectInCode(first);
                OpenForm(first);
            }
        }

        private void OnNavTapped(object? sender, TappedEventArgs e) => OpenSelectedEntry();

        // A selection made through the accessibility interface (VoiceOver, UI automation) never raises Tapped, so
        // a changed selection opens the entry as well. Tapped stays for tapping the entry that is already selected.
        private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_selectingInCode) OpenSelectedEntry();
        }

        private void SelectInCode(NavItem? item)
        {
            _selectingInCode = true;
            try
            {
                NavList.SelectedItem = item;
            }
            finally
            {
                _selectingInCode = false;
            }
        }

        private void OpenSelectedEntry()
        {
            // By the time Tapped fires the click has already updated the selection, so the
            // tapped entry is SelectedItem — including when it was already selected.
            if (NavList.SelectedItem is NavItem { IsHeader: false, ProgId.Length: > 0 } item)
            {
                OpenForm(item);

                // On a narrow (phone) layout the inline pane leaves the opened form almost no width,
                // so collapse it after a selection; on a wide layout it stays open. Same width
                // threshold as the responsive form layout, so menu and form switch together.
                if (DataContext is FormsViewModel vm
                    && (_singleForm || (Bounds.Width > 0 && Bounds.Width < FormView.DefaultCompactWidthThreshold)))
                {
                    vm.IsPaneOpen = false;
                }
            }
        }

        private void OpenForm(NavItem item)
        {
            if (_singleForm)
            {
                if ((string?)SingleHost.Tag != item.ProgId)
                {
                    SingleHost.Tag = item.ProgId;
                    SingleHost.Content = new FormWorkspace(item.ProgId);
                }

                return;
            }

            foreach (var existing in Tabs.Items.OfType<TabItem>())
            {
                if ((string?)existing.Tag == item.ProgId)
                {
                    Tabs.SelectedItem = existing;
                    return;
                }
            }

            var tab = new TabItem
            {
                Tag = item.ProgId,
                Header = BuildTabHeader(item.Title, item.ProgId),
                Content = new FormWorkspace(item.ProgId),
            };
            Tabs.Items.Add(tab);
            Tabs.SelectedItem = tab;
        }

        private Control BuildTabHeader(string title, string tag)
        {
            var text = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
            var close = new Button
            {
                Content = new PathIcon { Data = (Geometry)this.FindResource("ShellIconClose")!, Width = 10, Height = 10 },
                Tag = tag,
                Classes = { "tabclose" },
                VerticalAlignment = VerticalAlignment.Center,
            };
            close.Click += OnCloseTabClick;

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            panel.Children.Add(text);
            panel.Children.Add(close);
            return panel;
        }

        private void OnCloseTabClick(object? sender, RoutedEventArgs e)
        {
            // Stop the click from also selecting the tab being closed.
            e.Handled = true;
            if (sender is Button { Tag: string tag })
            {
                CloseTab(tag);
            }
        }

        private void CloseTab(string tag)
        {
            var tab = Tabs.Items.OfType<TabItem>().FirstOrDefault(t => (string?)t.Tag == tag);
            if (tab is null) return;

            var index = Tabs.Items.IndexOf(tab);
            Tabs.Items.Remove(tab);

            if (Tabs.Items.Count > 0)
            {
                Tabs.SelectedIndex = global::System.Math.Min(index, Tabs.Items.Count - 1);
            }

            SyncNavToActiveTab();
        }

        /// <summary>
        /// Handles a platform back request (Android hardware / gesture back) for the shell, unwinding
        /// one navigation level: a record returns to its list, otherwise the active document tab is
        /// closed. On the iOS and Android heads, which show one form and no tabs, only the record
        /// returns to its list. Routed here from <see cref="ShellView"/>'s <c>TopLevel.BackRequested</c> handler.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if a level was unwound and the request was consumed;
        /// <see langword="false"/> when nothing is left to unwind, letting the platform handle back (exit the app).
        /// </returns>
        public bool TryHandleBack()
        {
            if (_singleForm)
            {
                // One form fills the screen, so after its record returns to the list there is nothing left to unwind.
                return SingleHost.Content is FormWorkspace single && single.TryGoBack();
            }

            if (Tabs.SelectedItem is not TabItem active)
            {
                // No open tab — let the platform exit.
                return false;
            }

            // First unwind an open record back to its list, mirroring the on-screen Back button.
            if (active.Content is FormWorkspace workspace && workspace.TryGoBack())
            {
                return true;
            }

            // The active tab is already on its list: close the tab. The last close empties the shell,
            // so the next back request finds no tab and falls through to the platform (app exit).
            if (active.Tag is string tag)
            {
                CloseTab(tag);
                return true;
            }

            return false;
        }

        /// <summary>Highlights the menu entry matching the currently active tab.</summary>
        private void SyncNavToActiveTab()
        {
            var activeProgId = (Tabs.SelectedItem as TabItem)?.Tag as string;
            SelectInCode(activeProgId is null
                ? null
                : NavList.Items.OfType<NavItem>().FirstOrDefault(n => n.ProgId == activeProgId));
        }

        private void OnThemeToggled(object? sender, RoutedEventArgs e)
        {
            if (global::Avalonia.Application.Current is { } app)
            {
                app.RequestedThemeVariant = ThemeToggle.IsChecked == true
                    ? ThemeVariant.Dark
                    : ThemeVariant.Light;
            }
        }
    }
}
