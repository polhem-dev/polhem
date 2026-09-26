using System.ComponentModel;
using Polhem.Api.Core.MessagePack;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// SerializableDataRelation tests.
    /// </summary>
    public class SerializableDataRelationTests
    {
        [Fact]
        [DisplayName("The default constructor initializes empty collections and empty strings")]
        public void DefaultConstructor_InitializesEmpty()
        {
            var relation = new SerializableDataRelation();

            Assert.Equal(string.Empty, relation.RelationName);
            Assert.Equal(string.Empty, relation.ParentTable);
            Assert.Equal(string.Empty, relation.ChildTable);
            Assert.NotNull(relation.ParentColumns);
            Assert.Empty(relation.ParentColumns);
            Assert.NotNull(relation.ChildColumns);
            Assert.Empty(relation.ChildColumns);
        }

        [Fact]
        [DisplayName("Properties can be set and read back")]
        public void Properties_AreSettable()
        {
            var relation = new SerializableDataRelation
            {
                RelationName = "FK_Orders_Customers",
                ParentTable = "Customers",
                ChildTable = "Orders",
            };
            relation.ParentColumns.Add("CustomerId");
            relation.ChildColumns.Add("CustomerId");

            Assert.Equal("FK_Orders_Customers", relation.RelationName);
            Assert.Equal("Customers", relation.ParentTable);
            Assert.Equal("Orders", relation.ChildTable);
            Assert.Single(relation.ParentColumns);
            Assert.Single(relation.ChildColumns);
        }
    }
}
