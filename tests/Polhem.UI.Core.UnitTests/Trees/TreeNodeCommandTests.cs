using System.ComponentModel;
using Polhem.UI.Core.Trees;

namespace Polhem.UI.Core.UnitTests.Trees
{
    public class TreeNodeCommandTests
    {
        [Fact]
        [DisplayName("A new TreeNodeCommand is enabled, has no icon and does not begin a group")]
        public void Ctor_Defaults()
        {
            var command = new TreeNodeCommand("Add", () => { });

            Assert.Equal("Add", command.Label);
            Assert.True(command.IsEnabled);
            Assert.Null(command.IconKey);
            Assert.False(command.BeginsGroup);
        }

        [Fact]
        [DisplayName("Execute runs the action given to the constructor")]
        public void Execute_RunsAction()
        {
            var runs = 0;
            var command = new TreeNodeCommand("Add", () => runs++);

            command.Execute();

            Assert.Equal(1, runs);
        }

        [Fact]
        [DisplayName("The constructor rejects a null label or action")]
        public void Ctor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new TreeNodeCommand(null!, () => { }));
            Assert.Throws<ArgumentNullException>(() => new TreeNodeCommand("Add", null!));
        }
    }
}
