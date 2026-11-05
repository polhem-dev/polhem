using System.ComponentModel;
using Polhem.Core.Serialization;
using Polhem.Definition.Layouts;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// View → Toggle Side Bar: collapsing the sidebar and bringing it back with the view it showed; and the FormLayout
    /// editor's preview focus, which hides the sidebar while it is on.
    /// </summary>
    public sealed class MainWindowSidebarTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-sidebar-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private FormLayoutDocumentViewModel LoadLayout(string layoutId)
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, layoutId + ".FormLayout.xml");
            XmlCodec.SerializeToFile(new FormLayout { LayoutId = layoutId, ProgId = layoutId, Caption = layoutId }, path);
            return FormLayoutDocumentViewModel.Load(path);
        }

        [Fact]
        [DisplayName("A layout's preview focus hides the sidebar, and ending it brings the sidebar back")]
        public void PreviewFocus_HidesAndRestoresTheSidebar()
        {
            var vm = new MainWindowViewModel();
            var layout = LoadLayout("Order");
            vm.OpenDocuments.Add(layout);

            layout.TogglePreviewFocusCommand.Execute(null);
            Assert.False(vm.IsSidebarVisible);

            layout.TogglePreviewFocusCommand.Execute(null);
            Assert.True(vm.IsSidebarVisible);
        }

        [Fact]
        [DisplayName("The sidebar stays hidden until no open layout is focused on its preview")]
        public void PreviewFocus_TwoLayouts_RestoresAfterTheLast()
        {
            var vm = new MainWindowViewModel();
            var order = LoadLayout("Order");
            var customer = LoadLayout("Customer");
            vm.OpenDocuments.Add(order);
            vm.OpenDocuments.Add(customer);

            order.TogglePreviewFocusCommand.Execute(null);
            customer.TogglePreviewFocusCommand.Execute(null);
            order.TogglePreviewFocusCommand.Execute(null);
            Assert.False(vm.IsSidebarVisible);

            customer.TogglePreviewFocusCommand.Execute(null);
            Assert.True(vm.IsSidebarVisible);
        }

        [Fact]
        [DisplayName("Closing a layout focused on its preview brings the sidebar back")]
        public void PreviewFocus_LayoutClosed_RestoresTheSidebar()
        {
            var vm = new MainWindowViewModel();
            var layout = LoadLayout("Order");
            vm.OpenDocuments.Add(layout);
            layout.TogglePreviewFocusCommand.Execute(null);

            vm.OpenDocuments.Remove(layout);

            Assert.True(vm.IsSidebarVisible);
        }

        [Fact]
        [DisplayName("Ending the preview focus leaves a sidebar that was already hidden hidden")]
        public void PreviewFocus_SidebarHiddenBefore_StaysHidden()
        {
            var vm = new MainWindowViewModel();
            var layout = LoadLayout("Order");
            vm.OpenDocuments.Add(layout);
            vm.ToggleSidebarCommand.Execute(null);

            layout.TogglePreviewFocusCommand.Execute(null);
            layout.TogglePreviewFocusCommand.Execute(null);

            Assert.False(vm.IsSidebarVisible);
        }
        [Fact]
        [DisplayName("Toggle Side Bar collapses a visible sidebar")]
        public void ToggleSidebar_Visible_Collapses()
        {
            var vm = new MainWindowViewModel();

            vm.ToggleSidebarCommand.Execute(null);

            Assert.False(vm.IsSidebarVisible);
        }

        [Fact]
        [DisplayName("Toggle Side Bar brings the sidebar back with the view it showed")]
        public void ToggleSidebar_Collapsed_RestoresSameView()
        {
            var vm = new MainWindowViewModel();
            var view = vm.ActiveSidebarView;

            vm.ToggleSidebarCommand.Execute(null);
            vm.ToggleSidebarCommand.Execute(null);

            Assert.True(vm.IsSidebarVisible);
            Assert.Equal(view, vm.ActiveSidebarView);
        }

        [Fact]
        [DisplayName("No activity bar icon is highlighted while the sidebar is collapsed")]
        public void ToggleSidebar_Collapsed_NoActivityIconActive()
        {
            var vm = new MainWindowViewModel();

            vm.ToggleSidebarCommand.Execute(null);

            Assert.False(vm.IsExplorerActive || vm.IsProgramsActive || vm.IsTableSchemasActive);
        }
    }
}
