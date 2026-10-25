using System.ComponentModel;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// View → Toggle Side Bar: collapsing the sidebar and bringing it back with the view it showed.
    /// </summary>
    public class MainWindowSidebarTests
    {
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
