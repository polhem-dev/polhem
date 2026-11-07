using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.ObjectTree;
using Polhem.DefineEditor.Models;
using Polhem.DefineEditor.Services;
using Polhem.DefineEditor.ViewModels;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>The sidebar filter, Go to File, and Find in tree.</summary>
    public sealed class SearchTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "polhem-search-" + Guid.NewGuid().ToString("N"));

        public SearchTests()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "FormSchema"));
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }

        private string SaveSchema(string progId)
        {
            var schema = new FormSchema(progId, progId) { CategoryId = "company" };
            var table = schema.Tables!.Add(progId, progId);
            table.Fields!.Add("sys_id", "No", FieldDbType.String);
            table.Fields!.Add("order_date", "Order Date", FieldDbType.Date);
            table.Fields!.Add("ship_date", "Ship Date", FieldDbType.Date);
            var path = Path.Combine(_directory, "FormSchema", progId + ".FormSchema.xml");
            XmlCodec.SerializeToFile(schema, path);
            return path;
        }

        private static DefineNode File(string name, string? path = null, string? key = null) =>
            new() { Name = name, Kind = DefineNodeKind.DefineFile, DefineType = DefineType.FormSchema, FilePath = path ?? "/" + name, KeyText = key };

        private static DefineNode Group(string name, params DefineNode[] children)
        {
            var group = new DefineNode { Name = name, Kind = DefineNodeKind.Group };
            foreach (var child in children) group.Children.Add(child);
            return group;
        }

        [Fact]
        [DisplayName("The filter keeps matching files and the groups above them, expanded, and leaves the originals alone")]
        public void Filter_KeepsMatchesAndExpandedAncestors()
        {
            var root = Group("Define", Group("FormSchema", File("Order"), File("Customer")), Group("Language", File("Common")));

            var result = DefineTreeFilter.Filter([root], "ORD");

            var kept = Assert.Single(result);
            Assert.True(kept.IsExpanded);
            var formSchema = Assert.Single(kept.Children);
            Assert.True(formSchema.IsExpanded);
            Assert.Equal(["Order"], formSchema.Children.Select(c => c.Name));
            Assert.NotSame(root, kept);
            Assert.False(root.IsExpanded);
            Assert.Equal(2, root.Children[0].Children.Count);
        }

        [Fact]
        [DisplayName("A group whose name matches keeps all of its files, and no match leaves nothing")]
        public void Filter_MatchingGroupKeepsChildren_NoMatchIsEmpty()
        {
            var root = Group("Define", Group("Language", File("Common"), File("Order")));

            Assert.Equal(2, DefineTreeFilter.Filter([root], "lang")[0].Children[0].Children.Count);
            Assert.Empty(DefineTreeFilter.Filter([root], "zzz"));
        }

        [Fact]
        [DisplayName("The fuzzy score accepts characters apart, rejects them out of order, and ranks a typed run first")]
        public void Score_RanksContiguousAndWordStartsHigher()
        {
            Assert.NotNull(DefineTreeFilter.Score("Order.FormSchema.xml", "ordfs"));
            Assert.Null(DefineTreeFilter.Score("Order.FormSchema.xml", "sfo"));
            Assert.True(DefineTreeFilter.Score("Order", "ord") > DefineTreeFilter.Score("OrderDetail", "ord"));
            Assert.True(DefineTreeFilter.Score("Order", "ord") > DefineTreeFilter.Score("Coordinate", "ord"));
        }

        [Fact]
        [DisplayName("The sidebar filter swaps the visible views and clearing it shows the originals again")]
        public void SidebarFilter_FiltersVisibleViewsAndClears()
        {
            var vm = new MainWindowViewModel
            {
                Nodes = [Group("Define", Group("FormSchema", File("Order"), File("Customer")))],
            };

            vm.SidebarFilter = "cust";
            Assert.Equal(["Customer"], vm.VisibleNodes[0].Children[0].Children.Select(c => c.Name));

            vm.SidebarFilter = "nothing";
            Assert.True(vm.HasNoFilterMatches);

            vm.ClearSidebarFilterCommand.Execute(null);
            Assert.Same(vm.Nodes, vm.VisibleNodes);
            Assert.False(vm.HasNoFilterMatches);
        }

        [Fact]
        [DisplayName("Go to File lists the best match first, moves the highlight, and Enter opens the file in a tab")]
        public void QuickOpen_RanksMovesAndOpens()
        {
            var orderPath = SaveSchema("Order");
            var detailPath = SaveSchema("OrderDetail");
            var vm = new MainWindowViewModel
            {
                SolutionPath = _directory,
                Nodes = [Group("Define", Group("FormSchema", File("OrderDetail", detailPath, "OrderDetail"), File("Order", orderPath, "Order")))],
            };

            vm.ShowQuickOpenCommand.Execute(null);
            Assert.True(vm.IsQuickOpenVisible);
            vm.QuickOpenText = "order";

            Assert.Equal(["Order", "OrderDetail"], vm.QuickOpenItems.Select(i => i.Name));
            Assert.Equal(Path.Combine("FormSchema", "Order.FormSchema.xml"), vm.QuickOpenItems[0].Detail);
            vm.MoveQuickOpenSelection(1);
            vm.MoveQuickOpenSelection(1);
            Assert.Equal("OrderDetail", vm.SelectedQuickOpenItem!.Name);

            vm.OpenQuickOpenItemCommand.Execute(null);

            Assert.False(vm.IsQuickOpenVisible);
            Assert.Equal(detailPath, Assert.Single(vm.OpenDocuments).DocumentKey);
        }

        [Fact]
        [DisplayName("Go to File matches the name followed by the type, so a few letters of each pick that form's file")]
        public void QuickOpen_NameAndTypeAbbreviation_PicksThatFile()
        {
            var vm = new MainWindowViewModel
            {
                SolutionPath = _directory,
                Nodes =
                [
                    Group("Define", Group("FormLayout",
                        Layout("Department", "FormLayout/Department.FormLayout.xml"),
                        Layout("Order", "FormLayout/Order.FormLayout.xml"))),
                ],
            };

            vm.ShowQuickOpenCommand.Execute(null);
            vm.QuickOpenText = "ordfl";

            Assert.Equal("Order", vm.QuickOpenItems[0].Name);
        }

        private DefineNode Layout(string name, string relativePath) =>
            new()
            {
                Name = name,
                Kind = DefineNodeKind.DefineFile,
                DefineType = DefineType.FormLayout,
                FilePath = Path.Combine(_directory, relativePath),
                KeyText = name,
            };

        [Fact]
        [DisplayName("Find in tree selects matches in order, wraps around, and keeps a selection that still matches")]
        public void TreeFind_SelectsMatchesAndWraps()
        {
            var document = FormSchemaDocumentViewModel.Load(SaveSchema("Order"), SolutionContext.Empty);
            string? Selected() => (document.SelectedTreeNode?.Value as FormField)?.FieldName;

            document.TreeFindText = "date";
            Assert.Equal("order_date", Selected());
            Assert.Equal(LocalizationService.Current["TreeFind_Position"].Replace("{0}", "1").Replace("{1}", "2"), document.TreeFindStatus);

            document.FindNextCommand.Execute(null);
            Assert.Equal("ship_date", Selected());
            document.FindNextCommand.Execute(null);
            Assert.Equal("order_date", Selected());
            document.FindPreviousCommand.Execute(null);
            Assert.Equal("ship_date", Selected());

            document.TreeFindText = "ship";
            Assert.Equal("ship_date", Selected());
            Assert.True(document.SelectedTreeNode!.Parent!.IsExpanded);

            document.TreeFindText = "nothing";
            Assert.Equal(LocalizationService.Current["TreeFind_NoMatches"], document.TreeFindStatus);
            Assert.Equal("ship_date", Selected());
        }

        [Fact]
        [DisplayName("Find in tree expands a collapsed ancestor of the match it selects")]
        public void TreeFind_ExpandsCollapsedAncestor()
        {
            var document = FormSchemaDocumentViewModel.Load(SaveSchema("Order"), SolutionContext.Empty);
            var tableNode = document.RootNode.Children.First(c => c.IsFolder).Children[0];
            tableNode.IsExpanded = false;

            document.TreeFindText = "sys_id";

            Assert.True(tableNode.IsExpanded);
            Assert.IsType<FormField>(document.SelectedTreeNode!.Value);
            Assert.IsType<ObjectTreeNode>(document.SelectedTreeNode);
        }
    }
}
