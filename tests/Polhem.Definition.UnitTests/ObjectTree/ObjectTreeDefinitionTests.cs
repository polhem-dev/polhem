using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Layouts;
using Polhem.Definition.ObjectTree;

namespace Polhem.Definition.UnitTests.ObjectTree
{
    /// <summary>
    /// Builds trees from the real definition types, so a change to their annotations shows up here.
    /// </summary>
    public class ObjectTreeDefinitionTests
    {
        private static FormLayout CreateLayout()
        {
            var layout = new FormLayout { LayoutId = "Employee", ProgId = "Employee", Caption = "Employee" };
            var section = new LayoutSection { Name = "Main", Caption = "Main" };
            section.Fields!.Add(new LayoutField { FieldName = "sys_id", Caption = "Id" });
            layout.Sections!.Add(section);
            var grid = new LayoutGrid("WorkLog", "Work log");
            grid.Columns!.Add(new LayoutColumn("seq", "Seq", ControlType.TextEdit));
            layout.Details!.Add(grid);
            return layout;
        }

        private static FormSchema CreateSchema()
        {
            var schema = new FormSchema("Order", "Order") { CategoryId = "company" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            master.Fields!.Add("amount", "Amount", FieldDbType.Currency);
            var detail = schema.Tables!.Add("OrderLine", "Order line");
            detail.Fields!.Add("qty", "Qty", FieldDbType.Integer);
            schema.Rules!.Add("R1", "amount > 0", "Amount must be positive");
            return schema;
        }

        [Fact]
        [DisplayName("A FormLayout tree has Sections and Details folders, with fields under sections and columns under grids")]
        public void Build_FormLayout_MatchesAnnotationModel()
        {
            var layout = CreateLayout();

            var root = new ObjectTreeBuilder().Build(layout);

            Assert.Equal("Employee - Employee", root.Label);
            Assert.Equal(["Sections", "Details"], root.Children.Select(c => c.Label));
            Assert.All(root.Children, c => Assert.True(c.IsFolder));
            var section = Assert.Single(root.Children[0].Children);
            Assert.Equal("Main - Main", section.Label);
            Assert.Equal("sys_id - Id", Assert.Single(section.Children).Label);
            var grid = Assert.Single(root.Children[1].Children);
            Assert.Equal("WorkLog - Work log", grid.Label);
            Assert.Equal("seq - Seq", Assert.Single(grid.Children).Label);
        }

        [Fact]
        [DisplayName("A FormSchema tree has Tables and Rules folders, with each table's fields directly under it")]
        public void Build_FormSchema_MatchesAnnotationModel()
        {
            var schema = CreateSchema();

            var root = new ObjectTreeBuilder().Build(schema);

            Assert.Equal("Order - Order", root.Label);
            Assert.Equal(["Tables", "Rules"], root.Children.Select(c => c.Label));
            var tables = root.Children[0].Children;
            Assert.Equal(["Order - Order", "OrderLine - Order line"], tables.Select(t => t.Label));
            Assert.Equal(["sys_id - Order No", "amount - Amount"], tables[0].Children.Select(f => f.Label));
            Assert.All(tables[0].Children, f => Assert.False(f.IsFolder));
            Assert.Equal("R1 - amount > 0", Assert.Single(root.Children[1].Children).Label);
        }

        [Fact]
        [DisplayName("Back references marked TreeNodeIgnore are not followed, so each table and field appears once")]
        public void Build_FormSchema_BackReferencesNotFollowed()
        {
            var schema = CreateSchema();

            var root = new ObjectTreeBuilder().Build(schema);

            var all = Flatten(root).ToList();
            Assert.Single(all, n => ReferenceEquals(n.Value, schema));
            Assert.Equal(2, all.Count(n => n.Value is FormTable));
            Assert.Equal(3, all.Count(n => n.Value is FormField));
        }

        [Fact]
        [DisplayName("A Tag pointing back to an ancestor does not make the build recurse")]
        public void Build_TagPointsToAncestor_DoesNotRecurse()
        {
            var layout = CreateLayout();
            layout.Sections![0].Tag = layout;
            layout.Sections!.Tag = layout;

            var root = new ObjectTreeBuilder().Build(layout);

            Assert.Single(Flatten(root), n => ReferenceEquals(n.Value, layout));
        }

        [Fact]
        [DisplayName("Building from a collection item does not climb to its collection or siblings")]
        public void Build_FromCollectionItem_DoesNotFollowCollectionBackReference()
        {
            var schema = CreateSchema();
            var field = schema.Tables!["Order"].Fields!["amount"];

            var root = new ObjectTreeBuilder().Build(field);

            Assert.Empty(root.Children);
        }

        [Fact]
        [DisplayName("Building a tree does not change how a FormSchema serializes")]
        public void Build_FormSchema_SerializationUnchanged()
        {
            var schema = new FormSchema("Customer", "Customer") { CategoryId = "company" };
            schema.Tables!.Add("Customer", "Customer");
            var before = XmlCodec.Serialize(schema);

            new ObjectTreeBuilder().Build(schema);

            Assert.Equal(before, XmlCodec.Serialize(schema));
        }

        private static IEnumerable<ObjectTreeNode> Flatten(ObjectTreeNode node)
        {
            return node.Children.SelectMany(Flatten).Prepend(node);
        }
    }
}
