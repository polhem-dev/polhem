using System.ComponentModel;
using Polhem.Definition.Collections;
using Polhem.UI.Avalonia.Controls;

namespace Polhem.UI.Avalonia.UnitTests.Controls.Dialogs
{
    /// <summary>
    /// <see cref="CollectionEditDialog"/> on a host without native windows: the collection is edited in an overlay
    /// card, OK writes the edited copies back, and Cancel leaves the collection as it was.
    /// </summary>
    public class CollectionEditDialogTests
    {
        private sealed class Owner
        {
            public ListItemCollection Items { get; } = new() { { "A", "Active" }, { "C", "Closed" } };
        }

        private static (Owner Owner, CollectionEditContext Context) CreateContext()
        {
            var owner = new Owner();
            return (owner, new CollectionEditContext(owner, TypeDescriptor.GetProperties(owner)[nameof(Owner.Items)]!));
        }

        [Fact]
        [DisplayName("OK after adding an item returns true and writes the new item into the collection")]
        public void ShowAsync_CommittedAfterAdd_ReturnsTrueAndUpdatesCollection()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var (owner, context) = CreateContext();
            var task = CollectionEditDialog.ShowAsync(host.Host, context);

            var panel = host.Panel<CollectionEditPanel>();
            panel.AddItem();
            Assert.True(panel.Commit());

            Assert.True(InlineContinuationScope.Completed(task));
            Assert.Equal(3, owner.Items.Count);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("Cancel returns false and leaves the collection untouched")]
        public void ShowAsync_Cancelled_ReturnsFalseAndKeepsCollection()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var (owner, context) = CreateContext();
            var task = CollectionEditDialog.ShowAsync(host.Host, context);

            var panel = host.Panel<CollectionEditPanel>();
            panel.RemoveItems();
            panel.Cancel();

            Assert.False(InlineContinuationScope.Completed(task));
            Assert.Equal(["A", "C"], owner.Items.Select(i => i.Value));
        }

        [Fact]
        [DisplayName("The overlay card is titled with the property's display name")]
        public void ShowAsync_NoSource_TitlesWithPropertyName()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var (_, context) = CreateContext();
            var task = CollectionEditDialog.ShowAsync(host.Host, context);

            Assert.Equal(nameof(Owner.Items), host.Title);
            host.Panel<CollectionEditPanel>().Cancel();
            InlineContinuationScope.Completed(task);
        }

        [Fact]
        [DisplayName("A source property grid's label translator titles the dialog")]
        public void ShowAsync_WithSourceTranslator_TranslatesTitle()
        {
            using var scope = new InlineContinuationScope();
            var host = new OverlayTestHost();
            var (_, context) = CreateContext();
            var source = new PropertyGridControl
            {
                LabelTranslator = text => text.Kind == PropertyGridTextKind.DisplayName ? "Translated items" : null,
            };

            var task = CollectionEditDialog.ShowAsync(host.Host, context, source, CancellationToken.None);

            Assert.Equal("Translated items", host.Title);
            host.Panel<CollectionEditPanel>().Cancel();
            InlineContinuationScope.Completed(task);
        }

        [Fact]
        [DisplayName("A token cancelled before the call throws without showing anything")]
        public void ShowAsync_AlreadyCancelled_Throws()
        {
            var host = new OverlayTestHost();
            var (_, context) = CreateContext();

            var task = CollectionEditDialog.ShowAsync(host.Host, context, new CancellationToken(canceled: true));

            Assert.True(task.IsCanceled);
            Assert.Empty(host.Overlay.Children);
        }

        [Fact]
        [DisplayName("A null host or context is rejected")]
        public void ShowAsync_NullArguments_Throw()
        {
            var (_, context) = CreateContext();

            Assert.IsType<ArgumentNullException>(CollectionEditDialog.ShowAsync(null!, context).Exception?.InnerException);
            Assert.IsType<ArgumentNullException>(
                CollectionEditDialog.ShowAsync(new OverlayTestHost().Host, null!).Exception?.InnerException);
        }
    }
}
